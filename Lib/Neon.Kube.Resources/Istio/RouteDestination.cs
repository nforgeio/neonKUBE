//-----------------------------------------------------------------------------
// FILE:        RouteDestination.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
    /// Specifies a weighted L4 routing rule destination.
    /// </summary>
    public class RouteDestination : IValidate
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public RouteDestination()
        {
        }

        /// <summary>
        /// Destination uniquely identifies the instances of a service to which the request/connection should be forwarded to.
        /// </summary>
        [JsonProperty(PropertyName = "destination", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Destination Destination { get; set; }

        /// <summary>
        /// <para>
        /// The proportion of traffic to be forwarded to the service version. If there is only one destination in a rule, all traffic 
        /// will be routed to it irrespective of the weight.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "weight", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? weight { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}
