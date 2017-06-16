//-----------------------------------------------------------------------------
// FILE:	    HostingOptions.cs
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
    /// Specifies the cloud or colocation/on-premise hosting settings.
    /// </summary>
    public class HostingOptions
    {
        private const string defaultCloudSubnet    = "10.168.0.0/21";
        private const string defaulVpnReturnSubnet = "10.169.0.0/22";

        /// <summary>
        /// Default constructor that initializes a <see cref="HostingEnvironments.Machine"/> provider.
        /// </summary>
        public HostingOptions()
        {
        }

        /// <summary>
        /// Identifies the cloud or other hosting platform.  This defaults to <see cref="HostingEnvironments.Machine"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(HostingEnvironments.Machine)]
        public HostingEnvironments Environment { get; set; } = HostingEnvironments.Machine;

        /// <summary>
        /// Specifies the Amazon Web Services hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Aws", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AwsOptions Aws { get; set; } = null;

        /// <summary>
        /// Specifies the Microsoft Azure hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Azure", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AzureOptions Azure { get; set; } = null;

        /// <summary>
        /// Specifies the Google Cloud Platform hosting settings.
        /// </summary>
        [JsonProperty(PropertyName = "Google", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public GoogleOptions Google { get; set; } = null;

        /// <summary>
        /// Specifies the hosting settings when hosting directly on bare metal or virtual machines.
        /// </summary>
        [JsonProperty(PropertyName = "Machine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public MachineOptions Machine { get; set; } = null;

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
        /// This is not required for in-premise deployments.
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
        /// This property is ignored for the <see cref="HostingEnvironments.Machine"/> provider and is
        /// computed automatically by the <b>neon</b> tool when provisioning in a cloud environment.
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
        /// Specifies the DNS servers.  This defaults to the DNS services configured
        /// for the local hosting environment.
        /// </summary>
        [JsonProperty(PropertyName = "DnsServers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> DnsServers { get; set; } = new List<string>();

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

            var isCloudProvider = Environment != HostingEnvironments.Machine;

            switch (Environment)
            {
                case HostingEnvironments.Aws:

                    if (Aws == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Aws)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Aws.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Azure:

                    if (Azure == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Azure)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Azure.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Google:

                    if (Google == null)
                    {
                        throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(Google)}] must be initialized when cloud provider is [{Environment}].");
                    }

                    Google.Validate(clusterDefinition);
                    break;

                case HostingEnvironments.Machine:

                    break;

                default:

                    throw new NotImplementedException();
            }

            if (isCloudProvider && !clusterDefinition.Vpn.Enabled)
            {
                // VPN is implicitly enabled when hosting on a cloud.

                clusterDefinition.Vpn.Enabled = true;
            }

            if (isCloudProvider)
            {
                // Verify [CloudAddressSpace].

                if (string.IsNullOrEmpty(CloudAddressSpace))
                {
                    CloudAddressSpace = defaultCloudSubnet;
                }

                if (!NetworkCidr.TryParse(CloudAddressSpace, out var cloudAddressSpaceCidr))
                {
                    throw new ClusterDefinitionException($"Hosting [{nameof(CloudAddressSpace)}={CloudAddressSpace}] is not a valid IPv4 subnet.");
                }

                if (cloudAddressSpaceCidr.PrefixLength != 21)
                {
                    throw new ClusterDefinitionException($"Hosting [{nameof(CloudAddressSpace)}={CloudAddressSpace}] prefix length is not valid.  Only [/21] subnets are currently supported.");
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
                    throw new ClusterDefinitionException($"Hosting [{nameof(NodesSubnet)}={NodesSubnet}] subnet not large enough for the [{clusterDefinition.Nodes.Count()}] node addresses.");
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
                    if (Environment == HostingEnvironments.Machine)
                    {
                        if (string.IsNullOrEmpty(ManagerRouterAddress))
                        {
                            throw new ClusterDefinitionException($"Hosting [{nameof(ManagerRouterAddress)}] is required for on-premise deployments that enable VPN.  Set the public IP address or FQDN of your cluster router.");
                        }
                    }

                    // Verify [VpnReturnSubnet].

                    if (!NetworkCidr.TryParse(VpnReturnSubnet, out var vpnReturnCidr))
                    {
                        throw new ClusterDefinitionException($"Hosting [{nameof(VpnReturnSubnet)}={VpnReturnSubnet}] is not a valid subnet.");
                    }

                    if (vpnReturnCidr.PrefixLength > 23)
                    {
                        throw new ClusterDefinitionException($"Hosting [{nameof(VpnReturnSubnet)}={VpnReturnSubnet}] is too small.  The subnet prefix length cannot be longer than [23].");
                    }

                    // Verify [NodesSubnet].

                    if (!NetworkCidr.TryParse(NodesSubnet, out var nodesSubnetCidr))
                    {
                        throw new ClusterDefinitionException($"Hosting [{nameof(NodesSubnet)}={NodesSubnet}] is not a valid IPv4 subnet.");
                    }

                    if (nodesSubnetCidr.Overlaps(vpnReturnCidr))
                    {
                        throw new ClusterDefinitionException($"Hosting [{nameof(NodesSubnet)}={NodesSubnet}] and [{nameof(VpnReturnSubnet)}={VpnReturnSubnet}] overlap.");
                    }
                }
            }

            // Verify [DnsServers].

            if (DnsServers != null)
            {
                foreach (var dnsServer in DnsServers)
                {
                    if (!IPAddress.TryParse(dnsServer, out var address) || address.AddressFamily != AddressFamily.InterNetwork)
                    {
                        throw new ClusterDefinitionException($"Azure hosting [{nameof(DnsServers)}={dnsServer}] is not a valid IPv4 address.");
                    }
                }
            }
        }

        /// <summary>
        /// Clears all hosting provider details because they typically
        /// include hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
            Aws       = null;
            Azure     = null;
            Google    = null;
            Machine = null;
        }
    }
}
