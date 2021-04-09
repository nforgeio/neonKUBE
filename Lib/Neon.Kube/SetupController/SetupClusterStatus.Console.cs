//-----------------------------------------------------------------------------
// FILE:	    SetupClusterStatus.Console.cs
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
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace Neon.Kube
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
            if (stepNodeNames != null && !stepNodeNames.Contains(node.Name))
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
        /// Returns the current status for a host.
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

            if (Steps.Count == 0)
            {
                return;
            }

            var sbDisplay         = new StringBuilder();
            var maxStepLabelWidth = Steps.Max(step => step.Label.Length);
            var maxNodeNameWidth  = Nodes.Max(step => step.Name.Length);

            sbDisplay.Clear();

            sbDisplay.AppendLine();
            sbDisplay.AppendLine($" {controller.OperationTitle}");

            var displaySteps     = Steps.Where(step => !step.IsQuiet);
            var showStepProgress = false;

            if (maxDisplayedSteps > 0 && maxDisplayedSteps < displaySteps.Count())
            {
                // Limit the display steps to just those around the currently
                // executing step.

                var displayStepsCount = displaySteps.Count();
                var runningStep       = Steps.FirstOrDefault(s => s.State == SetupStepState.Running);

                if (runningStep != null)
                {
                    showStepProgress = true;

                    if (runningStep.Number <= 1)
                    {
                        displaySteps = displaySteps.Where(s => s.Number <= maxDisplayedSteps);
                    }
                    else if (runningStep.Number >= displayStepsCount - maxDisplayedSteps + 1)
                    {
                        displaySteps = displaySteps.Where(s => s.Number >= displayStepsCount - maxDisplayedSteps + 1);
                    }
                    else
                    {
                        var firstDisplayedNumber = runningStep.Number - maxDisplayedSteps / 2;
                        var lastDisplayedNumber  = firstDisplayedNumber + maxDisplayedSteps - 1;

                        displaySteps = displaySteps.Where(s => firstDisplayedNumber <= s.Number && s.Number <= lastDisplayedNumber);
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
                    case SetupStepState.Pending:

                        sbDisplay.AppendLine($"     {FormatStepNumber(step.Number)}{step.Label}");
                        break;

                    case SetupStepState.Running:

                        sbDisplay.AppendLine($" --> {FormatStepNumber(step.Number)}{step.Label}");
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
            // Display the node status

            if (controller.HasNodeSteps && showNodeStatus && CurrentStep != null)
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

                var stepNodeNamesSet = controller.GetStepNodeNames(CurrentStep.InternalStep);

                foreach (var node in Nodes)
                {
                    if (!stepNodeNamesSet.Contains(node.Name))
                    {
                        node.Status = string.Empty;
                    }
                }

                if (controller.NodeMetadataType == typeof(NodeDefinition))
                {
                    // Configuring cluster nodes with [NodeDefinition] metadata which.

                    if (Nodes.First().Metadata != null)
                    {
                        if (Nodes.Any(node => (node.Metadata as NodeDefinition).IsMaster))
                        {
                            sbDisplay.AppendLine();
                            sbDisplay.AppendLine(" Masters:");

                            foreach (var node in Nodes.Where(node => (node.Metadata as NodeDefinition).IsMaster))
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

            if (!string.IsNullOrEmpty(GlobalStatus))
            {
                sbDisplay.AppendLine();
                sbDisplay.AppendLine($"*** {GlobalStatus}");
            }

            sbDisplay.AppendLine();

            // Display the runtime for the steps after they all have been executed.

            if (showRuntime && !Steps.Any(step => step.State == SetupStepState.Pending || step.State == SetupStepState.Running))
            {
                var totalLabel    = " Total Setup Time";
                var maxLabelWidth = Steps.Max(step => step.Label.Length);

                if (maxLabelWidth < totalLabel.Length)
                {
                    maxLabelWidth = totalLabel.Length;
                }

                sbDisplay.AppendLine();
                sbDisplay.AppendLine();
                sbDisplay.AppendLine(" Step Runtime");
                sbDisplay.AppendLine(" ------------");

                var filler = string.Empty;

                foreach (var step in Steps)
                {
                    filler = new string(' ', maxLabelWidth - step.Label.Length);

                    if (step.State == SetupStepState.Done || step.State == SetupStepState.Failed)
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
                sbDisplay.AppendLine($" {totalLabel}:    {filler}{controller.Runtime} ({controller.Runtime.TotalSeconds} sec)");
                sbDisplay.AppendLine();
            }

            Console.Clear();
            Console.Write(sbDisplay.ToString());
        }
    }
}
