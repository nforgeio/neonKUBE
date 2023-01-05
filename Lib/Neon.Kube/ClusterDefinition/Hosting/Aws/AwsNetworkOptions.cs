//-----------------------------------------------------------------------------
// FILE:	    AwsNetworkOptions.cs
// CONTRIBUTOR: Jeff Lill
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// <para>
    /// Specifies AWS related network options, optionally specifying existing Elastic IP addresses
    /// to use for cluster ingress and egress.
    /// </para>
    /// <para>
    /// By default, clusters will be deployed with newly created addresses.  This means that the cluster
    /// ingress change will change everytime the cluster is redeployed, which means that you may need
    /// to update your DNS zone and also that the IP address for outbound traffic will also change which
    /// may require that you update whitelist rules for other services.
    /// </para>
    /// <para>
    /// You can mitigate this by creating ingress/egress elastic IPs, setting <see cref="ElasticIpIngressId"/>
    /// and <see cref="ElasticIpEgressId"/> to their IDs before deploying your cluster.
    /// </para>
    /// <note>
    /// <see cref="ElasticIpIngressId"/> and <see cref="ElasticIpEgressId"/> must be specified
    /// together or not at all.
    /// </note>
    /// </summary>
    public class AwsNetworkOptions
    {
        private const string defaultVpcSubnet = "10.100.0.0/16";
        private const string defaultPrivateSubnet = "10.100.0.0/24";
        private const string defaultPublicSubnet = "10.100.255.0/24";

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
        /// Specifies the subnet CIDR to used for AWS VPC (virtual private cloud) provisioned
        /// for the cluster.  This must surround the <see cref="NodeSubnet"/> and
        /// <see cref="PublicSubnet"/> subnets.  This defaults to <b>10.100.0.0/16</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VpcSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vpcSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultVpcSubnet)]
        public string VpcSubnet { get; set; } = defaultVpcSubnet;

        /// <summary>
        /// Specifies the private subnet CIDR within <see cref="VpcSubnet"/> for the private subnet
        /// where the cluster node instances will be provisioned.  This defaults to <b>10.100.0.0/24</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "privateSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultPrivateSubnet)]
        public string NodeSubnet { get; set; } = defaultPrivateSubnet;

        /// <summary>
        /// Specifies the public subnet CIDR within <see cref="VpcSubnet"/> for the public subnet where
        /// the AWS network load balancer will be provisioned to manage inbound cluster traffic.
        /// This defaults to <b>10.100.255.0/16</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PublicSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "publicSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultPublicSubnet)]
        public string PublicSubnet { get; set; } = defaultPublicSubnet;

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

            //-----------------------------------------------------------------
            // Network subnets

            VpcSubnet = VpcSubnet ?? defaultVpcSubnet;
            NodeSubnet = NodeSubnet ?? defaultPrivateSubnet;
            PublicSubnet = PublicSubnet ?? defaultPublicSubnet;

            const int minAwsPrefix = 16;
            const int maxAwsPrefix = 28;

            // VpcSubnet

            if (!NetworkCidr.TryParse(VpcSubnet, out var vpcSubnet))
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(VpcSubnet)}={VpcSubnet}] is not a valid subnet.");
            }

            if (vpcSubnet.PrefixLength < minAwsPrefix)
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(VpcSubnet)}={VpcSubnet}] is too large.  The smallest CIDR prefix supported by AWS is [/{minAwsPrefix}].");
            }

            if (vpcSubnet.PrefixLength > maxAwsPrefix)
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(VpcSubnet)}={VpcSubnet}] is too large.  The largest CIDR prefix supported by AWS is [/{maxAwsPrefix}].");
            }

            // PrivateSubnet

            if (!NetworkCidr.TryParse(NodeSubnet, out var privateSubnet))
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(NodeSubnet)}={NodeSubnet}] is not a valid subnet.");
            }

            if (vpcSubnet.PrefixLength < minAwsPrefix)
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(NodeSubnet)}={NodeSubnet}] is too large.  The smallest CIDR prefix supported by AWS is [/{minAwsPrefix}].");
            }

            if (vpcSubnet.PrefixLength > maxAwsPrefix)
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(NodeSubnet)}={NodeSubnet}] is too large.  The largest CIDR prefix supported by AWS is [/{maxAwsPrefix}].");
            }

            // PublicSubnet

            if (!NetworkCidr.TryParse(PublicSubnet, out var publicSubnet))
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(PublicSubnet)}={PublicSubnet}] is not a valid subnet.");
            }

            // Ensure that the subnets fit together.

            if (!vpcSubnet.Contains(privateSubnet))
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(PublicSubnet)}={PublicSubnet}] is not contained within [{nameof(VpcSubnet)}={VpcSubnet}].");
            }

            if (!vpcSubnet.Contains(publicSubnet))
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(NodeSubnet)}={NodeSubnet}] is not contained within [{nameof(VpcSubnet)}={VpcSubnet}].");
            }

            if (privateSubnet.Overlaps(publicSubnet))
            {
                throw new ClusterDefinitionException($"AWS hosting [{nameof(NodeSubnet)}={NodeSubnet}] and [{nameof(PublicSubnet)}={PublicSubnet}] cannot overlap.");
            }
        }
    }
}
