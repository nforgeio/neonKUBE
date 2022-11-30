//-----------------------------------------------------------------------------
// FILE:	    AcmeChallengeSolver.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using Neon.JsonConverters;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Resources
{
/// <summary>
/// The kubernetes spec for a cert-manager ClusterIssuer.
/// </summary>
    public class AcmeChallengeSolver
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AcmeChallengeSolver()
        {
        }

        /// <summary>
        /// Selector selects a set of DNSNames on the Certificate resource that should be solved using this challenge solver. If not specified, 
        /// the solver will be treated as the ‘default’ solver with the lowest priority, i.e. if any other solver has a more specific match, 
        /// it will be used instead.
        /// </summary>
        [JsonProperty(PropertyName = "selector", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "selector", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public CertificateDnsNameSelector Selector { get; set; } = null;

        /// <summary>
        /// Email is the email address to be associated with the ACME account. This field is optional, but it is strongly recommended to 
        /// be set. It will be used to contact you in case of issues with your account or certificates, including expiry notification emails. 
        /// This field may be updated after the account is initially registered.
        /// </summary>
        [JsonProperty(PropertyName = "dns01", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "dns01", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AcmeChallengeSolverDns01 Dns01 { get; set; } = null;

        /// <inheritdoc/>
        public void Validate()
        {
        }
    }
}
