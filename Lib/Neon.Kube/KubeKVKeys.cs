//-----------------------------------------------------------------------------
// FILE:	    KubeKVKeys.cs
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
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using k8s.Models;

using Neon.Collections;
using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Defines the <see cref="KubeKV"/> related keys and patterns used by neonKUBE system compnents 
    /// </summary>
    public static class KubeKVKeys
    {
        //---------------------------------------------------------------------
        // neon-cluster-operator

        /// <summary>
        /// Pattern used for retrieving all <b>neon-cluster-operator</b> related key/values.
        /// </summary>
        public const string NeonClusterOperatorPattern = "neon.cluster-operator.*";

        /// <summary>
        /// <b>bool:</b> Disables Harbor system image checks and loading of missing images.
        /// This defaults to <c>false</c>.
        /// </summary>
        public const string NeonClusterOperatorDisableHarborImageSync = "neon.cluster-operator.disable-harbor-image-sync";

        /// <summary>
        /// <b>string:</b> The datetime of the last time that container images were synced.
        /// This defaults to <c>false</c>.
        /// </summary>
        public const string NeonClusterOperatorLastHarborImageSync = "neon.cluster-operator.last-harbor-image-sync";

        /// <summary>
        /// <b>string:</b> The state of the Harbor setup job.
        /// This defaults to <c>false</c>.
        /// </summary>
        public const string NeonClusterOperatorJobHarborSetup = "neon.cluster-operator.job.harbor-setup";

        /// <summary>
        /// <b>string:</b> The state of the Grafana setup job.
        /// This defaults to <c>false</c>.
        /// </summary>
        public const string NeonClusterOperatorJobGrafanaSetup = "neon.cluster-operator.job.grafana-setup";
    }
}
