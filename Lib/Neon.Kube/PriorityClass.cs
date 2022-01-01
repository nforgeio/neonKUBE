//-----------------------------------------------------------------------------
// FILE:	    PriorityClass.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// Higher priorities have higher values.
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
    /// Here are the known priority values, highest priority first.
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
    ///     <term><see cref="NeonStorage"/> (1000006000)</term>
    ///     <description>
    ///     Used for critical OpenEBS related storage services that will likely back critical
    ///     neonKUBE and user deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonDataTier"/> (1000005000)</term>
    ///     <description>
    ///     Used for neonKUBE database deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonApiTier"/> (1000004000)</term>
    ///     <description>
    ///     Used for neonKUBE API deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="NeonAppTier"/> (1000003000)</term>
    ///     <description>
    ///     Used for neonKUBE application deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UserDataTier"/> (1000002000)</term>
    ///     <description>
    ///     Available for user database deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UserApiTier"/> (1000001000)</term>
    ///     <description>
    ///     Available for user API deployments.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="UserAppTier"/> (1000000000)</term>
    ///     <description>
    ///     Available for user application deployments.
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// The values defined above won't change and they are spaced well apart so users
    /// can insert additional priorities as required.
    /// </para>
    /// <para>
    /// We've organized these in tiers so you can deploy data services with priority 
    /// higher than API services with priority higher than application services, to help 
    /// ensure that applications will be evicted before API services they depend on with
    /// the API services evicted before the data services the API depends on.
    /// </para>
    /// <para>
    /// The user tiers defined here are just a starting point and you're free to add
    /// add additional priorities as required.  We recommend that most user defined
    /// priorities be lower than <see cref="NeonAppTier"/> (1000003000) to avoid conflicting 
    /// with critical Kubernetes and neonKUBE deployments.
    /// </para>
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
            /// <param name="isSystem">Optionally indicates that this is a built-in Kubernetes priority.</param>
            public PriorityDef(string name, int value, bool isSystem = false)
            {
                this.Name     = name;
                this.Value    = value;
                this.IsSystem = isSystem;
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
            /// Returns <b>true</b> for built-in Kubernetes priorities.
            /// </summary>
            public bool IsSystem { get; private set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Static constructor.
        /// </summary>
        static PriorityClass()
        {
            var list = new List<PriorityDef>();

            list.Add(SystemNodeCritical    = new PriorityDef("system-node-critical ",   2000001000, isSystem: true));
            list.Add(SystemClusterCritical = new PriorityDef("system-cluster-critical", 2000000000, isSystem: true));
            list.Add(NeonStorage           = new PriorityDef("neon-storage",            1000006000));
            list.Add(NeonDataTier          = new PriorityDef("neon-data-tier",          1000005000));
            list.Add(NeonApiTier           = new PriorityDef("neon-api-tier",           1000004000));
            list.Add(NeonAppTier           = new PriorityDef("neon-app-tier",           1000003000));
            list.Add(UserDataTier          = new PriorityDef("user-data-tier",          1000002000));
            list.Add(UserApiTier           = new PriorityDef("user-api-tier",           1000001000));
            list.Add(UserAppTier           = new PriorityDef("user-app-tier",           1000000000));
            
            Values = list;
        }

        /// <summary>
        /// Built-in Kubernetes priority used for the most important pods 
        /// running on a node. (2000001000)
        /// </summary>
        public static PriorityDef SystemNodeCritical { get; private set; }

        /// <summary>
        /// Built-in Kubernetes priority used for the important pods 
        /// running on a cluster.  This is one step down from 
        /// <see cref="SystemNodeCritical"/>. (2000000000)
        /// </summary>
        public static PriorityDef SystemClusterCritical { get; private set; }

        /// <summary>
        /// Used for OpenEBS related storage deployments.  This is one step
        /// down from <see cref="SystemClusterCritical"/>. (1000006000)
        /// 
        /// </summary>
        public static PriorityDef NeonStorage { get; private set; }

        /// <summary>
        /// Used for neonKUBE database deployments (like Harbor storage).
        /// This is one step down from <see cref="NeonStorage"/>. (1000005000)
        /// </summary>
        public static PriorityDef NeonDataTier { get; private set; }

        /// <summary>
        /// Used for neonKUBE API deployments.  This is one step down from
        /// <see cref="NeonDataTier"/>. (1000004000)
        /// </summary>
        public static PriorityDef NeonApiTier { get; private set; }

        /// <summary>
        /// Used for neonKUBE application deployments.  This is one step
        /// down from <see cref="NeonAppTier"/>. (1000003000)
        /// </summary>
        public static PriorityDef NeonAppTier { get; private set; }

        /// <summary>
        /// Available for user database deployments.  This is one step down from 
        /// <see cref="NeonAppTier"/>. (1000002000)
        /// </summary>
        public static PriorityDef UserDataTier { get; private set; }

        /// <summary>
        /// Available for user API deployments.  This is one step down from
        /// <see cref="UserDataTier"/>. (1000001000)
        /// </summary>
        public static PriorityDef UserApiTier { get; private set; }

        /// <summary>
        /// Available for user application deployments.  This is one step down
        /// from <see cref="UserApiTier"/>. (1000000000)
        /// </summary>
        public static PriorityDef UserAppTier { get; private set; }

        /// <summary>
        /// Returns the list of all known built-in pod priorities.
        /// </summary>
        public static IReadOnlyList<PriorityDef> Values { get; private set; }

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
",
"---\n");
            }

            return sb.ToString();
        }
    }
}
