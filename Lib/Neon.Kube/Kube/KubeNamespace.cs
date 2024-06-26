//-----------------------------------------------------------------------------
// FILE:        KubeNamespace.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.SSH;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Defines the namespace names created for NeonKUBE clusters.
    /// </para>
    /// <note>
    /// These constants are tagged with <see cref="KubeValueAttribute"/> so they can
    /// be referenced directly from Helm charts like: $&lt;KubeNamespace.KubeSystem&gt;
    /// </note>
    /// </summary>
    public static class KubeNamespace
    {
        /// <summary>
        /// The default namespace.
        /// </summary>
        [KubeValue]
        public const string Default = "default";

        /// <summary>
        /// Hosts Istio components.
        /// </summary>
        [KubeValue]
        public const string IstioSystem = "istio-system";

        /// <summary>
        /// Hosts the Kubernetes node leases.
        /// </summary>
        [KubeValue]
        public const string KubeNodeLease = "kube-node-lease";

        /// <summary>
        /// Hosts Kubernetes public services.
        /// </summary>
        [KubeValue]
        public const string KubePublic = "kube-public";

        /// <summary>
        /// Hosts Kubernetes infrastructure components.
        /// </summary>
        [KubeValue]
        public const string KubeSystem = "kube-system";

        /// <summary>
        /// Hosts cluster monitoring.
        /// </summary>
        [KubeValue]
        public const string NeonMonitor = "neon-monitor";

        /// <summary>
        /// Hosts cluster status information.
        /// </summary>
        [KubeValue]
        public const string NeonStatus = "neon-status";

        /// <summary>
        /// Hosts OpenEBS components.
        /// </summary>
        [KubeValue]
        public const string NeonStorage = "neon-storage";

        /// <summary>
        /// Hosts NeonKUBE infrastructure.
        /// </summary>
        [KubeValue]
        public const string NeonSystem = "neon-system";

        /// <summary>
        /// Returns the set of stock Kubernetes clusters namespaces.
        /// </summary>
        public static IReadOnlyList<string> KubernetesNamespaces { get; private set; }

        /// <summary>
        /// Returns the set of stock NeonKUBE cluster namespaces.
        /// </summary>
        public static IReadOnlyList<string> NeonNamespaces { get; private set; }

        /// <summary>
        /// Returns the set of all stock Kubernetes and NeonKUBE cluster namespaces.
        /// </summary>
        public static IReadOnlySet<string> InternalNamespaces { get; private set; }

        /// <summary>
        /// Returns the set of all Kubernetes and NeonKUBE namespaces but without the <b>default</b> namespace.
        /// </summary>
        public static IReadOnlySet<string> InternalNamespacesWithoutDefault { get; private set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static KubeNamespace()
        {
            KubernetesNamespaces = new List<string>()
            {
                Default,
                KubeNodeLease,
                KubePublic,
                KubeSystem
            }
            .AsReadOnly();

            NeonNamespaces = new List<string>()
            {
                IstioSystem,
                NeonMonitor,
                NeonStorage,
                NeonSystem,
                NeonStatus
            }
            .AsReadOnly();

            var internalNamespaces = new HashSet<string>();

            foreach (var @namespace in KubernetesNamespaces.Union(NeonNamespaces).ToList().AsReadOnly())
            {
                internalNamespaces.Add(@namespace);
            }

            InternalNamespaces = internalNamespaces;

            var internalNamespacesWithoutDefault = new HashSet<string>();

            foreach (var @namespace in InternalNamespaces.Where(@namespace => @namespace != KubeNamespace.Default))
            {
                internalNamespacesWithoutDefault.Add(@namespace);
            }

            InternalNamespacesWithoutDefault = internalNamespacesWithoutDefault;
        }
    }
}
