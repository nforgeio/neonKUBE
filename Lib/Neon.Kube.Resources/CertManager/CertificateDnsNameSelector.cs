//-----------------------------------------------------------------------------
// FILE:	    CertificateDnsNameSelector.cs
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
using System.Linq;
using System.Text;

using Neon.JsonConverters;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.CertManager
{
/// <summary>
/// The kubernetes spec for a cert-manager ClusterIssuer.
/// </summary>
    public class CertificateDnsNameSelector
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public CertificateDnsNameSelector()
        {
        }

        /// <summary>
        /// A label selector that is used to refine the set of certificate’s that this challenge solver will apply to.
        /// </summary>
        [JsonProperty(PropertyName = "matchLabels", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "matchLabels", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public V1LabelSelector MatchLabels { get; set; } = null;

        /// <summary>
        /// List of DNSNames that this solver will be used to solve. If specified and a match is found, a dnsNames selector will take precedence 
        /// over a dnsZones selector. If multiple solvers match with the same dnsNames value, the solver with the most matching labels in 
        /// matchLabels will be selected. If neither has more matches, the solver defined earlier in the list will be selected.
        /// </summary>
        [JsonProperty(PropertyName = "dnsNames", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "dnsNames", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> DnsNames { get; set; } = null;

        /// <summary>
        /// List of DNSZones that this solver will be used to solve. The most specific DNS zone match specified here will take precedence over 
        /// other DNS zone matches, so a solver specifying sys.example.com will be selected over one specifying example.com for the domain 
        /// www.sys.example.com. If multiple solvers match with the same dnsZones value, the solver with the most matching labels in matchLabels 
        /// will be selected. If neither has more matches, the solver defined earlier in the list will be selected.
        /// </summary>
        [JsonProperty(PropertyName = "dnsZones", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "dnsZones", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> DnsZones { get; set; } = null;
    }
}
