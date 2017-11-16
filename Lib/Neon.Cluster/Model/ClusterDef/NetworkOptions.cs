//-----------------------------------------------------------------------------
// FILE:	    NetworkOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the network options for a neonCLUSTER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonCLUSTERs are provisioned with two standard overlay networks: <b>neon-public</b> and <b>neon-private</b>.
    /// </para>
    /// <para>
    /// <b>neon-public</b> is configured by default on the <b>10.249.0.0/16</b> subnet and is intended to
    /// host public facing service endpoints to be served by the <b>neon-proxy-public</b> proxy service.
    /// </para>
    /// <para>
    /// <b>neon-private</b> is configured by default on the <b>10.248.0.0/16</b> subnet and is intended to
    /// host internal service endpoints to be served by the <b>neon-proxy-private</b> proxy service.
    /// </para>
    /// </remarks>
    public class NetworkOptions
    {
        private const string defaultPublicSubnet    = "10.249.0.0/16";
        private const string defaultPrivateSubnet   = "10.248.0.0/16";
        private const string defaultCloudSubnet     = "10.168.0.0/21";
        private const string defaulVpnReturnSubnet  = "10.169.0.0/22";


        // WARNING: [pdns-server] and its [pdns-remote-backend] packages must come from the same build.

        private const string defaultPdnsServerPackageUri          = "https://jefflill.github.io/neoncluster/binaries/ubuntu/pdns-server_4.1.0~rc1-1pdns.xenial_amd64.deb";
        private const string defaultPdnsBackendRemotePackageUri   = "https://jefflill.github.io/neoncluster/binaries/ubuntu/pdns-backend-remote_4.1.0~rc1-1pdns.xenial_amd64.deb";
        private const string defaultPdnsRecursorPackagePackageUri = "https://jefflill.github.io/neoncluster/binaries/ubuntu/pdns-recursor_4.1.0~alpha1-1pdns.xenial_amd64.deb";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public NetworkOptions()
        {
        }

        /// <summary>
        /// The subnet to be assigned to the built-in <b>neon-public</b> overlay network.  This defaults to <b>10.249.0.0/16</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PublicSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultPublicSubnet)]
        public string PublicSubnet { get; set; } = defaultPublicSubnet;

        /// <summary>
        /// Allow non-Docker swarm mode service containers to attach to the built-in <b>neon-public</b> cluster 
        /// overlay network.  This defaults to <b>true</b> for flexibility but you may consider disabling this for
        /// better security.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The advantage of enabling is is that any container will be able to connect to the default network
        /// and access swarm mode services.  The downside is that this makes it possible for a bad guy who
        /// gains root access to a single node could potentially deploy a malicious container that could also
        /// join the network.  With this disabled, the bad guy would need to gain access to one of the manager
        /// nodes to deploy a malicious service.
        /// </para>
        /// <para>
        /// Unforunately, it's not currently possible to change this setting after a cluster is deployed.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PublicAttachable", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool PublicAttachable { get; set; } = true;

        /// <summary>
        /// The subnet to be assigned to the built-in <b>neon-public</b> overlay network.  This defaults to <b>10.248.0.0/16</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultPrivateSubnet)]
        public string PrivateSubnet { get; set; } = defaultPrivateSubnet;

        /// <summary>
        /// Allow non-Docker swarm mode service containers to attach to the built-in <b>neon-private</b> cluster 
        /// overlay network.  This defaults to <b>true</b> for flexibility but you may consider disabling this for
        /// better security.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The advantage of enabling is is that any container will be able to connect to the default network
        /// and access swarm mode services.  The downside is that this makes it possible for a bad guy who
        /// gains root access to a single node could potentially deploy a malicious container that could also
        /// join the network.  With this disabled, the bad guy would need to gain access to one of the manager
        /// nodes to deploy a malicious service.
        /// </para>
        /// <para>
        /// Unforunately, it's not currently possible to change this setting after a cluster is deployed.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PrivateAttachable", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool PrivateAttachable { get; set; } = true;

        /// <summary>
        /// The IP addresses of the upstream DNS nameservers to be used by the cluster.  This defaults to the 
        /// Google Public DNS servers: <b>[ "8.8.8.8", "8.8.4.4" ]</b> when the property is <c>null</c> or empty.
        /// </summary>
        /// <remarks>
        /// <para>
        /// neonCLUSTERs configure the Consul servers running on the manager nodes to handle the DNS requests
        /// from the cluster host nodes and containers by default.  This enables the registration of services
        /// with Consul that will be resolved to specific IP addresses.  This is used by the <b>proxy-manager</b>
        /// to support stateful services deployed as multiple containers and may also be used in other future
        /// scenarios.
        /// </para>
        /// <para>
        /// neonCLUSTER Consul DNS servers answer requests for names with the <b>cluster</b> top-level domain.
        /// Other requests will be handled recursively by forwarding the request to one of the IP addresses
        /// specified here.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "Nameservers", Required = Required.Default)]
        [DefaultValue(null)]
        public string[] Nameservers { get; set; } = null;

        /// <summary>
        /// <para>
        /// URI for the <a href="https://www.powerdns.com/auth.html">PowerDNS Authoritative Server</a> package 
        /// to use for provisioning cluster dynbamic DNS services on the cluster mnanagers.  This defaults to 
        /// a known good release.
        /// </para>
        /// <note>
        /// <see cref="PdnsServerPackageUri"/> and <see cref="PdnsBackendRemotePackageUri"/> must specify packages
        /// from the same PowerDNS build.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "PdnsServerPackageUri", Required = Required.Default)]
        [DefaultValue(defaultPdnsServerPackageUri)]
        public string PdnsServerPackageUri { get; set; } = defaultPdnsServerPackageUri;

        /// <summary>
        /// <para>
        /// URI for the <a href="https://www.powerdns.com/auth.html">PowerDNS Authoritative Server Remote Backend</a>
        /// package to use for provisioning cluster dynamic DNS services on the cluster managers.  This defaults to 
        /// a known good release.
        /// </para>
        /// <note>
        /// <see cref="PdnsServerPackageUri"/> and <see cref="PdnsBackendRemotePackageUri"/> must specify packages
        /// from the same PowerDNS build.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "PdnsBackendRemotePackageUri", Required = Required.Default)]
        [DefaultValue(defaultPdnsBackendRemotePackageUri)]
        public string PdnsBackendRemotePackageUri { get; set; } = defaultPdnsBackendRemotePackageUri;

        /// <summary>
        /// URI for the <a href="https://www.powerdns.com/recursor.html">PowerDNS Recursor</a> package 
        /// to use for provisioning cluster DNS services.  This defaults to a known good release.
        /// </summary>
        [JsonProperty(PropertyName = "PdnsRecursorPackageUri", Required = Required.Default)]
        [DefaultValue(defaultPdnsRecursorPackagePackageUri)]
        public string PdnsRecursorPackageUri { get; set; } = defaultPdnsRecursorPackagePackageUri;

        /// <summary>
        /// Enables the deployment of the PowerDNS Authoritative server to the cluster manager nodes
        /// along with the <b>neon-dns</b> and <b>neon-dns-health</b> services to provide dynamic
        /// DNS capabilities to the cluster.  Worker nodes will be configured to use the managers
        /// as their upstream DNS servers.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "DynamicDns", Required = Required.Default)]
        [DefaultValue(true)]
        public bool DynamicDns { get; set; } = true;

        /// <summary>
        /// The cluster's manager load balancer's FQDN or IP address during cluster
        /// provisioning or <c>null</c> if the cluster does not have load balancers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For cloud deployments, the <b>neon-cli</b> will set this to IP address or
        /// FQDN of the public cluster load balancer responsible for forwarding traffic
        /// to the manager nodes.
        /// </para>
        /// <para>
        /// This needs to be explicitly defined for on-premise clusters that also
        /// deploy VPN servers.  In this case, you'll need to specify the public IP
        /// address or FQDN of your cluster router.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "ManagerRouterAddress", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string ManagerRouterAddress { get; set; }

        /// <summary>
        /// The cluster's worker load balancer's FQDN or IP address during cluster
        /// provisioning or <c>null</c> if the cluster does not have load balancers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For cloud deployments, the <b>neon-cli</b> will set this to IP address or
        /// FQDN of the public cluster load balancer responsible for forwarding traffic
        /// to the worker nodes.
        /// </para>
        /// <para>
        /// This is not required for on-premise deployments.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "WorkerRouterAddress", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string WorkerRouterAddress { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the overall cluster address space (CIDR) for cloud environments.  This must be a <b>/21</b>
        /// subnet with 2048 IP addresses for deployment to cloud providers.  This defaults to 
        /// <b>10.168.0.0/21</b>.
        /// </para>
        /// <note>
        /// This property is ignored for the <see cref="HostingEnvironments.Machine"/> provider.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <note>
        /// IMPORTANT: You should take care to ensure that this subnet does not conflict
        /// with subnets assigned to your company or home networks and if you ever intend
        /// to deploy multiple clusters and link them via VPNs, you must ensure that
        /// each cluster is assigned a unique address space.
        /// </note>
        /// <para>
        /// The <b>neon-cli</b> divides this address space into four subnets when deploying 
        /// a cluster to a cloud platform such as AWS or Azure.  The table below
        /// describes this using the the default address space <b>10.168.0.0/21</b>:
        /// </para>
        /// <list type="list">
        /// <item>
        ///     <term><b>10.168.0.0/22</b></term>
        ///     <description>
        ///     <see cref="CloudVNetSubnet"/>: The cloud address space is split in half and the 
        ///     first half will be used as the overall address space for the virtual network to
        ///     be created for the cluster.  The VNET address space will be split into to equal
        ///     sized subnets to be assigned to manager node NICs as well as NICs for all nodes.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>10.168.0.0/23</b></term>
        ///     <description>
        ///     <see cref="NodesSubnet"/>: The first half of <see cref="CloudVNetSubnet"/> will
        ///     be assigned to the NICs for all nodes.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>10.168.2.0/23</b></term>
        ///     <description>
        ///     The second half of <see cref="CloudVNetSubnet"/> will be assigned to the NICs 
        ///     for manager nodes.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>10.168.4.0/22</b></term>
        ///     <description>
        ///     <see cref="VpnReturnSubnet"/>: The second half of the cloud address space is 
        ///     reserved for the OpenVPN tunnels with the OpenVPN tunnel on each cluster manager
        ///     being assigned a <b>/25</b> subnet from this address space.
        ///     </description>
        /// </item>
        /// </list>
        /// <para>
        /// The default value <b>10.168.0.0/21</b> is a reasonable choice
        /// in many situations, but you may need to customize this to
        /// avoid subnet conflicts.
        /// </para>
        /// <para>
        /// You may specify subnets in any of the private Internet address spaces:
        /// </para>
        /// <list type="bullet">
        /// <item><b>10.0.0.0/8</b></item>
        /// <item><b>172.16.0.0/12</b></item>
        /// <item><b>192.168.0.0/16</b></item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "CloudAddressSpace", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultCloudSubnet)]
        public string CloudAddressSpace { get; set; } = defaultCloudSubnet;

        /// <summary>
        /// <para>
        /// The <b>/22</b> address space to be assigned to the cluster's cloud virtual network.
        /// </para>
        /// <note>
        /// This property is ignored for the <see cref="HostingEnvironments.Machine"/> provider and is
        /// computed automatically by the <b>neon-cli</b> when provisioning in a cloud environment.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "CloudVNetSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CloudVNetSubnet { get; set; }

        /// <summary>
        /// <para>
        /// The <b>/23</b> subnet for the cluster node IP addresses.  All cluster nodes will have
        /// a NIC attached to this subnet.
        /// </para>
        /// <note>
        /// This property must be configured the <see cref="HostingEnvironments.Machine"/> provider and 
        /// is computed automatically by the <b>neon</b> tool when provisioning in a cloud environment.
        /// </note>
        /// <note>
        /// For on-premise clusters, the statically assigned IP addresses assigned 
        /// to the nodes must reside within the this subnet.  For clusters hosted by
        /// cloud providers, the <b>neon-cli</b> will split this into three subnets:
        /// <see cref="NodesSubnet"/>, <see cref="CloudVpnSubnet"/> and <see cref="VpnReturnSubnet"/>
        /// and will automatically assign IP addresses to the virtual machines.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "NodesSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string NodesSubnet { get; set; }

        /// <summary>
        /// <para>
        /// The cloud VPN server <b>/23</b> subnet.  Cluster managers will have a NIC attached to this subnet 
        /// in addition to one attached to <see cref="NodesSubnet"/>.  These will be reponsible for forwarding
        /// return traffic from the nodes in the <see cref="NodesSubnet"/> back to the VPN clients.
        /// </para>
        /// <note>
        /// This property is ignored for the <see cref="HostingEnvironments.Machine"/> provider and is
        /// computed automatically by the <b>neon-cli</b> when provisioning in a cloud environment.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "CloudVpnSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CloudVpnSubnet { get; set; }

        /// <summary>
        /// <para>
        /// The cluster VPN client return <b>/22</b> subnet.  This where OpenVPN servers will 
        /// run and also acts as the pool of addresses that will be assigned to connecting VPN clients.
        /// This will be further split into <b>/25</b> subnets assigned to each manager/OpenVPN
        /// server.
        /// </para>
        /// <note>
        /// This property must be specifically initialized when <see cref="Environment"/> is set to 
        /// <see cref="HostingEnvironments.Machine"/> and is computed automatically by the <b>neon-cli</b>
        /// when provisioning in a cloud environment.
        /// </note>
        /// <note>
        /// For on-premise clusters this will default to <b>10.169.0.0/22</b> which will work
        /// for many clusters.  You may need to adjust this to avoid conflicts with your
        /// local network or if you intend to deploy multiple clusters on the same network.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VpnReturnSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaulVpnReturnSubnet)]
        public string VpnReturnSubnet { get; set; } = defaulVpnReturnSubnet;

        /// <summary>
        /// Indicates that host IP addresses are to be configured explicitly as static values.
        /// This defaults to <c>true</c>.  This is ignored for cloud hosting environments.
        /// </summary>
        [JsonProperty(PropertyName = "StaticIP", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool StaticIP { get; set; } = true;

        /// <summary>
        /// Specifies the default network gateway to be configured for hosts when <see cref="StaticIP"/> is set to <c>true</c>.
        /// This defaults to the first usable address in the <see cref="NodesSubnet"/>.  For example, for the
        /// <b>10.0.0.0/24</b> subnet, this will be set to <b>10.0.0.1</b>.  This is ignored for cloud hosting 
        /// environments.
        /// </summary>
        [JsonProperty(PropertyName = "Gateway", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Gateway { get; set; } = null;

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (!NetworkCidr.TryParse(PublicSubnet, out var cidr))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(NetworkOptions)}.{nameof(PublicSubnet)}={PublicSubnet}].");
            }

            if (!NetworkCidr.TryParse(PrivateSubnet, out cidr))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(NetworkOptions)}.{nameof(PrivateSubnet)}={PrivateSubnet}].");
            }

            if (PublicSubnet == PrivateSubnet)
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(PublicSubnet)}] cannot be the same as [{nameof(PrivateSubnet)}] .");
            }

            if (Nameservers == null || Nameservers.Length == 0)
            {
                Nameservers = new string[] { "8.8.8.8", "8.8.4.4" };
            }

            foreach (var nameserver in Nameservers)
            {
                if (!IPAddress.TryParse(nameserver, out var address))
                {
                    throw new ClusterDefinitionException($"[{nameserver}] is not a valid [{nameof(NetworkOptions)}.{nameof(Nameservers)}] IP address.");
                }
            }

            PdnsServerPackageUri = PdnsServerPackageUri ?? defaultPdnsServerPackageUri;

            if (!Uri.TryCreate(PdnsServerPackageUri, UriKind.Absolute, out var uri1))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(PdnsServerPackageUri)}={PdnsServerPackageUri}] is not a valid URI.");
            }

            PdnsBackendRemotePackageUri = PdnsBackendRemotePackageUri ?? defaultPdnsBackendRemotePackageUri;

            if (!Uri.TryCreate(PdnsBackendRemotePackageUri, UriKind.Absolute, out var uri2))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(PdnsBackendRemotePackageUri)}={PdnsBackendRemotePackageUri}] is not a valid URI.");
            }

            PdnsRecursorPackageUri = PdnsRecursorPackageUri ?? defaultPdnsRecursorPackagePackageUri;

            if (!Uri.TryCreate(PdnsServerPackageUri, UriKind.Absolute, out var uri3))
            {
                throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(PdnsRecursorPackageUri)}={PdnsRecursorPackageUri}] is not a valid URI.");
            }

            if (clusterDefinition.Hosting.IsCloudProvider)
            {
                // Verify [CloudAddressSpace].

                if (string.IsNullOrEmpty(CloudAddressSpace))
                {
                    CloudAddressSpace = defaultCloudSubnet;
                }

                if (!NetworkCidr.TryParse(CloudAddressSpace, out var cloudAddressSpaceCidr))
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(CloudAddressSpace)}={CloudAddressSpace}] is not a valid IPv4 subnet.");
                }

                if (cloudAddressSpaceCidr.PrefixLength != 21)
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(CloudAddressSpace)}={CloudAddressSpace}] prefix length is not valid.  Only [/21] subnets are currently supported.");
                }

                // Compute [NodeSubnet] by splitting [ClusterSubnet] in quarters and taking the
                // first quarter.

                NetworkCidr nodeSubnetCidr;

                nodeSubnetCidr = new NetworkCidr(cloudAddressSpaceCidr.Address, cloudAddressSpaceCidr.PrefixLength + 2);
                NodesSubnet    = nodeSubnetCidr.ToString();

                // Ensure that the node subnet is big enough to allocate IP 
                // addresses for each node.

                if (clusterDefinition.Nodes.Count() > nodeSubnetCidr.AddressCount - 4)
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NodesSubnet)}={NodesSubnet}] subnet not large enough for the [{clusterDefinition.Nodes.Count()}] node addresses.");
                }

                // Verify/Compute VPN properties.

                if (clusterDefinition.Vpn.Enabled)
                {
                    // Compute [CloudVpnSubnet] by taking the second quarter of [ClusterSubnet].

                    NetworkCidr cloudVpnCidr;

                    cloudVpnCidr   = new NetworkCidr(nodeSubnetCidr.NextAddress, cloudAddressSpaceCidr.PrefixLength + 2);
                    CloudVpnSubnet = cloudVpnCidr.ToString();

                    // Compute [CloudVNetSubnet] by taking the first half of [CloudAddressSpace],
                    // which includes both [NodesSubnet] and [CloudVpnSubnet].

                    NetworkCidr cloudVNetSubnet;

                    cloudVNetSubnet = new NetworkCidr(cloudAddressSpaceCidr.Address, cloudAddressSpaceCidr.PrefixLength + 1);
                    CloudVNetSubnet = cloudVNetSubnet.ToString();

                    // Compute [VpnReturnSubnet] by taking the upper half of [ClusterSubnet].

                    NetworkCidr vpnReturnCidr;

                    vpnReturnCidr   = new NetworkCidr(cloudVpnCidr.NextAddress, 22);
                    VpnReturnSubnet = vpnReturnCidr.ToString();
                }
            }
            else
            {
                // Verify VPN properties.

                if (clusterDefinition.Vpn.Enabled)
                {
                    if (clusterDefinition.Hosting.Environment == HostingEnvironments.Machine)
                    {
                        if (string.IsNullOrEmpty(ManagerRouterAddress))
                        {
                            throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(ManagerRouterAddress)}] is required for on-premise deployments that enable VPN.  Set the public IP address or FQDN of your cluster router.");
                        }
                    }

                    // Verify [VpnReturnSubnet].

                    if (!NetworkCidr.TryParse(VpnReturnSubnet, out var vpnReturnCidr))
                    {
                        throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(VpnReturnSubnet)}={VpnReturnSubnet}] is not a valid subnet.");
                    }

                    if (vpnReturnCidr.PrefixLength > 23)
                    {
                        throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(VpnReturnSubnet)}={VpnReturnSubnet}] is too small.  The subnet prefix length cannot be longer than [23].");
                    }

                    // Verify [NodesSubnet].

                    if (!NetworkCidr.TryParse(NodesSubnet, out var nodesSubnetCidr))
                    {
                        throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NodesSubnet)}={NodesSubnet}] is not a valid IPv4 subnet.");
                    }

                    if (nodesSubnetCidr.Overlaps(vpnReturnCidr))
                    {
                        throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NodesSubnet)}={NodesSubnet}] and [{nameof(VpnReturnSubnet)}={VpnReturnSubnet}] overlap.");
                    }
                }
            }

            if (StaticIP)
            {
                if (string.IsNullOrEmpty(NodesSubnet))
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NetworkOptions.NodesSubnet)}] is required when [{nameof(NetworkOptions)}.{nameof(StaticIP)}=true]");
                }

                if (!NetworkCidr.TryParse(NodesSubnet, out var subnet))
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(NodesSubnet)}={clusterDefinition.Network.NodesSubnet}] is not a valid IPv4 subnet.");
                }

                if (string.IsNullOrEmpty(Gateway))
                {
                    // Default to the first valid address of the cluster nodes subnet 
                    // if this isn't already set.

                    Gateway = subnet.FirstUsableAddress.ToString();
                }

                if (string.IsNullOrEmpty(Gateway))
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(Gateway)}] is required when [{nameof(NetworkOptions)}.{nameof(StaticIP)}=true]");
                }

                if (!IPAddress.TryParse(Gateway, out var gateway) || gateway.AddressFamily != AddressFamily.InterNetwork)
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(Gateway)}={Gateway}] is not a valid IPv4 address.");
                }

                if (!subnet.Contains(gateway))
                {
                    throw new ClusterDefinitionException($"[{nameof(NetworkOptions)}.{nameof(Gateway)}={Gateway}] address is not within the [{nameof(NetworkOptions)}.{nameof(NetworkOptions.NodesSubnet)}={clusterDefinition.Network.NodesSubnet}] subnet.");
                }
            }
        }
    }
}
