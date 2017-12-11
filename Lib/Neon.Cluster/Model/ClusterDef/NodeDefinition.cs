//-----------------------------------------------------------------------------
// FILE:	    NodeDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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
        /// the cluster.  This defaults to <see cref="NetworkPorts.OpenVPN"/> for manager nodes
        /// and <b>0</b> for workers.
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
        [JsonProperty(PropertyName = "VpnReturnAddress", Required = Required.Default)]
        [DefaultValue(null)]
        public string VpnReturnAddress { get; set; }

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
        [JsonProperty(PropertyName = "VpnReturnSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VpnReturnSubnet { get; set; }

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
        }
    }
}
