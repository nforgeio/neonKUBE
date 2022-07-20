//-----------------------------------------------------------------------------
// FILE:	    TransitionGroup.cs
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
using System.Collections.Generic;
using System.Linq;

namespace Neon.Tailwind.HeadlessUI
{
    public class TransitionGroup : ComponentBase
    {
        private readonly List<Transition> transitions = new();

        [Parameter] public RenderFragment ChildContent { get; set; } = default!;
        [Parameter] public bool Show { get; set; }

        public void RegisterTransition(Transition transition)
        {
            transitions.Add(transition);
        }

        public void NotifyEndTransition()
        {
            InvokeAsync(StateHasChanged);
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (Show || !transitions.All(t => t.State == TransitionState.Hidden))
            {
                builder.OpenComponent<CascadingValue<TransitionGroup>>(0);
                builder.AddMultipleAttributes(1, new Dictionary<string, object>
                {
                    ["Value"] = this,
                    ["ChildContent"] = ChildContent
                });
                builder.CloseComponent();
            }
        }
    }
}
