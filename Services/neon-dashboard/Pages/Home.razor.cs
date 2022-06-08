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

namespace NeonDashboard.Pages
{
    [Authorize]
    public partial class Home : PageBase
    {
        private ClusterInfo clusterInfo;
        private V1NodeList nodeList;
        private NodeMetricsList nodeMetrics;

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

            tasks.Add(AppState.Kube.GetNodesStatusAsync());
            tasks.Add(AppState.Kube.GetNodeMetricsAsync());
            tasks.Add(AppState.Metrics.GetMemoryUsageAsync(DateTime.UtcNow.AddMinutes(-10), DateTime.UtcNow));

            await Task.WhenAll(tasks);
        }
    }
}