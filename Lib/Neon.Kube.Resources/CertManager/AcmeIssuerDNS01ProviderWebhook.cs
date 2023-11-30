//-----------------------------------------------------------------------------
// FILE:        AcmeIssuerDns01ProviderWebhook.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
    /// Defines the Route 53 configuration for AWS.
    /// </summary>
    public class AcmeIssuerDns01ProviderWebhook
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AcmeIssuerDns01ProviderWebhook()
        {
        }

        /// <summary>
        /// The API group name that should be used when POSTing ChallengePayload resources to the webhook apiserver. This should be 
        /// the same as the GroupName specified in the webhook provider implementation.
        /// </summary>
        [JsonProperty(PropertyName = "groupName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "groupName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string GroupName { get; set; } = null;

        /// <summary>
        /// The name of the solver to use, as defined in the webhook provider implementation. This will typically be the name of the 
        /// provider, e.g. ‘neon-acme’.
        /// </summary>
        [JsonProperty(PropertyName = "solverName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "solverName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SolverName { get; set; } = null;

        /// <summary>
        /// Additional configuration that should be passed to the webhook apiserver when challenges are processed. This can contain arbitrary
        /// JSON data. Secret values should not be specified in this stanza. If secret values are needed (e.g. credentials for a DNS service), 
        /// you should use a SecretKeySelector to reference a Secret resource. For details on the schema of this field, consult the webhook
        /// provider implementation’s documentation.
        /// </summary>
        [JsonProperty(PropertyName = "config", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "config", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, object> Config { get; set; } = null;

        /// <summary>
        /// Validates the properties.
        /// </summary>
        public void Validate()
        {
        }
    }
}
