//-----------------------------------------------------------------------------
// FILE:	    AwsNetworkOptions.cs
// CONTRIBUTOR: Jeff Lill
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
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies AWS related network options.
    /// </summary>
    public class AwsNetworkOptions
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AwsNetworkOptions()
        {
        }

        /// <summary>
        /// <para>
        /// Optionally specifies an existing Elastic IP address to be used by the cluster load balancer
        /// for receiving network traffic.  Set this to your Elastic IP allocation ID (something
        /// like <b>eipalloc-080a1efa9c04ad72</b>).  This is useful for ensuring that your cluster
        /// is always provisioned with a known static IP.
        /// </para>
        /// <note>
        /// When this isn't specified, the cluster will be configured with new Elastic IPs that will
        /// be released when the cluster is deleted.
        /// </note>
        /// <note>
        /// <see cref="ElasticIpIngressId"/> and <see cref="ElasticIpEgressId"/> must be specified
        /// together or not at all.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ElasticIpIngressId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "elasticIpIngressId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ElasticIpIngressId { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies an existing Elastic IP address to be used by the cluster load balancer
        /// for sending network traffic.  Set this to your Elastic IP allocation ID (something
        /// like <b>eipalloc-080a1efa9c04ad88</b>).  This is useful for ensuring that your cluster
        /// is always provisioned with a known static IP.
        /// </para>
        /// <note>
        /// When this isn't specified, the cluster will be configured with new Elastic IPs that will
        /// be released when the cluster is deleted.
        /// </note>
        /// <note>
        /// <see cref="ElasticIpIngressId"/> and <see cref="ElasticIpEgressId"/> must be specified
        /// together or not at all.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ElasticIpEgressId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "elasticIpEgressId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ElasticIpEgressId { get; set; } = null;

        /// <summary>
        /// Returns <c>true</c> when the cluster references existing Elastic IP addresses for ingress and egress.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool HasCustomElasticIPs => !string.IsNullOrEmpty(ElasticIpIngressId) && !string.IsNullOrEmpty(ElasticIpEgressId);

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var elasticIpIdRegex = new Regex(@"^eipalloc-[0-9a-f]+$");

            if (!string.IsNullOrEmpty(ElasticIpIngressId))
            {
                if (!elasticIpIdRegex.IsMatch(ElasticIpIngressId))
                {
                    throw new ClusterDefinitionException($"AWS hosting [{nameof(ElasticIpIngressId)}={ElasticIpIngressId}] is not a valid Elastic IP allocation ID.");
                }
            }

            if (!string.IsNullOrEmpty(ElasticIpEgressId))
            {
                if (!elasticIpIdRegex.IsMatch(ElasticIpEgressId))
                {
                    throw new ClusterDefinitionException($"AWS hosting [{nameof(ElasticIpEgressId)}={ElasticIpEgressId}] is not a valid Elastic IP allocation ID.");
                }
            }

            if (!string.IsNullOrEmpty(ElasticIpIngressId) || !string.IsNullOrEmpty(ElasticIpEgressId))
            {
                if (string.IsNullOrEmpty(ElasticIpIngressId))
                {
                    throw new ClusterDefinitionException($"AWS hosting [{nameof(ElasticIpEgressId)}] is also required when [{nameof(ElasticIpIngressId)}] is specified.");
                }

                if (string.IsNullOrEmpty(ElasticIpEgressId))
                {
                    throw new ClusterDefinitionException($"AWS hosting [{nameof(ElasticIpIngressId)}] is also required when [{nameof(ElasticIpEgressId)}] is specified.");
                }

                if (ElasticIpIngressId == ElasticIpEgressId)
                {
                    throw new ClusterDefinitionException($"AWS hosting [{nameof(ElasticIpIngressId)}] and [{nameof(ElasticIpEgressId)}] must be different.");
                }
            }
        }
    }
}
