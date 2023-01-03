//-----------------------------------------------------------------------------
// FILE:	    HTTPFaultInjection.Delay.cs
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
    /// <para>
    /// Delay specification is used to inject latency into the request forwarding path. The following example will introduce a 5
    /// second delay in 1 out of every 1000 requests to the “v1” version of the “reviews” service from all pods with label env: prod
    /// </para>
    /// </summary>
    public class Delay : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPFaultInjection.Delay class.
        /// </summary>
        public Delay()
        {
        }

        /// <summary>
        /// <para>
        /// Add a fixed delay before forwarding the request. Format: 1h/1m/1s/1ms. MUST be >=1ms.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "fixedDelay", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string FixedDelay { get; set; }

        /// <summary>
        /// <para>
        /// Add a fixed delay before forwarding the request. Format: 1h/1m/1s/1ms. MUST be >=1ms.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "percentage", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Percent Percentage { get; set; }

        /// <summary>
        /// <para>
        /// Percentage of requests on which the delay will be injected (0-100). Use of integer percent value is deprecated. Use the double percentage field instead.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "percent", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? Percent { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}
