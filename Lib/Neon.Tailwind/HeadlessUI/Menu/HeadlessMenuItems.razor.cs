//-----------------------------------------------------------------------------
// FILE:	    HeadlessMenuItems.razor.cs
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
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Neon.Tailwind
{
    public partial class HeadlessMenuItems : ComponentBase, IAsyncDisposable
    {
        [CascadingParameter] public HeadlessMenu CascadedMenu { get; set; } = default!;

        [Parameter] public RenderFragment ChildContent { get; set; }

        [Parameter] public bool Static { get; set; }
        [Parameter] public string TagName { get; set; } = "div";
        [Parameter] public string Id { get; set; } = HtmlElement.GenerateId();

        [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        private HtmlElement rootElement;
        private KeyDownEventHandler keyDownEventHandler;
        private HeadlessMenu Menu { get; set; } = default!;

        [MemberNotNull(nameof(Menu), nameof(CascadedMenu))]
        public override Task SetParametersAsync(ParameterView parameters)
        {
            //This is here to follow the pattern/example as implmented in Microsoft's InputBase component
            //https://github.com/dotnet/aspnetcore/blob/main/src/Components/Web/src/Forms/InputBase.cs

            parameters.SetParameterProperties(this);

            if (Menu == null)
            {
                if (CascadedMenu == null)
                    throw new InvalidOperationException($"You must use {nameof(HeadlessMenuItems)} inside an {nameof(HeadlessMenu)}.");

                Menu = CascadedMenu;
            }
            else if (CascadedMenu != Menu)
            {
                throw new InvalidOperationException($"{nameof(HeadlessMenuItems)} does not support changing the {nameof(HeadlessMenu)} dynamically.");
            }

            return base.SetParametersAsync(ParameterView.Empty);
        }
        protected override void OnInitialized() => Menu.RegisterItems(this);
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (keyDownEventHandler != null)
                await keyDownEventHandler.RegisterElement(rootElement!);
        }

        public async Task HandleKeyDown(KeyboardEventArgs eventArgs)
        {
            string key = eventArgs.Key;
            if (string.IsNullOrEmpty(key)) return;

            switch (key)
            {
                case KeyboardKey.ArrowDown:
                    Menu.GoToItem(MenuFocus.Next);
                    break;
                case KeyboardKey.ArrowUp:
                    Menu.GoToItem(MenuFocus.Previous);
                    break;
                case KeyboardKey.Home:
                case KeyboardKey.PageUp:
                    Menu.GoToItem(MenuFocus.First);
                    break;
                case KeyboardKey.End:
                case KeyboardKey.PageDown:
                    Menu.GoToItem(MenuFocus.Last);
                    break;
                case KeyboardKey.Enter:
                case KeyboardKey.Escape:
                    await Menu.Close();
                    break;
                case KeyboardKey.Tab:
                    await Menu.Close(true);
                    break;
                default:
                    Menu.Search(key);
                    break;
            }
        }

        public ValueTask FocusAsync() => rootElement?.FocusAsync() ?? ValueTask.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            if (keyDownEventHandler != null)
                await keyDownEventHandler.UnregisterElement(rootElement!);
        }

        public static implicit operator ElementReference(HeadlessMenuItems element) => element?.rootElement ?? default!;
    }
}
