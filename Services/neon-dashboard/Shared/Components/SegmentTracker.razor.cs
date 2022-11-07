//-----------------------------------------------------------------------------
// FILE:	    SegmentTracker.razor.cs
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
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

using Neon.Tasks;

using Segment;

namespace NeonDashboard.Shared.Components
{
    public partial class SegmentTracker : ComponentBase, IDisposable
    {
        [Inject]
        AppState AppState { get; set; }

        [Inject]
        NavigationManager NavigationManager { get; set; }

        [Parameter]
        public string Name { get; set; }

        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            NavigationManager.LocationChanged -= OnLocationChanged;
            NavigationManager.LocationChanged += OnLocationChanged;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (firstRender)
            {
                // Track initial navigation
                await OnLocationChanged(NavigationManager.Uri);
            }
        }

        public void Dispose()
        {
            NavigationManager.LocationChanged -= OnLocationChanged;
        }

        private async void OnLocationChanged(object sender, LocationChangedEventArgs args) => await OnLocationChanged(args.Location);

        private async Task OnLocationChanged(string location)
        {
            var uri = new Uri(location);

            var options = new Segment.Model.Options();

            if (string.IsNullOrEmpty(AppState.UserId))
            {
                if (await AppState.LocalStorage.ContainKeyAsync("anonymous_id"))
                {
                    AppState.UserId = await AppState.LocalStorage.GetItemAsync<string>("anonymous_id");
                }
                else
                {
                    AppState.UserId = Guid.NewGuid().ToString();
                    await AppState.LocalStorage.SetItemAsync("anonymous_id", AppState.UserId);
                }
            }

            Segment.Analytics.Client.Page(AppState.UserId, Name,
                new Segment.Model.Properties()
                {
                    { "Url", uri.AbsoluteUri },
                    { "Path", uri.AbsolutePath },
                    { "Title", Name },
                    { "ClusterId", AppState.ClusterId }
                },
                options);

            await Task.CompletedTask;
        }
    }
}