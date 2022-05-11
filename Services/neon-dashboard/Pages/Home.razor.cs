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

namespace NeonDashboard.Pages
{
    [Authorize]
    public partial class Home : PageBase
    {
        private ClusterInfo clusterInfo;

        /// <summary>
        /// Constructor.
        /// </summary>
        public Home()
        {
        }

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            PageTitle = NeonDashboardService.ClusterInfo.Name;
            clusterInfo = NeonDashboardService.ClusterInfo;
        }



        /// <inheritdoc/>
        protected override async Task OnParametersSetAsync()
        {
            AppState.CurrentDashboard = "neonkube";
            AppState.NotifyDashboardChanged();
        }
    }
}