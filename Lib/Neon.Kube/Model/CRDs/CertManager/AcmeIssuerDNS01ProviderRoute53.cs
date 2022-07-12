//-----------------------------------------------------------------------------
// FILE:	    AcmeIssuerDns01ProviderRoute53.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Text.Json.Serialization;
using Neon.JsonConverters;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Defines the Route 53 configuration for AWS.
    /// </summary>
    public class AcmeIssuerDns01ProviderRoute53
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AcmeIssuerDns01ProviderRoute53()
        {
        }

        /// <summary>
        /// The AccessKeyID is used for authentication. If not set we fall-back to using env vars, shared credentials file or AWS Instance 
        /// metadata see: https://docs.aws.amazon.com/sdk-for-go/v1/developer-guide/configuring-sdk.html#specifying-credentials
        /// </summary>
        [JsonProperty(PropertyName = "accessKeyID", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "accessKeyID", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string AccessKeyId { get; set; } = null;

        /// <summary>
        /// The SecretAccessKey is used for authentication. If not set we fall-back to using env vars, shared credentials file or AWS Instance 
        /// metadata https://docs.aws.amazon.com/sdk-for-go/v1/developer-guide/configuring-sdk.html#specifying-credentials
        /// </summary>
        [JsonProperty(PropertyName = "secretAccessKeySecretRef", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AcmeSecretKeySelector SecretAccessKeySecretRef  { get; set; } = null;

        /// <summary>
        /// The SecretAccessKey is used for authentication. If not set we fall-back to using env vars, shared credentials file or AWS Instance 
        /// metadata https://docs.aws.amazon.com/sdk-for-go/v1/developer-guide/configuring-sdk.html#specifying-credentials
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonProperty(PropertyName = "secretAccessKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "secretAccessKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SecretAccessKey { get; set; } = null;

        /// <summary>
        /// Role is a Role ARN which the Route53 provider will assume using either the explicit credentials AccessKeyID/SecretAccessKey 
        /// or the inferred credentials from environment variables, shared credentials file or AWS Instance metadata
        /// </summary>
        [JsonProperty(PropertyName = "role", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "role", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Role { get; set; } = null;

        /// <summary>
        /// If set, the provider will manage only this zone in Route53 and will not do an lookup using the route53:ListHostedZonesByName api call.
        /// </summary>
        [JsonProperty(PropertyName = "hostedZoneID", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hostedZoneID", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HostedZoneId { get; set; } = null;

        /// <summary>
        /// Always set the region when using AccessKeyID and SecretAccessKey
        /// </summary>
        [JsonProperty(PropertyName = "region", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "region", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Region { get; set; } = null;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            SecretAccessKeySecretRef = SecretAccessKeySecretRef ?? new AcmeSecretKeySelector()
            {
                Key  = "secret",
                Name = "neon-acme-secret-route53"
            };
        }
    }
}
