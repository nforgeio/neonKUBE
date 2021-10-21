﻿//-----------------------------------------------------------------------------
// FILE:	    HTTPRouteDestination.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// Each routing rule is associated with one or more service versions (see glossary in beginning of document). Weights associated 
    /// with the version determine the proportion of traffic it receives. For example, the following rule will route 25% of traffic for 
    /// the “reviews” service to instances with the “v2” tag and the remaining traffic (i.e., 75%) to “v1”.
    /// </summary>
    public class HTTPRouteDestination : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPRouteDestination class.
        /// </summary>
        public HTTPRouteDestination()
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
        /// The proportion of traffic to be forwarded to the service version. (0-100). Sum of weights across destinations SHOULD BE == 100. 
        /// If there is only one destination in a rule, the weight value is assumed to be 100.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "weight", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? weight { get; set; }

        /// <summary>
        /// <para>
        /// Header manipulation rules
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "headers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Headers Headers { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="Microsoft.Rest.ValidationException">
        /// Thrown if validation fails
        /// </exception>
        public virtual void Validate()
        {
        }
    }
}