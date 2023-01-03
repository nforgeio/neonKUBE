//-----------------------------------------------------------------------------
// FILE:	    AppState.Metrics.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

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
        /// <summary>
        /// Metrics related state.
        /// </summary>
        public class __Metrics : AppStateBase
        {
            /// <summary>
            /// Event action for updates to Kube properties.
            /// </summary>
            public event Action OnChange;
            private void NotifyStateChanged() => OnChange?.Invoke();

            private PrometheusClient PrometheusClient => NeonDashboardService.PrometheusClient;

            /// <summary>
            /// Prometheis result containing the total memory usage for the cluster.
            /// </summary>
            public PrometheusResponse<PrometheusMatrixResult> MemoryUsageBytes;
            
            /// <summary>
            /// The total amount of memory available to the cluster.
            /// </summary>
            public decimal MemoryTotalBytes = -1;

            /// <summary>
            /// Prometheus result containing the CPU use percentage for the cluster.
            /// </summary>
            public PrometheusResponse<PrometheusMatrixResult> CPUUsagePercent;
            
            /// <summary>
            /// The total number of CPU cores available to the cluster.
            /// </summary>
            public decimal CPUTotal = -1;

            /// <summary>
            /// Prometheus result containing the total disk usage for the cluster.
            /// </summary>
            public PrometheusResponse<PrometheusMatrixResult> DiskUsageBytes;
            
            /// <summary>
            /// The total amount of disk space available to the cluster.
            /// </summary>
            public decimal DiskTotalBytes = -1;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="state"></param>
            public __Metrics(AppState state)
                : base(state)
            {
                
            }

            /// <summary>
            /// Get the total memory usage for the cluster.
            /// </summary>
            /// <param name="start"></param>
            /// <param name="end"></param>
            /// <param name="stepSize"></param>
            /// <returns></returns>
            public async Task<PrometheusResponse<PrometheusMatrixResult>> GetMemoryUsageAsync(DateTime start, DateTime end, TimeSpan stepSize = default)
            {
                await SyncContext.Clear;

                var query = $@"sum(node_memory_MemTotal_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}""}}) - sum(node_memory_MemFree_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}""}})";
                MemoryUsageBytes = await QueryRangeAsync(query, start, end, stepSize);

                NotifyStateChanged();

                return MemoryUsageBytes;
            }

            /// <summary>
            /// Gets the total amount of memory available to the cluster.
            /// </summary>
            /// <returns></returns>
            public async Task GetMemoryTotalAsync()
            {
                await SyncContext.Clear;

                var query = $@"sum(node_memory_MemTotal_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}""}})";
                
                var result = await QueryAsync(query);

                if (result == null)
                {
                    return;
                }

                if (decimal.TryParse(result.Data.Result.First().Value.Value, out var memoryTotal)) 
                {
                    MemoryTotalBytes = memoryTotal;
                }

                NotifyStateChanged();

                return;
            }

            /// <summary>
            /// Gets the CPU usage from the cluster.
            /// </summary>
            /// <param name="start"></param>
            /// <param name="end"></param>
            /// <param name="stepSize"></param>
            /// <returns></returns>
            public async Task<PrometheusResponse<PrometheusMatrixResult>> GetCpuUsageAsync(DateTime start, DateTime end, TimeSpan stepSize = default)
            {
                await SyncContext.Clear;

                var query = $@"(sum(irate(node_cpu_seconds_total{{mode = ""idle"", cluster=~""{NeonDashboardService.ClusterInfo.Name}""}}[10m])))";
                CPUUsagePercent = await QueryRangeAsync(query, start, end, stepSize);

                NotifyStateChanged();

                return CPUUsagePercent;
            }

            /// <summary>
            /// Gets the total number of CPUs available to the cluster.
            /// </summary>
            /// <returns></returns>
            public async Task GetCpuTotalAsync()
            {
                await SyncContext.Clear;

                var query = $@"sum(count without(cpu, mode) (node_cpu_seconds_total{{mode = ""idle"", cluster=~""{NeonDashboardService.ClusterInfo.Name}""}}))";
                
                var result = await QueryAsync(query);

                if (result == null)
                {
                    return;
                }

                if (decimal.TryParse(result.Data.Result.First().Value.Value, out var cpu))
                {
                    CPUTotal = cpu;
                }

                NotifyStateChanged();

                return;
            }

            /// <summary>
            /// Gets the total disk usage for the cluster.
            /// </summary>
            /// <param name="start"></param>
            /// <param name="end"></param>
            /// <param name="stepSize"></param>
            /// <returns></returns>
            public async Task<PrometheusResponse<PrometheusMatrixResult>> GetDiskUsageAsync(DateTime start, DateTime end, TimeSpan stepSize = default)
            {
                await SyncContext.Clear;

                var query = $@"sum(node_filesystem_size_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}"", mountpoint=""/"",fstype!=""rootfs""}}) - sum(node_filesystem_avail_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}"", mountpoint=""/"",fstype!=""rootfs""}})";
                DiskUsageBytes = await QueryRangeAsync(query, start, end, stepSize);

                NotifyStateChanged();

                return DiskUsageBytes;
            }

            /// <summary>
            /// Gets the total amount of disk space available to the cluster.
            /// </summary>
            /// <returns></returns>
            public async Task GetDiskTotalAsync()
            {
                await SyncContext.Clear;

                var query = $@"sum(node_filesystem_avail_bytes{{cluster=~""{NeonDashboardService.ClusterInfo.Name}"", mountpoint=""/"",fstype!=""rootfs""}})";

                var result = await QueryAsync(query);

                if (result == null)
                {
                    return;
                }

                if (decimal.TryParse(result.Data.Result.First().Value.Value, out var disk))
                {
                    DiskTotalBytes = disk;
                }

                NotifyStateChanged();

                return;
            }

            /// <summary>
            /// Executes a range query.
            /// </summary>
            /// <param name="query">The query to be executed.</param>
            /// <param name="start">The start time.</param>
            /// <param name="end">The end time.</param>
            /// <param name="stepSize">The optional step size.  This defaults to <b>15</b> seconds.</param>
            /// <param name="cacheInterval">The cache interval</param>
            /// <returns></returns>
            public async Task<PrometheusResponse<PrometheusMatrixResult>> QueryRangeAsync(string query, DateTime start, DateTime end, TimeSpan stepSize = default, int cacheInterval = 1)
            {
                await SyncContext.Clear;

                // Round intervals so that they cache better.

                start = start.RoundDown(TimeSpan.FromMinutes(cacheInterval));
                end   = end.RoundDown(TimeSpan.FromMinutes(cacheInterval));

                Logger.LogDebugEx(() => $"[Metrics] Executing range query. Query: [{query}], Start [{start}], End: [{end}], StepSize: [{stepSize}], CacheInterval: [{cacheInterval}]");

                var key = $"neon-dashboard_{Neon.Cryptography.CryptoHelper.ComputeMD5String(query)}";

                try
                {
                    var value = await Cache.GetAsync<PrometheusResponse<PrometheusMatrixResult>>(key);
                    if (value != null)
                    {
                        Logger.LogDebugEx(() => $"[Metrics] Returning from Cache. Query: [{query}], Start [{start}], End: [{end}], StepSize: [{stepSize}], CacheInterval: [{cacheInterval}]");

                        return value;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogErrorEx(e);
                }

                try
                {
                    var result = await PrometheusClient.QueryRangeAsync(query, start, end, stepSize);

                    _ = Cache.SetAsync(key, result);

                    return result;
                }
                catch (Exception e)
                {
                    Logger.LogErrorEx(e);
                    return null;
                }
            }

            private async Task<PrometheusResponse<PrometheusVectorResult>> QueryAsync(string query)
            {
                await SyncContext.Clear;

                Logger.LogDebugEx(() => $"[Metrics] Executing query. Query: [{query}]");

                var key = Neon.Cryptography.CryptoHelper.ComputeMD5String(query);

                try
                {
                    var value = await Cache.GetAsync<PrometheusResponse<PrometheusVectorResult>>(key);
                    if (value != null)
                    {
                        Logger.LogDebugEx(() => $"[Metrics] Returning from Cache. Query: [{query}]");

                        return value;
                    }
                }
                catch (Exception e)
                {
                    Logger.LogErrorEx(e);
                }

                try
                {
                    var result = await PrometheusClient.QueryAsync(query);

                    _ = Cache.SetAsync(key, result);

                    return result;
                }
                catch (Exception e)
                {
                    Logger.LogErrorEx(e);
                    return null;
                }
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