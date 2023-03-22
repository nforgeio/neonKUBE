//-----------------------------------------------------------------------------
// FILE:	    Home.razor.cs
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
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.Virtualization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Tasks;

using NeonDashboard.Shared.Components;

using k8s;
using k8s.Models;

using ChartJs.Blazor;
using ChartJs.Blazor.LineChart;
using ChartJs.Blazor.Common;
using ChartJs.Blazor.Util;
using ChartJs.Blazor.Common.Axes;
using ChartJs.Blazor.Common.Enums;
using ChartJs.Blazor.Interop;
using ChartJs.Blazor.Common.Handlers;

namespace NeonDashboard.Pages
{
    [Authorize]
    public partial class Home : PageBase
    {
        private ClusterInfo clusterInfo;

        private LineConfig memoryChartConfig;
        private Chart      memoryChart;

        private LineConfig cpuChartConfig;
        private Chart      cpuChart;

        private LineConfig diskChartConfig;
        private Chart      diskChart;
        
        private static int chartLookBack = 10;

        private Dictionary<string, string> clusterMetaData;

        /// <summary>
        /// Constructor.
        /// </summary>
        public Home()
        {
        }

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            PageTitle   = NeonDashboardService.ClusterInfo.Name;
            clusterInfo = NeonDashboardService.ClusterInfo;

            AppState.OnDashboardChange += StateHasChanged;
            AppState.Kube.OnChange     += StateHasChanged;
            AppState.Metrics.OnChange  += StateHasChanged;

            clusterMetaData = new Dictionary<string, string>()
            {
                {"Version", clusterInfo.ClusterVersion },
                {"Datacenter",  clusterInfo.Datacenter },
                {"Enviroment", clusterInfo.Environment.ToString() },
                {"Purpose", clusterInfo.Purpose.ToString() }
            };

            LineOptions options = new LineOptions()
            {
                Responsive = true,
                MaintainAspectRatio = false,
                Scales = new Scales()
                {
                }
            };

            memoryChartConfig = new LineConfig()
            {
                Options = options
            };

            cpuChartConfig = new LineConfig()
            {
                Options = options
            };

            diskChartConfig = new LineConfig()
            {
                Options = options
            };
        }

        /// <inheritdoc/>
        protected override async Task OnParametersSetAsync()
        {
            await SyncContext.Clear;
            
            AppState.CurrentDashboard = "neonkube";
            AppState.NotifyDashboardChanged();

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await SyncContext.Clear;

            if (firstRender)
            {
                _ = GetNodeStatusAsync();
                _ = AppState.Kube.GetCertExpirationAsync();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            AppState.OnDashboardChange -= StateHasChanged;
            AppState.Kube.OnChange     -= StateHasChanged;
            AppState.Metrics.OnChange  -= StateHasChanged;
        }

        private async Task GetNodeStatusAsync()
        {
            await SyncContext.Clear;

            var tasks = new List<Task>()
            {
                AppState.Kube.GetNodesStatusAsync(),
                UpdateMemoryAsync(),
                UpdateCpuAsync(),
                UpdateDiskAsync()
            };

            await Task.WhenAll(tasks);
        }

        private async Task UpdateChartAsync(List<string> labels, List<decimal> data, LineConfig config, Chart chart, string labelname)
        {
            await SyncContext.Clear;

            config.Data.Labels.Clear();
            foreach (var label in labels)
            {
                config.Data.Labels.Add(label);
            }

            config.Data.Datasets.Clear();
            config.Data.Datasets.Add(new LineDataset<decimal>(data)
            {
                Label = labelname,
            });

            try
            {
                await chart.Update();
                StateHasChanged();
            }
            catch (Exception e)
            {
                Logger.LogErrorEx(NeonHelper.JsonSerialize(e));
            }
        }

        private async Task UpdateMemoryAsync()
        {
            await SyncContext.Clear;
            
            var tasks = new List<Task>()
            {
                AppState.Metrics.GetMemoryUsageAsync(DateTime.UtcNow.AddMinutes(chartLookBack * -1), DateTime.UtcNow),
                AppState.Metrics.GetMemoryTotalAsync()
            };

            await Task.WhenAll(tasks);

            if (AppState.Metrics.MemoryTotalBytes < 0 || AppState.Metrics.MemoryUsageBytes == null)
            {
                return;
            }

            var memoryUsageX = AppState.Metrics.MemoryUsageBytes.Data.Result?.First()?.Values?.Select(x => AppState.Metrics.UnixTimeStampToDateTime(x.Time).ToShortTimeString()).ToList();
            var memoryUsageY = AppState.Metrics.MemoryUsageBytes.Data.Result?.First().Values.Select(x => decimal.Parse(x.Value) / 1000000000).ToList();

            await UpdateChartAsync(memoryUsageX, memoryUsageY, memoryChartConfig, memoryChart, $"Memory usage (total memory: {ByteUnits.ToGB(AppState.Metrics.MemoryTotalBytes)})");
        }

        private async Task UpdateCpuAsync()
        {
            await SyncContext.Clear;

            var tasks = new List<Task>()
            {
                AppState.Metrics.GetCpuUsageAsync(DateTime.UtcNow.AddMinutes(chartLookBack * -1), DateTime.UtcNow),
                AppState.Metrics.GetCpuTotalAsync()
            };

            await Task.WhenAll(tasks);

            if (AppState.Metrics.CPUUsagePercent == null || AppState.Metrics.CPUTotal < 0)
            {
                return;
            }

            var cpuUsageX = AppState.Metrics.CPUUsagePercent.Data.Result?.First()?.Values?.Select(x => AppState.Metrics.UnixTimeStampToDateTime(x.Time).ToShortTimeString()).ToList();
            var cpuUsageY = AppState.Metrics.CPUUsagePercent.Data.Result.First().Values.Select(x => AppState.Metrics.CPUTotal - decimal.Parse(x.Value) ).ToList();

            await UpdateChartAsync(cpuUsageX, cpuUsageY, cpuChartConfig, cpuChart, $"CPU usage (total cores: {AppState.Metrics.CPUTotal})");
        }

        private async Task UpdateDiskAsync()
        {
            await SyncContext.Clear;

            var tasks = new List<Task>()
            {
                AppState.Metrics.GetDiskUsageAsync(DateTime.UtcNow.AddMinutes(chartLookBack * -1), DateTime.UtcNow),
                AppState.Metrics.GetDiskTotalAsync()
            };

            await Task.WhenAll(tasks);

            if (AppState.Metrics.DiskUsageBytes == null || AppState.Metrics.DiskTotalBytes < 0)
            {
                return;
            }

            var diskUsageX = AppState.Metrics.DiskUsageBytes.Data.Result?.First()?.Values?.Select(x => AppState.Metrics.UnixTimeStampToDateTime(x.Time).ToShortTimeString()).ToList();
            var diskUsageY = AppState.Metrics.DiskUsageBytes.Data.Result.First().Values.Select(x => decimal.Parse(x.Value) / 1000000000).ToList();

            await UpdateChartAsync(diskUsageX, diskUsageY, diskChartConfig, diskChart, $"Disk usage (total disk: {ByteUnits.ToGB(AppState.Metrics.DiskTotalBytes)})");
        }
    }
}