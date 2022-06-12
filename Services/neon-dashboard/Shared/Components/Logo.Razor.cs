//-----------------------------------------------------------------------------
// FILE:	    Logo.razor.cs
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
    public partial class Logo : ComponentBase, IDisposable
	{
		/// <summary>
		/// Constructor.
		/// </summary>
		public Logo() { }

		/// <inheritdoc/>
		protected override void OnInitialized() { }

		/// <inheritdoc/>
		public void Dispose() { }
	}
}
