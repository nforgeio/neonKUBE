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

using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Neon.Tailwind.HeadlessUI
{
    public partial class HeadlessDialog : ComponentBase
    {
        private bool isOpen = true;

        /// <summary>
        /// The UI content.
        /// </summary>
        [Parameter] 
        public RenderFragment ChildContent { get; set; }

        /// <summary>
        /// Applied the entire time an element is entering. Usually you define your duration 
        /// and what properties you want to transition.
        /// </summary>
        [Parameter] 
        public string Enter { get; set; }

        /// <summary>
        /// The starting point to enter from.
        /// </summary>
        [Parameter] 
        public string EnterFrom { get; set; }

        /// <summary>
        /// The ending point to enter to
        /// </summary>
        [Parameter] 
        public string EnterTo { get; set; }


        [Parameter] 
        public int EnterDuration { get; set; }

        [Parameter] 
        public string Leave { get; set; }

        [Parameter] 
        public string LeaveFrom { get; set; }

        [Parameter] 
        public string LeaveTo { get; set; }

        [Parameter] 
        public int LeaveDuration { get; set; }

        [Parameter] 
        public bool Show { get; set; }

        [Parameter] 
        public EventCallback OnClick { get; set; }

        [Parameter] 
        public EventCallback OnOpen { get; set; }

        [Parameter] 
        public EventCallback OnClose { get; set; }

        [Parameter] 
        public int DebouceTimeout { get; set; } = 350;

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> Attributes { get; set; }

        private Transition           transition { get; set; }
        private HeadlessDialogPanel  dialogPanel { get; set; }
        private ClickOffEventHandler clickOffEventHandler { get; set; }
        private HtmlElement rootElement;

        public MenuState State { get; protected set; } = MenuState.Closed;

        public HeadlessDialog()
        {
        }

        public void RegisterPanel(HeadlessDialogPanel item)
        {
            dialogPanel = item;
            if (isOpen)
            {
                InvokeAsync(dialogPanel.Open);
            }
            else
            {
                InvokeAsync(dialogPanel.Close);
            }
        }

        public void UnregisterPanel(HeadlessDialogPanel item)
        {
            dialogPanel = null;
        }

        public async Task Toggle()
        {
            if (State == MenuState.Closed)
                await Open();
            else
                await Close();
        }

        public async Task Open()
        {
            isOpen = true;
            await OnOpen.InvokeAsync();
            await clickOffEventHandler.RegisterElement(dialogPanel);
            await InvokeAsync(StateHasChanged);
        }

        public async Task Close(bool suppressFocus = false)
        {
            isOpen = false;
            await OnClose.InvokeAsync();
            await clickOffEventHandler.UnregisterElement(dialogPanel);
            await InvokeAsync(StateHasChanged);
        }

        public Task HandleClickOff()
        {
            if (!isOpen)
            {
                return Task.CompletedTask;
            }

            return Close();
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
        }

        protected override void OnInitialized()
        {
            isOpen = Show;
        }


        public void Dispose() { }
    }
}
