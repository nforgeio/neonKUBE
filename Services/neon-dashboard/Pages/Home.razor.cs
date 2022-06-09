        //-----------------------------------------------------------------------------
// FILE:	    Home.razor.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

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
        
        private static int chartLookBack = 60;

        private Dictionary<string, string> clusterMetaData;
        /// <summary>
        /// Constructor.
        /// </summary>
        public Home()
        {
            memoryChartConfig = new LineConfig();
            cpuChartConfig    = new LineConfig();
            diskChartConfig   = new LineConfig();
        }

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            PageTitle   = NeonDashboardService.ClusterInfo.Name;
            clusterInfo = NeonDashboardService.ClusterInfo;

            AppState.Kube.OnChange    += StateHasChanged;
            AppState.Metrics.OnChange += StateHasChanged;

            clusterMetaData = new Dictionary<string, string>()
            {
                {"Version", clusterInfo.ClusterVersion },
                {"Data Center",  clusterInfo.Datacenter },
                {"Hosting Enviroment", clusterInfo.HostingEnvironment.ToString() },
                {"Environment", clusterInfo.Environment.ToString() }




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
                await GetNodeStatusAsync();
            }
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

        private async Task UpdateMemoryAsync()
        {
            await SyncContext.Clear;
            
            var tasks = new List<Task>()
            {
                AppState.Metrics.GetMemoryUsageAsync(DateTime.UtcNow.AddMinutes(chartLookBack * -1), DateTime.UtcNow),
                AppState.Metrics.GetMemoryTotalAsync()
            };

            await Task.WhenAll(tasks);

            var memoryUsageX = AppState.Metrics.MemoryUsageBytes.Data.Result?.First()?.Values?.Select(x => AppState.Metrics.UnixTimeStampToDateTime(x.Time).ToShortTimeString()).ToList();
            var memoryUsageY = AppState.Metrics.MemoryUsageBytes.Data.Result.First().Values.Select(x => decimal.Parse(x.Value) / 1000000000).ToList();

            memoryChartConfig.Data.Labels.Clear();
            foreach (var label in memoryUsageX)
            {
                memoryChartConfig.Data.Labels.Add(label);
            }

            memoryChartConfig.Data.Datasets.Clear();
            memoryChartConfig.Data.Datasets.Add(new LineDataset<decimal>(memoryUsageY)
            {
                Label = $"Memory usage (total memory: {ByteUnits.ToGB(AppState.Metrics.MemoryTotalBytes)})",
            });

            await memoryChart.Update();

            StateHasChanged();
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

            var cpuUsageX = AppState.Metrics.CPUUsagePercent.Data.Result?.First()?.Values?.Select(x => AppState.Metrics.UnixTimeStampToDateTime(x.Time).ToShortTimeString()).ToList();
            var cpuUsageY = AppState.Metrics.CPUUsagePercent.Data.Result.First().Values.Select(x => decimal.Parse(x.Value) * AppState.Metrics.CPUTotal).ToList();

            foreach (var l in cpuUsageX)
            {
                cpuChartConfig.Data.Labels.Add(l);
            }
            cpuChartConfig.Data.Datasets.Add(new LineDataset<decimal>(cpuUsageY)
            {
                Label = $"CPU usage (total cores: {AppState.Metrics.CPUTotal})",
            });

            await cpuChart.Update();

            StateHasChanged();
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

            var diskUsageX = AppState.Metrics.DiskUsageBytes.Data.Result?.First()?.Values?.Select(x => AppState.Metrics.UnixTimeStampToDateTime(x.Time).ToShortTimeString()).ToList();
            var diskUsageY = AppState.Metrics.DiskUsageBytes.Data.Result.First().Values.Select(x => decimal.Parse(x.Value) / 1000000000).ToList();

            foreach (var l in diskUsageX)
            {
                diskChartConfig.Data.Labels.Add(l);
            }
            diskChartConfig.Data.Datasets.Add(new LineDataset<decimal>(diskUsageY)
            {
                Label = $"Disk usage (total disk: {ByteUnits.ToGB(AppState.Metrics.DiskTotalBytes)})",
            });

            await diskChart.Update();

            StateHasChanged();
        }
    }
}