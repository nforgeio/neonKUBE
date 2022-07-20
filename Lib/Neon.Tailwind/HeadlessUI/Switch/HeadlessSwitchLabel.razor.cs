//-----------------------------------------------------------------------------
// FILE:	    HeadlessSwitchLabel.razor.cs
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
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading.Tasks;

namespace Neon.Tailwind.HeadlessUI
{
    public partial class HeadlessSwitchLabel : ComponentBase
    {
        [Parameter] public bool Passive { get; set; }
        [Parameter] public RenderFragment ChildContent { get; set; }
        [CascadingParameter] public HeadlessSwitchGroup CascadedGroup { get; set; } = default!;

        [Parameter(CaptureUnmatchedValues = true)] public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        public HeadlessSwitchGroup Group { get; set; } = default!;

        [MemberNotNull(nameof(Group), nameof(CascadedGroup))]
        public override Task SetParametersAsync(ParameterView parameters)
        {
            //This is here to follow the pattern/example as implmented in Microsoft's InputBase component
            //https://github.com/dotnet/aspnetcore/blob/main/src/Components/Web/src/Forms/InputBase.cs

            parameters.SetParameterProperties(this);

            if (Group == null)
            {
                if (CascadedGroup == null)
                    throw new InvalidOperationException($"You must use {nameof(HeadlessSwitchLabel)} inside an {nameof(HeadlessSwitchGroup)}.");

                Group = CascadedGroup;
            }
            else if (CascadedGroup != Group)
            {
                throw new InvalidOperationException($"{nameof(HeadlessSwitchLabel)} does not support changing the {nameof(HeadlessSwitchGroup)} dynamically.");
            }

            return base.SetParametersAsync(ParameterView.Empty);
        }

        public void HandleClick()
        {
            if (!Passive)
                Group?.ToggleSwitch();
        }
    }
}
