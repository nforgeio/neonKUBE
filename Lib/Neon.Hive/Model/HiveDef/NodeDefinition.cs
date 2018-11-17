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
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Describes a Neon Docker host node.
    /// </summary>
    public class NodeDefinition
    {
        //---------------------------------------------------------------------
        // Static methods

        /// <summary>
        /// The Ansible group name regex validator.  Group names must start with a letter
        /// and then can be followed by zero or more letters, digits, or underscores.
        /// </summary>
        private static readonly Regex groupNameRegex = new Regex(@"^[a-z][a-z0-9\-_]*$", RegexOptions.IgnoreCase);

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
        /// Uniquely identifies the node within the hive.
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
        /// to <c>null</c> before provisioning a hive.  This will be initialized while
        /// by the <b>neon-cli</b> tool for manager nodes when provisioning in a cloud provider.
        /// </summary>
        [JsonProperty(PropertyName = "PublicAddress", Required = Required.Default)]
        [DefaultValue(null)]
        public string PublicAddress { get; set; } = null;

        /// <summary>
        /// The node's IP address or <c>null</c> if one has not been assigned yet.
        /// Note that an node's IP address cannot be changed once the node has
        /// been added to the hive.
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
        /// container deployment across the hive.  Neon uses <b>Consul</b> (https://www.consul.io/) 
        /// for service discovery and <b>Docker Swarm</b> (https://docs.docker.com/swarm/) for
        /// container orchestration.  These services will be deployed to management nodes.
        /// </para>
        /// <para>
        /// An odd number of management nodes must be deployed in a hive (to help prevent
        /// split-brain).  One management node may be deployed for non-production environments,
        /// but to enable high-availability, three or five management nodes may be deployed.
        /// </para>
        /// <note>
        /// Consul documentation recommends no more than 5 nodes be deployed per hive to
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
        /// Worker nodes are part of the Docker Swarm but do not perform any Swarm
        /// management activties.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsWorker
        {
            get { return Role.Equals(NodeRole.Worker, StringComparison.InvariantCultureIgnoreCase); }
        }

        /// <summary>
        /// Returns <c>true</c> for nodes that are part of the neonHIVE but not 
        /// within the Docker Swarm.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
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
        [YamlIgnore]
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
        /// the hive.  This defaults to <see cref="NetworkPorts.OpenVPN"/> for the first manager
        /// node (sorted by name), (<see cref="NetworkPorts.OpenVPN"/> + 1), for the second
        /// manager node an so on for subsequent managers.  This defaults to <b>0</b> for workers.
        /// </para>
        /// <para>
        /// For cloud deployments, this will be initialized by the <b>neon-cli</b> during
        /// hive setup such that each manager node will be assigned a unique port with
        /// a traffic manager rule that forwards external traffic from <see cref="VpnFrontendPort"/>
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
        /// be used when routing return traffic from other hive nodes back to a
        /// connected VPN client.  This is only set when provisioning a hive VPN.  
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
        /// This will be calculated automatically during hive setup by manager nodes if the
        /// hive VPN is enabled.
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
        /// Specifies the hive host groups to which this node belongs.  This can be used to organize
        /// nodes (most likely pets) into groups that will be managed by Ansible playbooks.  These
        /// group are in addition to the standard host groups automatically supported by <b>neoncli</b>:
        /// <b>all</b>, <b>managers</b>, <b>workers</b>, <b>swarm</b>, and <b>pets</b>.
        /// </summary>
        [JsonProperty(PropertyName = "HostGroups", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> HostGroups { get; set; } = new List<string>();

        /// <summary>
        /// Azure provisioning options for this node, or <c>null</c> to use reasonable defaults.
        /// </summary>
        [JsonProperty(PropertyName = "Azure")]
        public AzureNodeOptions Azure { get; set; }

        /// <summary>
        /// Identifies the hypervisor instance where this node is to be provisioned for Hyper-V
        /// or XenServer based hives.  This name must map to the name of one of the <see cref="HostingOptions.VmHosts"/>
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
        /// This is specified as a string that can be a byte count or a number with units like <b>512MB</b>, 
        /// <b>0.5GB</b>, <b>2GB</b>, or <b>1TB</b>.  This defaults to the value specified by 
        /// <see cref="HostingOptions.VmMemory"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmMemory { get; set; } = null;

        /// <summary>
        /// <para>
        /// Specifies the minimum amount of memory to allocate to each hive virtual machine.  This is specified as a string that
        /// can be a long byte count or a byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, or <b>1TB</b>
        /// or may be set to <c>null</c> to set the same value as <see cref="VmMemory"/>.  This defaults to the value specified by
        /// <see cref="HostingOptions.VmMinimumMemory"/>.
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
        /// that can be a byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, or <b>1TB</b>.  This defaults 
        /// to the value specified by <see cref="HostingOptions.VmDisk"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VmDisk { get; set; } = null;

        /// <summary>
        /// Returns the maximum number processors to allocate for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <returns>The number of cores.</returns>
        public int GetVmProcessors(HiveDefinition hiveDefinition)
        {
            if (VmProcessors != 0)
            {
                return VmProcessors;
            }
            else
            {
                return hiveDefinition.Hosting.VmProcessors;
            }
        }

        /// <summary>
        /// Returns the maximum number of bytes of memory allocate to for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <returns>The size in bytes.</returns>
        public long GetVmMemory(HiveDefinition hiveDefinition)
        {
            if (!string.IsNullOrEmpty(VmMemory))
            {
                return HiveDefinition.ValidateSize(VmMemory, this.GetType(), nameof(VmMemory));
            }
            else
            {
                return HiveDefinition.ValidateSize(hiveDefinition.Hosting.VmMemory, hiveDefinition.Hosting.GetType(), nameof(hiveDefinition.Hosting.VmMemory));
            }
        }

        /// <summary>
        /// Returns the minimum number of bytes of memory allocate to for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <returns>The size in bytes.</returns>
        public long GetVmMinimumMemory(HiveDefinition hiveDefinition)
        {
            if (!string.IsNullOrEmpty(VmMinimumMemory))
            {
                return HiveDefinition.ValidateSize(VmMinimumMemory, this.GetType(), nameof(VmMinimumMemory));
            }
            else if (!string.IsNullOrEmpty(hiveDefinition.Hosting.VmMinimumMemory))
            {
                return HiveDefinition.ValidateSize(hiveDefinition.Hosting.VmMinimumMemory, hiveDefinition.Hosting.GetType(), nameof(hiveDefinition.Hosting.VmMinimumMemory));
            }
            else
            {
                // Return [VmMemory] otherwise.

                return GetVmMemory(hiveDefinition);
            }
        }

        /// <summary>
        /// Returns the maximum number of bytes to disk allocate to for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <returns>The size in bytes.</returns>
        public long GetVmDisk(HiveDefinition hiveDefinition)
        {
            if (!string.IsNullOrEmpty(VmDisk))
            {
                return HiveDefinition.ValidateSize(VmDisk, this.GetType(), nameof(VmDisk));
            }
            else
            {
                return HiveDefinition.ValidateSize(hiveDefinition.Hosting.VmDisk, hiveDefinition.Hosting.GetType(), nameof(hiveDefinition.Hosting.VmDisk));
            }
        }

        /// <summary>
        /// Returns the size in bytes of the Ceph drive created for this node if 
        /// integrated Ceph storage cluster is enabled.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <returns>The size in bytes or zero if Ceph is not enabled.</returns>
        public long GetCephOSDDriveSize(HiveDefinition hiveDefinition)
        {
            if (!hiveDefinition.HiveFS.Enabled)
            {
                return 0;
            }

            if (Labels.CephOSDDriveSizeGB > 0)
            {
                return Labels.CephOSDDriveSizeGB * NeonHelper.Giga;
            }
            else
            {
                Labels.CephOSDDriveSizeGB = (int)(HiveDefinition.ValidateSize(hiveDefinition.HiveFS.OSDDriveSize, hiveDefinition.Hosting.GetType(), nameof(hiveDefinition.HiveFS.OSDDriveSize))/NeonHelper.Giga);

                return (long)Labels.CephOSDDriveSizeGB * NeonHelper.Giga;
            }
        }

        /// <summary>
        /// Returns the size in bytes of RAM to allocate to the OSD cache
        /// on this node integrated Ceph storage cluster is enabled and
        /// OSD is deployed to the node.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <returns>The size in bytes or zero if Ceph is not enabled.</returns>
        public long GetCephOSDCacheSize(HiveDefinition hiveDefinition)
        {
            if (!hiveDefinition.HiveFS.Enabled)
            {
                return 0;
            }

            if (Labels.CephOSDCacheSizeMB > 0)
            {
                return Labels.CephOSDCacheSizeMB * NeonHelper.Mega;
            }
            else
            {
                Labels.CephOSDCacheSizeMB = (int)(HiveDefinition.ValidateSize(hiveDefinition.HiveFS.OSDCacheSize, hiveDefinition.Hosting.GetType(), nameof(hiveDefinition.HiveFS.OSDCacheSize))/NeonHelper.Mega);

                return (long)Labels.CephOSDCacheSizeMB * NeonHelper.Mega;
            }
        }

        /// <summary>
        /// Returns the size in bytes of drive space to allocate to the
        /// OSD journal on this node integrated Ceph storage cluster is 
        /// enabled and OSD is deployed to the node.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <returns>The size in bytes or zero if Ceph is not enabled.</returns>
        public long GetCephOSDJournalSize(HiveDefinition hiveDefinition)
        {
            if (!hiveDefinition.HiveFS.Enabled)
            {
                return 0;
            }

            if (Labels.CephOSDJournalSizeMB > 0)
            {
                return Labels.CephOSDJournalSizeMB * NeonHelper.Mega;
            }
            else
            {
                Labels.CephOSDJournalSizeMB = (int)(HiveDefinition.ValidateSize(hiveDefinition.HiveFS.OSDJournalSize, hiveDefinition.Hosting.GetType(), nameof(hiveDefinition.HiveFS.OSDJournalSize)) / NeonHelper.Mega);

                return (long)Labels.CephOSDJournalSizeMB * NeonHelper.Mega;
            }
        }

        /// <summary>
        /// Returns the size in bytes of RAM to allocate to the MDS cache
        /// on this node integrated Ceph storage cluster is enabled and
        /// MDS is deployed to the node.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <returns>The size in bytes or zero if Ceph is not enabled.</returns>
        public long GetCephMDSCacheSize(HiveDefinition hiveDefinition)
        {
            if (!hiveDefinition.HiveFS.Enabled)
            {
                return 0;
            }

            if (Labels.CephMDSCacheSizeMB > 0)
            {
                return Labels.CephMDSCacheSizeMB * NeonHelper.Mega;
            }
            else
            {
                Labels.CephMDSCacheSizeMB = (int)(HiveDefinition.ValidateSize(hiveDefinition.HiveFS.MDSCacheSize, hiveDefinition.Hosting.GetType(), nameof(hiveDefinition.HiveFS.MDSCacheSize)) / NeonHelper.Mega);

                return (long)Labels.CephMDSCacheSizeMB * NeonHelper.Mega;
            }
        }

        /// <summary>
        /// <b>HACK:</b> This used by <see cref="SetupController{T}"/> to introduce a delay for this
        /// node when executing the next setup step.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal TimeSpan StepDelay { get; set; }

        /// <summary>
        /// Validates the node definition.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            Labels     = Labels ?? new NodeLabels(this);
            HostGroups = HostGroups ?? new List<string>();

            if (Name == null)
            {
                throw new HiveDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}] property is required.");
            }

            if (!HiveDefinition.IsValidName(Name))
            {
                throw new HiveDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (name == "localhost")
            {
                throw new HiveDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}={Name}] property is not valid.  [localhost] is reserved.");
            }

            if (Name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new HiveDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}={Name}] property is not valid because node names starting with [node-] are reserved.");
            }

            if (Name.Equals(HiveDefinition.VirtualSwarmManagerName, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new HiveDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}={Name}] property is not valid.  [{HiveDefinition.VirtualSwarmManagerName}] is reserved for targeting Swarm related Ansible tasks.");
            }

            if (hiveDefinition.Hosting.IsOnPremiseProvider)
            {
                if (string.IsNullOrEmpty(PrivateAddress))
                {
                    throw new HiveDefinitionException($"Node [{Name}] requires [{nameof(PrivateAddress)}] when hosting in an on-premise facility.");
                }

                if (!IPAddress.TryParse(PrivateAddress, out var nodeAddress))
                {
                    throw new HiveDefinitionException($"Node [{Name}] has invalid IP address [{PrivateAddress}].");
                }
            }

            if (IsManager && hiveDefinition.Hosting.IsOnPremiseProvider && hiveDefinition.Vpn.Enabled)
            {
                if (!NetHelper.IsValidPort(VpnFrontendPort))
                {
                    throw new HiveDefinitionException($"Manager node [{Name}] has [{nameof(VpnFrontendPort)}={VpnFrontendPort}] which is not a valid network port.");
                }
            }

            Labels.Validate(hiveDefinition);

            foreach (var group in HostGroups)
            {
                if (string.IsNullOrWhiteSpace(group))
                {
                    throw new HiveDefinitionException($"Node [{Name}] assigns an empty group in [{nameof(HostGroups)}].");
                }
                else if (HiveHostGroups.BuiltIn.Contains(group))
                {
                    throw new HiveDefinitionException($"Node [{Name}] assigns the standard [{group}] in [{nameof(HostGroups)}].  Standard groups cannot be explicitly assigned since [neon-cli] handles them automatically.");
                }
                else if (!groupNameRegex.IsMatch(group))
                {
                    throw new HiveDefinitionException($"Node [{Name}] assigns the invalid group [{group}] in [{nameof(HostGroups)}].  Group names must start with a letter and then can be followed by zero or more letters, digits, dashes, and underscores.");
                }
            }

            if (Azure != null)
            {
                Azure.Validate(hiveDefinition, this.Name);
            }

            if (hiveDefinition.Hosting.IsRemoteHypervisorProvider)
            {
                if (string.IsNullOrEmpty(VmHost))
                {
                    throw new HiveDefinitionException($"Node [{Name}] does not specify a hypervisor [{nameof(NodeDefinition)}.{nameof(NodeDefinition.VmHost)}].");
                }
                else if (hiveDefinition.Hosting.VmHosts.FirstOrDefault(h => h.Name.Equals(VmHost, StringComparison.InvariantCultureIgnoreCase)) == null)
                {
                    throw new HiveDefinitionException($"Node [{Name}] references hypervisor [{VmHost}] which is defined in [{nameof(HostingOptions)}={nameof(HostingOptions.VmHosts)}].");
                }
            }

            if (VmMemory != null)
            {
                HiveDefinition.ValidateSize(VmMemory, this.GetType(), nameof(VmMemory));
            }

            if (VmMinimumMemory != null)
            {
                HiveDefinition.ValidateSize(VmMinimumMemory, this.GetType(), nameof(VmMinimumMemory));
            }

            if (VmDisk != null)
            {
                HiveDefinition.ValidateSize(VmDisk, this.GetType(), nameof(VmDisk));
            }
        }
    }
}
