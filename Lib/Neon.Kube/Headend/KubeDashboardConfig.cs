//-----------------------------------------------------------------------------
// FILE:	    KubeDashboardConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the ""License"");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an ""AS IS"" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Manages the Kubernetes dashboard YAML configurations.
    /// </summary>
    internal static partial class KubeDashboardConfig
    {
        /// <summary>
        /// Returns the dashboard configuration to be used for the specified version
        /// of Kubernetes.
        /// </summary>
        /// <param name="kubernetesVersion">The target Kubernetes version.</param>
        /// <returns>The dashboard version and its YAML configuration file.</returns>
        /// <exception cref="NotSupportedException">Thrown if there isn't a known compatble dashboard.</exception>
        public static (string Version, string ConfigYaml) GetDashboardConfigFor(string kubernetesVersion)
        {
            // NOTE: Dashboard YAML configurations are downloaded from here:
            //
            //
            //      https://raw.githubusercontent.com/kubernetes/dashboard/v{DASHBOARD_VERSION}/src/deploy/recommended/kubernetes-dashboard.yaml

            switch (kubernetesVersion)
            {
                case "1.13.3":

                    return (Version: "1.10.1", ConfigYaml: DashboardYaml_1_10_1);

                case "1.14.1":

                    return (Version: "1.10.1", ConfigYaml: DashboardYaml_1_10_1);

                case "1.15.0":

                    return (Version: "1.10.1", ConfigYaml: DashboardYaml_1_10_1);

                case "1.15.4":

                    return (Version: "1.10.1", ConfigYaml: DashboardYaml_1_10_1);

                case "1.16.0":

                    return (Version: "2.0.0-beta4", ConfigYaml: DashboardYaml_2_0_0_beta4);

                default:

                    throw new NotSupportedException($"No known dashboard that's compatible with Kubernetes [{kubernetesVersion}].");
            }
        }
    }
}
