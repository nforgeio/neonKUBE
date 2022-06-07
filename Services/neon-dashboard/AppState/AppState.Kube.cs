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
            /// Constructor.
            /// </summary>
            /// <param name="state"></param>
            public __Kube(AppState state)
                : base(state)
            {
            }

            public async Task<V1NodeList> GetNodesAsync()
            {
                var key = "nodes";

                try
                {
                    var value = await Cache.GetAsync<V1NodeList>(key);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }

                var nodes = await K8s.ListNodeAsync();

                _ = Cache.SetAsync("nodes", nodes);

                return nodes;
            }

            public async Task<NodeMetricsList> GetNodeMetricsAsync()
            {
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

                return nodeMetricsList;
            }

            
        }
    }
}