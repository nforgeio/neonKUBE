//-----------------------------------------------------------------------------
// FILE:	    NetworkOptions.cs
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
using System.Net;
using System.Net.Sockets;
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
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Describes the network options for a cluster.
    /// </summary>
    public class NetworkOptions
    {
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// Used for checking subnet conflicts below.
        /// </summary>
        private class SubnetDefinition
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="name">Subnet name.</param>
            /// <param name="cidr">Subnet CIDR.</param>
            public SubnetDefinition(string name, NetworkCidr cidr)
            {
                this.Name = $"{nameof(NetworkOptions)}.{name}";
                this.Cidr = cidr;
            }

            /// <summary>
            /// Identifies the subnet.
            /// </summary>
            public string Name { get; set; }

            /// <summary>
            /// The subnet CIDR.
            /// </summary>
            public NetworkCidr Cidr { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string    defaultPodSubnet                = KubeConst.DefaultPodSubnet;
        private const string    defaultServiceSubnet            = KubeConst.DefaultServiceSubnet;
        private const string    defaultCloudNodeSubnet          = "10.100.0.0/16";
        private const int       defaultReservedIngressStartPort = 64000;
        private const int       defaultReservedIngressEndPort   = 64999;
        private const int       additionalReservedPorts         = 100;
        private const string    defaultCertificateDuration      = "504h";
        private const string    defaultCertificateRenewBefore   = "336h";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public NetworkOptions()
        {
        }

        /// <summary>
        /// Specifies the subnet for entire host network for on-premise environments like
        /// <see cref="HostingEnvironment.BareMetal"/>, <see cref="HostingEnvironment.HyperV"/> and
        /// <see cref="HostingEnvironment.XenServer"/>.  This is required for those environments and
        /// ignored for other environments which specify network subnets in their related hosting
        /// options.
        /// </summary>
        [JsonProperty(PropertyName = "PremiseSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "premiseSubnet", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PremiseSubnet { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the pod subnet to be used for the cluster.  This subnet will be
        /// split so that each node will be allocated its own subnet.  This defaults
        /// to <b>10.254.0.0/16</b>.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> This subnet must not conflict with any other subnets
        /// provisioned within the premise network.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "PodSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "podSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultPodSubnet)]
        public string PodSubnet { get; set; } = defaultPodSubnet;

        /// <summary>
        /// Specifies the subnet subnet to be used for the allocating service addresses
        /// within the cluster.  This defaults to <b>10.253.0.0/16</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ServiceSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "serviceSubnet", ApplyNamingConventions = false)]
        [DefaultValue(defaultServiceSubnet)]
        public string ServiceSubnet { get; set; } = defaultServiceSubnet;

        /// <summary>
        /// The IP addresses of the upstream DNS nameservers to be used by the cluster.  This defaults to the 
        /// Google Public DNS servers: <b>[ "8.8.8.8", "8.8.4.4" ]</b> when the property is <c>null</c> or empty.
        /// </summary>
        [JsonProperty(PropertyName = "Nameservers", Required = Required.Default)]
        [YamlMember(Alias = "nameservers", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Nameservers { get; set; } = null;

        /// <summary>
        /// Specifies the default network gateway address to be configured for hosts.  This defaults to the 
        /// first usable address in the <see cref="PremiseSubnet"/>.  For example, for the <b>10.0.0.0/24</b> 
        /// subnet, this will be set to <b>10.0.0.1</b>.  This is ignored for cloud hosting 
        /// environments.
        /// </summary>
        [JsonProperty(PropertyName = "Gateway", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "gateway", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Gateway { get; set; } = null;

        /// <summary>
        /// Optionally enable Istio mutual TLS support for cross pod communication.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "MutualPodTLS", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "mutualPodTLS", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool MutualPodTLS { get; set; } = false;

        /// <summary>
        /// Optionally sets the ingress routing rules external traffic received by nodes
        /// with <see cref="NodeDefinition.Ingress"/> enabled into one or more Istio ingress
        /// gateway services which are then responsible for routing to the target Kubernetes 
        /// services.
        /// </summary>
        [JsonProperty(PropertyName = "IngressRules", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ingressRules", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<IngressRule> IngressRules { get; set; } = new List<IngressRule>();

        /// <summary>
        /// <para>
        /// Optionally specifies the default cluster load balancer health check settings
        /// for the <see cref="IngressRules"/>.  This defaults to reasonable values and
        /// can be overriden for specific rules.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "IngressHealthCheck", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ingressHealthCheck", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HealthCheckOptions IngressHealthCheck { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies whitelisted and/or blacklisted external addresses for
        /// outbound traffic.  This defaults to allowing outbound traffic to anywhere 
        /// when the property is <c>null</c> or empty.
        /// </para>
        /// <note>
        /// Address rules are processed in order from first to last, so you may consider
        /// putting your blacklist rules before your whitelist rules.
        /// </note>
        /// <note>
        /// These rules currently apply to all network ports.
        /// </note>
        /// <note>
        /// This is not currently supported on AWS.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "EgressAddressRules", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "egressAddressRules", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<AddressRule> EgressAddressRules { get; set; } = new List<AddressRule>();

        /// <summary>
        /// <para>
        /// Optionally specifies whitelisted and/or blacklisted external addresses for
        /// node management via SSH NAT rules as well as cluster management via the 
        /// Kubernetes API via port 6443.  This defaults to allowing inbound traffic 
        /// from anywhere when the property is <c>null</c> or empty.
        /// </para>
        /// <note>
        /// Address rules are processed in order from first to last, so you may consider
        /// putting your blacklist rules before your whitelist rules.
        /// </note>
        /// <note>
        /// This is currently supported only for clusters hosted on Azure.  AWS doesn't support
        /// this scenario and we currently don't support automatic router configuration for
        /// on-premise environments.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ManagementAddressRules", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "managementAddressRules", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<AddressRule> ManagementAddressRules { get; set; } = new List<AddressRule>();

        /// <summary>
        /// <para>
        /// Specifies the start of a range of ingress load balancer ports reserved by
        /// neonKUBE.  These are reserved for temporarily exposing SSH from individual 
        /// cluster nodes to the Internet during cluster setup as well as afterwards so 
        /// that a cluster node can be accessed remotely by a cluster operator as well
        /// as for other purposes and for potential future features such as an integrated
        /// VPN.
        /// </para>
        /// <note>
        /// The number ports between <see cref="ReservedIngressStartPort"/> and <see cref="ReservedIngressEndPort"/>
        /// must include at least as many ports as there will be nodes deployed to the cluster
        /// for the temporary SSH NAT rules plus another 100 ports reserved for other purposes.
        /// This range defaults to <b>64000-64999</b> which will support a cluster with up to
        /// 900 nodes.  This default range is unlikely to conflict with ports a cluster is likely
        /// to need expose to the Internet like HTTP/HTTPS (80/443).  You can change this range 
        /// for your cluster to resolve any conflicts when necessary.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ReservedIngressStartPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "reservedIngressStartPort", ApplyNamingConventions = false)]
        [DefaultValue(defaultReservedIngressStartPort)]
        public int ReservedIngressStartPort { get; set; } = defaultReservedIngressStartPort;

        /// <summary>
        /// <para>
        /// Specifies the end of a range of ingress load balancer ports reserved by
        /// neonKUBE.  These are reserved for temporarily exposing SSH from individual 
        /// cluster nodes to the Internet during cluster setup as well as afterwards so 
        /// that a cluster node can be accessed remotely by a cluster operator as well
        /// as for other purposes and for potential future features such as an integrated
        /// </para>
        /// <note>
        /// The number ports between <see cref="ReservedIngressStartPort"/> and <see cref="ReservedIngressEndPort"/>
        /// must include at least as many ports as there will be nodes deployed to the cluster
        /// for the temporary SSH NAT rules plus another 100 ports reserved for other purposes.
        /// This range defaults to <b>64000-64999</b> which will support a cluster with up to
        /// 900 nodes.  This default range is unlikely to conflict with ports a cluster is likely
        /// to need expose to the Internet like HTTP/HTTPS (80/443).  You can change this range 
        /// for your cluster to resolve any conflicts when necessary.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ReservedIngressEndPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "reservedIngressEndPort", ApplyNamingConventions = false)]
        [DefaultValue(defaultReservedIngressEndPort)]
        public int ReservedIngressEndPort { get; set; } = defaultReservedIngressEndPort;

        /// <summary>
        /// Returns the port number for the reserved management SSH ingress NAT rule.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal int FirstExternalSshPort => ReservedIngressStartPort + additionalReservedPorts;

        /// <summary>
        /// Returns the last possible external SSH port.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal int LastExternalSshPort => ReservedIngressEndPort;

        /// <summary>
        /// Specifies the maximum lifespan for internal cluster TLS certificates as a GOLANG formatted string.  
        /// This defaults to <b>504h</b> (21 days).  See <see cref="GoDuration.Parse(string)"/> for details 
        /// about the timespan format.
        /// </summary>
        [JsonProperty(PropertyName = "CertificateDuration", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "certificateDuration", ApplyNamingConventions = false)]
        [DefaultValue(defaultCertificateDuration)]
        public string CertificateDuration { get; set; } = defaultCertificateDuration;

        /// <summary>
        /// Specifies the time to wait before attempting to renew for internal cluster TLS certificates.
        /// This must be less than <see cref="CertificateDuration"/> and defaults to <b>336h</b> (14 days).
        /// See <see cref="GoDuration.Parse(string)"/> for details about the timespan format.
        /// </summary>
        [JsonProperty(PropertyName = "CertificateRenewBefore", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "certificateRenewBefore", ApplyNamingConventions = false)]
        [DefaultValue(defaultCertificateRenewBefore)]
        public string CertificateRenewBefore { get; set; } = defaultCertificateRenewBefore;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var isCloud       = clusterDefinition.Hosting.IsCloudProvider;
            var subnets       = new List<SubnetDefinition>();
            var gateway       = (IPAddress)null;
            var premiseSubnet = (NetworkCidr)null;

            // Nameservers

            Nameservers = Nameservers ?? new List<string>();

            if (!isCloud && (Nameservers == null || Nameservers.Count == 0))
            {
                Nameservers = new List<string> { "8.8.8.8", "8.8.4.4" };
            }

            foreach (var nameserver in Nameservers)
            {
                if (!NetHelper.TryParseIPv4Address(nameserver, out var address))
                {
                    throw new ClusterDefinitionException($"[{nameserver}] is not a valid [{nameof(NetworkOptions)}.{nameof(Nameservers)}] IP address.");
                }
            }

            // Note that we don't need to check the network settings for cloud environments or
            // WSL2 deployments because we'll just use ambient settings in these cases.

            if (!isCloud && clusterDefinition.Hosting.Environment != HostingEnvironment.Wsl2)
            {
                // Verify [PremiseSubnet].

                if (!NetworkCidr.TryParse(PremiseSubnet, out premiseSubnet))
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(PremiseSubnet)}={PremiseSubnet}] is not a valid IPv4 subnet.");
                }

                // Verify [Gateway]

                if (string.IsNullOrEmpty(Gateway))
                {
                    // Default to the first valid address of the cluster nodes subnet 
                    // if this isn't already set.

                    Gateway = premiseSubnet.FirstUsableAddress.ToString();
                }

                if (!NetHelper.TryParseIPv4Address(Gateway, out gateway) || gateway.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(Gateway)}={Gateway}] is not a valid IPv4 address.");
                }

                if (!premiseSubnet.Contains(gateway))
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(Gateway)}={Gateway}] address is not within the [{nameof(NetworkOptions)}.{nameof(NetworkOptions.PremiseSubnet)}={PremiseSubnet}] subnet.");
                }
            }

            // Verify [PodSubnet].

            if (!NetworkCidr.TryParse(PodSubnet, out var podSubnet))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(PodSubnet)}={PodSubnet}] is not a valid IPv4 subnet.");
            }

            subnets.Add(new SubnetDefinition(nameof(PodSubnet), podSubnet));

            // Verify [ServiceSubnet].

            if (!NetworkCidr.TryParse(ServiceSubnet, out var serviceSubnet))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(ServiceSubnet)}={ServiceSubnet}] is not a valid IPv4 subnet.");
            }

            subnets.Add(new SubnetDefinition(nameof(ServiceSubnet), serviceSubnet));

            // Verify that none of the major subnets conflict.

            foreach (var subnet in subnets)
            {
                foreach (var next in subnets)
                {
                    if (subnet == next)
                    {
                        continue;   // Don't test against self.
                    }

                    if (subnet.Cidr.Overlaps(next.Cidr))
                    {
                        throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}]: Subnet conflict: [{subnet.Name}={subnet.Cidr}] and [{next.Name}={next.Cidr}] overlap.");
                    }
                }
            }

            // Verify [IngressRules] and also ensure that all rule names are unique.

            if (IngressRules == null || IngressRules?.Count == 0)
            {
                IngressRules = new List<IngressRule>()
                {
                    new IngressRule()
                    {
                        Name         = "http2",
                        Protocol     = IngressProtocol.Tcp,
                        ExternalPort = 80,
                        TargetPort   = 8080,
                        NodePort     = KubeNodePorts.IstioIngressHttp
                    },
                    new IngressRule()
                    {
                        Name         = "https",
                        Protocol     = IngressProtocol.Tcp,
                        ExternalPort = 443,
                        TargetPort   = 8443,
                        NodePort     = KubeNodePorts.IstioIngressHttps
                    }
                };
            }

            var ingressRuleNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var rule in IngressRules)
            {
                rule.Validate(clusterDefinition);

                if (ingressRuleNames.Contains(rule.Name))
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}]: Ingress Rule Conflict: Multiple rules have the same name: [{rule.Name}].");
                }

                ingressRuleNames.Add(rule.Name);
            }

            // Verify [EgressAddressRules].

            EgressAddressRules = EgressAddressRules ?? new List<AddressRule>();

            foreach (var rule in EgressAddressRules)
            {
                rule.Validate(clusterDefinition, nameof(EgressAddressRules));
            }

            // Verify [SshAddressRules].

            ManagementAddressRules = ManagementAddressRules ?? new List<AddressRule>();

            foreach (var rule in ManagementAddressRules)
            {
                rule.Validate(clusterDefinition, nameof(ManagementAddressRules));
            }

            // Verify that the [ReservedIngressStartPort...ReservedIngressEndPort] range doesn't 
            // include common reserved ports.

            var reservedPorts = new int[]
            {
                NetworkPorts.HTTP,
                NetworkPorts.HTTPS,
                NetworkPorts.KubernetesApiServer
            };

            foreach (int reservedPort in reservedPorts)
            {
                if (ReservedIngressStartPort <= reservedPort && reservedPort <= ReservedIngressEndPort)
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}]: The reserved ingress port range of [{ReservedIngressStartPort}...{ReservedIngressEndPort}] cannot include the port [{reservedPort}].");
                }
            }

            // Validate the certificate durations.

            CertificateDuration    ??= defaultCertificateDuration;
            CertificateRenewBefore ??= defaultCertificateRenewBefore;

            if (!GoDuration.TryParse(CertificateDuration, out var duration))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(CertificateDuration)}={CertificateDuration}] cannot be parsed as a GOLANG duration.");
            }

            if (!GoDuration.TryParse(CertificateRenewBefore, out var renewBefore))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(CertificateRenewBefore)}={CertificateRenewBefore}] cannot be parsed as a GOLANG duration.");
            }

            if (duration.TimeSpan < TimeSpan.FromSeconds(1))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(CertificateDuration)}={CertificateDuration}] cannot be less than 1 second.");
            }

            if (renewBefore.TimeSpan < TimeSpan.FromSeconds(1))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(CertificateRenewBefore)}={CertificateRenewBefore}] cannot be less than 1 second.");
            }

            if (duration.TimeSpan < renewBefore.TimeSpan)
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(CertificateDuration)}={CertificateDuration}] is not greater than or equal to [{nameof(NetworkOptions)}.{nameof(CertificateRenewBefore)}={CertificateRenewBefore}].");
            }
        }

        /// <summary>
        /// Determines whether a port is within the external SSH network port range.
        /// </summary>
        /// <param name="port">The port being tested.</param>
        /// <returns><c>true</c> for external SSH ports.</returns>
        internal bool IsExternalSshPort(int port)
        {
            return FirstExternalSshPort <= port && port <= LastExternalSshPort;
        }

        /// <summary>
        /// Ensures that for cloud deployments, an explicit node address assignment does not conflict 
        /// with any VNET addresses reserved by the cloud provider or neonKUBE.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="nodeDefinition">The node definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown for cloud deployments where the node specifies an explicit IP address that conflicts with a reserved VNET address.</exception>
        internal void ValidateCloudNodeAddress(ClusterDefinition clusterDefinition, NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));

            if (!NetHelper.IsValidPort(ReservedIngressStartPort))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(ReservedIngressStartPort)}={ReservedIngressStartPort}] port.");
            }

            if (!NetHelper.IsValidPort(ReservedIngressEndPort))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(ReservedIngressEndPort)}={ReservedIngressEndPort}] port.");
            }

            if (ReservedIngressStartPort >= ReservedIngressEndPort)
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(ReservedIngressStartPort)}={ReservedIngressStartPort}]-[{nameof(ReservedIngressEndPort)}={ReservedIngressEndPort}] range.  [{nameof(ReservedIngressStartPort)}] must be greater than [{nameof(ReservedIngressEndPort)}].");
            }

            if (ReservedIngressEndPort - ReservedIngressStartPort + additionalReservedPorts < clusterDefinition.Nodes.Count())
            {
                throw new ClusterDefinitionException($"[{nameof(ReservedIngressStartPort)}]-[{nameof(ReservedIngressEndPort)}] range is not large enough to support [{clusterDefinition.Nodes.Count()}] cluster nodes in addition to [{additionalReservedPorts}] additional reserved ports.");
            }

            IngressHealthCheck?.Validate(clusterDefinition, nameof(NetworkOptions));
        }
    }
}
