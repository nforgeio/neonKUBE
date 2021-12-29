//-----------------------------------------------------------------------------
// FILE:	    Dashboard.razor.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NeonDashboard.Shared.Components
{
    public partial class Dashboard : ComponentBase, IDisposable
    {
        public Dashboard() { }
        public Dashboard(
            string name,
            string uri, 
            string description = null)
        {
            Name = name;
            Uri = uri;
            Description = description;
        }

        [CascadingParameter(Name = "CurrentDashboard")]
        public string CurrentDashboard { get; set; }

        [Parameter]
        public string Name { get; set; } = null;

        [Parameter]
        public string Uri { get; set; } = null;

        [Parameter]
        public string Description { get; set; }

        public bool IsVisible
        {
            get 
            { 
                return AppState.CurrentDashboard == Name;
            }
        }

        protected override void OnInitialized()
        {
            AppState.OnDashboardChange += StateHasChanged;
        }

        public void Dispose()
        {
            AppState.OnDashboardChange -= StateHasChanged;
        }
    }
}