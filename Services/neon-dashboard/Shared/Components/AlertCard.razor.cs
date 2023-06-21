//-----------------------------------------------------------------------------
// FILE:        Login.cshtml.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

namespace NeonDashboard.Shared.Components
{
     public enum StyleType
    {
        Default,
        Outline,
        Error,
        Success,
        Warning
    };

    public partial class AlertCard : ComponentBase, IDisposable
    {
        /// <summary>
        /// alert card css style one of StyleType
        /// </summary>
        [Parameter]
        public StyleType Type { get; set; } = StyleType.Default;

        /// <summary>
        /// title text
        /// </summary>
        [Parameter]
        public string Title { get; set; }

        /// <summary>
        /// content on left side of body
        /// </summary>
        [Parameter]
        public RenderFragment Left { get; set; }

        /// <summary>
        /// content on right side of body
        /// </summary>
        [Parameter]
        public RenderFragment Right { get; set; }

        /// <summary>
        /// main body content. eg. description 
        /// </summary>
        [Parameter]
        public RenderFragment ChildContent { get; set; }

        private static  Dictionary<StyleType, string> CardStyle = new Dictionary<StyleType, string>()
        {
            {StyleType.Default,"text-slate-50 bg-card " },
            {StyleType.Outline,"text-slate-50 border border-slate-500" },
            {StyleType.Error,"" },
            {StyleType.Success,"" },
            {StyleType.Warning,"" },
        };

        /// <inheritdoc/>
        public void Dispose()
        {
        }
    }
}
