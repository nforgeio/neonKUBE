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
        /// <para>
        /// The IP addresses of the DNS nameservers to be used by the cluster.
        /// </para>
        /// <para>
        /// For cloud environments, this defaults the name servers provided by the cloud.  For on-premise
        /// environments, this defaults to the Google Public DNS servers: <b>["8.8.8.8", "8.8.4.4" ]</b>.
        /// </para>
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
        /// <para>
        /// Optionally sets the ingress routing rules external traffic received by nodes
        /// with <see cref="NodeDefinition.Ingress"/> enabled into one or more Istio ingress
        /// gateway services which are then responsible for routing to the target Kubernetes 
        /// services.
        /// </para>
        /// <para>
        /// This defaults to allowing inbound <b>HTTP/HTTPS</b> traffic and cluster setup
        /// also adds a TCP rule for the Kubernetes API server on port <b>6442</b>.
        /// </para>
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
        /// Specifies the ACME options.
        /// </summary>
        [JsonProperty(PropertyName = "Acme", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "acme", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AcmeOptions AcmeOptions { get; set; } = new AcmeOptions();

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var networkOptionsPrefix = $"{nameof(ClusterDefinition.Network)}";
            var isCloud              = clusterDefinition.Hosting.IsCloudProvider;
            var subnets              = new List<SubnetDefinition>();
            var gateway              = (IPAddress)null;
            var premiseSubnet        = (NetworkCidr)null;

            // Nameservers:
            //
            // For cloud environments, we'll going to leave the nameserver list alone and possibly
            // empty, letting the specific cloud hosting manager configure the default cloud nameserver
            // when none are specified.
            //
            // For non-cloud environments, we'll set the Google Public DNS nameservers when none
            // are specified.

            Nameservers ??= new List<string>();

            if (!isCloud && (Nameservers == null || Nameservers.Count == 0))
            {
                Nameservers = new List<string> { "8.8.8.8", "8.8.4.4" };
            }

            foreach (var nameserver in Nameservers)
            {
                if (!NetHelper.TryParseIPv4Address(nameserver, out var address))
                {
                    throw new ClusterDefinitionException($"[{networkOptionsPrefix}.{nameof(ClusterDefinition.Network.Nameservers)}={nameserver}] is not a valid IPv4 address.");
                }
            }

            // Note that we don't need to check the network settings for cloud environments
            // because we'll just use ambient settings in these cases.

            if (!isCloud)
            {
                // Verify [PremiseSubnet].

                if (!NetworkCidr.TryParse(PremiseSubnet, out premiseSubnet))
                {
                    throw new ClusterDefinitionException($"[{networkOptionsPrefix}.{nameof(PremiseSubnet)}={PremiseSubnet}] is not a valid IPv4 subnet.");
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
                    throw new ClusterDefinitionException($"[{networkOptionsPrefix}.{nameof(Gateway)}={Gateway}] is not a valid IPv4 address.");
                }

                if (!premiseSubnet.Contains(gateway))
                {
                    throw new ClusterDefinitionException($"[{networkOptionsPrefix}.{nameof(Gateway)}={Gateway}] address is not within the [{networkOptionsPrefix}.{nameof(NetworkOptions.PremiseSubnet)}={PremiseSubnet}] subnet.");
                }
            }

            // Verify [PodSubnet].

            if (!NetworkCidr.TryParse(PodSubnet, out var podSubnet))
            {
                throw new ClusterDefinitionException($"[{networkOptionsPrefix}.{nameof(PodSubnet)}={PodSubnet}] is not a valid IPv4 subnet.");
            }

            subnets.Add(new SubnetDefinition(nameof(PodSubnet), podSubnet));

            // Verify [ServiceSubnet].

            if (!NetworkCidr.TryParse(ServiceSubnet, out var serviceSubnet))
            {
                throw new ClusterDefinitionException($"[{networkOptionsPrefix}.{nameof(ServiceSubnet)}={ServiceSubnet}] is not a valid IPv4 subnet.");
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
                        throw new ClusterDefinitionException($"[{networkOptionsPrefix}]: Subnet conflict: [{subnet.Name}={subnet.Cidr}] and [{next.Name}={next.Cidr}] overlap.");
                    }
                }
            }

            // Rules for HTTP/HTTPS are required.
            //
            // NOTE:
            // -----
            // We're not going to allow users to specify an ingress rule for the Kubernetes
            // API server here because that mapping is special and needs to be routed only
            // to the control-plane nodes.  We're just going to delete any rule using this port.

            IngressRules ??= new List<IngressRule>();

            if (!IngressRules.Any(rule => rule.Name == "http"))
            {
                IngressRules.Add(
                    new IngressRule()
                    {
                        Name         = "http",
                        Protocol     = IngressProtocol.Tcp,
                        ExternalPort = NetworkPorts.HTTP,
                        NodePort     = KubeNodePort.IstioIngressHttp,
                        TargetPort   = 8080
                    });
            }

            if (!IngressRules.Any(rule => rule.Name == "https"))
            {
                IngressRules.Add(
                    new IngressRule()
                    {
                        Name         = "https",
                        Protocol     = IngressProtocol.Tcp,
                        ExternalPort = NetworkPorts.HTTPS,
                        NodePort     = KubeNodePort.IstioIngressHttps,
                        TargetPort   = 8443
                    });
            }

            var apiServerRules = IngressRules
                .Where(rule => rule.ExternalPort == NetworkPorts.KubernetesApiServer)
                .ToList();

            foreach (var rule in apiServerRules)
            {
                IngressRules.Remove(rule);
            }

            // Ensure that ingress rules are valid and that their names are unique.

            var ingressRuleNames = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var rule in IngressRules)
            {
                rule.Validate(clusterDefinition);

                if (ingressRuleNames.Contains(rule.Name))
                {
                    throw new ClusterDefinitionException($"[{networkOptionsPrefix}]: Ingress Rule Conflict: Multiple rules have the same name: [{rule.Name}]");
                }

                ingressRuleNames.Add(rule.Name);
            }

            // Ensure that external ports are unique.

            var externalPorts = new HashSet<int>();

            foreach (var rule in IngressRules)
            {
                if (externalPorts.Contains(rule.ExternalPort))
                {
                    throw new ClusterDefinitionException($"[{networkOptionsPrefix}]: Ingress Rule Conflict: Multiple rules use the same external port: [{rule.ExternalPort}]");
                }

                externalPorts.Add(rule.ExternalPort);
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
                    throw new ClusterDefinitionException($"[{networkOptionsPrefix}]: The reserved ingress port range of [{ReservedIngressStartPort}...{ReservedIngressEndPort}] cannot include the port [{reservedPort}].");
                }
            }

            AcmeOptions = AcmeOptions ?? new AcmeOptions();
            AcmeOptions.Validate(clusterDefinition);
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
    }
}
