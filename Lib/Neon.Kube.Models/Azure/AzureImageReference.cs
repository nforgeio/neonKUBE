//-----------------------------------------------------------------------------
// FILE:        AzureImageReference.cs
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
    /// Identifies an Azure image.
    /// </summary>
    [Target("all")]
    public interface AzureImageReference
    {
        /// <summary>
        /// Identifies the Azure publisher.
        /// </summary>
        [JsonProperty(PropertyName = "Publisher", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        string Publisher { get; set; }

        /// <summary>
        /// Identifies the Azure marketplace product/offer.
        /// </summary>
        [JsonProperty(PropertyName = "Offer", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        string Offer { get; set; }

        /// <summary>
        /// Identifies the image SKU.
        /// </summary>
        [JsonProperty(PropertyName = "Sku", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        string Sku { get; set; }

        /// <summary>
        /// Identifies the image version.
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        string Version { get; set; }

        /// <summary>
        /// Identifies the image reference as a URN.
        /// </summary>
        [JsonProperty(PropertyName = "Urn", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        string Urn { get; set; }
    }
}
