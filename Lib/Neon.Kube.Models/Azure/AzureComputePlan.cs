//-----------------------------------------------------------------------------
// FILE:        AzureComputePlan.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Kube;
using Neon.ModelGen;
using Newtonsoft.Json;

namespace Neon.Kube.Models
{
    /// <summary>
    /// Describes an Azure marketplace VM image compute plan.
    /// </summary>
    [Target("all")]
    public interface AzureComputePlan
    {
        /// <summary>
        /// Specifies the name of the entity publishing the image to the marketplace.
        /// </summary>
        [JsonProperty(PropertyName = "Publisher", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        string Publisher { get; set; }

        /// <summary>
        /// Specifies the name of the publisher's offer.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        string Name { get; set; }

        /// <summary>
        /// Specifies the product/offer name.
        /// </summary>
        [JsonProperty(PropertyName = "Product", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        string Product { get; set; }
    }
}
