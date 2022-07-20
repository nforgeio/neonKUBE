//-----------------------------------------------------------------------------
// FILE:	    Portal.cs
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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Neon.Tailwind.HeadlessUI
{
    public class Portal : ComponentBase
    {
        [Inject] public IPortalBinder PortalBinder { get; set; }
        //[Parameter] public string Id { get; set; } = GenerateId();
        [Parameter] public string Name { get; set; } = "root";
        //[Parameter] public string Class { get; set; }
        //[Parameter] public string TagName { get; set; } = "div";
        //[Parameter] public int    ZIndex { get; set; } = 999;

        private RenderFragment content;

        protected override void OnInitialized()
        {
            PortalBinder?.RegisterPortal(Name, this);
        }

        public static string GenerateId() => Guid.NewGuid().ToString("N");

        public void RenderContent(RenderFragment content)
        {
            this.content = content;
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.AddContent(0, content);
        }
    }
}
