//-----------------------------------------------------------------------------
// FILE:	    Icon.razor.cs
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

namespace NeonDashboard.Shared.Components
{
    public partial class Icon : ComponentBase, IDisposable
    {
        /// <summary>
        /// type of icon to display
        /// </summary>
        [Parameter]
        public string Content { get; set; }

        /// <summary>
        /// override color, by default copies current text color
        /// </summary>
        [Parameter]
        public string Color { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public Icon() { }

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
