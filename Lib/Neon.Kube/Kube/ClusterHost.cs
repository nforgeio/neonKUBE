//-----------------------------------------------------------------------------
// FILE:	    ClusterHost.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Threading.Tasks;
using k8s.Models;

using Neon.Collections;
using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Important cluster host names.
    /// </summary>
    public static class ClusterHost
    {
        /// <summary>
        /// Alertmanager service.
        /// </summary>
        public const string AlertManager = "neon-alertmanager";

        /// <summary>
        /// Neon Dashboard.
        /// </summary>
        public const string NeonDashboard = "neon-dashboard";

        /// <summary>
        /// Single sign on service.
        /// </summary>
        public const string Sso = "neon-sso";

        /// <summary>
        /// Grafana dashboard.
        /// </summary>
        public const string Grafana = "neon-grafana";

        /// <summary>
        /// Harbor Notary service.
        /// </summary>
        public const string HarborNotary = "neon-notary";

        /// <summary>
        /// Harbor registry service.
        /// </summary>
        public const string HarborRegistry = "neon-registry";

        /// <summary>
        /// Kiali dashboard.
        /// </summary>
        public const string Kiali = "neon-kiali";

        /// <summary>
        /// Kubernetes dashboard service.
        /// </summary>
        public const string KubernetesDashboard = "neon-k8s";

        /// <summary>
        /// Minio Operator dashboard.
        /// </summary>
        public const string Minio = "neon-minio";
    }
}
