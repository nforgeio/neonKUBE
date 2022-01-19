//-----------------------------------------------------------------------------
// FILE:	    BottomMenu.razor.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

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
    public partial class BottomMenu : ComponentBase, IDisposable
    {
        public BottomMenu() { }

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