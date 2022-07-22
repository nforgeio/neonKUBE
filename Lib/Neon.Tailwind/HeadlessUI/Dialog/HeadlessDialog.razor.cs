//-----------------------------------------------------------------------------
// FILE:	    HeadlessDialog.razor.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;

namespace Neon.Tailwind
{
    public partial class HeadlessDialog : ComponentBase, IAsyncDisposable
    {
        private bool isOpen = true;

        /// <summary>
        /// The UI content.
        /// </summary>
        [Parameter] 
        public RenderFragment<HeadlessDialog> ChildContent { get; set; }

        /// <summary>
        /// Whether the transition should run on initial mount.
        /// </summary>
        [Parameter]
        public bool Show { get; set; } = false;

        /// <summary>
        /// Called when the dialog is opened.
        /// </summary>
        [Parameter] 
        public EventCallback OnOpen { get; set; }

        /// <summary>
        /// Called when the dialog is closed.
        /// </summary>
        [Parameter] 
        public EventCallback OnClose { get; set; }

        /// <summary>
        /// Additional attributes to be applied to the child content.
        /// </summary>
        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> Attributes { get; set; }

        private Transition           transition { get; set; }
        private HeadlessDialogPanel  dialogPanel { get; set; }
        private ClickOffEventHandler clickOffEventHandler { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public HeadlessDialog()
        {
        }

        /// <inheritdoc/>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await base.OnAfterRenderAsync(firstRender);

            if (clickOffEventHandler != null)
            {
                await clickOffEventHandler.RegisterElement(dialogPanel!);
            }
        }

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            base.OnInitialized();
            isOpen = Show;
        }

        /// <inheritdoc/>
        public void Dispose() 
        { 
        }

        /// <summary>
        /// The current 
        /// </summary>
        public MenuState State { get; protected set; } = MenuState.Closed;

        /// <summary>
        /// Method called by a <see cref="HeadlessDialogPanel"/> to register itself with
        /// the current <see cref="HeadlessDialog"/>.
        /// </summary>
        /// <param name="item"></param>
        public async Task RegisterPanel(HeadlessDialogPanel item)
        {
            await Task.CompletedTask;
            dialogPanel = item;
        }

        /// <summary>
        /// Method called by a <see cref="HeadlessDialogPanel"/> to unregister itself with
        /// the current <see cref="HeadlessDialog"/>.
        /// </summary>
        /// <param name="item"></param>
        public async Task UnregisterPanel(HeadlessDialogPanel item)
        {
            await Task.CompletedTask;
            dialogPanel = null;
        }

        /// <summary>
        /// Toggles the current <see cref="HeadlessDialog"/>
        /// </summary>
        /// <returns></returns>
        public async Task Toggle()
        {
            if (State == MenuState.Closed)
                await Open();
            else
                await Close();
        }

        /// <summary>
        /// Opens the current <see cref="HeadlessDialog"/>
        /// </summary>
        /// <returns></returns>
        public async Task Open()
        {
            Show = true;
            await OnOpen.InvokeAsync();
            
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Closes the current <see cref="HeadlessDialog"/>
        /// </summary>
        /// <returns></returns>
        public async Task Close()
        {
            Show = false;
            await OnClose.InvokeAsync();
            await dialogPanel.Close();
            await clickOffEventHandler.UnregisterElement(dialogPanel);
            await InvokeAsync(StateHasChanged);
        }

        /// <summary>
        /// Handler for when the user clicks away from the <see cref="HeadlessDialogPanel"/>.
        /// This by default will close the <see cref="HeadlessDialog"/>.
        /// </summary>
        /// <returns></returns>
        public Task HandleClickOff()
        {
            return Close();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}
