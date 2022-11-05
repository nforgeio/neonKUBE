//-----------------------------------------------------------------------------
// FILE:	    Percent.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Percent specifies the number of a port to be used for matching or selection for final routing.
    /// </para>
    /// </summary>
    public class Percent : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the Percent class.
        /// </summary>
        public Percent()
        {
        }

        /// <summary>
        /// A percentage in the range of [0.0, 100.0].
        /// </summary>
        [JsonProperty(PropertyName = "value", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public double? Value { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
            Covenant.Assert(!Value.HasValue || (Value >= 0.0 && Value <= 100.0), "Percent must be in the range of [0.0, 100.0].");
        }
    }
}
