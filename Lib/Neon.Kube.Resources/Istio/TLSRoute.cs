//-----------------------------------------------------------------------------
// FILE:	    TLSRoute.cs
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

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Describes match conditions and actions for routing unterminated TLS traffic (TLS/HTTPS) The following routing rule forwards unterminated 
    /// TLS traffic arriving at port 443 of gateway called “mygateway” to internal services in the mesh based on the SNI value.
    /// </summary>
    public class TLSRoute : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the TLSRoute class.
        /// </summary>
        public TLSRoute()
        {
        }

        /// <summary>
        /// Match conditions to be satisfied for the rule to be activated. All conditions inside a single match block have AND semantics, 
        /// while the list of match blocks have OR semantics. The rule is matched if any one of the match blocks succeed.
        /// </summary>
        [JsonProperty(PropertyName = "match", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<TLSMatchAttributes> Match { get; set; }

        /// <summary>
        /// The protocol exposed on the TLSRoute.
        /// </summary>
        [JsonProperty(PropertyName = "route", Required = Required.Always)]
        public List<RouteDestination> Route { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}
