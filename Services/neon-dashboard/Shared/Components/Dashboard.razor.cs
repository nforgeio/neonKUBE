//-----------------------------------------------------------------------------
// FILE:	    Dashboard.razor.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace NeonDashboard.Shared.Components
{
    public partial class Dashboard : ComponentBase, IDropUpItem, IDisposable
    {
        [Inject]
        AppState AppState { get; set; }

        public Dashboard() { }
        public Dashboard(
            string id,
            string name,
            string url = null, 
            string description = null,
            int    displayOrder = int.MaxValue)
        {
            Id           = id;
            Name         = name;
            Url          = url;
            Description  = description;
            DisplayOrder = displayOrder;
        }

        [CascadingParameter(Name = "CurrentDashboard")]
        public string CurrentDashboard { get; set; }

        [Parameter]
        [JsonProperty(PropertyName = "Id", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Id { get; set; } = null;

        [Parameter]
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; } = null;

        [Parameter]
        [JsonProperty(PropertyName = "Url", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "url", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Url { get; set; } = null;

        [Parameter]
        [JsonProperty(PropertyName = "Description", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "description", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Description { get; set; }

        [Parameter]
        [JsonProperty(PropertyName = "DisplayOrder", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "displayOrder", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int? DisplayOrder { get; set; }

        /// <summary>
        /// The height of the current frame. If this is the currently selected dashboard, return the max height.
        /// If this is not the current dashboard, then return 0 so that it is out of the way.
        /// </summary>
        public string Height
        {
            get
            {
                if (AppState?.CurrentDashboard == Id)
                {
                    return "100%";
                }
                return "0";
            }
        }

        /// <summary>
        /// The width of the current frame. If this is the currently selected dashboard, return the max width.
        /// If this is not the current dashboard, then return 0 so that it is out of the way.
        /// </summary>
        public string Width
        {
            get
            {
                if (AppState?.CurrentDashboard == Id)
                {
                    return "100%";
                }
                return "0";
            }
        }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            AppState.OnDashboardChange += StateHasChanged;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            AppState.OnDashboardChange -= StateHasChanged;
        }

        /// <inheritdoc />
        public string GetName()
        {
            return Name;
        }

        /// <inheritdoc />
        public string GetUrl()
        {
            return Url;
        }

        /// <inheritdoc />
        public string GetId()
        {
            return Id;
        }
    }
}