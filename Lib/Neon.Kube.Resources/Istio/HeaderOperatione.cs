//-----------------------------------------------------------------------------
// FILE:	    HeaderOperations.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// HeaderOperations Describes the header manipulations to apply.
    /// </summary>
    public class HeaderOperations : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HeaderOperations class.
        /// </summary>
        public HeaderOperations()
        {
        }

        /// <summary>
        /// Overwrite the headers specified by key with the given values.
        /// </summary>
        [JsonProperty(PropertyName = "set", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, string> Set { get; set; }

        /// <summary>
        /// Append the given values to the headers specified by keys (will create a comma-separated list of values).
        /// </summary>
        [JsonProperty(PropertyName = "add", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, string> Add { get; set; }

        /// <summary>
        /// Remove a the specified headers.
        /// </summary>
        [JsonProperty(PropertyName = "remove", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Remove { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}