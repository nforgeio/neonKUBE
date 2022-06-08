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

using Neon.Kube;
using Neon.Tasks;

using NeonDashboard.Shared.Components;

using k8s;
using k8s.Models;

using PSC.Blazor.Components.Chartjs;
using PSC.Blazor.Components.Chartjs.Enums;
using PSC.Blazor.Components.Chartjs.Models;
using PSC.Blazor.Components.Chartjs.Models.Common;
using PSC.Blazor.Components.Chartjs.Models.Line;

namespace NeonDashboard.Pages
{
    [Authorize]
    public partial class Home : PageBase
    {
        private ClusterInfo clusterInfo;
        private V1NodeList nodeList;
        private NodeMetricsList nodeMetrics;

        private LineChartConfig memoryChartConfig;
        private List<string>    memoryUsageX;
        private List<decimal>   memoryUsageY;
        private Chart           memoryChart;

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

            AppState.Kube.OnChange += StateHasChanged;
            AppState.Metrics.OnChange += StateHasChanged;
        }

        /// <inheritdoc/>
        protected override async Task OnParametersSetAsync()
        {
            AppState.CurrentDashboard = "neonkube";
            AppState.NotifyDashboardChanged();

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await GetNodeStatusAsync();
            }
        }

        private async Task GetNodeStatusAsync()
        {
            await SyncContext.Clear;

            var tasks = new List<Task>();

            await AppState.Kube.GetNodesStatusAsync();
            await AppState.Kube.GetNodeMetricsAsync();
            await AppState.Metrics.GetMemoryUsageAsync(DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow);
            await AppState.Metrics.GetMemoryTotalAsync();

            await DrawChartsAsync();
        }

        private async Task DrawChartsAsync()
        {
            memoryUsageX = AppState.Metrics.MemoryUsageBytes.Data.Result?.First()?.Values?.Select(x => AppState.Metrics.UnixTimeStampToDateTime(x.Time).ToShortTimeString()).ToList();
            memoryUsageY = AppState.Metrics.MemoryUsageBytes.Data.Result.First().Values.Select(x => decimal.Parse(x.Value) / 1000000000).ToList();
            memoryChartConfig = new LineChartConfig();
            memoryChartConfig.Data.Labels = memoryUsageX;
            memoryChartConfig.Data.Datasets.Add(new LineDataset()
            {
                Label = "Memory usage",
                Data = memoryUsageY,
                Tension = 0.1M
            });

            StateHasChanged();
        }
    }
}