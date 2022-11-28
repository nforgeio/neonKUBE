//-----------------------------------------------------------------------------
// FILE:	    Index.razor.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

using Neon.Tasks;

using NeonDashboard.Shared.Components;

namespace NeonDashboard.Pages
{
    [Authorize]
    public partial class Index : PageBase
    {
        /// <summary>
        /// The id of the current selected dashboard.
        /// </summary>
        [Parameter]
        public string CurrentDashboard { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public Index()
        {
        }

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            AppState.OnDashboardChange += StateHasChanged;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            AppState.OnDashboardChange -= StateHasChanged;
        }

        /// <inheritdoc/>
        protected override async Task OnParametersSetAsync()
        {
            await SyncContext.Clear;

            if (string.IsNullOrEmpty(CurrentDashboard))
            {
                NavigationManager.NavigateTo("neonkube", true);
            }

            if (!AppState.DashboardFrames.Any(d => d.Id == CurrentDashboard && d.Id != "neonkube"))
            {
                var dashboard = AppState.Dashboards.Where(d => d.Id == CurrentDashboard).FirstOrDefault();
                if (dashboard != null)
                {
                    AppState.DashboardFrames.Add(dashboard);
                }
            }

            AppState.CurrentDashboard = CurrentDashboard;

            if (!string.IsNullOrEmpty(CurrentDashboard) && HttpContextAccessor.HttpContext.Request.HttpContext.WebSockets.IsWebSocketRequest)
            {
                PageTitle = $"{AppState.NeonDashboardService.ClusterInfo.Name} - {AppState.GetCurrentDashboard(CurrentDashboard).Name}";
                Program.Service.DashboardViewCounter.WithLabels(CurrentDashboard).Inc();
            }
            else
            {
                PageTitle = $"{AppState.NeonDashboardService.ClusterInfo.Name}";
            }

            AppState.NotifyDashboardChanged();

            await Task.CompletedTask;
        }
    }
}