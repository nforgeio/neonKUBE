﻿//-----------------------------------------------------------------------------
// FILE:	    Index.razor.cs
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

using NeonDashboard.Shared.Components;

namespace NeonDashboard.Pages
{
    public partial class Index : PageBase
    {
        [Parameter]
        public string CurrentDashboard { get; set; }

        public Index()
        {
        }

        protected override void OnInitialized()
        {
            AppState.OnDashboardChange += StateHasChanged;
        }
        protected override async Task OnParametersSetAsync()
        {
            if (!AppState.DashboardFrames.Any(d => d.Name == CurrentDashboard))
            {
                var dashboard = AppState.Dashboards.Where(d => d.Name == CurrentDashboard).FirstOrDefault();
                if (dashboard != null)
                {
                    AppState.DashboardFrames.Add(dashboard);
                }
            }

            AppState.CurrentDashboard = CurrentDashboard;

            if (!string.IsNullOrEmpty(CurrentDashboard))
            {
                NeonDashboardService.DashboardViewCounter.WithLabels(CurrentDashboard).Inc();
            }

            AppState.NotifyDashboardChanged();

            await Task.CompletedTask;
        }
    }
}