//-----------------------------------------------------------------------------
// FILE:	    PriorityClass.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Enumerates the system and neonKUBE pod <a href="https://kubernetes.io/docs/concepts/scheduling-eviction/pod-priority-preemption/">PriorityClass</a>
    /// values.  These are used by Kubernetes when deciding which pod to evict from a node when necessary as well as for ordering how pods will be terminated
    /// when nodes are <a href="https://kubernetes.io/blog/2021/04/21/graceful-node-shutdown-beta/">shutdown</a> gracefully.  Each priority property defines
    /// the priority name and value.
    /// </para>
    /// <note>
    /// Higher priorities have higher values and non-Kubernetes defined priority values must be less than 1 billion.
    /// </note>
    /// <para>
    /// neonKUBE priority property names are prefixed by <b>"Neon"</b> and built-in Kubernetes priority property 
    /// names are prefixed by <b>"System"</b> and will have <see cref="PriorityClass.PriorityDef.IsSystem"/> set 
    /// to <c>true</c>.
    /// </para>
    /// <para>
    /// <see cref="Values"/> returns the list of all known priorities.
    /// </para>
    /// <para>
    /// Here are the known priority values, in decending order by priority.
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="SystemNodeCritical"/> (2000001000)</term>
    ///     <description>
    ///     Built-in Kubernetes priority used for the most important pods running on a node.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="SystemClusterCritical"/> (2000000000)</term>
    ///     <description>
    ///     Built-in Kubernetes priority used for the important pods running on a cluster.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonMax"/> (999999999)</term>
    ///     <description>
    ///     Idenifies the maximum priority reserved for neonKUBE applications.
    ///     You should avoid using priorities in the range of <see cref="NeonMin"/>
    ///     and <see cref="NeonMax"/> (inclusive) for your applications.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonOperator"/> (900008000)</term>
    ///     <description>
    ///     Used for critical neonKUBE operators.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonNetwork"/> (900007000)</term>
    ///     <description>
    ///     Used for neonKUBE database deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonStorage"/> (900006000)</term>
    ///     <description>
    ///     Used for critical OpenEBS related storage services that back critical
    ///     neonKUBE and user deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonData"/> (900005000)</term>
    ///     <description>
    ///     Used for neonKUBE database deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonApi"/> (900004000)</term>
    ///     <description>
    ///     Used for neonKUBE API deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonApp"/> (900003000)</term>
    ///     <description>
    ///     Used for neonKUBE application deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonMonitor"/> (900002000)</term>
    ///     <description>
    ///     Used for neonKUBE monitoring components.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonMin"/> (900000000)</term>
    ///     <description>
    ///     Idenifies the maximum priority reserved for neonKUBE applications.
    ///     You should avoid using priorities in the range of <see cref="NeonMin"/>
    ///     and <see cref="NeonMax"/> (inclusive) for your applications.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UserVeryHigh"/> (5000)</term>
    ///     <description>
    ///     Available for very-high priority user pods.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UserHigh"/> (4000)</term>
    ///     <description>
    ///     Available for high priority user pods.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UserMedium"/> (3000)</term>
    ///     <description>
    ///     Available for medium priority user pods.  Note that this is also configured as
    ///     the global default priority class.  Pods deployed without a specific priority
    ///     class will be assigned this one.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UserLow"/> (2000)</term>
    ///     <description>
    ///     Available for user user pods.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UserVeryLow"/> (1000)</term>
    ///     <description>
    ///     Available for very-low priority user pods.
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// The values defined above won't change and they are spaced well apart so users
    /// can insert additional priorities as required.
    /// </para>
    /// <para>
    /// The user priorities defined here are just a starting point and you're free to add
    /// add additional priorities as required or remove or edit the ones you degine.  We
    /// recommend that most user defined priorities be lower than <see cref="NeonApp"/> 
    /// to avoid conflicts with critical Kubernetes and neonKUBE pods.
    /// </para>
    /// <note>
    /// <see cref="UserMedium"/> is configured as the global priority class by default.
    /// This means that any pods you deploy without explicitly specifying a priority class
    /// will be assigned <b>1000002000</b> rather than <b>0</b>.  This can come in handy 
    /// when you have an existing cluster and realize you need to run new pods at a lower
    /// priority than already running pods, and you prefer not to mess the running pod
    /// priorities.
    /// </note>
    /// <note>
    /// You should avoid using priorities in the range of <see cref="NeonMin"/>
    /// and <see cref="NeonMax"/> (inclusive) for your applications.
    /// </note>
    /// <para>
    /// The <see cref="ToManifest"/> method returns the Kubernetes manifest text that
    /// to be allpied to the cluster to initialize the priority classes.
    /// </para>
    /// </summary>
    public static class PriorityClass
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Used to define a pod priority.
        /// </summary>
        public struct PriorityDef
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="name">The priority name.</param>
            /// <param name="value">The priority value.</param>
            /// <param name="description">Optionally specifies the priority description.</param>
            /// <param name="isSystem">Optionally indicates that this is a built-in Kubernetes priority.</param>
            /// <param name="isDefault">Optionally indicates that this is the global default priority class.</param>
            public PriorityDef(string name, int value, string description = null, bool isSystem = false, bool isDefault = false)
            {
                this.Name        = name;
                this.Value       = value;
                this.Description = description;
                this.IsSystem    = isSystem;
                this.IsDefault   = isDefault;
            }

            /// <summary>
            /// Returns the priority name.
            /// </summary>
            public string Name { get; private set; }

            /// <summary>
            /// Returns the priority value.
            /// </summary>
            public int Value { get; private set; }

            /// <summary>
            /// Returns the priority descripton (or <c>null</c>).
            /// </summary>
            public string Description { get; private set; }

            /// <summary>
            /// Returns <b>true</b> for built-in Kubernetes priorities.
            /// </summary>
            public bool IsSystem { get; private set; }

            /// <summary>
            /// Returns <c>true</c> for the global default priority class.
            /// </summary>
            public bool IsDefault { get; private set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Static constructor.
        /// </summary>
        static PriorityClass()
        {
            var list = new List<PriorityDef>();

            list.Add(SystemNodeCritical    = new PriorityDef("system-node-critical",    2000001000, isSystem: true));
            list.Add(SystemClusterCritical = new PriorityDef("system-cluster-critical", 2000000000, isSystem: true));

            list.Add(NeonMax               = new PriorityDef("neon-max",                 999999999, description: "Maximum priority reserved by neonKUBE"));
            list.Add(NeonOperator          = new PriorityDef("neon-operator",            900008000, description: "Used for critical neonKUBE operator pods"));
            list.Add(NeonNetwork           = new PriorityDef("neon-network",             900007000, description: "Used for critical neonKUBE networking pods"));
            list.Add(NeonStorage           = new PriorityDef("neon-storage",             900006000, description: "Used for critical neonKUBE low-level storage pods"));
            list.Add(NeonData              = new PriorityDef("neon-data",                900005000, description: "Used for critical neonKUBE databases pods"));
            list.Add(NeonApi               = new PriorityDef("neon-api",                 900004000, description: "Used for neonKUBE API pods"));
            list.Add(NeonApp               = new PriorityDef("neon-app",                 900003000, description: "Used for neonKUBE application and dashboard pods"));
            list.Add(NeonMonitor           = new PriorityDef("neon-monitor",             900002000, description: "Used for neonKUBE monitoring infrastructure pods"));
            list.Add(NeonMin               = new PriorityDef("neon-min",                 900000000, description: "Minimum priority reserved by neonKUBE"));

            list.Add(UserVeryHigh          = new PriorityDef("user-veryhigh",                 5000, description: "Used for very-high priority user pods"));
            list.Add(UserHigh              = new PriorityDef("user-high",                     4000, description: "Used for high priority user pods"));
            list.Add(UserMedium            = new PriorityDef("user-medium",                   3000, description: "Used for medium priority user pods", isDefault: true));
            list.Add(UserLow               = new PriorityDef("user-low",                      2000, description: "Used for low-priority user pods"));
            list.Add(UserVeryLow           = new PriorityDef("user-verylow",                  1000, description: "Used for very low priority user pods"));

            Values = list;

            Covenant.Assert(Values.Count(priorityDef => priorityDef.IsDefault) <= 1, "Only one priority class may be global.");
        }

        /// <summary>
        /// Built-in Kubernetes priority used for the most important pods 
        /// running on a node. <b>(2000001000)</b>
        /// </summary>
        public static PriorityDef SystemNodeCritical { get; private set; }

        /// <summary>
        /// Built-in Kubernetes priority used for the important pods 
        /// running on a cluster. <b>(2000000000)</b>
        /// </summary>
        public static PriorityDef SystemClusterCritical { get; private set; }

        /// <summary>
        /// Idenifies the maximum priority reserved for neonKUBE applications.
        /// You should avoid using priorities in the range of <see cref="NeonMin"/>
        /// and <see cref="NeonMax"/> (inclusive) for your applications.
        /// </summary>
        public static PriorityDef NeonMax { get; private set; }

        /// <summary>
        /// Used for critical neonKUBE operators. <b>(900008000)</b>
        /// </summary>
        public static PriorityDef NeonOperator { get; private set; }

        /// <summary>
        /// Used for neonKUBE database deployments. <b>(900007000)</b>
        /// </summary>
        public static PriorityDef NeonNetwork { get; private set; }

        /// <summary>
        /// Used for OpenEBS related storage deployments. <b>(1000006000)</b>
        /// </summary>
        public static PriorityDef NeonStorage { get; private set; }

        /// <summary>
        /// Used for neonKUBE database deployments. <b>(900005000)</b>
        /// </summary>
        public static PriorityDef NeonData { get; private set; }

        /// <summary>
        /// Used for neonKUBE API deployments. <b>(900004000)</b>
        /// </summary>
        public static PriorityDef NeonApi { get; private set; }

        /// <summary>
        /// Used for neonKUBE application deployments.  <b>(900003000)</b>
        /// </summary>
        public static PriorityDef NeonApp { get; private set; }

        /// <summary>
        /// Available for neonKUBE monitoring related components. <b>(900002000)</b>
        /// </summary>
        public static PriorityDef NeonMonitor { get; private set; }

        /// <summary>
        /// Idenifies the minimum priority reserved for neonKUBE applications.
        /// You should avoid using priorities in the range of <see cref="NeonMin"/>
        /// and <see cref="NeonMax"/> (inclusive) for your applications.
        /// </summary>
        public static PriorityDef NeonMin { get; private set; }

        /// <summary>
        /// Available for very high priority user pods.  <b>(5000)</b>
        /// </summary>
        public static PriorityDef UserVeryHigh { get; private set; }

        /// <summary>
        /// Available for high priority user pods.  <b>(4000)</b>
        /// </summary>
        public static PriorityDef UserHigh { get; private set; }

        /// <summary>
        /// Available for medium priority user pods and is also configured as the global default 
        /// when a priority isn't explicitly specified. <b>(3000)</b>
        /// </summary>
        public static PriorityDef UserMedium { get; private set; }

        /// <summary>
        /// Available for low priority user pods. <b>(2000)</b>
        /// </summary>
        public static PriorityDef UserLow { get; private set; }

        /// <summary>
        /// Available for very low priority user pods. <b>(1000)</b>
        /// </summary>
        public static PriorityDef UserVeryLow { get; private set; }

        /// <summary>
        /// Returns the list of all known built-in pod priorities.
        /// </summary>
        public static IReadOnlyList<PriorityDef> Values { get; private set; }

        /// <summary>
        /// Ensures that a priority class name is a standard neonKUBE priority class.
        /// </summary>
        /// <param name="priorityClass">The class name to check or <c>null</c>.</param>
        /// <exception cref="KeyNotFoundException">Thrown for unknown priority classes.</exception>
        public static void EnsureKnown(string priorityClass)
        {
            if (!string.IsNullOrEmpty(priorityClass) && !Values.Any(priorityDef => priorityDef.Name.Equals(priorityClass, StringComparison.InvariantCultureIgnoreCase)))
            {
                throw new KeyNotFoundException($"[{priorityClass}] is not one of the standard neonKUBE priority classes.");
            }
        }

        /// <summary>
        /// Generates the Kubernetes manifest to be used to initialize the 
        /// non-Kubernetes priority classes.
        /// </summary>
        /// <returns>The manifest text.</returns>
        public static string ToManifest()
        {
            var sb = new StringBuilder();

            foreach (var priorityClass in Values
                .Where(priorityClass => !priorityClass.IsSystem)
                .OrderByDescending(priorityClass => priorityClass.Value))
            {
                sb.AppendWithSeparator(
$@"apiVersion: scheduling.k8s.io/v1
kind: PriorityClass
metadata:
  name: {priorityClass.Name}
value: {priorityClass.Value}
description: {priorityClass.Description}
globalDefault: {(priorityClass.IsDefault ? "true" : "false")}
",
"---\n");
            }

            return sb.ToString();
        }
    }
}
