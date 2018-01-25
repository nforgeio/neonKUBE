//-----------------------------------------------------------------------------
// FILE:	    NodeDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
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
    /// Describes a Neon Docker host node.
    /// </summary>
    public class NodeDefinition
    {
        //---------------------------------------------------------------------
        // Static methods

        /// <summary>
        /// Parses a <see cref="NodeDefinition"/> from Docker node labels.
        /// </summary>
        /// <param name="labels">The Docker labels.</param>
        /// <returns>The parsed <see cref="NodeDefinition"/>.</returns>
        public static NodeDefinition ParseFromLabels(Dictionary<string, string> labels)
        {
            var node = new NodeDefinition();

            node.Labels.Parse(labels);

            return node;
        }

        //---------------------------------------------------------------------
        // Instance methods

        private string name;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeDefinition()
        {
            Labels = new NodeLabels(this);
        }

        /// <summary>
        /// Uniquely identifies the node within the cluster.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores and
        /// also that all names will be converted to lower case.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name
        {
            get { return name; }

            set
            {
                if (value != null)
                {
                    name = value.ToLowerInvariant();
                }
                else
                {
                    name = null;
                }
            }
        }

        /// <summary>
        /// The node's public IP address or DNS name.  This will be generally initialized
        /// to <c>null</c> before provisioning a cluster.  This will be initialized while
        /// by the <b>neon-cli</b> tool for manager nodes when provisioning in a cloud provider.
        /// </summary>
        [JsonProperty(PropertyName = "PublicAddress", Required = Required.Default)]
        [DefaultValue(null)]
        public string PublicAddress { get; set; } = null;

        /// <summary>
        /// The node's IP address or <c>null</c> if one has not been assigned yet.
        /// Note that an node's IP address cannot be changed once the node has
        /// been added to the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateAddress", Required = Required.Default)]
        [DefaultValue(null)]
        public string PrivateAddress { get; set; } = null;

        /// <summary>
        /// Indicates that the node will act as a management node (defaults to <c>false</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Management nodes are reponsible for managing service discovery and coordinating 
        /// container deployment across the cluster.  Neon uses <b>Consul</b> (https://www.consul.io/) 
        /// for service discovery and <b>Docker Swarm</b> (https://docs.docker.com/swarm/) for
        /// container orchestration.  These services will be deployed to management nodes.
        /// </para>
        /// <para>
        /// An odd number of management nodes must be deployed in a cluster (to help prevent
        /// split-brain).  One management node may be deployed for non-production environments,
        /// but to enable high-availability, three or five management nodes may be deployed.
        /// </para>
        /// <note>
        /// Consul documentation recommends no more than 5 nodes be deployed per cluster to
        /// prevent floods of network traffic from the internal gossip discovery protocol.
        /// Swarm does not have this limitation but to keep things simple, Neon is going 
        /// to standardize on a single management node concept.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "IsManager", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool IsManager
        {
            get { return Role.Equals(NodeRole.Manager, StringComparison.InvariantCultureIgnoreCase); }
        }

        /// <summary>
        /// Returns <c>true</c> for worker nodes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Worker nodes within a cluster are where application containers will be deployed.
        /// Any node that is not a <see cref="IsManager"/> is considered to be a worker.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        public bool IsWorker
        {
            get { return Role.Equals(NodeRole.Worker, StringComparison.InvariantCultureIgnoreCase); }
        }

        /// <summary>
        /// Returns <c>true</c> for nodes that are part of the neonCLUSTER but in the Docker Swarm.
        /// </summary>
        [JsonIgnore]
        public bool IsPet
        {
            get
            {
                switch (Role.ToLowerInvariant())
                {
                    case NodeRole.Pet:

                        return true;

                    default:

                        return false;
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> for nodes that are members of the Docker Swarm.
        /// </summary>
        [JsonIgnore]
        public bool InSwarm
        {
            get { return IsManager || IsWorker; }
        }

        /// <summary>
        /// Returns the node's <see cref="NodeRole"/>.  This defaults to <see cref="NodeRole.Worker"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Role", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(NodeRole.Worker)]
        public string Role { get; set; } = NodeRole.Worker;

        /// <summary>
        /// <para>
        /// Specifies the frontend port to be used to reach the OpenVPN server from outside
        /// the cluster.  This defaults to <see cref="NetworkPorts.OpenVPN"/> for the first manager
        /// node (sorted by name), (<see cref="NetworkPorts.OpenVPN"/> + 1), for the second
        /// manager node an so on for subsequent managers.  This defaults to <b>0</b> for workers.
        /// </para>
        /// <para>
        /// For cloud deployments, this will be initialized by the <b>neon-cli</b> during
        /// cluster setup such that each manager node will be assigned a unique port that
        /// with a load balancer rule that forwards external traffic from <see cref="VpnFrontendPort"/>
        /// to the <see cref="NetworkPorts.OpenVPN"/> port on the manager.
        /// </para>
        /// <para>
        /// For on-premise deployments, you should assign a unique <see cref="VpnFrontendPort"/>
        /// to each manager node and then manually configure your router with port forwarding 
        /// rules that forward TCP traffic from the external port to <see cref="NetworkPorts.OpenVPN"/>
        /// for each manager.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "VpnFrontendPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int VpnFrontendPort { get; set; } = 0;

        /// <summary>
        /// Set by the <b>neon-cli</b> to the private IP address for a manager node to
        /// be used when routing return traffic from other cluster nodes back to a
        /// connected VPN client.  This is only set when provisioning a cluster VPN.  
        /// </summary>
        [JsonProperty(PropertyName = "VpnPoolAddress", Required = Required.Default)]
        [DefaultValue(null)]
        public string VpnPoolAddress { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the subnet defining the block of addresses assigned to the OpenVPN server
        /// running on this manager node for the OpenVPN server's use as well as for the pool of
        /// addresses that will be assigned to connecting VPN clients.
        /// </para>
        /// <para>
        /// This will be calculated automatically during cluster setup by manager nodes if the
        /// cluster VPN is enabled.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "VpnPoolSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VpnPoolSubnet { get; set; }

        /// <summary>
        /// Specifies the Docker labels to be assigned to the host node.  These can provide
        /// detailed information such as the host CPU, RAM, storage, etc.  <see cref="NodeLabels"/>
        /// for more information.
        /// </summary>
        [JsonProperty(PropertyName = "Labels")]
        public NodeLabels Labels { get; set; }

        /// <summary>
        /// Azure provisioning options for this node, or <c>null</c> to use reasonable defaults.
        /// </summary>
        [JsonProperty(PropertyName = "Azure")]
        public AzureNodeOptions Azure { get; set; }

        /// <summary>
        /// Identifies the hypervisor instance where this node is to be provisioned for Hyper-V
        /// or XenServer based clusters.  This name must map to the name of one of the <see cref="HostingOptions.VmHosts"/>
        /// when set.
        /// </summary>
        [JsonProperty(PropertyName = "VmHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmHost { get; set; } = null;

        /// <summary>
        /// Specifies the number of processors to assigned to this node when provisioned on a hypervisor.  This
        /// defaults to the value specified by <see cref="HostingOptions.VmProcessors"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmProcessors", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int VmProcessors { get; set; } = 0;

        /// <summary>
        /// Specifies the maximum amount of memory to allocate to this node when provisioned on a hypervisor.  
        /// This is specified as a string that can be a long byte count or a long with units like <b>512MB</b>
        /// or <b>2GB</b>.  This defaults to the value specified by <see cref="HostingOptions.VmMemory"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmMemory { get; set; } = null;

        /// <summary>
        /// <para>
        /// Specifies the minimum amount of memory to allocate to each cluster virtual machine.  This is specified as a string that
        /// can be a a long byte count or a long with units like <b>512MB</b> or <b>2GB</b> or may be set to <c>null</c> to set
        /// the same value as <see cref="VmMemory"/>.  This defaults to the value specified by <see cref="HostingOptions.VmMinimumMemory"/>.
        /// </para>
        /// <note>
        /// This is currently honored only when provisioning to a local Hyper-V instance (typically as a developer).  This is ignored
        /// for XenServer and when provisioning to remote Hyper-V instances.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "VmMinimumMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmMinimumMemory { get; set; } = null;

        /// <summary>
        /// The amount of disk space to allocate to this node when when provisioned on a hypervisor.  This is specified as a string
        /// that can be a long byte count or a long with units like <b>512MB</b> or <b>2GB</b>.  This defaults to the value specified 
        /// by <see cref="HostingOptions.VmDisk"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmDisk { get; set; } = null;

        /// <summary>
        /// Returns the maximum number processors to allocate for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The number of cores.</returns>
        internal int GetVmProcessors(ClusterDefinition clusterDefinition)
        {
            if (VmProcessors != 0)
            {
                return VmProcessors;
            }
            else
            {
                return clusterDefinition.Hosting.VmProcessors;
            }
        }

        /// <summary>
        /// Returns the maximum number of bytes of memory allocate to for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes.</returns>
        internal long GetVmMemory(ClusterDefinition clusterDefinition)
        {
            if (VmMemory != null)
            {
                return HostingOptions.ValidateVMSize(VmMemory, this.GetType(), nameof(VmMemory));
            }
            else
            {
                return HostingOptions.ValidateVMSize(clusterDefinition.Hosting.VmMemory, clusterDefinition.Hosting.GetType(), nameof(clusterDefinition.Hosting.VmMemory));
            }
        }

        /// <summary>
        /// Returns the minimum number of bytes of memory allocate to for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes.</returns>
        internal long GetVmMinimumMemory(ClusterDefinition clusterDefinition)
        {
            if (VmMinimumMemory != null)
            {
                return HostingOptions.ValidateVMSize(VmMinimumMemory, this.GetType(), nameof(VmMinimumMemory));
            }
            else if (clusterDefinition.Hosting.VmMinimumMemory != null)
            {
                return HostingOptions.ValidateVMSize(clusterDefinition.Hosting.VmMinimumMemory, clusterDefinition.Hosting.GetType(), nameof(clusterDefinition.Hosting.VmMinimumMemory));
            }
            else
            {
                // Return [VmMemory] otherwise.

                return GetVmMemory(clusterDefinition);
            }
        }

        /// <summary>
        /// Returns the maximum number of bytes to disk allocate to for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes.</returns>
        internal long GetVmDisk(ClusterDefinition clusterDefinition)
        {
            if (VmDisk != null)
            {
                return HostingOptions.ValidateVMSize(VmDisk, this.GetType(), nameof(VmDisk));
            }
            else
            {
                return HostingOptions.ValidateVMSize(clusterDefinition.Hosting.VmDisk, clusterDefinition.Hosting.GetType(), nameof(clusterDefinition.Hosting.VmDisk));
            }
        }

        /// <summary>
        /// Validates the node definition.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            Labels = Labels ?? new NodeLabels(this);

            if (Name == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}] property is required.");
            }

            if (!ClusterDefinition.IsValidName(Name))
            {
                throw new ClusterDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (clusterDefinition.Hosting.Environment == HostingEnvironments.Machine)
            {
                if (string.IsNullOrEmpty(PrivateAddress))
                {
                    throw new ClusterDefinitionException($"Node [{Name}] requires [{nameof(PrivateAddress)}] when hosting in a private facility.");
                }

                if (!IPAddress.TryParse(PrivateAddress, out var nodeAddress))
                {
                    throw new ClusterDefinitionException($"Node [{Name}] has invalid IP address [{PrivateAddress}].");
                }
            }

            if (IsManager && clusterDefinition.Hosting.Environment == HostingEnvironments.Machine && clusterDefinition.Vpn.Enabled)
            {
                if (!NetHelper.IsValidPort(VpnFrontendPort))
                {
                    throw new ClusterDefinitionException($"Manager node [{Name}] has [{nameof(VpnFrontendPort)}={VpnFrontendPort}] which is not a valid network port.");
                }
            }

            Labels.Validate(clusterDefinition);

            if (Azure != null)
            {
                Azure.Validate(clusterDefinition, this.Name);
            }

            if (clusterDefinition.Hosting.IsHypervisorProvider)
            {
                if (string.IsNullOrEmpty(VmHost))
                {
                    throw new ClusterDefinitionException($"Node [{Name}] does not specify a hypervisor [{nameof(NodeDefinition)}.{nameof(NodeDefinition.VmHost)}].");
                }
                else if (clusterDefinition.Hosting.VmHosts.FirstOrDefault(h => h.Name.Equals(VmHost, StringComparison.InvariantCultureIgnoreCase)) == null)
                {
                    throw new ClusterDefinitionException($"Node [{Name}] references hypervisor [{VmHost}] which is defined in [{nameof(HostingOptions)}={nameof(HostingOptions.VmHosts)}].");
                }
            }

            if (VmMemory != null)
            {
                HostingOptions.ValidateVMSize(VmMemory, this.GetType(), nameof(VmMemory));
            }

            if (VmMinimumMemory != null)
            {
                HostingOptions.ValidateVMSize(VmMinimumMemory, this.GetType(), nameof(VmMinimumMemory));
            }

            if (VmDisk != null)
            {
                HostingOptions.ValidateVMSize(VmDisk, this.GetType(), nameof(VmDisk));
            }
        }
    }
}
