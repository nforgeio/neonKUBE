//-----------------------------------------------------------------------------
// FILE:	    PageBase.razor.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using Microsoft.JSInterop;

using Neon.Diagnostics;

using NeonDashboard.Shared;
using NeonDashboard.Shared.Components;

namespace NeonDashboard.Pages
{
    public partial class PageBase : ComponentBase
    {
        [Inject]
        public AppState AppState { get; set; }

        [Inject]
        public NavigationManager NavigationManager { get; set; }

        [Inject]
        public IHttpContextAccessor HttpContextAccessor { get; set; }

        [Inject]
        public IJSRuntime JS { get; set; }

        [Parameter]
        public string PageTitle { get; set; } = "NeonKUBE Dashboard";

        [Parameter]
        public string Description { get; set; } = "";

        public ILogger Logger => AppState.Logger;

        public Service NeonDashboardService => AppState.NeonDashboardService;

        public PageBase()
        {
        }
    }
}