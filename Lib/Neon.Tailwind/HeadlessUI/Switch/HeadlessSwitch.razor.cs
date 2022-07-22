//-----------------------------------------------------------------------------
// FILE:	    HeadlessSwitch.razor.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Tailwind
{
    public partial class HeadlessSwitch : ComponentBase
    {
        [Parameter] public string Id { get; set; } = HtmlElement.GenerateId();
        [Parameter] public string TagName { get; set; } = "button";

        [Parameter] public bool Checked { get; set; }
        [Parameter] public bool IsEnabled { get; set; } = true;
        [Parameter] public EventCallback<bool> CheckedChanged { get; set; }


        [Parameter] public RenderFragment ChildContent { get; set; }

        [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        [CascadingParameter] public HeadlessSwitchGroup Group { get; set; }

        protected bool CurrentChecked
        {
            get => Checked;
            set
            {
                var hasChanged = value != Checked;
                if (hasChanged)
                {
                    Checked = value;
                    _ = CheckedChanged.InvokeAsync(Checked);
                }
            }
        }

        protected override void OnInitialized() => Group?.RegisterSwitch(this);

        public void Toggle() => CurrentChecked = !CurrentChecked;

        public void HandleClick()
        {
            if (IsEnabled)
                Toggle();
        }


    }
}
