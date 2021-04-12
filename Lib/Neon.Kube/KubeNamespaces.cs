//-----------------------------------------------------------------------------
// FILE:	    KubeNamespaces.cs
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

using Neon.Common;
using Neon.SSH;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// Defines the Kubernetes namespace names known to neonKUBE.
    /// </summary>
    public static class KubeNamespaces
    {
        /// <summary>
        /// The default namespace.
        /// </summary>
        public const string Default = "default";

        /// <summary>
        /// Hosts the Kubernetes dashboard.
        /// </summary>
        public const string KubernetesDashboard = "kubernetes-dashboard";

        /// <summary>
        /// Hosts Kubernetes public services.
        /// </summary>
        public const string KubePublic = "kube-public";

        /// <summary>
        /// Hosts Kubernetes infrastructure components.
        /// </summary>
        public const string KubeSystem = "kube-system";

        /// <summary>
        /// Hosts the remaining Istio components.
        /// </summary>
        public const string NeonIngress = "neon-ingress";

        /// <summary>
        /// Hosts cluster monitoring.
        /// </summary>
        public const string NeonMonitor = "neon-monitor";

        /// <summary>
        /// Hosts OpenEBS components.
        /// </summary>
        public const string NeonStorage = "neon-storage";

        /// <summary>
        /// Hosts neonKUBE infrastructure.
        /// </summary>
        public const string NeonSystem = "neon-system";
    }
}
