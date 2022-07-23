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

namespace Neon.Tailwind
{
    /// <summary>
    /// Portals provide a first-class way to render children into a DOM node that 
    /// exists outside the DOM hierarchy of the parent component
    /// </summary>
    public class Portal : ComponentBase
    {
        /// <summary>
        /// The injected <see cref="IPortalBinder"/>.
        /// </summary>
        [Inject] 
        public IPortalBinder PortalBinder { get; set; }

        /// <summary>
        /// The name of the Portal.
        /// </summary>
        [Parameter] 
        public string Name { get; set; } = "root";

        private RenderFragment content;

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            PortalBinder?.RegisterPortal(Name, this);
        }

        /// <summary>
        /// Renders the <see cref="RenderFragment"/>.
        /// </summary>
        /// <param name="content"></param>
        public void RenderContent(RenderFragment content)
        {
            this.content = content;
            StateHasChanged();
        }

        /// <inheritdoc/>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenElement(0, "div");
            builder.AddAttribute(1, "id", Name);
            builder.AddContent(2, content);
            builder.CloseElement();


        }
    }
}
