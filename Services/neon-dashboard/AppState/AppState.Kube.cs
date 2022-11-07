//-----------------------------------------------------------------------------
// FILE:	    AppState.Kube.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Tasks;

using NeonDashboard.Shared;
using NeonDashboard.Shared.Components;

using Blazor.Analytics;
using Blazor.Analytics.Components;

using Blazored.LocalStorage;

using k8s;
using k8s.Models;

namespace NeonDashboard
{
    public partial class AppState
    {
        /// <summary>
        /// Kubernetes related state.
        /// </summary>
        public class __Kube : AppStateBase
        {
            /// <summary>
            /// Event action for updates to Kube properties.
            /// </summary>
            public event Action OnChange;
            private void NotifyStateChanged() => OnChange?.Invoke();

            /// <summary>
            /// The total number of nodes in the current cluster.
            /// </summary>
            public int TotalNodes { get; private set; }

            /// <summary>
            /// The total number of active nodes in the cluster.
            /// </summary>
            public int ActiveNodes { get; private set; }

            /// <summary>
            /// The number of failed nodes in the cluster.
            /// </summary>
            public int UnhealthyNodes { get; private set; }

            /// <summary>
            /// The date that the cluster was created.
            /// </summary>
            public DateTime CreationTimestamp { get; private set; }

            /// <summary>
            /// The expiration date of the control plane certificate.
            /// </summary>
            public DateTime KubeCertExpiration { get; set; }

            /// <summary>
            /// The list of nodes. This contains node related metadata.
            /// </summary>
            public V1NodeList Nodes;

            private static List<string> negativeNodeConditions = new List<string>()
            {
                "KubeletUnhealthy",
                "ContainerRuntimeUnhealthy",
                "KernelDeadlock",
                "NetworkUnavailable",
                "MemoryPressure",
                "DiskPressure",
                "PIDPressure",
            };

            private static List<string> positiveNodeConditions = new List<string>()
            {
                "Ready"
            };            
            
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="state"></param>
            public __Kube(AppState state)
                : base(state)
            {
            }

            /// <summary>
            /// Get node status from the Kubernetes API server.
            /// </summary>
            /// <returns></returns>
            public async Task GetNodesStatusAsync()
            {
                await SyncContext.Clear;

                var key = "nodes";

                try
                {
                    var value = await Cache.GetAsync<V1NodeList>(key);
                    if (value != null)
                    {
                        Nodes = value;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogErrorEx(e);
                }

                try
                {

                    Nodes = await K8s.ListNodeAsync();

                    TotalNodes = Nodes.Items.Count();
                    UnhealthyNodes = Nodes.Items.Where(node => node.Status.Conditions.Any(condition => negativeNodeConditions.Contains(condition.Type) && condition.Status == "True")).Count();
                    ActiveNodes = Nodes.Items.Where(node => node.Status.Conditions.Any(condition => condition.Type == "Ready" && condition.Status == "True")).Count();

                    NotifyStateChanged();
                } 
                catch (Exception e)
                {
                    Logger.LogErrorEx(e);
                }
            }

            public async Task GetCertExpirationAsync()
            {
                var config = KubernetesClientConfiguration.BuildDefaultConfig();

                X509Certificate2 certificate = null;
                var httpClientHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (_, cert, __, ___) =>
                    {
                        certificate = new X509Certificate2(cert.GetRawCertData());
                        return true;
                    }
                };

                var httpClient = new HttpClient(httpClientHandler);
                await httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, config.Host));

                KubeCertExpiration = certificate.NotAfter;

                NotifyStateChanged();
            }
        }
    }
}