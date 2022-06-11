//-----------------------------------------------------------------------------
// FILE:	    LoadingCircle.razor.cs
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
    public partial class LoadingCircle : ComponentBase, IDisposable
    {
		/// <summary>
		/// width of progress circle
		/// </summary>
		[Parameter]
		public double Width { get; set; } = 10;

		/// <summary>
		/// color of front
		/// </summary>
		[Parameter]
		public string Color { get; set; } = "";

		/// <summary>
		/// color of bg of circle
		/// </summary>
		[Parameter]
		public string BackgroundColor { get; set; } = "";

		/// <summary>
		/// current progress percentage, from 0-1
		/// </summary>
		[Parameter]
		public double Progress { get; set; } = 0;

		/// <summary>
		/// Child content
		/// </summary>
		[Parameter]
		public RenderFragment ChildContent { get; set; }

		/// <summary>
		/// Constructor.
		/// </summary>
		public LoadingCircle() { }

		/// <inheritdoc/>
		protected override void OnInitialized() { }

		/// <inheritdoc/>
		public void Dispose() { }
	}
}
