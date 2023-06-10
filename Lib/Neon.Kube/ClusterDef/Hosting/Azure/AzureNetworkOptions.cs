//-----------------------------------------------------------------------------
// FILE:	    AzureNetworkOptions.cs
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
    /// Specifies Azure related cluster networking options.
    /// </para>
    /// </summary>
    /// <remarks>
    /// <para>
    /// These options are used to customize the cluster's ingress IP address as well as
    /// the NAT gateway used to route traffic from the cluster to the Internet.  By default,
    /// clusters will create two public IP addresses, one attached to the load balancer
    /// for inbound traffic and the other to the NAT gateway for outbound traffic, with
    /// each address belonging to the cluster's resource group.
    /// </para>
    /// <para>
    /// This works well for many clusters, but one downside is that these addresses
    /// will be deleted when the cluster is removed (which removes everything in the
    /// resource group).  This means that if or when the cluster is redeployed, new
    /// public addresses will be created, potentially requiring that DNS records and
    /// address whitelists may need to be updated as well.
    /// </para>
    /// <para>
    /// To avoid this, you may create public IP addresses before deploying your cluster
    /// and then setting <see cref="IngressPublicIpAddressId"/> and/or <see cref="EgressPublicIpPrefixId"/>
    /// to the IDs of the addresses you created and the cluster will be deployed using 
    /// these addresses instead.  Since these addresses are not in the resource group,
    /// they won't be deleted when the cluster is removed, so you'll be able to reuse them
    /// later.
    /// </para>
    /// <para><b>SNAT Exhaustion</b></para>
    /// <para>
    /// Outbound traffic from the cluster routes through the cluster SNAT Gateway which is
    /// assigned a single public IP address by default.  This configuration allows the
    /// cluster to establish about 64K outbound connections at any given moment.  This
    /// will be sufficient for many clusters but larger or particularly chatty clusters
    /// may exceed this limit.  Unfortunately, diagnosing this when it happens can be 
    /// difficult because applications just see this as random socket connection timeouts.
    /// This is known as <a href="https://docs.microsoft.com/en-us/azure/load-balancer/load-balancer-outbound-connections#outboundrules">SNAT Exhaustion</a>.
    /// </para>
    /// <para>
    /// The way to mitigate this is to add additional public IP addresses to the NAT Gateway
    /// because each address added can support about 64K connections.  NEONKUBE clusters can
    /// do this by adding a <b>public IP prefix</b> to the NAT Gateway.  These represent
    /// between multiple IP addresses that are adjacent to each other in the address space.
    /// </para>
    /// <para>
    /// Public IP address prefixes are specified by the number of mask bits in an IPv4
    /// CIDR.  This can be customized by setting <see cref="EgressPublicIpPrefixLength"/> 
    /// to one of these values:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>31</b></term>
    ///     <description>
    ///     Creates a public IPv4 prefix with <b>2</b> public IP addresses.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>30</b></term>
    ///     <description>
    ///     Creates a public IPv4 prefix with <b>4</b> public IP addresses.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>29</b></term>
    ///     <description>
    ///     Creates a public IPv4 prefix with <b>8</b> public IP addresses.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>28</b></term>
    ///     <description>
    ///     Creates a public IPv4 prefix with <b>16</b> public IP addresses (the maximum supported by Azure).
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>0</b></term>
    ///     <description>
    ///     Disables prefix creation for the cluster.
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// Larger clusters may need to select a prefix with additional IP addresses to avoid
    /// <a href="https://docs.microsoft.com/en-us/azure/load-balancer/load-balancer-outbound-connections#outboundrules">SNAT Exhaustion</a>.
    /// </para>
    /// <para>
    /// You may also create a public IP prefix before deploying your cluster and setting
    /// <see cref="EgressPublicIpPrefixId"/> to its ID.
    /// </para>
    /// </remarks>
    public class AzureNetworkOptions
    {
        /// <summary>
        /// Minimum Azure supported NAT Gateway TCP reset idle timeout in minutes.
        /// </summary>
        private const int minAzureNatGatewayTcpIdleTimeoutMinutes = 4;

        /// <summary>
        /// Maximum Azure supported NAT Gateway TCP reset idle timeout in minutes.
        /// </summary>
        private const int maxAzureNatGatewayTcpIdleTimeoutMinutes = 120;

        private const string defaultVnetSubnet = "10.100.0.0/24";
        private const string defaultNodeSubnet = "10.100.0.0/24";

        /// <summary>
        /// Constructor.
        /// </summary>
        public AzureNetworkOptions()
        {
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the ID of an existing public IPv4 address to be assigned
        /// to the load balancer to receive inbound network traffic.  A new address will
        /// be created when this isn't specified.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> This resource must be located in the same region as the cluster.
        /// </note>
        /// <note>
        /// Setting this is handy when clusters are reprovisioned because the cluster will 
        /// end up with the same public address as before, meaning you won't have to update your
        /// DNS configuration, etc.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "IngressPublicIpAddressId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ingressPublicIpAddressId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string IngressPublicIpAddressId { get; set; }

        /// <summary>
        /// <para>
        /// Optionally specifies the ID of an existing public IPv4 address to be assigned
        /// to the NAT Gateway to send outboung network traffic.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> This resource must be located in the same region as the cluster.
        /// </note>
        /// <note>
        /// Setting this is handy when clusters are reprovisioned because the cluster will 
        /// end up using the same egress address as before, meaning you won't have to update
        /// whitelist rules for other services, etc.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "EgressPublicIpAddressId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "egressPublicIpAddressId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string EgressPublicIpAddressId { get; set; }

        /// <summary>
        /// <para>
        /// Optionally specifies the ID of an existing public IPv4 prefix to be assigned
        /// to the NAT Gateway to send outboung network traffic.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> This resource must be located in the same region as the cluster.
        /// </note>
        /// <note>
        /// Setting this is handy when clusters are reprovisioned because the cluster will 
        /// end up using the same egress addresses as before, meaning you won't have to update
        /// whitelist rules for other services, etc.
        /// </note>
        /// <note>
        /// Azure clusters support a maximum of 16 IP addresses per prefix.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "EgressPublicIpPrefixId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "egressPublicIpPrefixId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string EgressPublicIpPrefixId { get; set; }

        /// <summary>
        /// <para>
        /// Optionally indicates that a public IPv4 prefix with the specified prefix length should
        /// be created and assigned to the NAT Gateway for outbound traffic.  Set this to a one of
        /// the following non-zero values to enable this:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>31</b></term>
        ///     <description>
        ///     Creates a public IPv4 prefix with <b>2</b> public IP addresses.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>30</b></term>
        ///     <description>
        ///     Creates a public IPv4 prefix with <b>4</b> public IP addresses.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>29</b></term>
        ///     <description>
        ///     Creates a public IPv4 prefix with <b>8</b> public IP addresses.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>28</b></term>
        ///     <description>
        ///     Creates a public IPv4 prefix with <b>16</b> public IP addresses (the maximum supported by Azure).
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>0</b></term>
        ///     <description>
        ///     Disables prefix creation for the cluster.
        ///     </description>
        /// </item>
        /// </list>
        /// <para>
        /// Larger clusters may need to select a prefix with additional IP addresses to avoid
        /// <a href="https://docs.microsoft.com/en-us/azure/load-balancer/load-balancer-outbound-connections#outboundrules">SNAT Exhaustion</a>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "EgressPublicIpPrefixLength", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "egressPublicIpPrefixLength", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int EgressPublicIpPrefixLength { get; set; } = 0;

        /// <summary>
        /// Optionally specifies the maximum time in minutes that the cluster's NAT gateway will
        /// retain an idle outbound TCP connection.  This may be set to between [4..120] minutes
        /// inclusive.  This defaults to <b>120 minutes</b>.
        /// </summary>
        [JsonProperty(PropertyName = "MaxNatGatewayTcpIdle", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "maxNatGatewayTcpIdle", ApplyNamingConventions = false)]
        [DefaultValue(maxAzureNatGatewayTcpIdleTimeoutMinutes)]
        public int MaxNatGatewayTcpIdle { get; set; } = maxAzureNatGatewayTcpIdleTimeoutMinutes;

        /// <summary>
        /// Specifies the subnet for the Azure VNET.  This defaults to <b>10.100.0.0/24</b>
        /// </summary>
        [JsonProperty(PropertyName = "VnetSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vnetSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultVnetSubnet)]
        public string VnetSubnet { get; set; } = defaultVnetSubnet;

        /// <summary>
        /// specifies the subnet within <see cref="VnetSubnet"/> where the cluster nodes will be provisioned.
        /// This defaults to <b>10.100.0.0/24</b>.
        /// </summary>
        [JsonProperty(PropertyName = "NodeSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodeSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultNodeSubnet)]
        public string NodeSubnet { get; set; } = defaultNodeSubnet;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var optionsPropertyPath = $"{nameof(clusterDefinition.Hosting)}.{nameof(clusterDefinition.Hosting.Azure)}.{nameof(clusterDefinition.Hosting.Azure.Network)}";

            // Verify egress related properties.

            var propertyCount = 0;

            if (!string.IsNullOrEmpty(EgressPublicIpAddressId))
            {
                propertyCount++;
            }

            if (!string.IsNullOrEmpty(EgressPublicIpPrefixId))
            {
                propertyCount++;
            }

            if (EgressPublicIpPrefixLength > 0)
            {
                propertyCount++;
            }

            if (propertyCount > 1)
            {
                throw new ClusterDefinitionException($"Only one of [{optionsPropertyPath}.{nameof(EgressPublicIpAddressId)}, {nameof(EgressPublicIpPrefixId)}, or {nameof(EgressPublicIpPrefixLength)} can be specified.");
            }

            if (EgressPublicIpPrefixLength != 0 && (EgressPublicIpPrefixLength < 28 || EgressPublicIpPrefixLength > 31))
            {
                throw new ClusterDefinitionException($" [{optionsPropertyPath}.{nameof(EgressPublicIpPrefixLength)}={EgressPublicIpPrefixLength}] is invalid.  Supported values: 0, 28, 29, 30, or 31");
            }

            // Verify MaxNatGatewayTcpIdle.

            var idlePropertyPath = $"{optionsPropertyPath}.{nameof(AzureNetworkOptions.MaxNatGatewayTcpIdle)})";

            if (MaxNatGatewayTcpIdle < minAzureNatGatewayTcpIdleTimeoutMinutes)
            {
                throw new ClusterDefinitionException($"[{idlePropertyPath}={MaxNatGatewayTcpIdle}]: Cannot be less that the minimum [{minAzureNatGatewayTcpIdleTimeoutMinutes}].");
            }

            if (MaxNatGatewayTcpIdle > maxAzureNatGatewayTcpIdleTimeoutMinutes)
            {
                throw new ClusterDefinitionException($"[{idlePropertyPath}={MaxNatGatewayTcpIdle}]: Cannot be less that the maximum [{maxAzureNatGatewayTcpIdleTimeoutMinutes}].");
            }

            // Verify subnets

            if (!NetworkCidr.TryParse(VnetSubnet, out var vnetSubnet))
            {
                throw new ClusterDefinitionException($"[{optionsPropertyPath}.{nameof(VnetSubnet)}={VnetSubnet}] is not a valid subnet.");
            }

            if (!NetworkCidr.TryParse(NodeSubnet, out var nodeSubnet))
            {
                throw new ClusterDefinitionException($"[{optionsPropertyPath}.{nameof(NodeSubnet)}={NodeSubnet}] is not a valid subnet.");
            }

            if (!vnetSubnet.Contains(nodeSubnet))
            {
                throw new ClusterDefinitionException($"[{optionsPropertyPath}.{nameof(NodeSubnet)}={NodeSubnet}] is contained within [{nameof(VnetSubnet)}={VnetSubnet}].");
            }
        }
    }
}
