//-----------------------------------------------------------------------------
// FILE:	    HeadlessButton.razor.cs
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
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Neon.Tailwind.HeadlessUI
{
    public partial class HeadlessButton : ComponentBase, IAsyncDisposable
    {
        [Inject] protected IJSRuntime jsRuntime { get; set; }
        private IJSObjectReference    jsModule;

        [Parameter] public bool IsEnabled { get; set; } = true;
        [Parameter] public bool IsVisible { get; set; } = true;

        [Parameter] public EventCallback OnClick { get; set; }

        [Parameter] public string TagName { get; set; } = "button";
        [Parameter] public string Type { get; set; } = "button";
        [Parameter] public string AriaLabel { get; set; }

        [Parameter] public RenderFragment ChildContent { get; set; }
        [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        protected HtmlElement buttonElement;
        private string previouslyRenderedElementId = null;

        protected override void OnInitialized()
        {
            
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            await EnsureInitialized();
            buttonElement?.FocusAsync();
        }

        private async Task EnsureInitialized()
        {
            if (buttonElement == null) return;
            if (buttonElement.AsElementReference().Id != previouslyRenderedElementId)
            {
                try
                {
                    await PreventDefaultKeyBehaviorOnEnterAndSpace();
                    previouslyRenderedElementId = ((ElementReference)buttonElement).Id;
                }
                catch (JSException)
                {
                    //if we are prerendering we don't have access to the jsRuntime so just ignore the exception
                }
            }
        }

        private async Task PreventDefaultKeyBehaviorOnEnterAndSpace()
        {
            if (jsRuntime is null || buttonElement is null) return;

            jsModule = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/Neon.Tailwind/common.js");
            await jsModule.InvokeVoidAsync("preventDefaultKeyBehaviorOnKeys", buttonElement.AsElementReference(), new List<string> { KeyboardKey.Enter, KeyboardKey.Space });
        }

        protected async Task HandleClick(MouseEventArgs e)
        {
            if (!IsEnabled || !IsVisible) return;

            await OnClick.InvokeAsync((this, e));
        }

        [JSInvokable]
        public async Task HandleKeyUp(KeyboardEventArgs eventArgs)
        {
            if (!IsEnabled || !IsVisible) return;

            switch (eventArgs.Key)
            {
                case KeyboardKey.Space:
                case KeyboardKey.Enter:
                    {
                        await OnClick.InvokeAsync((this, new MouseEventArgs()));
                        break;
                    }
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (jsModule is null || buttonElement is null) return;
            await jsModule.InvokeVoidAsync("preventDefaultKeyBehaviorOnKeys", buttonElement.AsElementReference(), new List<string> { }, false);
        }

    }
}
