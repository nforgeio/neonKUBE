//-----------------------------------------------------------------------------
// FILE:	    Alerts.razor.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using NeonDashboard;

namespace NeonDashboard.Shared
{
    public partial class Alerts : ComponentBase, IDisposable
    {
        /// <summary>
        /// adds timestamp to "events" for demo purposes, refactor later
        /// </summary>
        [Parameter]
        public DateTime? DemoTimeStamp { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public Alerts() { }

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
