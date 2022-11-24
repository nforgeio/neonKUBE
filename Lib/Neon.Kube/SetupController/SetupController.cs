//-----------------------------------------------------------------------------
// FILE:	    SetupController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Tasks;

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
        // Local types

        internal class Step : ISetupControllerStep
        {
            public int                                                      Number;
            public string                                                   Label;
            public bool                                                     IsQuiet;
            public ISetupController                                         SubController;
            public object                                                   ParentStep;
            public Action<ISetupController>                                 SyncGlobalAction;
            public Func<ISetupController, Task>                             AsyncGlobalAction;
            public Action<ISetupController, NodeSshProxy<NodeMetadata>>     SyncNodeAction;
            public Func<ISetupController, NodeSshProxy<NodeMetadata>, Task> AsyncNodeAction;
            public Func<ISetupController, NodeSshProxy<NodeMetadata>, bool> Predicate;
            public SetupStepState                                           State;
            public int                                                      ParallelLimit;
            public bool                                                     WasExecuted;

            /// <inheritdoc/>
            public bool IsGlobalStep => SyncGlobalAction != null || AsyncGlobalAction != null;

            /// <inheritdoc/>
            public TimeSpan RunTime { get; set; }

            /// <inheritdoc/>
            public override string ToString()
            {
                return string.IsNullOrEmpty(Label) ? "*** unlabeled step ***" : Label;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const int UnlimitedParallel = 500;  // Treat this as "unlimited"

        private object                                  syncLock       = new object();
        private List<IDisposable>                       disposables    = new List<IDisposable>();
        private ISetupController                        parent         = null;
        private bool                                    isRunning      = false;
        private Dictionary<string, SetupPendingTasks>   pendingGroups  = new Dictionary<string, SetupPendingTasks>(StringComparer.InvariantCultureIgnoreCase);
        private CancellationTokenSource                 cts            = new CancellationTokenSource();
        private bool                                    finishedRaised = false;
        private int                                     maxStackSize;
        private string                                  globalStatus;
        private List<NodeSshProxy<NodeMetadata>>        nodes;
        private List<INodeSshProxy>                     hosts;
        private List<Step>                              steps;
        private Step                                    currentStep;
        private bool                                    isFaulted;
        private string                                  clusterLogPath;
        private TextWriter                              clusterLogWriter;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="operationTitle">Summarizes the high-level operation being performed.</param>
        /// <param name="nodes">The node proxies for the cluster nodes being manipulated.</param>
        /// <param name="logFolder">Specifies the path to the log folder.</param>
        /// <param name="disableConsoleOutput">
        /// Optionally disables status output to the console.  This is typically
        /// enabled for non-console applications.
        /// </param>
        public SetupController(string operationTitle, IEnumerable<NodeSshProxy<NodeMetadata>> nodes, string logFolder, bool disableConsoleOutput = false)
            : this(new string[] { operationTitle }, nodes, logFolder, disableConsoleOutput: disableConsoleOutput)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="operationTitle">Summarizes the high-level operation being performed.</param>
        /// <param name="nodes">The node proxies for the cluster nodes being manipulated.</param>
        /// <param name="logFolder">Specifies the path to the log folder.</param>
        /// <param name="disableConsoleOutput">
        /// Optionally disables status output to the console.  This is typically
        /// enabled for non-console applications.
        /// </param>
        public SetupController(string[] operationTitle, IEnumerable<NodeSshProxy<NodeMetadata>> nodes, string logFolder, bool disableConsoleOutput = false)
        {
            Covenant.Requires<ArgumentNullException>(operationTitle != null, nameof(operationTitle));
            Covenant.Requires<ArgumentNullException>(nodes != null, nameof(nodes));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(logFolder));

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
            this.hosts          = new List<INodeSshProxy>();
            this.steps          = new List<Step>();
            this.clusterLogPath = Path.Combine(logFolder, KubeConst.ClusterLogName);

            if (!disableConsoleOutput)
            {
                ConsoleWriter = new SetupConsoleWriter(disableConsoleOutput);
            }
        }

        /// <inheritdoc/>
        public event EventHandler<Exception> Finished;

        /// <summary>
        /// Raises the <see cref="Finished"/> event.  Note that this event is raised only once
        /// per setup controller instance.
        /// </summary>
        /// <param name="e">Optionally specifies the <see cref="Exception"/> when the setup operation was cancelled or failed.</param>
        private void RaiseFinished(Exception e = null)
        {
            if (finishedRaised)
            {
                return;
            }

            try
            {
                Finished?.Invoke(this, e);
            }
            finally
            {
                finishedRaised = true;
            }
        }

        /// <inheritdoc/>
        public void AddDisposable(IDisposable disposable)
        {
            Covenant.Requires<ArgumentNullException>(disposable != null, nameof(disposable));

            disposables.Add(disposable);
        }

        /// <summary>
        /// <para>
        /// Optionally called by hosting managers to associate any virtual machine host proxies that
        /// are participating in the setup operation with the controller.  These are used to write
        /// fault details to the global cluster log after the operation completes.
        /// </para>
        /// <note>
        /// This must be called before the controller starts.
        /// </note>
        /// </summary>
        /// <param name="hosts">The hosts being assoicated.</param>
        public void SetHosts(IEnumerable<INodeSshProxy> hosts)
        {
            Covenant.Requires<ArgumentNullException>(hosts != null, nameof(hosts));
            Covenant.Assert(!isRunning, $"[{nameof(SetHosts)}()] cannot be called after the controller has started running.");
            Covenant.Assert(this.hosts.Count() == 0, $"[{nameof(SetHosts)}()] cannot be called more than once for a controller.");

            this.hosts.AddRange(hosts);
        }

        /// <inheritdoc/>
        public IEnumerable<NodeLog> GetNodeLogs()
        {
            return nodes
                .OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase)
                .Select(node => node.GetLog());
        }

        /// <inheritdoc/>
        public IEnumerable<NodeLog> GetHostLogs()
        {
            return hosts
                .OrderBy(host => host.Name, StringComparer.InvariantCultureIgnoreCase)
                .Select(host => host.GetLog());
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
        /// Ensures that controller execution has not started.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when <see cref="RunAsync(int)"/> has been called to start execution.</exception>
        private void EnsureNotRunning()
        {
            if (isRunning)
            {
                throw new InvalidOperationException("Cannot add setup controller steps after execution has started.");
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
        public object AddGlobalStep(
            string                      stepLabel,
            Action<ISetupController>    action,
            bool                        quiet         = false,
            ISetupController            subController = null,
            int                         position      = -1)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You may no longer add steps.");
            EnsureNotRunning();

            if (position < 0)
            {
                position = steps.Count;
            }

            var step = new Step()
            {
                Label            = stepLabel,
                IsQuiet          = quiet,
                SyncGlobalAction = action,
                Predicate        = (controller, node) => false,
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
        public object AddGlobalStep(
            string                          stepLabel,
            Func<ISetupController, Task>    action,
            bool                            quiet         = false,
            ISetupController                subController = null,
            int                             position      = -1)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no may longer add steps.");
            EnsureNotRunning();

            if (position < 0)
            {
                position = steps.Count;
            }

            var step = new Step()
            {
                Label             = stepLabel,
                IsQuiet           = quiet,
                AsyncGlobalAction = action,
                Predicate         = (controller, node) => false,
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
            EnsureNotRunning();

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

                            parentStep.State   = substep.State;
                            parentStep.RunTime = substep.RunTime;
                        }
                    },
                    quiet:         substep.IsQuiet,
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
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You no may longer add steps.");
            EnsureNotRunning();

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
                                writer.WriteLine($"{node.Address,16}:    {node.Name}");
                            }
                        }

                        LogProgressError(sb.ToString());
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
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You may no longer add steps.");
            EnsureNotRunning();

            if (timeout == null)
            {
                timeout = TimeSpan.FromMinutes(10);
            }
            if (position < 0)
            {
                position = steps.Count;
            }

            if (nodePredicate == null)
            {
                nodePredicate = (controller, node) => true;
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
                position:        position);
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
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You may no longer add steps.");
            EnsureNotRunning();

            if (nodePredicate == null)
            {
                nodePredicate = (controller, node) => true;
            }

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
                position:        position);
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
        /// <exception cref="InvalidOperationException">Thrown when <see cref="RunAsync(int)"/> has been called to start execution.</exception>
        public object AddNodeStep(
            string stepLabel,
            Action<ISetupController, NodeSshProxy<NodeMetadata>>        nodeAction,
            Func<ISetupController, NodeSshProxy<NodeMetadata>, bool>    nodePredicate   = null,
            bool                                                        quiet           = false,
            bool                                                        noParallelLimit = false,
            int                                                         position        = -1,
            int                                                         parallelLimit   = 0)
        {
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You may no longer add steps.");
            EnsureNotRunning();

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
            Covenant.Requires<InvalidOperationException>(parent == null, "This controller is already a subcontroller.  You may no longer add steps.");
            EnsureNotRunning();

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

        /// <inheritdoc/>
        public void SetGlobalStepStatus(string status = null)
        {
            if (!string.IsNullOrEmpty(status) && status != globalStatus)
            {
                LogGlobal($"STATUS: {status}");
            }

            globalStatus = status ?? string.Empty;
        }

        /// <summary>
        /// Sets the <see cref="LinuxSshProxy.IsInvolved"/> property for all nodes passed
        /// as <paramref name="stepNodes"/> and clears that for any nodes that are not
        /// involved in the next step.  This also clears the <see cref="LinuxSshProxy.IsConfiguring"/>,
        /// <see cref="LinuxSshProxy.IsReady"/> and <see cref="LinuxSshProxy.IsFaulted"/> properties
        /// for all nodes in preparation for executing the next step.
        /// </summary>
        /// <param name="step">The next step.</param>
        /// <param name="stepNodes">The set of node participating in the next setup step.</param>
        private void SetNodeInvolved(Step step, IEnumerable<NodeSshProxy<NodeMetadata>> stepNodes)
        {
            Covenant.Requires<ArgumentNullException>(step != null, nameof(step));
            Covenant.Requires<ArgumentNullException>(stepNodes != null, nameof(stepNodes));

            foreach (var node in nodes)
            {
                node.IsInvolved    = false;
                node.IsConfiguring = false;
                node.IsReady       = false;
                node.IsFaulted     = false;
            }

            // Set [node.IsInvolved = true] for non-quiet steps that involve
            // the node.

            if (!step.IsQuiet)
            {
                foreach (var node in stepNodes)
                {
                    node.IsInvolved = true;
                }
            }
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

                // NOTE: We're not going to notify the UX for quiet/hidden steps but
                //       we will report these in the logs.

                if (!step.IsQuiet)
                {
                    ProgressEvent?.Invoke(
                        new SetupProgressMessage()
                        {
                            IsError       = false,
                            CancelPending = false,
                            Node          = null,
                            Verb          = $"step {step.Number}",
                            Text          = step.Label
                        });

                    StepStarted?.Invoke(this, new SetupStepDetails(step.Number, step.Label));
                }

                LogGlobal($"");
                LogGlobal($"===============================================================================");
                
                if (step.IsQuiet)
                {
                    LogGlobal($"STEP (HIDDEN): {step.Label}");
                }
                else
                {
                    LogGlobal($"STEP {step.Number}: {step.Label}");
                }

                LogGlobal($"");

                step.State       = SetupStepState.Running;
                step.WasExecuted = true;

                var stepNodes         = nodes.Where(node => step.Predicate(this, node));
                var stepNodeNamesSet  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                SetNodeInvolved(step, stepNodes);

                foreach (var node in stepNodes)
                {
                    stepNodeNamesSet.Add(node.Name);
                }

                // Clear the node ready/status in preparation for executing the next step.

                foreach (var node in nodes)
                {
                    node.IsReady = false;
                    node.Status  = string.Empty;
                }

                foreach (var host in hosts)
                {
                    host.IsReady = false;
                    host.Status  = string.Empty;
                }

                var parallelOptions = new ParallelOptions()
                {
                    MaxDegreeOfParallelism = step.ParallelLimit > 0 ? step.ParallelLimit : MaxParallel
                };

                var stepThread = NeonHelper.StartThread(
                    () =>
                    {
                        var stepDisposition = SetupStepState.Done;

                        currentStep = step;

                        if (step.SyncNodeAction != null)
                        {
                            // Execute the step on the selected nodes.

                            Parallel.ForEach(stepNodes, parallelOptions,
                                node =>
                                {
                                    if (IsCancelPending)
                                    {
                                        stepDisposition = SetupStepState.Cancelled;
                                        return;
                                    }

                                    try
                                    {
                                        node.IsConfiguring = true;

                                        step.SyncNodeAction(this, node);

                                        node.Status = "[x] DONE";
                                        node.IsReady = true;
                                    }
                                    catch (OperationCanceledException)
                                    {
                                        stepDisposition = SetupStepState.Cancelled;
                                    }
                                    catch (Exception e)
                                    {
                                        stepDisposition = SetupStepState.Failed;

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
                                    if (IsCancelPending)
                                    {
                                        stepDisposition = SetupStepState.Cancelled;
                                        return;
                                    }

                                    try
                                    {
                                        var nodeDefinition = node.Metadata as NodeDefinition;

                                        node.IsConfiguring = true;

                                        var runTask = Task.Run(
                                            async () =>
                                            {
                                                await step.AsyncNodeAction(this, node);
                                            });

                                        runTask.WaitWithoutAggregate();

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

                                        stepDisposition = SetupStepState.Failed;

                                        node.Fault(NeonHelper.ExceptionError(e));
                                        node.LogException(e);
                                    }
                                });
                        }
                        else if (step.SyncGlobalAction != null)
                        {
                            if (IsCancelPending)
                            {
                                stepDisposition = SetupStepState.Cancelled;
                                return;
                            }

                            try
                            {
                                step.SyncGlobalAction(this);

                                foreach (var node in stepNodes)
                                {
                                    node.IsReady = true;
                                }

                                SetGlobalStepStatus();
                            }
                            catch (OperationCanceledException)
                            {
                                stepDisposition = SetupStepState.Cancelled;

                                SetGlobalStepStatus($"*** CANCELLED");
                            }
                            catch (Exception e)
                            {
                                isFaulted       = true;
                                stepDisposition = SetupStepState.Failed;

                                SetGlobalStepStatus($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                                LogGlobalException(e);
                            }
                        }
                        else if (step.AsyncGlobalAction != null)
                        {
                            if (IsCancelPending)
                            {
                                stepDisposition = SetupStepState.Cancelled;
                                return;
                            }

                            try
                            {
                                var runTask = Task.Run(
                                    async () =>
                                    {
                                        await step.AsyncGlobalAction(this);
                                    });

                                runTask.WaitWithoutAggregate();

                                foreach (var node in stepNodes)
                                {
                                    node.IsReady = true;
                                }

                                SetGlobalStepStatus();
                            }
                            catch (OperationCanceledException)
                            {
                                stepDisposition = SetupStepState.Cancelled;

                                SetGlobalStepStatus($"*** CANCELLED");
                            }
                            catch (AggregateException e)
                            {
                                if (e.Contains<OperationCanceledException>())
                                {
                                    stepDisposition = SetupStepState.Cancelled;

                                    SetGlobalStepStatus($"*** CANCELLED");
                                }
                                else
                                {
                                    isFaulted       = true;
                                    stepDisposition = SetupStepState.Failed;

                                    SetGlobalStepStatus($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                                    LogGlobalException(e);
                                }
                            }
                            catch (Exception e)
                            {
                                isFaulted       = true;
                                stepDisposition = SetupStepState.Failed;

                                SetGlobalStepStatus($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                                LogGlobalException(e);
                            }
                        }

                        step.State = stepDisposition;

                        // Log information about any faulted nodes.

                        var faultedNodesBuilder = new StringBuilder();

                        foreach (var node in nodes
                            .Where(node => node.IsFaulted)
                            .OrderBy(node => node.Name.ToLowerInvariant()))
                        {
                            faultedNodesBuilder.AppendWithSeparator(node.Name);
                        }

                        var faultedNodes = faultedNodesBuilder.ToString();

                        if (!string.IsNullOrEmpty(faultedNodes))
                        {
                            LogGlobal();
                            LogGlobalError($"FAULTED NODES: {faultedNodes}");
                            LogGlobal();
                        }
                    },
                    maxStackSize: maxStackSize);

                // While the setup step is executing above in a thread, we're going to loop here
                // and raise [StatusChangedEvent] when we detect a status change giving any UI
                // a chance to update.
                //
                // Note that we're going to loop here until the step execution thread above
                // terminates.

                var statusInterval = TimeSpan.FromMilliseconds(100);
                var lastJson       = (string)null;

                while (true)
                {
                    Covenant.Assert(ContainsKey(KubeSetupProperty.ClusterLogin), $"Setup controller is missing the required [{nameof(KubeSetupProperty.ClusterLogin)}] property.");

                    var status  = new SetupClusterStatus(this);
                    var newJson = NeonHelper.JsonSerialize(status);

                    if (lastJson == null || lastJson != newJson)
                    {
                        lock (syncLock)
                        {
                            StatusChangedEvent?.Invoke(status);
                        }

                        lastJson = newJson;
                    }

                    if (stepThread.Join(statusInterval))
                    {
                        // The step has completed executing.

                        break;
                    }
                }

                isFaulted = isFaulted || stepNodes.FirstOrDefault(node => node.IsFaulted) != null;

                return !IsFaulted;
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
                throw new NeonKubeException($"[{nodes.Count(n => n.IsFaulted)}] nodes are faulted.");
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
        public event EventHandler<SetupStepDetails> StepStarted;

        /// <inheritdoc/>
        public SetupConsoleWriter ConsoleWriter { get; private set; }

        /// <inheritdoc/>
        public event SetupProgressDelegate ProgressEvent;

        /// <inheritdoc/>
        public void LogProgress(string message)
        {
            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            SetGlobalStepStatus(message);

            if (ProgressEvent != null)
            {
                lock (syncLock)
                {
                    ProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Text          = message,
                            CancelPending = IsCancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogProgress(string verb, string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(verb), nameof(verb));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            SetGlobalStepStatus($"{verb}: {message}");

            if (ProgressEvent != null)
            {
                lock (syncLock)
                {
                    ProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Verb          = verb,
                            Text          = message,
                            CancelPending = IsCancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogProgress(SSH.ILinuxSshProxy node, string message)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            ((NodeSshProxy<NodeMetadata>)node).Status = message;

            if (ProgressEvent != null)
            {
                lock (syncLock)
                {
                    ProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Node          = node,
                            Text          = message,
                            CancelPending = IsCancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogProgress(SSH.ILinuxSshProxy node, string verb, string message)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(verb), nameof(verb));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            ((NodeSshProxy<NodeMetadata>)node).Status = $"{verb} {message}";

            if (ProgressEvent != null)
            {
                lock (syncLock)
                {
                    ProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Node          = node,
                            Verb          = verb,
                            Text          = message,
                            CancelPending = IsCancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogProgressError(string message)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            this.isFaulted         = true;
            this.LastProgressError = message;

            SetGlobalStepStatus($"ERROR: {message}");
            LogGlobalError(message);

            if (ProgressEvent != null)
            {
                lock (syncLock)
                {
                    ProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Text          = message,
                            IsError       = true,
                            CancelPending = IsCancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogProgressError(SSH.ILinuxSshProxy node, string message)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            ((NodeSshProxy<NodeMetadata>)node).Status    = message;
            ((NodeSshProxy<NodeMetadata>)node).IsFaulted = true;

            if (ProgressEvent != null)
            {
                lock (syncLock)
                {
                    ProgressEvent.Invoke(
                        new SetupProgressMessage()
                        {
                            Node          = node,
                            Text          = message,
                            IsError       = true,
                            CancelPending = IsCancelPending
                        });
                }
            }
        }

        /// <inheritdoc/>
        public void LogGlobal(string message = null)
        {
            clusterLogWriter?.WriteLine(message ?? string.Empty);
            clusterLogWriter?.Flush();
        }

        /// <inheritdoc/>
        public void LogGlobalError(string message = null)
        {
            LogGlobal($"*** ERROR: {message}");
        }

        /// <inheritdoc/>
        public void LogGlobalException(Exception e)
        {
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            LogGlobal();
            LogGlobal($"*** ERROR: {NeonHelper.ExceptionError(e)}");
            LogGlobal($"*** STACK:");
            LogGlobal(e.StackTrace);
        }

        /// <inheritdoc/>
        public bool IsFaulted => isFaulted || nodes.Any(node => node.IsFaulted);

        /// <inheritdoc/>
        public string LastProgressError { get; private set; }

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
        public bool ShowRuntime { get; set; } = false;

        /// <inheritdoc/>
        public IEnumerable<SetupStepStatus> GetStepStatus()
        {
            return steps.Select(step => new SetupStepStatus(step.Number, step.Label, step.State, step, step.RunTime));
        }

        /// <inheritdoc/>
        public Task<SetupDisposition> RunAsync(int maxStackSize = 250 * (int)ByteUnits.KibiBytes)
        {
            Covenant.Requires<ArgumentException>(maxStackSize >= 0, nameof(maxStackSize));

            // Set this so we can ensure that the step list can no longer be modified
            // after execution has started.

            this.isRunning    = true;
            this.maxStackSize = maxStackSize;

            // This method has been synchronous for a very long time (maybe 5 years).
            // We need to make this async now so that it will integrate well with
            // the neonDESKTOP UX. 
            //
            // We're simply going to wrap the main setup loop onto a new thread and
            // have setup execute there.  We'll use a task completion source to signal
            // the caller when we're done.

            var cluster = Get<ClusterProxy>(KubeSetupProperty.ClusterProxy, null);
            var tcs     = new TaskCompletionSource<SetupDisposition>();

            // Initialize the global logger.

            Directory.CreateDirectory(Path.GetDirectoryName(clusterLogPath));

            clusterLogWriter = new StreamWriter(new FileStream(clusterLogPath, FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite));

            // Start the step execution thread.

            NeonHelper.StartThread(
                () =>
                {
                    try
                    {
                        LogGlobal(LogBeginMarker);
                        cluster?.LogLine(LogBeginMarker);

                        // Number the steps.  Note that [quiet] steps aren't assigned a step number.

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

                        foreach (var step in steps)
                        {
                            if (IsCancelPending)
                            {
                                step.State = SetupStepState.Cancelled;

                                // Close out the global log.

                                CloseClusterLog(LogFailedMarker);
                                cluster?.LogLine(LogFailedMarker);
                                ConsoleWriter?.Stop();

                                // We need to close the cluster log first so the [Finished] event handles
                                // will be able to open and read the log files to capture telemetry.

                                CloseClusterLog(LogFailedMarker);

                                // Give any [Finished] handlers a chance to do their thing
                                // before stopping the controller.

                                RaiseFinished(new OperationCanceledException());

                                Cleanup();
                                tcs.TrySetResult(SetupDisposition.Cancelled);
                            }

                            if (!ExecuteStep(step))
                            {
                                break;
                            }
                        }

                        if (IsFaulted)
                        {
                            // Log the status of any faulted hosts.

                            if (hosts.Any(host => host.IsFaulted))
                            {
                                LogGlobal();
                                LogGlobal("FAULTED HOSTS:");
                                LogGlobal();

                                var maxNodeName = nodes.Max(node => node.Name.Length);
                                var nameColumnWidth = maxNodeName + 4;

                                foreach (var host in hosts
                                    .Where(host => host.IsFaulted)
                                    .OrderBy(host => host.Name.ToLowerInvariant()))
                                {
                                    var nameColumn = host.Name + ":";
                                    var nameFiller = new string(' ', nameColumnWidth - nameColumn.Length);

                                    LogGlobal($"{nameColumn}{nameFiller}{host.Status}");
                                }
                            }

                            // Log the status of any faulted nodes.

                            if (nodes.Any(node => node.IsFaulted))
                            {
                                LogGlobal();
                                LogGlobal("FAULTED NODES:");
                                LogGlobal();

                                var maxNodeName = nodes.Max(node => node.Name.Length);
                                var nameColumnWidth = maxNodeName + 4;

                                foreach (var node in nodes
                                    .Where(node => node.IsFaulted)
                                    .OrderBy(node => node.Name.ToLowerInvariant()))
                                {
                                    var nameColumn = node.Name + ":";
                                    var nameFiller = new string(' ', nameColumnWidth - nameColumn.Length);

                                    LogGlobal($"{nameColumn}{nameFiller}{node.Status}");
                                }
                            }

                            // We need to close the cluster log first so the [Finished] event handles
                            // will be able to open and read the log files to capture telemetry.

                            CloseClusterLog(LogFailedMarker);

                            // Give any [Finished] handlers a chance to do their thing
                            // before stopping the controller.

                            RaiseFinished(new NeonKubeException("One or more nodes are faulted."));

                            // Close out the global log.

                            ConsoleWriter?.Stop();

                            Cleanup();
                            tcs.TrySetResult(SetupDisposition.Failed);
                            return;
                        }

                        foreach (var node in nodes)
                        {
                            node.Status = IsCancelPending ? "[x] CANCELLED" : "[x] READY";
                        }

                        // Raise one more status changed and then stop the console writer
                        // so the console will be configured to write normally.

                        if (StatusChangedEvent != null)
                        {
                            lock (syncLock)
                            {
                                StatusChangedEvent?.Invoke(new SetupClusterStatus(this));
                            }
                        }

                        // We need to close the cluster log first so the [Finished] event handles
                        // will be able to open and read the log files to capture telemetry.

                        CloseClusterLog(LogFailedMarker);

                        // Give any [Finished] handlers a chance to do their thing
                        // before stopping the controller.

                        RaiseFinished();

                        // Close out the global log.

                        ConsoleWriter?.Stop();
                        Cleanup();

                        tcs.TrySetResult(IsCancelPending ? SetupDisposition.Cancelled : SetupDisposition.Succeeded);
                        return;
                    }
                    catch (OperationCanceledException)
                    {
                        if (StatusChangedEvent != null)
                        {
                            lock (syncLock)
                            {
                                StatusChangedEvent?.Invoke(new SetupClusterStatus(this));
                            }
                        }

                        // Close out the global log.

                        CloseClusterLog(LogFailedMarker);
                        ConsoleWriter?.Stop();
                        Cleanup();

                        tcs.TrySetResult(SetupDisposition.Cancelled);
                    }
                    catch (AggregateException e)
                    {
                        if (e.Contains<OperationCanceledException>())
                        {
                            if (StatusChangedEvent != null)
                            {
                                lock (syncLock)
                                {
                                    StatusChangedEvent?.Invoke(new SetupClusterStatus(this));
                                }
                            }

                            CloseClusterLog(LogFailedMarker);

                            // Give any [Finished] handlers a chance to do their thing
                            // before stopping the controller.

                            RaiseFinished(e);

                            // Close out the global log.

                            ConsoleWriter?.Stop();
                            Cleanup();

                            tcs.TrySetResult(SetupDisposition.Cancelled);
                        }
                        else
                        {
                            if (StatusChangedEvent != null)
                            {
                                lock (syncLock)
                                {
                                    StatusChangedEvent?.Invoke(new SetupClusterStatus(this));
                                }
                            }

                            CloseClusterLog(LogFailedMarker);

                            // Give any [Finished] handlers a chance to do their thing
                            // before stopping the controller.

                            RaiseFinished(e);

                            // Close out the global log.

                            ConsoleWriter?.Stop();
                            Cleanup();
                            tcs.TrySetException(e);
                        }
                    }
                    catch (Exception e)
                    {
                        if (StatusChangedEvent != null)
                        {
                            lock (syncLock)
                            {
                                StatusChangedEvent?.Invoke(new SetupClusterStatus(this));
                            }
                        }

                        // We need to close the cluster log first so the [Finished] event handles
                        // will be able to open and read the log files to capture telemetry.

                        CloseClusterLog(LogFailedMarker);

                        // Give any [Finished] handlers a chance to do their thing
                        // before stopping the controller.

                        RaiseFinished(e);

                        // Close out the global log.

                        ConsoleWriter?.Stop();
                        Cleanup();
                        tcs.TrySetException(e);
                    }
                    finally
                    {
                        Cleanup();
                    }
                },
                maxStackSize: maxStackSize);

            return tcs.Task;
        }

        /// <summary>
        /// Closes the cluster log file if it's open.
        /// </summary>
        /// <param name="logEndMarker">Specifies the marker line to be used to end the log.</param>
        private void CloseClusterLog(string logEndMarker)
        {
            if (clusterLogWriter == null)
            {
                return;
            }

            LogGlobal();
            LogGlobal(logEndMarker);

            clusterLogWriter?.Flush();
            clusterLogWriter?.Dispose();
            clusterLogWriter = null;
        }

        /// <summary>
        /// Handles disposing the cluster proxy, any disposables, along with 
        /// the cluster log writer.
        /// </summary>
        private void Cleanup()
        {
            var cluster = this.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            cluster?.Dispose();

            // Dispose any disposables.

            foreach (var disposable in disposables)
            {
                disposable.Dispose();
            }

            // Close the global log.

            CloseClusterLog(LogEndMarker);
        }

        /// <inheritdoc/>
        public void Cancel()
        {
            if (!cts.IsCancellationRequested)
            {
                cts.Cancel();
            }
        }

        /// <inheritdoc/>
        public void ThrowIfCancelled()
        {
            cts.Token.ThrowIfCancellationRequested();
        }

        /// <inheritdoc/>
        public CancellationToken CancellationToken => cts.Token;

        /// <inheritdoc/>
        public bool IsCancelPending => cts.IsCancellationRequested;

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

        /// <inheritdoc/>
        public IEnumerable<SetupNodeStatus> GetNodeStatus()
        {
            return nodes
                .OrderBy(node => node.Name, StringComparer.InvariantCultureIgnoreCase)
                .Select(node => new SetupNodeStatus(node, node.Metadata))
                .ToArray();
        }

        /// <inheritdoc/>
        public bool DisablePendingTasks { get; set; } = false;

        /// <inheritdoc/>
        public async Task AddPendingTaskAsync(string groupName, Task task, string verb, string message, INodeSshProxy node = null)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(groupName), nameof(groupName));
            Covenant.Requires<ArgumentNullException>(task != null, nameof(task));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(verb), nameof(verb));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(message), nameof(message));

            if (DisablePendingTasks)
            {
                lock (syncLock)
                {
                    if (!pendingGroups.TryGetValue(groupName, out var group))
                    {
                        pendingGroups.Add(groupName, new SetupPendingTasks());
                    }

                    if (node != null)
                    {
                        LogProgress(node, verb: verb, message: message);
                    }
                    else
                    {
                        LogProgress(verb: verb, message: message);
                    }
                }

                await task;

                if (node != null)
                {
                    LogProgress(node, verb: string.Empty, message: string.Empty);
                }
                else
                {
                    LogProgress(verb: string.Empty, message: string.Empty);
                }
            }
            else
            {
                lock (syncLock)
                {
                    if (!pendingGroups.TryGetValue(groupName, out var group))
                    {
                        group = new SetupPendingTasks();

                        pendingGroups.Add(groupName, group);
                    }

                    group.Add(task, verb, message, node);
                }
            }
        }

        /// <inheritdoc/>
        public async Task WaitForPendingTasksAsync(string groupName)
        {
            await SyncContext.Clear;

            SetupPendingTasks group;

            lock (syncLock)
            {
                group = pendingGroups[groupName];
            }

            await group.WaitAsync(this);
        }

        /// <inheritdoc/>
        public List<string> GetPendingGroups()
        {
            lock (syncLock)
            {
                return pendingGroups
                    .Where(item => !item.Value.IsComplete)
                    .Select(item => item.Key)
                    .ToList();
            }
        }
    }
}
