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
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Neon.Tailwind.HeadlessUI
{
    public partial class HeadlessDialogPanel : ComponentBase, IAsyncDisposable
    {
        private bool isOpen;
        [CascadingParameter] public HeadlessDialog CascadedDialog { get; set; } = default!;

        [Parameter] public RenderFragment<HeadlessDialogPanel> ChildContent { get; set; }

        [Parameter] public bool IsEnabled { get; set; } = true;
        [Parameter] public bool IsVisible { get; set; } = true;

        [Parameter] public string SearchValue { get; set; } = "";

        [Parameter] public string TagName { get; set; } = "div";
        [Parameter] public string Id { get; set; } = HtmlElement.GenerateId();


        [Parameter] public string Enter { get; set; }
        [Parameter] public string EnterFrom { get; set; }
        [Parameter] public string EnterTo { get; set; }
        [Parameter] public int EnterDuration { get; set; }
        [Parameter] public string Leave { get; set; }
        [Parameter] public string LeaveFrom { get; set; }
        [Parameter] public string LeaveTo { get; set; }
        [Parameter] public int LeaveDuration { get; set; }
        [Parameter] public bool Show { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }

        [Parameter(CaptureUnmatchedValues = true)] 
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        private Transition transition;
        private HeadlessDialog Dialog { get; set; } = default!;

        private HtmlElement rootElement;
        public static implicit operator ElementReference(HeadlessDialogPanel element) => element?.rootElement ?? default!;
        bool shouldFocus;

        protected override void OnInitialized()
        {
            isOpen = Show;
            Dialog.RegisterPanel(this);
        }
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (shouldFocus)
            {
                shouldFocus = false;
                await Task.Yield();
                await FocusAsync();
            }
        }

        public void Dispose()
        {
            Dialog.UnregisterPanel(this);
        }

        [MemberNotNull(nameof(Dialog), nameof(CascadedDialog))]
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
            isOpen = true;
            shouldFocus = true;
            await Dialog.Open();
            StateHasChanged();
        }

        public async Task Close()
        {
            //if (!IsEnabled) return;
            isOpen = false;
            await Dialog.Close();

            StateHasChanged();
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }
}