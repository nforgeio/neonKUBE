//-----------------------------------------------------------------------------
// FILE:	    SetupController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Net;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Manages a cluster setup operation consisting of a series of setup steps
    /// while displaying status to the <see cref="Console"/>.
    /// </summary>
    /// <typeparam name="NodeMetadata">Specifies the node metadata type.</typeparam>
    /// <remarks>
    /// This class inherits from <see cref="ObjectDictionary"/> which can be used to maintain
    /// state that can be accessed by the setup step actions.  This dictionary is keyed by
    /// case-sensitive strings and can store and retrieve objects with differing types.
    /// </remarks>
    public class SetupController<NodeMetadata> : ObjectDictionary, ISetupController
        where NodeMetadata : class
    {
        //---------------------------------------------------------------------
        // Private types

        private class Step
        {
            public int                                                          Number;
            public string                                                       Label;
            public bool                                                         IsQuiet;
            public ISetupController                                             SubController;
            public object                                                       ParentStep;
            public Action<ISetupController>                                     SyncGlobalAction;
            public Func<ISetupController, Task>                                 AsyncGlobalAction;
            public Action<ISetupController, NodeSshProxy<NodeMetadata>>         SyncNodeAction;
            public Func<ISetupController, NodeSshProxy<NodeMetadata>, Task>     AsyncNodeAction;
            public Func<ISetupController, NodeSshProxy<NodeMetadata>, bool>     Predicate;
            public SetupStepState                                               Status;
            public int                                                          ParallelLimit;
            public bool                                                         WasExecuted;
            public TimeSpan                                                     RunTime;

            /// <inheritdoc/>
            public override string ToString()
            {
                return string.IsNullOrEmpty(Label) ? "*** unlabeled step ***" : Label;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const int UnlimitedParallel = 500;  // Treat this as "unlimited"

        private object                              syncLock    = new object();
        private List<IDisposable>                   disposables = new List<IDisposable>();
        private ISetupController                    parent      = null;
        private string                              globalStatus;
        private List<NodeSshProxy<NodeMetadata>>    nodes;
        private List<Step>                          steps;
        private Step                                currentStep;
        private bool                                isFaulted;
        private bool                                cancelPending;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="operationTitle">Summarizes the high-level operation being performed.</param>
        /// <param name="nodes">The node proxies for the cluster nodes being manipulated.</param>
        public SetupController(string operationTitle, IEnumerable<NodeSshProxy<NodeMetadata>> nodes)
            : this(new string[] { operationTitle }, nodes)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="operationTitle">Summarizes the high-level operation being performed.</param>
        /// <param name="nodes">The node proxies for the cluster nodes being manipulated.</param>
        public SetupController(string[] operationTitle, IEnumerable<NodeSshProxy<NodeMetadata>> nodes)
        {
            var title = string.Empty;

            foreach (var name in operationTitle)
            {
                if (title.Length > 0)
                {
                    title += ' ';
                }

                title += name;
            }

            this.OperationTitle = title;
            this.nodes          = nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
            this.steps          = new List<Step>();
        }
        /// <inheritdoc/>
        public void AddDisposable(IDisposable disposable)
        {
            Covenant.Requires<ArgumentNullException>(disposable != null, nameof(disposable));

            disposables.Add(disposable);
        }

        /// <inheritdoc/>
        public IEnumerable<NodeLog> GetNodeLogs()
        {
            return nodes
                .OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase)
                .Select(node => node.GetNodeLog());
        }

        /// <summary>
        /// Sets the <see cref="LinuxSshProxy.DefaultRunOptions"/> property for
        /// all nodes managed by the controller.
        /// </summary>
        /// <param name="options">The options to be set.</param>
        public void SetDefaultRunOptions(RunOptions options)
        {
            foreach (var node in nodes)
            {
                node.DefaultRunOptions = options;
            }
        }

        /// <summary>
        /// Adds a synchronous global configuration step.
        /// </summary>
        /// <param name="stepLabel">Specifies the step label.</param>
        /// <param name="action">The synchronous global action to be performed.</param>
        /// <param name="quiet">Optionally specifies that the step is not to be reported in the progress.</param>
        /// <param name="subController">Optionally specifies the related subcontroller.</param>
        /// <param name="position">
        /// The optional zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        /// <returns><b>INTERNAL USE ONLY:</b> The new internal step as an <see cref="object"/>.</returns>
        public object AddGlobalStep(string stepLabel, Action<ISetupController> action, bool quiet = false, ISetupController subController = null, int position = -1)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no longer add steps.");

            if (position < 0)
            {
                position = steps.Count;
            }

            var step = new Step()
            {
                Label            = stepLabel,
                IsQuiet          = quiet,
                SyncGlobalAction = action,
                Predicate        = (controller, node) => true,
                SubController    = subController
            };

            steps.Insert(position, step);

            return step;
        }

        /// <summary>
        /// Adds an asynchronous global configuration step.
        /// </summary>
        /// <param name="stepLabel">Specifies the step label.</param>
        /// <param name="action">The asynchronous global action to be performed.</param>
        /// <param name="quiet">Optionally specifies that the step is not to be reported in the progress.</param>
        /// <param name="subController">Optionally specifies the related subcontroller.</param>
        /// <param name="position">
        /// The optional zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        /// <returns><b>INTERNAL USE ONLY:</b> The new internal step as an <see cref="object"/>.</returns>
        public object AddGlobalStep(string stepLabel, Func<ISetupController, Task> action, bool quiet = false, ISetupController subController = null, int position = -1)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no longer add steps.");

            if (position < 0)
            {
                position = steps.Count;
            }

            var step = new Step()
            {
                Label             = stepLabel,
                IsQuiet           = quiet,
                AsyncGlobalAction = action,
                Predicate         = (controller, node) => true,
                SubController     = subController
            };

            steps.Insert(position, step);

            return step;
        }

        /// <summary>
        /// Adds the steps from a subcontroller to the current controller.
        /// </summary>
        /// <typeparam name="ServerMetadata">Specifies the type of the subcontroller's node metadata.</typeparam>
        /// <param name="subController">The subcontroller.</param>
        /// <remarks>
        /// <para>
        /// This is useful for situations where an operation requires interactions 
        /// with machines that are not cluster nodes.  Currently, we're using this
        /// for connecting to XenServers to provision cluster nodes there before
        /// moving on to preparing the cluster nodes and configuring the cluster.
        /// </para>
        /// <note>
        /// This method copies the state from this controller (the parent) to
        /// the subcontroller before executing the first subcontroller step.
        /// </note>
        /// <note>
        /// Subcontroller steps may only be added to the parent level.  You may
        /// not nest these any deeper than that.
        /// </note>
        /// </remarks>
        public void AddControllerStep<ServerMetadata>(SetupController<ServerMetadata> subController)
            where ServerMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(subController != null, nameof(subController));
            Covenant.Requires<InvalidOperationException>(subController.parent == null, "The subcontroller is already a step of a parent controller.");
            Covenant.Requires<InvalidOperationException>(this.parent == null, "Cannot nest subcontroller steps more than one level deep.");

            subController.parent = this;

            // Add a hidden step that copies the parent state to the subcontroller.

            AddGlobalStep("copy controller state",
                controller =>
                {
                    subController.Clear();

                    foreach (var item in controller)
                    {
                        subController.Add(item.Key, item.Value);
                    }
                },
                quiet: true);

            // We're going to append a global step to the parent controller for each
            // step from the subcontroller and then forward the executions to the
            // subcontroller.

            foreach (var substep in subController.steps)
            {
                var parentStep = AddGlobalStep(substep.Label,
                    controller =>
                    {
                        // We're going to forward state from the substep to the
                        // parent manually here.

                        var parentStep = (Step)substep.ParentStep;

                        try
                        {
                            subController.ExecuteStep(substep);
                        }
                        finally
                        {
                            // We need to bubble up any subcontroller node faults.

                            this.isFaulted = this.isFaulted || subController.nodes.Any(node => node.IsFaulted);

                            parentStep.Status  = substep.Status;
                            parentStep.RunTime = substep.RunTime;
                        }
                    },
                    quiet: substep.IsQuiet,
                    subController: subController);

                substep.ParentStep = parentStep;
            }
        }

        /// <summary>
        /// Adds a global step that scans for existing machines that conflict with any of
        /// the IP addressess assigned to the cluster.  This is used by some hosting managers
        /// to ensure that we're not conficting with and exising cluster or other assets
        /// deployed on the LAN.
        /// </summary>
        /// <param name="stepLabel">Optionally specifies the step label.</param>
        /// <returns><b>INTERNAL USE ONLY:</b> The new internal step as an <see cref="object"/>.</returns>
        public object AddCheckForIpConflcits(string stepLabel = "scan for IP address conflicts")
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no longer add steps.");

            return AddGlobalStep(stepLabel,
                controller =>
                {
                    var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var pingOptions   = new PingOptions(ttl: 32, dontFragment: true);
                    var pingTimeout   = TimeSpan.FromSeconds(2);
                    var pingConflicts = new List<NodeDefinition>();
                    var pingAttempts  = 2;

                    // I'm going to use up to 20 threads at a time here for simplicity
                    // rather then doing this as async operations.

                    var parallelOptions = new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = 20
                    };

                    Parallel.ForEach(cluster.Definition.NodeDefinitions.Values, parallelOptions,
                        node =>
                        {
                            using (var pinger = new Pinger())
                            {
                                // We're going to try pinging up to [pingAttempts] times for each node
                                // just in case the network is sketchy and we're losing reply packets.

                                for (int i = 0; i < pingAttempts; i++)
                                {
                                    var reply = pinger.SendPingAsync(node.Address, (int)pingTimeout.TotalMilliseconds).Result;

                                    if (reply.Status == IPStatus.Success)
                                    {
                                        lock (pingConflicts)
                                        {
                                            pingConflicts.Add(node);
                                        }

                                        break;
                                    }
                                }
                            }
                        });

                    if (pingConflicts.Count > 0)
                    {
                        var sb = new StringBuilder();

                        using (var writer = new StringWriter(sb))
                        {
                            writer.WriteLine($"Cannot provision the cluster because [{pingConflicts.Count}] other machines conflict with the following cluster nodes:");
                            
                            foreach (var node in pingConflicts.OrderBy(node => NetHelper.AddressToUint(NetHelper.ParseIPv4Address(node.Address))))
                            {
                                writer.WriteLine($"{node.Address, 16}:    {node.Name}");
                            }
                        }

                        LogBaseError(sb.ToString());
                    }
                });
        }

        /// <summary>
        /// Adds a synchronous global step that waits for all nodes to be online.
        /// </summary>
        /// <param name="stepLabel">Optionally specifies the step label.</param>
        /// <param name="status">The optional node status.</param>
        /// <param name="nodePredicate">
        /// Optional predicate used to select the nodes that participate in the step
        /// or <c>null</c> to select all nodes.
        /// </param>
        /// <param name="quiet">Optionally specifies that the step is not to be reported in the progress.</param>
        /// <param name="timeout">Optionally specifies the maximum time to wait (defaults to <b>10 minutes</b>).</param>
        /// <param name="position">
        /// The optional zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        /// <returns><b>INTERNAL USE ONLY:</b> The new internal step as an <see cref="object"/>.</returns>
        public object AddWaitUntilOnlineStep(
            string                                                      stepLabel     = "connect", 
            string                                                      status        = null, 
            Func<ISetupController, NodeSshProxy<NodeMetadata>, bool>    nodePredicate = null, 
            bool                                                        quiet         = false, 
            TimeSpan?                                                   timeout       = null, 
            int                                                         position      = -1)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no longer add steps.");

            if (timeout == null)
            {
                timeout = TimeSpan.FromMinutes(10);
            }
            if (position < 0)
            {
                position = steps.Count;
            }

            return AddNodeStep(stepLabel,
                (controller, node) =>
                {
                    node.Status = status ?? "connecting...";
                    node.WaitForBoot(timeout: timeout);
                    node.IsReady = true;
                },
                nodePredicate,
                quiet,
                noParallelLimit: true,
                position: position);
        }

        /// <summary>
        /// Adds a synchronous global step that waits for a specified period of time.
        /// </summary>
        /// <param name="stepLabel">Specifies the step label.</param>
        /// <param name="delay">The amount of time to wait.</param>
        /// <param name="status">The optional node status.</param>
        /// <param name="nodePredicate">
        /// Optional predicate used to select the nodes that participate in the step
        /// or <c>null</c> to select all nodes.
        /// </param>
        /// <param name="quiet">Optionally specifies that the step is not to be reported in the progress.</param>
        /// <param name="position">
        /// The optional zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        /// <returns><b>INTERNAL USE ONLY:</b> The new internal step as an <see cref="object"/>.</returns>
        public object AddDelayStep(
            string                                                      stepLabel, 
            TimeSpan                                                    delay, 
            string                                                      status        = null,
            Func<ISetupController, NodeSshProxy<NodeMetadata>, bool>    nodePredicate = null, 
            bool                                                        quiet         = false, 
            int                                                         position      = -1)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no longer add steps.");

            return AddNodeStep(stepLabel,
                (controller, node) =>
                {
                    node.Status = status ?? $"delay: [{delay.TotalSeconds}] seconds";
                    Thread.Sleep(delay);
                    node.IsReady = true;
                },
                nodePredicate, 
                quiet,
                noParallelLimit: true,
                position: position);
        }

        /// <summary>
        /// Appends a synchronous node configuration step.
        /// </summary>
        /// <param name="stepLabel">Specifies the step label.</param>
        /// <param name="nodeAction">
        /// The action to be performed on each node.  Two parameters will be passed
        /// to this action: the node's <see cref="NodeSshProxy{T}"/> and a <see cref="TimeSpan"/>
        /// indicating the amount of time the action should wait before performing
        /// the operation, if the operation hasn't already been performed.
        /// </param>
        /// <param name="nodePredicate">
        /// Optional predicate used to select the nodes that participate in the step
        /// or <c>null</c> to select all nodes.
        /// </param>
        /// <param name="quiet">Optionally specifies that the step is not to be reported in the progress.</param>
        /// <param name="noParallelLimit">
        /// Optionally ignores the global <see cref="SetupController{T}.MaxParallel"/> 
        /// limit for the new step when greater.
        /// </param>
        /// <param name="position">
        /// Optionally specifies the zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        /// <param name="parallelLimit">
        /// Optionally specifies the maximum number of operations to be performed
        /// in parallel for this step, overriding the controller default.
        /// </param>
        /// <returns><b>INTERNAL USE ONLY:</b> The new internal step as an <see cref="object"/>.</returns>
        public object AddNodeStep(
            string stepLabel,
            Action<ISetupController, NodeSshProxy<NodeMetadata>>        nodeAction,
            Func<ISetupController, NodeSshProxy<NodeMetadata>, bool>    nodePredicate   = null,
            bool                                                        quiet           = false,
            bool                                                        noParallelLimit = false,
            int                                                         position        = -1,
            int                                                         parallelLimit   = 0)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no longer add steps.");

            nodeAction    = nodeAction ?? new Action<ISetupController, NodeSshProxy<NodeMetadata>>((controller, node) => { });
            nodePredicate = nodePredicate ?? new Func<ISetupController, NodeSshProxy<NodeMetadata>, bool>((controller, node) => true);

            if (position < 0)
            {
                position = steps.Count;
            }

            var step = new Step()
            {
                Label          = stepLabel,
                IsQuiet        = quiet,
                SyncNodeAction = nodeAction,
                Predicate      = nodePredicate,
                ParallelLimit  = noParallelLimit ? UnlimitedParallel : 0
            };

            if (parallelLimit > 0)
            {
                step.ParallelLimit = parallelLimit;
            }

            steps.Insert(position, step);

            return step;
        }

        /// <summary>
        /// Appends an asynchronous node configuration step.
        /// </summary>
        /// <param name="stepLabel">Specifies the step label.</param>
        /// <param name="nodeAction">
        /// The action to be performed on each node.  Two parameters will be passed
        /// to this action: the node's <see cref="NodeSshProxy{T}"/> and a <see cref="TimeSpan"/>
        /// indicating the amount of time the action should wait before performing
        /// the operation, if the operation hasn't already been performed.
        /// </param>
        /// <param name="nodePredicate">
        /// Optional predicate used to select the nodes that participate in the step
        /// or <c>null</c> to select all nodes.
        /// </param>
        /// <param name="quiet">Optionally specifies that the step is not to be reported in the progress.</param>
        /// <param name="noParallelLimit">
        /// Optionally ignores the global <see cref="SetupController{T}.MaxParallel"/> 
        /// limit for the new step when greater.
        /// </param>
        /// <param name="position">
        /// Optionally specifies the zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        /// <param name="parallelLimit">
        /// Optionally specifies the maximum number of operations to be performed
        /// in parallel for this step, overriding the controller default.
        /// </param>
        /// <returns><b>INTERNAL USE ONLY:</b> The new internal step as an <see cref="object"/>.</returns>
        public object AddNodeStep(
            string stepLabel,
            Func<ISetupController, NodeSshProxy<NodeMetadata>, Task>    nodeAction,
            Func<ISetupController, NodeSshProxy<NodeMetadata>, bool>    nodePredicate   = null,
            bool                                                        quiet           = false,
            bool                                                        noParallelLimit = false,
            int                                                         position        = -1,
            int                                                         parallelLimit   = 0)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no longer add steps.");

            nodeAction    = nodeAction ?? new Func<ISetupController, NodeSshProxy<NodeMetadata>, Task>((controller, node) => { return Task.CompletedTask; });
            nodePredicate = nodePredicate ?? new Func<ISetupController, NodeSshProxy<NodeMetadata>, bool>((controller, node) => true);

            if (position < 0)
            {
                position = steps.Count;
            }

            var step = new Step()
            {
                Label           = stepLabel,
                IsQuiet         = quiet,
                AsyncNodeAction = nodeAction,
                Predicate       = nodePredicate,
                ParallelLimit   = noParallelLimit ? UnlimitedParallel : 0
            };

            if (parallelLimit > 0)
            {
                step.ParallelLimit = parallelLimit;
            }

            steps.Insert(position, step);

            return step;
        }

        /// <summary>
        /// Sets the operation status text.
        /// </summary>
        /// <param name="status">The optional operation status text.</param>
        public void SetGlobalStepStatus(string status = null)
        {
            globalStatus = status ?? string.Empty;
        }

        /// <summary>
        /// Performs an operation step on the selected nodes (if any).
        /// </summary>
        /// <param name="step">A step being performed.</param>
        /// <returns><c>true</c> if the step succeeded.</returns>
        /// <remarks>
        /// <para>
        /// This method begins by setting the <see cref="LinuxSshProxy.IsReady"/>
        /// state of each selected nodes to <c>false</c> and then it starts a new thread for
        /// each node and performs the action on these threads.
        /// </para>
        /// <para>
        /// In parallel, the method spins on the current thread, displaying status while
        /// waiting for each of the nodes to transition to the <see cref="LinuxSshProxy.IsReady"/>=<c>true</c>
        /// state.
        /// </para>
        /// <para>
        /// The method returns <c>true</c> after all of the node actions have completed
        /// and none of the nodes have <see cref="LinuxSshProxy.IsFaulted"/>=<c>true</c>.
        /// </para>
        /// <note>
        /// This method does nothing if a previous step failed.
        /// </note>
        /// </remarks>
        private bool ExecuteStep(Step step)
        {
            var stopWatch = new Stopwatch();

            stopWatch.Start();

            try
            {
                if (isFaulted)
                {
                    return false;
                }

                step.Status      = SetupStepState.Running;
                step.WasExecuted = true;

                var stepNodes        = nodes.Where(node => step.Predicate(this, node));
                var stepNodeNamesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var node in stepNodes)
                {
                    stepNodeNamesSet.Add(node.Name);

                    node.IsReady = false;
                }

                foreach (var node in nodes)
                {
                    if (stepNodeNamesSet.Contains(node.Name))
                    {
                        node.Status = string.Empty;
                    }
                    else
                    {
                        node.Status = string.Empty;
                    }
                }

                var parallelOptions = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = step.ParallelLimit > 0 ? step.ParallelLimit : MaxParallel
                };

                NeonHelper.StartThread(
                    () =>
                    {
                        currentStep = step;

                        if (step.SyncNodeAction != null)
                        {
                            // Execute the step on the selected nodes.

                            Parallel.ForEach(stepNodes, parallelOptions,
                                node =>
                                {
                                    try
                                    {
                                        step.SyncNodeAction(this, node);

                                        node.Status  = "[x] DONE";
                                        node.IsReady = true;
                                    }
                                    catch (Exception e)
                                    {
                                        node.Fault(NeonHelper.ExceptionError(e));
                                        node.LogException(e);
                                    }
                                });
                        }
                        else if (step.AsyncNodeAction != null)
                        {
                            // Execute the step on the selected nodes.

                            Parallel.ForEach(stepNodes, parallelOptions,
                                node =>
                                {
                                    try
                                    {
                                        var nodeDefinition = node.Metadata as NodeDefinition;

                                        var runTask = Task.Run(
                                            async () =>
                                            {
                                                await step.AsyncNodeAction(this, node);
                                            });

                                        runTask.Wait();

                                        node.Status  = "[x] DONE";
                                        node.IsReady = true;
                                    }
                                    catch (Exception e)
                                    {
                                        var aggregateException = e as AggregateException;

                                        if (aggregateException != null && aggregateException.InnerExceptions.Count == 1)
                                        {
                                            e = aggregateException.InnerExceptions.Single();
                                        }

                                        node.Fault(NeonHelper.ExceptionError(e));
                                        node.LogException(e);
                                    }
                                });
                        }
                        else if (step.SyncGlobalAction != null)
                        {
                            try
                            {
                                step.SyncGlobalAction(this);
                            }
                            catch (Exception e)
                            {
                                // $todo(jefflill):
                                //
                                // We're going to report global step exceptions as if they
                                // happened on the first master node because there's no
                                // other place to log this in the current design.
                                //
                                // I suppose we could create a [global.log] file or something
                                // and put this there and also indicate this somewhere in
                                // the console output, but this is not worth messing with
                                // right now.

                                if (typeof(NodeMetadata) == typeof(NodeDefinition))
                                {
                                    var firstMaster = nodes
                                        .Where(node => (node.Metadata as NodeDefinition).IsMaster)
                                        .OrderBy(node => node.Name)
                                        .First();

                                    firstMaster.Fault(NeonHelper.ExceptionError(e));
                                    firstMaster.LogException(e);
                                }
                            }

                            foreach (var node in stepNodes)
                            {
                                node.IsReady = true;
                            }

                            SetGlobalStepStatus();
                        }
                        else if (step.AsyncGlobalAction != null)
                        {
                            try
                            {
                                var runTask = Task.Run(
                                    async () =>
                                    {
                                        await step.AsyncGlobalAction(this);
                                    });

                                runTask.Wait();
                            }
                            catch (Exception e)
                            {
                                var aggregateException = e as AggregateException;

                                if (aggregateException != null && aggregateException.InnerExceptions.Count == 1)
                                {
                                    e = aggregateException.InnerExceptions.Single();
                                }

                                // $todo(jefflill):
                                //
                                // We're going to report global step exceptions as if they
                                // happened on the first master node because there's no
                                // other place to log this in the current design.
                                //
                                // I suppose we could create a [global.log] file or something
                                // and put this there and also indicate this somewhere in
                                // the console output, but this is not worth messing with
                                // right now.

                                if (typeof(NodeMetadata) == typeof(NodeDefinition))
                                {
                                    var firstMaster = nodes
                                        .Where(node => (node.Metadata as NodeDefinition).IsMaster)
                                        .OrderBy(node => node.Name)
                                        .First();

                                    firstMaster.Fault(NeonHelper.ExceptionError(e));
                                    firstMaster.LogException(e);
                                }
                            }

                            foreach (var node in stepNodes)
                            {
                                node.IsReady = true;
                            }

                            SetGlobalStepStatus();
                        }
                    });

                // The setup steps are executing above in one or more threads and we're
                // going to loop here to raise [StatusEvent] when we detect a change.

                var statusInterval = TimeSpan.FromMilliseconds(100);
                var lastJson       = (string)null;

                while (true)
                {
                    Covenant.Assert(ContainsKey(KubeSetupProperty.ClusterLogin), $"Setup controller is missing the required [{nameof(KubeSetupProperty.ClusterLogin)}] property.");

                    var status  = new SetupClusterStatus(this);
                    var newJson = NeonHelper.JsonSerialize(status);

                    if (lastJson == null || lastJson != newJson)
                    {
                        StatusChangedEvent?.Invoke(status);
                        lastJson = newJson;
                    }

                    if (stepNodes.Count(node => !node.IsReady) == 0)
                    {
                        // Looks like we're done.

                        break;
                    }

                    Thread.Sleep(statusInterval);
                }

                isFaulted = isFaulted || stepNodes.FirstOrDefault(node => node.IsFaulted) != null;

                if (isFaulted)
                {
                    step.Status = SetupStepState.Failed;

                    return false;
                }
                else
                {
                    step.Status = SetupStepState.Done;

                    return true;
                }
            }
            finally
            {
                step.RunTime = stopWatch.Elapsed;
            }
        }

        /// <summary>
        /// Throws an exception if any of the operation steps did not complete successfully.
        /// </summary>
        public void ThrowOnError()
        {
            if (isFaulted)
            {
                throw new KubeException($"[{nodes.Count(n => n.IsFaulted)}] nodes are faulted.");
            }
        }

        //---------------------------------------------------------------------
        // ISetupController implementation

        // These methods are intended to unify how progress and error messages are
        // reported by prepare/setup code as well as to refactor how higher-level
        // code can receive and process these.
        //
        // The setup code has evolved over 4+ years, starting with the setup controller
        // being configured by the [neon-cli] console application assuming that status
        // can be written directly to the console and that errors can be handled by
        // just terminating [neon-cli].
        // 
        // This is part of the final setup refactor where all cluster prepare/setup 
        // related code is relocated to the [Neon.Kube] library so it can be referenced
        // by different kinds of applications.  This will include [neon-cli] as well
        // as neonDESKTOP right now and perhaps Temporal based workflows in the future
        // as part of a neonCLOUD offering.
        //
        // The LogProgress() methods update global or node-specific status.  For nodes,
        // this will be set as the node status text.  The Error() methods do the same
        // thing for error messages while also ensuring that setup terminates after the
        // current step completes.

        /// <inheritdoc/>
        public string LogBeginMarker { get; set; }

        /// <inheritdoc/>
        public string LogEndMarker { get; set; }

        /// <inheritdoc/>
        public string LogFailedMarker { get; set; }

        /// <inheritdoc/>
        public event SetupStatusChangedDelegate StatusChangedEvent;

        /// <inheritdoc/>
        public event SetupProgressDelegate BaseProgressEvent;

        /// <inheritdoc/>
        public void LogBaseProgress(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            if (BaseProgressEvent != null)
            {
                lock (syncLock)
                {
                    BaseProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Text       = message,
                            CancelPending = cancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogBaseProgress(string verb, string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(verb), nameof(verb));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            if (BaseProgressEvent != null)
            {
                lock (syncLock)
                {
                    BaseProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Verb          = verb,
                            Text       = message,
                            CancelPending = cancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogBaseProgress(LinuxSshProxy node, string message)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            ((NodeSshProxy<NodeMetadata>)node).Status = message;

            if (BaseProgressEvent != null)
            {
                lock (syncLock)
                {
                    BaseProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Node          = node,
                            Text       = message,
                            CancelPending = cancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogProgress(LinuxSshProxy node, string verb, string message)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(verb), nameof(verb));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            ((NodeSshProxy<NodeMetadata>)node).Status = $"{verb}: {message}";

            if (BaseProgressEvent != null)
            {
                lock (syncLock)
                {
                    BaseProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Node          = node,
                            Verb          = verb,
                            Text       = message,
                            CancelPending = cancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogBaseError(string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            this.isFaulted = true;
            this.LastBaseError = message;

            if (BaseProgressEvent != null)
            {
                lock (syncLock)
                {
                    BaseProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Text       = message,
                            IsError       = true,
                            CancelPending = cancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogError(LinuxSshProxy node, string message)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            ((NodeSshProxy<NodeMetadata>)node).Status    = message;
            ((NodeSshProxy<NodeMetadata>)node).IsFaulted = true;

            if (BaseProgressEvent != null)
            {
                lock (syncLock)
                {
                    BaseProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Node          = node,
                            Text       = message,
                            IsError       = true,
                            CancelPending = cancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public bool IsFaulted => isFaulted || nodes.Any(node => node.IsFaulted);

        /// <inheritdoc/>
        public string LastBaseError { get; private set; }

        /// <inheritdoc/>
        public bool HasNodeSteps => steps.Any(step => step.AsyncNodeAction != null || step.SyncNodeAction != null);

        /// <inheritdoc/>
        public Type NodeMetadataType => typeof(NodeMetadata);

        /// <inheritdoc/>
        public HashSet<string> GetStepNodeNames(object internalStep)
        {
            Covenant.Requires<ArgumentNullException>(internalStep != null, nameof(internalStep));

            var nodeSet   = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            var step      = (Step)internalStep;
            var stepNodes = nodes.Where(node => step.Predicate(this, node));

            foreach (var node in stepNodes)
            {
                nodeSet.Add(node.Name);
            }

            return nodeSet;
        }

        /// <inheritdoc/>
        public string GlobalStatus => globalStatus;

        /// <inheritdoc/>
        public string OperationTitle { get; private set; }

        /// <inheritdoc/>
        public bool ShowStatus { get; set; } = false;

        /// <inheritdoc/>
        public bool ShowNodeStatus { get; set; } = true;

        /// <inheritdoc/>
        public int MaxDisplayedSteps { get; set; } = 5;

        /// <inheritdoc/>
        public int MaxParallel { get; set; } = int.MaxValue;

        /// <inheritdoc/>
        public int StepCount => steps.Count;

        /// <inheritdoc/>
        public int CurrentStepNumber
        {
            get
            {
                if (currentStep == null || currentStep.Number == 0)
                {
                    return -1;
                }
                else
                {
                    return currentStep.Number;
                }
            }
        }

        /// <inheritdoc/>
        public TimeSpan Runtime { get; private set; }

        /// <inheritdoc/>
        public bool ShowRuntime { get; set; } = false;

        /// <inheritdoc/>
        public IEnumerable<SetupStepStatus> GetStepStatus()
        {
            return steps.Select(step => new SetupStepStatus(step.Number, step.Label, step.Status, step.RunTime, step));
        }

        /// <inheritdoc/>
        public Task<SetupDisposition> RunAsync(bool leaveNodesConnected = false)
        {
            // This method had been synchronous for a very long time (maybe 5 years)
            // but we need to make this async now so that it would integrate well
            // with the neonDESKTOP UX. 
            //
            // We're simply going to wrap the main setup loop into a new thread and
            // have setup execute there.  We'll use a task completion source to signal
            // the caller when we're done.

            var cluster = Get<ClusterProxy>(KubeSetupProperty.ClusterProxy, null);
            var tcs     = new TaskCompletionSource<SetupDisposition>();

            NeonHelper.StartThread(
                () =>
                {
                    try
                    {
                        cluster?.LogLine(LogBeginMarker);

                        // We're going to time how long this takes.

                        var stopWatch = new Stopwatch();

                        stopWatch.Start();

                        // Number the steps.  Note that quiet steps don't get their own step number.

                        var position = 1;

                        foreach (var step in steps)
                        {
                            if (step.IsQuiet)
                            {
                                step.Number = 0;
                            }
                            else
                            {
                                step.Number = position++;
                            }
                        }

                        // NOTE: We don't display node status if there aren't any node specific steps.

                        try
                        {
                            foreach (var step in steps)
                            {
                                if (cancelPending)
                                {
                                    tcs.SetResult(SetupDisposition.Cancelled);
                                    return;
                                }

                                if (!ExecuteStep(step))
                                {
                                    break;
                                }
                            }

                            if (isFaulted)
                            {
                                cluster?.LogLine(LogFailedMarker);

                                tcs.SetResult(SetupDisposition.Failed);
                                return;
                            }

                            foreach (var node in nodes)
                            {
                                node.Status = "[x] READY";
                            }

                            cluster?.LogLine(LogEndMarker);

                            tcs.SetResult(SetupDisposition.Succeeded);
                            return;
                        }
                        finally
                        {
                            Runtime = stopWatch.Elapsed;

                            if (!leaveNodesConnected)
                            {
                                // Disconnect all of the nodes.

                                foreach (var node in nodes)
                                {
                                    node.Disconnect();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                    finally
                    {
                        // Dispose all node proxies.

                        foreach (var node in cluster.Nodes)
                        {
                            node.Dispose();
                        }

                        // Dispose any disposables.

                        foreach (var disposable in disposables)
                        {
                            disposable.Dispose();
                        }

                        // Raise one more status changed and wait for a bit so any
                        // listening UI will have a chance to display the status.

                        if (StatusChangedEvent != null)
                        {
                            StatusChangedEvent.Invoke(new SetupClusterStatus(this));
                            Thread.Sleep(TimeSpan.FromSeconds(0.5));
                        }
                    }
                });

            return tcs.Task;
        }

        /// <inheritdoc/>
        public bool CancelPending
        {
            get => cancelPending;

            set
            {
                if (value == true)
                {
                    cancelPending = true;
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<SetupNodeStatus> GetNodeStatus()
        {
            return nodes
                .OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase)
                .Select(node => new SetupNodeStatus(node.Name, node.Status, node))
                .ToArray();
        }

        /// <inheritdoc/>
        public IEnumerable<SetupNodeStatus> GetHostStatus()
        {
            var currentStep = this.currentStep;

            if (currentStep?.SubController == null)
            {
                return Array.Empty<SetupNodeStatus>();
            }

            return currentStep.SubController.GetNodeStatus();
        }
    }
}
