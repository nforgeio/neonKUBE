//-----------------------------------------------------------------------------
// FILE:        SetupClusterStatus.Console.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube.ClusterDef;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace Neon.Kube.Setup
{
    public partial class SetupClusterStatus
    {
        //---------------------------------------------------------------------
        // Code that writes the cluster status to the Console.

        /// <summary>
        /// Returns the current status for a node.
        /// </summary>
        /// <param name="stepNodeNames">The set of node names participating in the current step.</param>
        /// <param name="node">The node being queried.</param>
        /// <returns>The status.</returns>
        private string GetStatus(HashSet<string> stepNodeNames, SetupNodeStatus node)
        {
            if (false && stepNodeNames != null && !stepNodeNames.Contains(node.Name))
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
        /// Returns the current status for a hypervisor hosting one or more cluster nodes.
        /// </summary>
        /// <param name="host">The node being queried.</param>
        /// <returns>The status.</returns>
        private string GetHostStatus(SetupNodeStatus host)
        {
            // We mark completed steps with a "[x] " or "[!] " prefix and
            // indent non-completed steps status with four blanks.

            if (host.Status.StartsWith("[x] ") || host.Status.StartsWith("[!] "))
            {
                return host.Status;
            }
            else
            {
                return "    " + host.Status;
            }
        }

        /// <summary>
        /// Formats a step index into a form suitable for display.
        /// </summary>
        /// <param name="stepNumber">The step index.</param>
        /// <returns>The formatted step number.</returns>
        private string FormatStepNumber(int stepNumber)
        {
            int     stepCount = Steps.Count();
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
        /// <param name="maxDisplayedSteps">The maximum number of steps to be displayed.  This <b>defaults to 5</b>.</param>
        /// <param name="showNodeStatus">Controls whether individual node status is displayed.  This defaults to <c>true</c>.</param>
        /// <param name="showRuntime">Controls whether step runtime is displayed after all steps have completed.  This defaults to <c>true</c>.</param>
        public void WriteToConsole(int maxDisplayedSteps = 5, bool showNodeStatus = true, bool showRuntime = true)
        {
            Covenant.Requires<ArgumentException>(maxDisplayedSteps > 0, nameof(maxDisplayedSteps));

            if (Steps.Count == 0 || CurrentStep == null)
            {
                return;
            }

            var sbDisplay         = new StringBuilder();
            var maxStepLabelWidth = Steps.Max(step => step.Label.Length);
            var maxNodeNameWidth  = Nodes.Max(step => step.Name.Length);

            sbDisplay.Clear();

            sbDisplay.AppendLine();
            sbDisplay.AppendLine($" {controller.OperationTitle}");

            var nonQuietSteps    = Steps.Where(step => !step.IsQuiet).ToList();
            var displaySteps     = new List<SetupStepStatus>();
            var showStepProgress = false;

            if (maxDisplayedSteps > 0 && maxDisplayedSteps < nonQuietSteps.Count())
            {
                // Limit the display steps to just those around the currently
                // executing step.

                var runningStep = CurrentStep;

                if (CurrentStep != null && !CurrentStep.IsQuiet)
                {
                    runningStep = CurrentStep;
                }

                if (runningStep != null)
                {
                    showStepProgress = true;

                    // Determine the number of steps before the running step as well as the
                    // number of steps after to include in the display steps.

                    var maxStepsBefore = (maxDisplayedSteps - 1) / 2;
                    var maxStepsAfter  = maxDisplayedSteps - maxStepsBefore - 1;
                    var stepsBefore    = Math.Min(runningStep.Number - 1, maxStepsBefore);
                    var stepsAfter     = Math.Min(nonQuietSteps.Max(step => step.Number) - runningStep.Number, (maxDisplayedSteps - 1) - stepsBefore);

                    if (stepsAfter < maxStepsAfter)
                    {
                        // Adjust the number of steps displayed before the current step
                        // so that [maxDisplayedSteps] steps will be displayed, adjusting
                        // for situations where there aren't enough steps.

                        stepsBefore = Math.Min(maxStepsAfter - stepsAfter, runningStep.Number - 1);
                    }

                    // Build the list of steps we'll be displaying.

                    if (stepsBefore > 0)
                    {
                        displaySteps.AddRange(nonQuietSteps.Where(step => step.Number >= runningStep.Number - stepsBefore && step.Number < runningStep.Number));
                    }

                    displaySteps.Add(runningStep);

                    if (stepsAfter > 0)
                    {
                        displaySteps.AddRange(nonQuietSteps.Where(step => step.Number <= runningStep.Number + stepsAfter && step.Number > runningStep.Number));
                    }
                }
            }

            sbDisplay.AppendLine();

            if (showStepProgress)
            {
                var width      = maxStepLabelWidth + "[x] DONE".Length + 2;
                var stepCount  = Steps.Count(s => !s.IsQuiet);
                var stepNumber = CurrentStep == null ? 0 : CurrentStep.Number;
                var progress   = new string('-', Math.Max(0, (int)(width * ((stepNumber - 1.0) / stepCount)) - 1));

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
                switch (step.State)
                {
                    case SetupStepState.NotInvolved:
                    case SetupStepState.Pending:

                        sbDisplay.AppendLine($"     {FormatStepNumber(step.Number)}{step.Label}");
                        break;

                    case SetupStepState.Running:

                        sbDisplay.AppendLine($" --> {FormatStepNumber(step.Number)}{step.Label}");
                        break;

                    case SetupStepState.Cancelled:

                        sbDisplay.AppendLine($"     {FormatStepNumber(step.Number)}{step.Label}{new string(' ', maxStepLabelWidth - step.Label.Length)}   [x] CANCELLED");
                        break;

                    case SetupStepState.Done:

                        sbDisplay.AppendLine($"     {FormatStepNumber(step.Number)}{step.Label}{new string(' ', maxStepLabelWidth - step.Label.Length)}   [x] DONE");
                        break;

                    case SetupStepState.Failed:

                        sbDisplay.AppendLine($"     {FormatStepNumber(step.Number)}{step.Label}{new string(' ', maxStepLabelWidth - step.Label.Length)}   [!] FAIL"); ;
                        break;
                }
            }

            //-----------------------------------------------------------------
            // Display the host status

            var hosts = controller.GetHostStatus();

            if (showNodeStatus && !hosts.IsEmpty())
            {
                var maxHostNameWidth = hosts.Max(status => status.Name.Length);

                sbDisplay.AppendLine();
                sbDisplay.AppendLine(" Hosts:");

                foreach (var host in hosts)
                {
                    sbDisplay.AppendLine($"    {host.Name}{new string(' ', maxHostNameWidth - host.Name.Length)}   {GetHostStatus(host)}");
                }
            }

            //-----------------------------------------------------------------
            // Display the node status when executing a node step.

            if (controller.HasNodeSteps && showNodeStatus)
            {
                // $hack(jefflill):
                //
                // I'm hardcoding the status display here for two scenarios:
                //
                //      1. Configuring cluster nodes with [NodeDefinition] metadata.
                //      2. Provisioning cluster nodes on XenServer and remote Hyper-V hosts.
                //
                // It would be more flexible to implement some kind of callback or virtual
                // method to handle this.

                var stepNodeNamesSet = controller.GetStepNodeNames(CurrentStep.InternalStep);

                if (controller.NodeMetadataType == typeof(NodeDefinition))
                {
                    // Configuring cluster nodes with [NodeDefinition] metadata which.

                    if (Nodes.First().Metadata != null)
                    {
                        if (Nodes.Any(node => (node.Metadata as NodeDefinition).IsControlPane))
                        {
                            sbDisplay.AppendLine();
                            sbDisplay.AppendLine(" Control-Plane:");

                            foreach (var node in Nodes.Where(node => (node.Metadata as NodeDefinition).IsControlPane))
                            {
                                sbDisplay.AppendLine($"    {node.Name}{new string(' ', maxNodeNameWidth - node.Name.Length)}   {GetStatus(stepNodeNamesSet, node)}");
                            }
                        }

                        if (Nodes.Any(node => (node.Metadata as NodeDefinition).IsWorker))
                        {
                            sbDisplay.AppendLine();
                            sbDisplay.AppendLine(" Workers:");

                            foreach (var node in Nodes.Where(n => (n.Metadata as NodeDefinition).IsWorker))
                            {
                                sbDisplay.AppendLine($"    {node.Name}{new string(' ', maxNodeNameWidth - node.Name.Length)}   {GetStatus(stepNodeNamesSet, node)}");
                            }
                        }
                    }
                    else
                    {
                        sbDisplay.AppendLine();
                        sbDisplay.AppendLine(" Nodes:");

                        foreach (var node in Nodes)
                        {
                            sbDisplay.AppendLine($"    {node.Name}{new string(' ', maxNodeNameWidth - node.Name.Length)}   {GetStatus(stepNodeNamesSet, node)}");
                        }
                    }
                }
            }

            // Display any global operation status.

            sbDisplay.AppendLine();

            if (!string.IsNullOrWhiteSpace(GlobalStatus))
            {
                sbDisplay.AppendLine($" Cluster:");
                sbDisplay.AppendLine($"    {GlobalStatus}");
            }
            else
            {
                sbDisplay.AppendLine();
                sbDisplay.AppendLine();
            }

            // Display the runtime for the steps after they all have been executed.

            if (showRuntime && !Steps.Any(step => step.State == SetupStepState.Pending || step.State == SetupStepState.Running || step.State == SetupStepState.NotInvolved))
            {
                var totalLabel    = "Total Setup Time";
                var maxLabelWidth = Steps.Max(step => step.Label.Length);

                if (maxLabelWidth < totalLabel.Length)
                {
                    maxLabelWidth = totalLabel.Length;
                }

                // Compute the total run time.

                var totalRuntime = TimeSpan.Zero;

                foreach (var step in Steps)
                {
                    totalRuntime += step.InternalStep.RunTime;
                }

                sbDisplay.AppendLine();
                sbDisplay.AppendLine(" Step Runtime");
                sbDisplay.AppendLine(" ------------");

                var filler = string.Empty;

                foreach (var step in Steps)
                {
                    filler = new string(' ', maxLabelWidth - step.Label.Length);

                    if (step.State == SetupStepState.Cancelled || step.State == SetupStepState.Done || step.State == SetupStepState.Failed)
                    {
                        sbDisplay.AppendLine($" {step.Label}:    {filler}{step.Runtime} ({step.Runtime.TotalSeconds} sec)");
                    }
                    else
                    {
                        sbDisplay.AppendLine($" {step.Label}:    {filler}* NOT EXECUTED");
                    }
                }

                filler = new string(' ', maxLabelWidth - totalLabel.Length);

                sbDisplay.AppendLine(" " + new string('-', totalLabel.Length + 1));
                sbDisplay.AppendLine($" {totalLabel}:    {filler}{totalRuntime} ({totalRuntime.TotalSeconds} sec)");
                sbDisplay.AppendLine();
            }

            sbDisplay.AppendLine();

            // This updates the console without flickering.

            controller.ConsoleWriter?.Update(sbDisplay.ToString());
        }
    }
}
