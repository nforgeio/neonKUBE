//-----------------------------------------------------------------------------
// FILE:	    AppState.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
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
            public int FailedNodes { get; private set; }

            /// <summary>
            /// The date that the cluster was created.
            /// </summary>
            public DateTime CreationTimestamp { get; private set; }

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
                    Logger.LogError(e);
                }

                Nodes = await K8s.ListNodeAsync();

                TotalNodes = Nodes.Items.Count();
                FailedNodes = Nodes.Items.Where(node => node.Status.Conditions.Any(condition => negativeNodeConditions.Contains(condition.Type) && condition.Status == "True")).Count();
                ActiveNodes = Nodes.Items.Where(node => node.Status.Conditions.Any(condition => condition.Type == "Ready" && condition.Status == "True")).Count();

                NotifyStateChanged();
            }

            public async Task<NodeMetricsList> GetNodeMetricsAsync()
            {
                await SyncContext.Clear;

                var key = "node-metrics";

                try
                {
                    var value = await Cache.GetAsync<NodeMetricsList>(key);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }

                var nodeMetricsList = await K8s.GetKubernetesNodesMetricsAsync();

                _ = Cache.SetAsync(key, nodeMetricsList);

                NotifyStateChanged();

                return nodeMetricsList;
            }
        }
    }
}