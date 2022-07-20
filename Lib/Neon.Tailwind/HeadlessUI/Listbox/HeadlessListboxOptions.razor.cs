//-----------------------------------------------------------------------------
// FILE:	    HeadlessListboxOptions.razor.cs
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

namespace Neon.Tailwind.HeadlessUI
{
    public partial class HeadlessListboxOptions<TValue> : ComponentBase, IAsyncDisposable
    {
        [CascadingParameter] public HeadlessListbox<TValue> CascadedListbox { get; set; } = default!;

        [Parameter] public RenderFragment ChildContent { get; set; }

        [Parameter] public bool Static { get; set; }
        [Parameter] public string TagName { get; set; } = "ul";
        [Parameter] public string Id { get; set; } = HtmlElement.GenerateId();

        [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        private HtmlElement rootElement;
        private KeyDownEventHandler keyDownEventHandler;

        protected HeadlessListbox<TValue> Listbox { get; set; } = default!;

        [MemberNotNull(nameof(Listbox), nameof(CascadedListbox))]
        public override Task SetParametersAsync(ParameterView parameters)
        {
            parameters.SetParameterProperties(this);

            if (Listbox == null)
            {
                if (CascadedListbox == null)
                    throw new InvalidOperationException($"You must use {nameof(HeadlessListboxOptions<TValue>)} inside an {nameof(HeadlessListbox<TValue>)}.");

                Listbox = CascadedListbox;
            }
            else if (CascadedListbox != Listbox)
            {
                throw new InvalidOperationException($"{nameof(HeadlessListboxOptions<TValue>)} does not support changing the {nameof(HeadlessListbox<TValue>)} dynamically.");
            }

            return base.SetParametersAsync(ParameterView.Empty);
        }
        protected override void OnInitialized() => Listbox.RegisterOptions(this);
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
                case KeyboardKey.Enter:
                    Listbox.SetActiveAsValue();
                    await Listbox.Close();
                    break;
                case KeyboardKey.ArrowDown:
                    Listbox.GoToOption(ListboxFocus.Next);
                    break;
                case KeyboardKey.ArrowUp:
                    Listbox.GoToOption(ListboxFocus.Previous);
                    break;
                case KeyboardKey.Home:
                case KeyboardKey.PageUp:
                    Listbox.GoToOption(ListboxFocus.First);
                    break;
                case KeyboardKey.End:
                case KeyboardKey.PageDown:
                    Listbox.GoToOption(ListboxFocus.Last);
                    break;
                case KeyboardKey.Escape:
                    await Listbox.Close();
                    break;
                case KeyboardKey.Tab:
                    await Listbox.Close(true);
                    break;
                default:
                    Listbox.Search(key);
                    break;
            }
        }

        public ValueTask FocusAsync() => rootElement?.FocusAsync() ?? ValueTask.CompletedTask;

        public async ValueTask DisposeAsync()
        {
            if (keyDownEventHandler != null)
                await keyDownEventHandler.UnregisterElement(rootElement!);
        }

        public static implicit operator ElementReference(HeadlessListboxOptions<TValue> element) => element?.rootElement ?? default!;
    }
}
