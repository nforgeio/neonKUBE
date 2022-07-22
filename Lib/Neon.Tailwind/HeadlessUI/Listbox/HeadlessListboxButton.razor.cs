//-----------------------------------------------------------------------------
// FILE:	    HeadlessListboxButton.razor.cs
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

namespace Neon.Tailwind
{
    public partial class HeadlessListboxButton<TValue> : ComponentBase
    {
        [CascadingParameter] public HeadlessListbox<TValue> CascadedListbox { get; set; } = default!;

        [Parameter] public RenderFragment ChildContent { get; set; }
        [Parameter] public string Id { get; set; } = HtmlElement.GenerateId();
        [Parameter] public string TagName { get; set; } = "button";

        [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        private HtmlElement rootElement;
        private KeyDownEventHandler keyDownEventHandler;
        protected HeadlessListbox<TValue> Listbox { get; set; } = default!;

        protected override void OnInitialized()
        {
            Listbox.RegisterButton(this);
        }

        [MemberNotNull(nameof(Listbox), nameof(CascadedListbox))]
        public override Task SetParametersAsync(ParameterView parameters)
        {
            parameters.SetParameterProperties(this);

            if (Listbox == null)
            {
                if (CascadedListbox == null)
                    throw new InvalidOperationException($"You must use {nameof(HeadlessListboxButton<TValue>)} inside an {nameof(HeadlessListbox<TValue>)}.");

                Listbox = CascadedListbox;
            }
            else if (CascadedListbox != Listbox)
            {
                throw new InvalidOperationException($"{nameof(HeadlessListboxButton<TValue>)} does not support changing the {nameof(HeadlessListbox<TValue>)} dynamically.");
            }

            return base.SetParametersAsync(ParameterView.Empty);
        }


        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (keyDownEventHandler != null)
                await keyDownEventHandler.RegisterElement(rootElement!);
        }

        protected async Task HandleKeyDown(KeyboardEventArgs eventArgs)
        {
            switch (eventArgs.Key)
            {
                case KeyboardKey.Space:
                case KeyboardKey.Enter:
                case KeyboardKey.ArrowDown:
                    {
                        await Listbox.Open();
                        Listbox.GoToOption(ListboxFocus.First);
                        break;
                    }
                case KeyboardKey.ArrowUp:
                    {
                        await Listbox.Open();
                        Listbox.GoToOption(ListboxFocus.Last);
                        break;
                    }

            }
        }

        protected async Task HandleFocus(EventArgs eventArgs)
        {
            if (Listbox.State == ListboxState.Open)
                await Listbox.OptionsFocusAsync();
        }

        public async Task HandleClick() => await Listbox.Toggle();
        public ValueTask FocusAsync() => rootElement?.FocusAsync() ?? ValueTask.CompletedTask;
        public static implicit operator ElementReference(HeadlessListboxButton<TValue> element) => element?.rootElement ?? default!;
    }
}
