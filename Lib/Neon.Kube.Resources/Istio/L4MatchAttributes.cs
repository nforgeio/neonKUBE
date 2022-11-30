//-----------------------------------------------------------------------------
// FILE:	    L4MatchAttributes.cs
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
    /// L4 connection match attributes. Note that L4 connection matching support is incomplete.
    /// </summary>
    public class L4MatchAttributes : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the L4MatchAttributes class.
        /// </summary>
        public L4MatchAttributes()
        {
        }

        /// <summary>
        /// IPv4 or IPv6 ip addresses of destination with optional subnet. E.g., a.b.c.d/xx form or just a.b.c.d.
        /// </summary>
        [JsonProperty(PropertyName = "destinationSubnets", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> DestinationSubnets { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the port on the host that is being addressed. Many services only expose a single port or label ports with the 
        /// protocols they support, in these cases it is not required to explicitly select the port.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "port", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? Port { get; set; }

        /// <summary>
        /// <para>
        /// One or more labels that constrain the applicability of a rule to workloads with the given labels. If the VirtualService has a list of gateways 
        /// specified in the top-level gateways field, it should include the reserved gateway mesh in order for this field to be applicable.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "sourceLabels", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, string> SourceLabels { get; set; }

        /// <summary>
        /// <para>
        /// Names of gateways where the rule should be applied. Gateway names in the top-level gateways field of the VirtualService (if any) 
        /// are overridden. The gateway match is independent of sourceLabels.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "gateways", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Gateways { get; set; }

        /// <summary>
        /// <para>
        /// Source namespace constraining the applicability of a rule to workloads in that namespace. If the VirtualService has a list of gateways 
        /// specified in the top-level gateways field, it must include the reserved gateway mesh for this field to be applicable.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "sourceNamespace", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string SourceNamespace { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}
