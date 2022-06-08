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
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Net;
using Neon.Tasks;

using NeonDashboard.Shared;
using NeonDashboard.Shared.Components;

using Blazor.Analytics;
using Blazor.Analytics.Components;

using Blazored.LocalStorage;

using k8s;
using k8s.Models;

using Prometheus;
using System.Globalization;
using Neon.Collections;

namespace NeonDashboard
{
    public partial class AppState
    {
        public class __Metrics : AppStateBase
        {
            /// <summary>
            /// Event action for updates to Kube properties.
            /// </summary>
            public event Action OnChange;
            private void NotifyStateChanged() => OnChange?.Invoke();

            private PrometheusClient mimirClient;

            public PrometheusResponse<PrometheusMatrixResult> MemoryUsageBytes;
            public decimal MemoryTotalBytes;
            public PrometheusResponse<PrometheusMatrixResult> CPUUsage;
            public PrometheusResponse<PrometheusMatrixResult> DiskUsage;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="state"></param>
            public __Metrics(AppState state)
                : base(state)
            {
                AppState = state;
                mimirClient = new PrometheusClient("https://metrics.9cfe8456addfb3ee.neoncluster.io/prometheus/");
            }

            public async Task GetMemoryUsageAsync(DateTime start, DateTime end, string stepSize = "15s")
            {
                await SyncContext.Clear;
                
                var query = $@"sum(node_memory_MemTotal_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}""}}) - sum(node_memory_MemFree_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}""}})";
                MemoryUsageBytes = await QueryRangeAsync(query, start, end, stepSize);

                NotifyStateChanged();
            }

            public async Task GetMemoryTotalAsync()
            {
                await SyncContext.Clear;

                var query = $@"sum(node_memory_MemTotal_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}""}})";
                MemoryTotalBytes = decimal.Parse((await QueryAsync(query)).Data.Result.First().Value.Value);

                NotifyStateChanged();
            }

            public async Task<PrometheusResponse<PrometheusMatrixResult>> QueryRangeAsync(string query, DateTime start, DateTime end, string stepSize = "15s")
            {
                await SyncContext.Clear;
                
                var key = $"neon-dashboard_{Neon.Cryptography.CryptoHelper.ComputeMD5String(query)}";

                try
                {
                    var value = await Cache.GetAsync<PrometheusResponse<PrometheusMatrixResult>>(key);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }

                var result = await mimirClient.QueryRangeAsync(query, start, end, stepSize);

                _ = Cache.SetAsync(key, result);

                return result;
            }

            private async Task<PrometheusResponse<PrometheusVectorResult>> QueryAsync(string query)
            {
                await SyncContext.Clear;

                var key = Neon.Cryptography.CryptoHelper.ComputeMD5String(query);

                try
                {
                    var value = await Cache.GetAsync<PrometheusResponse<PrometheusVectorResult>>(key);
                    if (value != null)
                    {
                        return value;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogError(e);
                }

                var result = await mimirClient.QueryAsync(query);

                _ = Cache.SetAsync(key, result);

                return result;
            }

            /// <summary>
            /// Converts unix timestamp to <see cref="DateTime"/>.
            /// </summary>
            /// <param name="unixTimeStamp"></param>
            /// <returns></returns>
            public DateTime UnixTimeStampToDateTime(double unixTimeStamp)
            {
                // Unix timestamp is seconds past epoch
                DateTime dateTime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
                dateTime = dateTime.AddSeconds(unixTimeStamp).ToLocalTime();
                return dateTime;
            }
        }
    }
}