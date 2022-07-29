//-----------------------------------------------------------------------------
// FILE:	    HeadlessDialog.razor.cs
// CONTRIBUTOR: Simon Zhang
// COPYRIGHT:  	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Neon.Tailwind
{
    public partial class HeadlessDisclosure : ComponentBase, IAsyncDisposable
    {

        /// <summary>
        /// The UI content.
        /// </summary>
        [Parameter]
        public RenderFragment<HeadlessDisclosure> ChildContent { get; set; }

        /// <summary>
        /// Whether the transition should run on initial mount.
        /// </summary>
        [Parameter]
        public bool Show { get; set; } = false;

        [Parameter] public bool IsEnabled { get; set; } = true;

        private HeadlessDisclosurePanel disclosurePanel { get; set; }
        private HeadlessDisclosureButton disclosureButton { get; set; }
        
        /// <summary>
        /// The current 
        /// </summary>
        public DisclosureState State { get; protected set; } = DisclosureState.Closed;

        /// <summary>
        /// Constructor.
        /// </summary>
        public HeadlessDisclosure()
        {
        }
 
        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            base.OnInitialized();
            IsOpen = Show;
        }

        public async Task RegisterButton(HeadlessDisclosureButton button)
        {
            await Task.CompletedTask;
            disclosureButton = button;


        }

        public async Task RegisterPanel(HeadlessDisclosurePanel item)
        {
            await Task.CompletedTask;
            disclosurePanel = item;
        }

        /// <summary>
        /// Method called by a <see cref="HeadlessDisclosurePanel"/> to unregister itself with
        /// the current <see cref="HeadlessDisclosure"/>.
        /// </summary>
        /// <param name="item"></param>
        public async Task UnregisterPanel(HeadlessDisclosurePanel item)
        {
            await Task.CompletedTask;
            disclosurePanel = null;
        }
        /// <summary>
        /// Method called by a <see cref="HeadlessDisclosurePanel"/> to unregister itself with
        /// the current <see cref="HeadlessDisclosure"/>.
        /// </summary>
        /// <param name="item"></param>
        public async Task UnregisterButton(HeadlessDisclosureButton button)
        {
            await Task.CompletedTask;
            disclosureButton = null;
        }


        /// <summary>
        /// Opens the current <see cref="HeadlessDisclosure"/>
        /// </summary>
        /// <returns></returns>
        public async Task Open()
        {
            Show = true;
            await disclosurePanel.Open();
            State = DisclosureState.Open;
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Closes the current <see cref="HeadlessDisclosure"/>
        /// </summary>
        /// <returns></returns>
        public async Task Close()
        {
            Show = false;
            await disclosurePanel.Close();
            State = DisclosureState.Closed;

            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Toggles the current <see cref="HeadlessDisclosure"/>
        /// </summary>
        /// <returns></returns>
        public async Task Toggle()
        {
            if (State == DisclosureState.Closed)
                await Open();
            else
                await Close();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
  

}
