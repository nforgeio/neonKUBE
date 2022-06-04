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
        private static string cachePrefix = "neon-dashboard";
        private static DistributedCacheEntryOptions cacheEntryOptions = new DistributedCacheEntryOptions()
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
        };

        public async Task<V1NodeList> GetNodesAsync()
        {
            var key = createCacheKey("nodes");

            try
            {
                var value = await GetAsync<V1NodeList>(key);
                if (value != null)
                {
                    return value;
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            var nodes = await NeonDashboardService.Kubernetes.ListNodeAsync();

            _ = SetAsync(key, nodes);

            return nodes;
        }

        public async Task<NodeMetricsList> GetNodeMetricsAsync()
        {
            var key = createCacheKey("node-metrics");

            try
            {
                var value = await GetAsync<NodeMetricsList>(key);
                if (value != null)
                {
                    return value;
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e);
            }

            var nodeMetricsList = await NeonDashboardService.Kubernetes.GetKubernetesNodesMetricsAsync();

            _ = SetAsync(key, nodeMetricsList);

            return nodeMetricsList;
        }

        private async Task<T> GetAsync<T>(string key)
        {
            var value = await Cache.GetAsync(key);

            if (value != null)
            {
                return NeonHelper.JsonDeserialize<T>(value);
            }

            return default;
        }

        private async Task SetAsync(string key, object value)
        {
            await Cache.SetAsync(key, NeonHelper.JsonSerializeToBytes(value), cacheEntryOptions);
        }

        private string createCacheKey(string key)
        {
            return $"{cachePrefix}_{key}";
        }
    }
}