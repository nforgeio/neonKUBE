//-----------------------------------------------------------------------------
// FILE:	    HeadlessDialogPanel.razor.cs
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
using Microsoft.AspNetCore.Components.Web;
using Neon.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Neon.Tailwind
{
    public partial class HeadlessDialogPanel : ComponentBase, IAsyncDisposable
    {
        /// <summary>
        /// The cascaded <see cref="HeadlessDialog"/>.
        /// </summary>
        [CascadingParameter] 
        public HeadlessDialog CascadedDialog { get; set; } = default!;

        /// <summary>
        /// The styled Dialog panel.
        /// </summary>
        [Parameter] 
        public RenderFragment<HeadlessDialogPanel> ChildContent { get; set; }

        /// <summary>
        /// Whether the dialog panel is enabled.
        /// </summary>
        [Parameter] public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// Whether the dialog panel is visible.
        /// </summary>
        [Parameter] public bool IsVisible { get; set; } = true;

        /// <summary>
        /// The container ID.
        /// </summary>
        [Parameter] 
        public string Id { get; set; } = HtmlElement.GenerateId();

        /// <summary>
        /// Whether the panel should be initially shown.
        /// </summary>
        [Parameter] 
        public bool Show { get; set; }

        /// <summary>
        /// Callback that is called when the dialog is opened.
        /// </summary>
        [Parameter]
        public EventCallback OnOpen { get; set; }

        /// <summary>
        /// Callback that is called when the dialog is closed.
        /// </summary>
        [Parameter]
        public EventCallback OnClose { get; set; }

        /// <summary>
        /// Additional HTML attributes to be applied to the <see cref="RenderFragment"/>.
        /// </summary>
        [Parameter(CaptureUnmatchedValues = true)] 
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        private bool isOpen;
        private HeadlessDialog Dialog { get; set; } = default!;

        private HtmlElement rootElement;
        public static implicit operator ElementReference(HeadlessDialogPanel element)
        {
            return element?.rootElement ?? default!;
        }
        bool shouldFocus;

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            isOpen = Show;
        }

        /// <inheritdoc/>
        protected override async Task OnInitializedAsync()
        {
            await Dialog.RegisterPanel(this);
        }

        /// <inheritdoc/>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (shouldFocus)
            {
                shouldFocus = false;
                await Task.Yield();
                //await FocusAsync();
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            _ = Dialog.UnregisterPanel(this);
        }

        [MemberNotNull(nameof(Dialog), nameof(CascadedDialog))]

        protected override async Task OnParametersSetAsync()
        {
            if (Show) 
            { 
                await Task.CompletedTask;
            }
        }

        /// <inheritdoc/>
        public override Task SetParametersAsync(ParameterView parameters)
        {
            //This is here to follow the pattern/example as implmented in Microsoft's InputBase component
            //https://github.com/dotnet/aspnetcore/blob/main/src/Components/Web/src/Forms/InputBase.cs

            parameters.SetParameterProperties(this);

            if (Dialog == null)
            {
                if (CascadedDialog == null)
                    throw new InvalidOperationException($"You must use {nameof(HeadlessDialogPanel)} inside an {nameof(HeadlessDialog)}.");

                Dialog= CascadedDialog;
            }
            else if (CascadedDialog != Dialog)
            {
                throw new InvalidOperationException($"{nameof(HeadlessDialogPanel)} does not support changing the {nameof(HeadlessDialog)} dynamically.");
            }

            return base.SetParametersAsync(ParameterView.Empty);
        }

        private async Task HandleClick(MouseEventArgs e)
        {
            await SyncContext.Clear;

            if (!IsEnabled) return;
        }

        private void HandleFocus(EventArgs e)
        {
            if (IsEnabled)
            {
                return;
            }
        }
        private async Task HandlePointerMove(PointerEventArgs e)
        {
            await SyncContext.Clear;
            
            if (!IsEnabled) return;
        }
        private void HandleMouseOut(MouseEventArgs e)
        {
            if (!IsEnabled) return;
        }

        public ValueTask FocusAsync() => rootElement?.FocusAsync() ?? ValueTask.CompletedTask;

        public async Task Open()
        {
            //if (!IsEnabled) return;
            Show = true;
            shouldFocus = true;
            await OnOpen.InvokeAsync();
            //await Dialog.Open();
            await InvokeAsync(StateHasChanged);
        }

        public async Task Close()
        {
            //if (!IsEnabled) return;
            Show = false;
            //await Dialog.Close();
            await OnClose.InvokeAsync();
            await InvokeAsync(StateHasChanged);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}