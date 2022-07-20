//-----------------------------------------------------------------------------
// FILE:	    KeyDownEventHandler.cs
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
using System.Reflection.Metadata;
using System.Threading.Tasks;

namespace Neon.Tailwind.HeadlessUI
{
    public class KeyDownEventHandler : EventHandlerComponentBase<KeyDownEventHandler>
    {
        [Parameter] public EventCallback<KeyboardEventArgs> OnKeyDown { get; set; }
        [Parameter] public List<string> PreventDefaultForKeys { get; set; } = new();
        [JSInvokable] public Task HandleKeyDown(KeyboardEventArgs args)
        {
            return OnKeyDown.InvokeAsync(args);
        }
        
        public KeyDownEventHandler() : base("keydownhandler", nameof(HandleKeyDown)) { }

        protected override IEnumerable<object> GetAdditionalInitializationParameters()
        {
            yield return PreventDefaultForKeys.ToArray();
        }
    }
}
