//-----------------------------------------------------------------------------
// FILE:	    AcmeChallengeSolverDns01.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using Neon.JsonConverters;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.CertManager
{
    /// <summary>
    /// Used to configure a DNS01 challenge provider to be used when solving DNS01 challenges. 
    /// Only one DNS provider may be configured per solver.
    /// </summary>
    public class AcmeChallengeSolverDns01
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AcmeChallengeSolverDns01()
        {
        }

        /// <summary>
        /// CNAMEStrategy configures how the DNS01 provider should handle CNAME records when found in DNS zones.
        /// </summary>
        [JsonProperty(PropertyName = "cnameStrategy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "cnameStrategy", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CnameStrategy { get; set; } = null;

        /// <summary>
        /// Specifies configuration for AWS Route53 DNS01 provider.
        /// </summary>
        [JsonProperty(PropertyName = "route53", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "route53", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AcmeIssuerDns01ProviderRoute53 Route53 { get; set; } = null;

        /// <summary>
        /// Specifies configuration for a webhook DNS01 provider, including where to POST ChallengePayload resources.
        /// </summary>
        [JsonProperty(PropertyName = "webhook", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "webhook", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AcmeIssuerDns01ProviderWebhook Webhook { get; set; } = null;

        /// <inheritdoc/>
        public void Validate()
        {
            if (Route53 != null)
            {
                Route53.Validate();
            }

            if (Webhook != null)
            {
                Webhook.Validate();
            }
        }
    }
}
