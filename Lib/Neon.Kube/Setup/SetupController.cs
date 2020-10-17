//-----------------------------------------------------------------------------
// FILE:	    SetupController.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Manages a cluster setup operation consisting of a series of  setup operations
    /// steps, while displaying status to the <see cref="Console"/>.
    /// </summary>
    /// <typeparam name="NodeMetadata">Specifies the node metadata type.</typeparam>
    public class SetupController<NodeMetadata>
        where NodeMetadata : class
    {
        //---------------------------------------------------------------------
        // Private types

        private enum StepStatus
        {
            None,
            Running,
            Done,
            Failed
        }

        private class Step
        {
            public int                                          Number;
            public string                                       Label;
            public bool                                         Quiet;
            public Action                                       SyncGlobalAction;
            public Func<Task>                                   AsyncGlobalAction;
            public Action<LinuxSshProxy<NodeMetadata>, TimeSpan>     SyncNodeAction;
            public Func<LinuxSshProxy<NodeMetadata>, TimeSpan, Task> AsyncNodeAction;
            public Func<LinuxSshProxy<NodeMetadata>, bool>           Predicate;
            public StepStatus                                   Status;
            public int                                          ParallelLimit;
            public TimeSpan                                     StepStagger;
        }

        //---------------------------------------------------------------------
        // Implementation

        private const int UnlimitedParallel = 500;  // Treat this as "unlimited"

        private string                                          operationTitle;
        private string                                          operationStatus;
        private List<LinuxSshProxy<NodeMetadata>>                    nodes;
        private List<Step>                                      steps;
        private Step                                            currentStep;
        private bool                                            error;
        private bool                                            hasNodeSteps;
        private StringBuilder                                   sbDisplay;
        private string                                          lastDisplay;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="operationTitle">Summarizes the high-level operation being performed.</param>
        /// <param name="nodes">The node proxies for the cluster nodes being manipulated.</param>
        public SetupController(string operationTitle, IEnumerable<LinuxSshProxy<NodeMetadata>> nodes)
            : this(new string[] { operationTitle }, nodes)
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="operationTitle">Summarizes the high-level operation being performed.</param>
        /// <param name="nodes">The node proxies for the cluster nodes being manipulated.</param>
        public SetupController(string[] operationTitle, IEnumerable<LinuxSshProxy<NodeMetadata>> nodes)
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

            this.operationTitle  = title;
            this.operationStatus = string.Empty;
            this.nodes           = nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase).ToList();
            this.steps           = new List<Step>();
            this.sbDisplay       = new StringBuilder();
            this.lastDisplay     = string.Empty;
        }

        /// <summary>
        /// Specifies whether the class should print setup status to the console.
        /// This defaults to <c>false</c>.
        /// </summary>
        public bool ShowStatus { get; set; } = false;

        /// <summary>
        /// Specifies whether that node status will be displayed.  This
        /// defaults to <c>true</c>.
        ///</summary>
        public bool ShowNodeStatus { get; set; } = true;

        /// <summary>
        /// Specifies the maximum number of setup steps to be displayed.
        /// This defaults to <b>5</b>.  You can set <b>0</b> to allow an 
        /// unlimited number of steps may be displayed.
        /// </summary>
        public int MaxDisplayedSteps { get; set; } = 5;

        /// <summary>
        /// The maximum number of nodes that will execute setup steps in parallel.  This
        /// defaults to effectively unconstrained.
        /// </summary>
        public int MaxParallel { get; set; } = int.MaxValue;

        /// <summary>
        /// Returns the number of setup steps.
        /// </summary>
        public int StepCount => steps.Count;

        /// <summary>
        /// Sets the <see cref="LinuxSshProxy{TMetadata}.DefaultRunOptions"/> property for
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
        /// <param name="stepLabel">Brief step summary.</param>
        /// <param name="action">The synchronous global action to be performed.</param>
        /// <param name="quiet">Optionally specifies that the step is not to be reported in the progress.</param>
        /// <param name="position">
        /// The optional zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        public void AddGlobalStep(string stepLabel, Action action, bool quiet = false, int position = -1)
        {
            if (position < 0)
            {
                position = steps.Count;
            }

            steps.Insert(
                position,
                new Step()
                {
                    Label        = stepLabel,
                    Quiet        = quiet,
                    SyncGlobalAction = action,
                    Predicate    = n => true,
                });
        }

        /// <summary>
        /// Adds an asynchronous global configuration step.
        /// </summary>
        /// <param name="stepLabel">Brief step summary.</param>
        /// <param name="action">The asynchronous global action to be performed.</param>
        /// <param name="quiet">Optionally specifies that the step is not to be reported in the progress.</param>
        /// <param name="position">
        /// The optional zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        public void AddGlobalStep(string stepLabel, Func<Task> action, bool quiet = false, int position = -1)
        {
            if (position < 0)
            {
                position = steps.Count;
            }

            steps.Insert(
                position,
                new Step()
                {
                    Label             = stepLabel,
                    Quiet             = quiet,
                    AsyncGlobalAction = action,
                    Predicate         = n => true,
                });
        }

        /// <summary>
        /// Adds a synchronous global step that waits for all nodes to be online.
        /// </summary>
        /// <param name="stepLabel">Brief step summary.</param>
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
        public void AddWaitUntilOnlineStep(
            string                              stepLabel     = "connect", 
            string                              status        = null, 
            Func<LinuxSshProxy<NodeMetadata>, bool>  nodePredicate = null, 
            bool                                quiet         = false, 
            TimeSpan?                           timeout       = null, 
            int                                 position      = -1)
        {
            if (timeout == null)
            {
                timeout = TimeSpan.FromMinutes(10);
            }
            if (position < 0)
            {
                position = steps.Count;
            }

            AddNodeStep(stepLabel,
                (node, stepDelay) =>
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
        /// <param name="stepLabel">Brief step summary.</param>
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
        public void AddDelayStep(
            string                              stepLabel, 
            TimeSpan                            delay, 
            string                              status        = null,
            Func<LinuxSshProxy<NodeMetadata>, bool>  nodePredicate = null, 
            bool                                quiet         = false, 
            int                                 position      = -1)
        {
            AddNodeStep(stepLabel,
                (node, stepDeley) =>
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
        /// <param name="stepLabel">Brief step summary.</param>
        /// <param name="nodeAction">
        /// The action to be performed on each node.  Two parameters will be passed
        /// to this action: the node's <see cref="LinuxSshProxy{T}"/> and a <see cref="TimeSpan"/>
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
        /// <param name="stepStaggerSeconds">
        /// Optionally specifies the time delay used to stagger execution
        /// of the nodes executing this step.  Setting a non-zero value of
        /// perhaps 5 seconds will help mitigate problems with multiple
        /// accessing nodes trying to download the same files from
        /// the Internet at the same time, potentially causing the remote
        /// endpoint to start throttling access.
        /// </param>
        /// <param name="position">
        /// Optionally specifies the zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        /// <param name="parallelLimit">
        /// Optionally specifies the maximum number of operations to be performed
        /// in parallel for this step, overriding the controller default.
        /// </param>
        public void AddNodeStep(
            string stepLabel,
            Action<LinuxSshProxy<NodeMetadata>, TimeSpan>    nodeAction,
            Func<LinuxSshProxy<NodeMetadata>, bool>          nodePredicate      = null,
            bool                                        quiet              = false,
            bool                                        noParallelLimit    = false,
            int                                         stepStaggerSeconds = 0,
            int                                         position           = -1,
            int                                         parallelLimit      = 0)
        {
            nodeAction    = nodeAction ?? new Action<LinuxSshProxy<NodeMetadata>, TimeSpan>((n, d) => { });
            nodePredicate = nodePredicate ?? new Func<LinuxSshProxy<NodeMetadata>, bool>(n => true);

            if (position < 0)
            {
                position = steps.Count;
            }

            var step = new Step()
            {
                Label          = stepLabel,
                Quiet          = quiet,
                SyncNodeAction = nodeAction,
                Predicate      = nodePredicate,
                ParallelLimit  = noParallelLimit ? UnlimitedParallel : 0,
                StepStagger    = TimeSpan.FromSeconds(stepStaggerSeconds)
            };

            if (parallelLimit > 0)
            {
                step.ParallelLimit = parallelLimit;
            }

            steps.Insert(position, step);
        }

        /// <summary>
        /// Appends an asynchronous node configuration step.
        /// </summary>
        /// <param name="stepLabel">Brief step summary.</param>
        /// <param name="nodeAction">
        /// The action to be performed on each node.  Two parameters will be passed
        /// to this action: the node's <see cref="LinuxSshProxy{T}"/> and a <see cref="TimeSpan"/>
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
        /// <param name="stepStaggerSeconds">
        /// Optionally specifies the time delay used to stagger execution
        /// of the nodes executing this step.  Setting a non-zero value of
        /// perhaps 5 seconds will help mitigate problems with multiple
        /// accessing nodes trying to download the same files from
        /// the Internet at the same time, potentially causing the remote
        /// endpoint to start throttling access.
        /// </param>
        /// <param name="position">
        /// Optionally specifies the zero-based index of the position where the step is
        /// to be inserted into the step list.
        /// </param>
        /// <param name="parallelLimit">
        /// Optionally specifies the maximum number of operations to be performed
        /// in parallel for this step, overriding the controller default.
        /// </param>
        public void AddNodeStep(
            string stepLabel,
            Func<LinuxSshProxy<NodeMetadata>, TimeSpan, Task>    nodeAction,
            Func<LinuxSshProxy<NodeMetadata>, bool>              nodePredicate      = null,
            bool                                            quiet              = false,
            bool                                            noParallelLimit    = false,
            int                                             stepStaggerSeconds = 0,
            int                                             position           = -1,
            int                                             parallelLimit      = 0)
        {
            nodeAction    = nodeAction ?? new Func<LinuxSshProxy<NodeMetadata>, TimeSpan, Task>((n, d) => { return Task.CompletedTask; });
            nodePredicate = nodePredicate ?? new Func<LinuxSshProxy<NodeMetadata>, bool>(n => true);

            if (position < 0)
            {
                position = steps.Count;
            }

            var step = new Step()
            {
                Label           = stepLabel,
                Quiet           = quiet,
                AsyncNodeAction = nodeAction,
                Predicate       = nodePredicate,
                ParallelLimit   = noParallelLimit ? UnlimitedParallel : 0,
                StepStagger     = TimeSpan.FromSeconds(stepStaggerSeconds)
            };

            if (parallelLimit > 0)
            {
                step.ParallelLimit = parallelLimit;
            }

            steps.Insert(position, step);
        }

        /// <summary>
        /// Performs the operation steps in the order they were added.
        /// </summary>
        /// <param name="leaveNodesConnected">Pass <c>true</c> leave the node proxies connected.</param>
        /// <returns><c>true</c> if all steps completed successfully.</returns>
        public bool Run(bool leaveNodesConnected = false)
        {
            // Number the steps.  Note that quiet steps don't 
            // get their own step number.

            var position = 1;

            foreach (var step in steps)
            {
                if (step.Quiet)
                {
                    step.Number = position;
                }
                else
                {
                    step.Number = position++;
                }
            }

            // We don't display node status if there aren't any node specific steps.

            hasNodeSteps = steps.Exists(s => s.SyncNodeAction != null || s.AsyncNodeAction != null);

            try
            {
                foreach (var step in steps)
                {
                    currentStep = step;

                    try
                    {
                        if (!PerformStep(step))
                        {
                            break;
                        }
                    }
                    finally
                    {
                        currentStep = null;
                    }
                }

                if (error)
                {
                    return false;
                }

                foreach (var node in nodes)
                {
                    node.Status = "[x] READY";
                }

                DisplayStatus();
                return true;
            }
            finally
            {
                if (!leaveNodesConnected)
                {
                    // Disconnect all of the nodes.

                    foreach (var node in nodes)
                    {
                        node.Disconnect();
                    }
                }

                Console.WriteLine();    // Add an extra line after the status to look nice.
            }
        }

        /// <summary>
        /// Sets the optation status text.
        /// </summary>
        /// <param name="status">The optional operation status text.</param>
        public void SetOperationStatus(string status = null)
        {
            operationStatus = status ?? string.Empty;
        }

        /// <summary>
        /// Performs an operation step on the selected nodes.
        /// </summary>
        /// <param name="step">A step being performed.</param>
        /// <returns><c>true</c> if the step succeeded.</returns>
        /// <remarks>
        /// <para>
        /// This method begins by setting the <see cref="LinuxSshProxy{TMetadata}.IsReady"/>
        /// state of each selected node to <c>false</c> and then it starts a new thread for
        /// each node and performs the action on these servnodeer threads.
        /// </para>
        /// <para>
        /// In parallel, the method spins on the current thread, displaying status while
        /// waiting for each of the nodes to transition to the <see cref="LinuxSshProxy{TMetadata}.IsReady"/>=<c>true</c>
        /// state.
        /// </para>
        /// <para>
        /// The method returns <c>true</c> after all of the node actions have completed
        /// and none of the nodes have <see cref="LinuxSshProxy{TMetadata}.IsFaulted"/>=<c>true</c>.
        /// </para>
        /// <note>
        /// This method does nothing if a previous step failed.
        /// </note>
        /// </remarks>
        private bool PerformStep(Step step)
        {
            if (error)
            {
                return false;
            }

            step.Status = StepStatus.Running;

            var stepNodes        = nodes.Where(step.Predicate);
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

            DisplayStatus(stepNodeNamesSet);

            var parallelOptions = new ParallelOptions()
            {
                MaxDegreeOfParallelism = step.ParallelLimit > 0 ? step.ParallelLimit : MaxParallel
            };

            NeonHelper.ThreadRun(
                () =>
                {
                    if (step.SyncNodeAction != null)
                    {
                        // Compute the amount of time to delay before executing
                        // the step on each node.  We're going to spread the step
                        // delay evenly across the nodes.
                        //
                        // Note that this only works when [NodeMetadata==NodeDefinition]

                        if (typeof(NodeMetadata) == typeof(NodeDefinition))
                        {
                            if (step.StepStagger <= TimeSpan.Zero || stepNodes.Count() <= 1)
                            {
                                foreach (var node in stepNodes)
                                {
                                    (node.Metadata as NodeDefinition).StepDelay = TimeSpan.Zero;
                                }
                            }
                            else
                            {
                                var delay = TimeSpan.Zero;

                                foreach (var node in stepNodes)
                                {
                                    (node.Metadata as NodeDefinition).StepDelay = delay;
                                    delay += step.StepStagger;
                                }
                            }
                        }

                        // Execute the step on the selected nodes.

                        Parallel.ForEach(stepNodes, parallelOptions,
                            node =>
                            {
                                try
                                {
                                    var nodeDefinition = node.Metadata as NodeDefinition;
                                    var stepDelay      = TimeSpan.Zero;

                                    if (nodeDefinition != null && nodeDefinition.StepDelay > TimeSpan.Zero)
                                    {
                                        stepDelay = nodeDefinition.StepDelay;
                                    }

                                    step.SyncNodeAction(node, stepDelay);

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
                        // Compute the amount of time to delay before executing
                        // the step on each node.  We're going to spread the step
                        // delay evenly across the nodes.
                        //
                        // Note that this only works when [NodeMetadata==NodeDefinition]

                        if (typeof(NodeMetadata) == typeof(NodeDefinition))
                        {
                            if (step.StepStagger <= TimeSpan.Zero || stepNodes.Count() <= 1)
                            {
                                foreach (var node in stepNodes)
                                {
                                    (node.Metadata as NodeDefinition).StepDelay = TimeSpan.Zero;
                                }
                            }
                            else
                            {
                                var delay = TimeSpan.Zero;

                                foreach (var node in stepNodes)
                                {
                                    (node.Metadata as NodeDefinition).StepDelay = delay;
                                    delay += step.StepStagger;
                                }
                            }
                        }

                        // Execute the step on the selected nodes.

                        Parallel.ForEach(stepNodes, parallelOptions,
                            node =>
                            {
                                try
                                {
                                    var nodeDefinition = node.Metadata as NodeDefinition;
                                    var stepDelay      = TimeSpan.Zero;

                                    if (nodeDefinition != null && nodeDefinition.StepDelay > TimeSpan.Zero)
                                    {
                                        stepDelay = nodeDefinition.StepDelay;
                                    }

                                    var runTask = Task.Run(
                                        async () =>
                                        {
                                            await step.AsyncNodeAction(node, stepDelay);
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
                            step.SyncGlobalAction();
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
                                    .Where(n => (n.Metadata as NodeDefinition).IsMaster)
                                    .OrderBy(n => n.Name)
                                    .First();

                                firstMaster.Fault(NeonHelper.ExceptionError(e));
                                firstMaster.LogException(e);
                            }
                        }

                        foreach (var node in stepNodes)
                        {
                            node.IsReady = true;
                        }
                    }
                    else if (step.AsyncGlobalAction != null)
                    {
                        try
                        {
                            var runTask = Task.Run(
                                async () =>
                                {
                                    await step.AsyncGlobalAction();
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
                                    .Where(n => (n.Metadata as NodeDefinition).IsMaster)
                                    .OrderBy(n => n.Name)
                                    .First();

                                firstMaster.Fault(NeonHelper.ExceptionError(e));
                                firstMaster.LogException(e);
                            }
                        }

                        foreach (var node in stepNodes)
                        {
                            node.IsReady = true;
                        }
                    }
                });

            while (true)
            {
                DisplayStatus(stepNodeNamesSet);

                if (stepNodes.Count(n => !n.IsReady) == 0)
                {
                    DisplayStatus(stepNodeNamesSet);
                    break;
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(100));
            }

            error = stepNodes.FirstOrDefault(n => n.IsFaulted) != null;

            if (error)
            {
                step.Status = StepStatus.Failed;

                return false;
            }
            else
            {
                step.Status = StepStatus.Done;

                return true;
            }
        }

        /// <summary>
        /// Returns the current status for a node.
        /// </summary>
        /// <param name="stepNodeNamesSet">The set of node names participating in the current step.</param>
        /// <param name="node">The node being queried.</param>
        /// <returns>The status prefix.</returns>
        private string GetStatus(HashSet<string> stepNodeNamesSet, LinuxSshProxy<NodeMetadata> node)
        {
            if (stepNodeNamesSet != null && !stepNodeNamesSet.Contains(node.Name))
            {
                return "  -";
            }
            else
            {
                // We mark completed steps with a "[x] " or "[!] " prefix and
                // indent non-completed steps status with four blanks.

                if (node.Status.StartsWith("[x] ") || node.Status.StartsWith("[!] "))
                {
                    return node.Status;
                }
                else
                {
                    return "    " + node.Status;
                }
            }
        }

        /// <summary>
        /// Formats a step index into a form suitable for display.
        /// </summary>
        /// <param name="stepNumber">The step index.</param>
        /// <returns>The formatted step number.</returns>
        private string FormatStepNumber(int stepNumber)
        {
            int     stepCount = steps.Count();
            string  number;

            if (stepCount < 10)
            {
                number = $"{stepNumber,1}";
            }
            else if (stepCount < 100)
            {
                number = $"{stepNumber,2}";
            }
            else
            {
                number = stepNumber.ToString();
            }

            return $"{number}. ";
        }

        /// <summary>
        /// Displays the current operation status on the <see cref="Console"/>.
        /// </summary>
        /// <param name="stepNodeNamesSet">
        /// The set of node names that participating in the current step or
        /// <c>null</c> if all nodes are included.
        /// </param>
        private void DisplayStatus(HashSet<string> stepNodeNamesSet = null)
        {
            if (!ShowStatus || steps.Count == 0)
            {
                return;
            }

            var maxStepLabelWidth = steps.Max(n => n.Label.Length);
            var maxNodeNameWidth  = nodes.Max(n => n.Name.Length);
            var maxHostNameWidth  = 0;
            
            if (typeof(NodeMetadata).Implements<IXenClient>())
            {
                maxHostNameWidth = nodes.Max(n => (n.Metadata as IXenClient).Name.Length);
            }

            sbDisplay.Clear();

            sbDisplay.AppendLine();
            sbDisplay.AppendLine($" {operationTitle}");

            var displaySteps     = steps.Where(s => !s.Quiet);
            var showStepProgress = false;

            if (MaxDisplayedSteps > 0 && MaxDisplayedSteps < displaySteps.Count())
            {
                // Limit the display steps to just those around the currently
                // executing step.

                var displayStepsCount = displaySteps.Count();
                var runningStep       = steps.FirstOrDefault(s => s.Status == StepStatus.Running);

                if (runningStep != null)
                {
                    showStepProgress = true;

                    if (runningStep.Number <= 1)
                    {
                        displaySteps = displaySteps.Where(s => s.Number <= MaxDisplayedSteps);
                    }
                    else if (runningStep.Number >= displayStepsCount - MaxDisplayedSteps + 1)
                    {
                        displaySteps = displaySteps.Where(s => s.Number >= displayStepsCount - MaxDisplayedSteps + 1);
                    }
                    else
                    {
                        var firstDisplayedNumber = runningStep.Number - MaxDisplayedSteps / 2;
                        var lastDisplayedNumber  = firstDisplayedNumber + MaxDisplayedSteps - 1;

                        displaySteps = displaySteps.Where(s => firstDisplayedNumber <= s.Number && s.Number <= lastDisplayedNumber);
                    }
                }
            }

            sbDisplay.AppendLine();

            if (showStepProgress)
            {
                var width     = maxStepLabelWidth + "[x] DONE".Length + 2;
                var stepCount = steps.Count(s => !s.Quiet);
                var progress  = new string('-', Math.Max(0, (int)(width * ((currentStep.Number - 1.0) / stepCount)) - 1));

                if (progress.Length > 0)
                {
                    progress += ">";
                }

                if (progress.Length < width)
                {
                    progress += new string(' ', width - progress.Length);
                }

                sbDisplay.AppendLine($" Steps: [{progress}]");
                sbDisplay.AppendLine();
            }
            else
            {
                sbDisplay.AppendLine(" Steps:");
            }

            foreach (var step in displaySteps)
            {
                switch (step.Status)
                {
                    case StepStatus.None:

                        sbDisplay.AppendLine($"     {FormatStepNumber(step.Number)}{step.Label}");
                        break;

                    case StepStatus.Running:

                        sbDisplay.AppendLine($" --> {FormatStepNumber(step.Number)}{step.Label}");
                        break;

                    case StepStatus.Done:

                        sbDisplay.AppendLine($"     {FormatStepNumber(step.Number)}{step.Label}{new string(' ', maxStepLabelWidth - step.Label.Length)}   [x] DONE");
                        break;

                    case StepStatus.Failed:

                        sbDisplay.AppendLine($"     {FormatStepNumber(step.Number)}{step.Label}{new string(' ', maxStepLabelWidth - step.Label.Length)}   [!] FAIL"); ;
                        break;
                }
            }

            if (hasNodeSteps && ShowNodeStatus)
            {
                // $hack(jefflill):
                //
                // I'm hardcoding the status display here for two scenarios:
                //
                //      1. Configuring cluster nodes with [NodeDefinition] metadata which.
                //      2. Provisioning cluster nodes on XenServer and remote Hyper-V hosts.
                //
                // It would be more flexible to implement some kind of callback or virtual
                // method to handle this.

                if (typeof(NodeMetadata) == typeof(NodeDefinition))
                {
                    // Configuring cluster nodes with [NodeDefinition] metadata which.

                    if (nodes.First().Metadata != null)
                    {
                        if (nodes.Exists(n => (n.Metadata as NodeDefinition).IsMaster))
                        {
                            sbDisplay.AppendLine();
                            sbDisplay.AppendLine(" Masters:");

                            foreach (var node in nodes.Where(n => (n.Metadata as NodeDefinition).IsMaster))
                            {
                                sbDisplay.AppendLine($"    {node.Name}{new string(' ', maxNodeNameWidth - node.Name.Length)}   {GetStatus(stepNodeNamesSet, node)}");
                            }
                        }

                        if (nodes.Exists(n => (n.Metadata as NodeDefinition).IsWorker))
                        {
                            sbDisplay.AppendLine();
                            sbDisplay.AppendLine(" Workers:");

                            foreach (var node in nodes.Where(n => (n.Metadata as NodeDefinition).IsWorker))
                            {
                                sbDisplay.AppendLine($"    {node.Name}{new string(' ', maxNodeNameWidth - node.Name.Length)}   {GetStatus(stepNodeNamesSet, node)}");
                            }
                        }
                    }
                    else
                    {
                        sbDisplay.AppendLine();
                        sbDisplay.AppendLine(" Nodes:");

                        foreach (var node in nodes)
                        {
                            sbDisplay.AppendLine($"    {node.Name}{new string(' ', maxNodeNameWidth - node.Name.Length)}   {GetStatus(stepNodeNamesSet, node)}");
                        }
                    }
                }
                else if (typeof(NodeMetadata).Implements<IXenClient>())
                {
                    // Provisioning cluster nodes on XenServer hosts.

                    sbDisplay.AppendLine();
                    sbDisplay.AppendLine(" Hypervisor Hosts:");

                    foreach (var node in nodes.OrderBy(n => (n.Metadata as IXenClient).Name, StringComparer.InvariantCultureIgnoreCase))
                    {
                        var xenHost = node.Metadata as IXenClient;

                        sbDisplay.AppendLine($"    {xenHost.Name}{new string(' ', maxHostNameWidth - xenHost.Name.Length)}: {GetStatus(stepNodeNamesSet, node)}");
                    }
                }
            }

            if (!string.IsNullOrEmpty(operationStatus))
            {
                sbDisplay.AppendLine();
                sbDisplay.AppendLine($"*** {operationStatus}");
            }

            var newDisplay = sbDisplay.ToString();

            if (newDisplay != lastDisplay)
            {
                Console.Clear();
                Console.Write(newDisplay);

                lastDisplay = newDisplay;
            }
        }

        /// <summary>
        /// Throws an exception if any of the operation steps did not complete successfully.
        /// </summary>
        public void ThrowOnError()
        {
            if (error)
            {
                throw new KubeException($"[{nodes.Count(n => n.IsFaulted)}] nodes are faulted.");
            }
        }
    }
}
