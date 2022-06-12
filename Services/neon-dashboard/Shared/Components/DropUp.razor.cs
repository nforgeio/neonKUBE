//-----------------------------------------------------------------------------
// FILE:	    DropUp.razor.cs
// CONTRIBUTOR: Simon Zhang, Marcus Bowyer
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
    public partial class DropUp : ComponentBase, IDisposable
    {
		[Parameter]
		public string Title { get; set; } = "Name";

		[Parameter]
		public Dashboard Active { get; set; }

		[Parameter]
		public string Icon { get; set; }

		[Parameter]
		public RenderFragment<IDropUpItem> DropList { get; set; }

		[Parameter, AllowNull]
		public IReadOnlyList<IDropUpItem> Items { get; set; }

		protected override void OnInitialized()
		{
            AppState.OnDashboardChange += StateHasChanged;
		}

		public void Dispose()
		{
			AppState.OnDashboardChange -= StateHasChanged;
		}

		bool HideMenu = true;

		void Show()
		{
			HideMenu = !HideMenu;
		}

		void MouseIn()
		{
			HideMenu = false;
		}

		void MouseOut()
		{
			HideMenu = true;
		}
    }
}