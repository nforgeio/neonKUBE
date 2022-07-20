//-----------------------------------------------------------------------------
// FILE:	    EventHandlerComponentBase.cs
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
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neon.Tailwind.HeadlessUI
{
    public abstract class EventHandlerComponentBase<TComponent> : ComponentBase, IAsyncDisposable
        where TComponent : EventHandlerComponentBase<TComponent>
    {
        protected IJSObjectReference jsHandlerReference;
        private readonly string jsFileName;
        private readonly string handlerMethodName;
        private readonly List<ElementReference> registeredElements = new();
        
        [Inject] public IJSRuntime JSRuntime { get; set; } = default!;

        protected EventHandlerComponentBase(string jsFileName, string handlerMethodName)
        {
            this.jsFileName = jsFileName;
            this.handlerMethodName = handlerMethodName;
        }

        protected override async Task OnInitializedAsync()
        {
            try
            {
                var jsModule = await JSRuntime.InvokeAsync<IJSObjectReference>("import", $"./_content/Neon.Tailwind/{jsFileName}.js");
                var objRef = DotNetObjectReference.Create((TComponent)this);

                var parameters = GetAdditionalInitializationParameters().ToList();
                parameters.Insert(0, objRef);
                parameters.Insert(1, handlerMethodName);

                jsHandlerReference = await jsModule.InvokeAsync<IJSObjectReference>("makeHandler", parameters.ToArray());
                foreach (var element in registeredElements)
                {
                    await jsHandlerReference.InvokeVoidAsync("registerElement", element);
                }
            }
            catch
            {
                //if we are prerendering we don't have access to the jsRuntime so just ignore the exception
            }
        }

        protected virtual IEnumerable<object> GetAdditionalInitializationParameters() => Enumerable.Empty<object>();

        public ValueTask DisposeAsync()
            => jsHandlerReference?.InvokeVoidAsync("unmount") ?? ValueTask.CompletedTask;

        public async Task RegisterElement(ElementReference element)
        {
            if (element.Id == null) return;
            if (registeredElements.Any(e => e.Id == element.Id)) return;
            registeredElements.Add(element);
            if (jsHandlerReference == null) return;
            await jsHandlerReference.InvokeVoidAsync("registerElement", element);
        }

        public async Task UnregisterElement(ElementReference element)
        {
            if (element.Id == null) return;
            registeredElements.Remove(element);
            if (jsHandlerReference == null) return;
            await jsHandlerReference.InvokeVoidAsync("unregisterElement", element);
        }
    }
}
