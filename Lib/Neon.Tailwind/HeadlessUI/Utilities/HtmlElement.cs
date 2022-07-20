//-----------------------------------------------------------------------------
// FILE:	    HtmlElement.cs
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
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Tailwind.HeadlessUI
{
    public class HtmlElement : ComponentBase
    {
        [Parameter] public string Id { get; set; } = GenerateId();
        [Parameter] public string TagName { get; set; } = "div";
        [Parameter] public string Type { get; set; } = null;
        [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> Attributes { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }
        
        [Parameter] public List<string> PreventDefaultOn { get; set; } = new();
        [Parameter] public List<string> StopPropagationOn { get; set; } = new();

        private ElementReference elementReference;

        public static string GenerateId() => Guid.NewGuid().ToString("N");
        
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (string.IsNullOrEmpty(TagName))
            {
                builder.AddContent(0, ChildContent);
                return;
            }

            builder.OpenElement(0, TagName);
            builder.AddAttribute(1, "id", Id);
            builder.AddAttribute(2, "type", Type);
            builder.AddMultipleAttributes(3, Attributes);
            foreach (var eventName in PreventDefaultOn.Where(s => !string.IsNullOrEmpty(s)))
                builder.AddEventPreventDefaultAttribute(4, eventName, true);
            foreach (var eventName in StopPropagationOn.Where(s => !string.IsNullOrEmpty(s)))
                builder.AddEventStopPropagationAttribute(5, eventName, true);
            builder.AddElementReferenceCapture(6, r => OnSetElementReference(r));
            builder.AddContent(6, ChildContent);
            builder.CloseElement();
        }
        
        public void OnSetElementReference(ElementReference reference) => elementReference = reference;
        public ValueTask FocusAsync() => elementReference.FocusAsync();

        public ElementReference AsElementReference() => elementReference;

        public static implicit operator ElementReference(HtmlElement element) => element == null ? default : element.elementReference;
    }
}
