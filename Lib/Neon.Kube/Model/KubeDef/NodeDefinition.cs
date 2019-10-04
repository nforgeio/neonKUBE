//-----------------------------------------------------------------------------
// FILE:	    NodeDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// Describes a cluster host node.
    /// </summary>
    public class NodeDefinition
    {
        //---------------------------------------------------------------------
        // Static methods

        /// <summary>
        /// Parses a <see cref="NodeDefinition"/> from Kubernetes node labels.
        /// </summary>
        /// <param name="labels">The node labels.</param>
        /// <returns>The parsed <see cref="NodeDefinition"/>.</returns>
        public static NodeDefinition ParseFromLabels(Dictionary<string, string> labels)
        {
            var node = new NodeDefinition();

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
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
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
        /// by the <b>neon-cli</b> tool for master nodes when provisioning in a cloud provider.
        /// </summary>
        [JsonProperty(PropertyName = "PublicAddress", Required = Required.Default)]
        [YamlMember(Alias = "publicAddress", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PublicAddress { get; set; } = null;

        /// <summary>
        /// The node's IP address or <c>null</c> if one has not been assigned yet.
        /// Note that an node's IP address cannot be changed once the node has
        /// been added to the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateAddress", Required = Required.Default)]
        [YamlMember(Alias = "privateAddress", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PrivateAddress { get; set; } = null;

        /// <summary>
        /// Indicates that the node will act as a master node (defaults to <c>false</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Master nodes are reponsible for managing service discovery and coordinating 
        /// pod deployment across the cluster.
        /// </para>
        /// <para>
        /// An odd number of master nodes must be deployed in a cluster (to help prevent
        /// split-brain).  One master node may be deployed for non-production environments,
        /// but to enable high-availability, three or five master nodes may be deployed.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsMaster
        {
            get { return Role.Equals(NodeRole.Master, StringComparison.InvariantCultureIgnoreCase); }
        }

        /// <summary>
        /// Returns <c>true</c> for worker nodes.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsWorker
        {
            get { return Role.Equals(NodeRole.Worker, StringComparison.InvariantCultureIgnoreCase); }
        }

        /// <summary>
        /// Returns the node's <see cref="NodeRole"/>.  This defaults to <see cref="NodeRole.Worker"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Role", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "role", ApplyNamingConventions = false)]
        [DefaultValue(NodeRole.Worker)]
        public string Role { get; set; } = NodeRole.Worker;

        /// <summary>
        /// Specifies the labels to be assigned to the host node.  These can provide
        /// detailed information such as the host CPU, RAM, storage, etc.  <see cref="NodeLabels"/>
        /// for more information.
        /// </summary>
        [JsonProperty(PropertyName = "Labels")]
        [YamlMember(Alias = "labels", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public NodeLabels Labels { get; set; }

        /// <summary>
        /// Specifies the taints to be assigned to the host node.  
        /// </summary>
        [JsonProperty(PropertyName = "Taints")]
        [YamlMember(Alias = "taints", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> Taints { get; set; }

        /// <summary>
        /// Azure provisioning options for this node, or <c>null</c> to use reasonable defaults.
        /// </summary>
        [JsonProperty(PropertyName = "Azure")]
        [YamlMember(Alias = "azure", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AzureNodeOptions Azure { get; set; }

        /// <summary>
        /// Identifies the hypervisor instance where this node is to be provisioned for Hyper-V
        /// or XenServer based clusters.  This name must map to the name of one of the <see cref="HostingOptions.VmHosts"/>
        /// when set.
        /// </summary>
        [JsonProperty(PropertyName = "VmHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmHost", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VmHost { get; set; } = null;

        /// <summary>
        /// Specifies the number of processors to assigned to this node when provisioned on a hypervisor.  This
        /// defaults to the value specified by <see cref="HostingOptions.VmProcessors"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmProcessors", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmProcessors", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int VmProcessors { get; set; } = 0;

        /// <summary>
        /// Specifies the maximum amount of memory to allocate to this node when provisioned on a hypervisor.  
        /// This is specified as a string that can be a byte count or a number with units like <b>512MB</b>, 
        /// <b>0.5GB</b>, <b>2GB</b>, or <b>1TB</b>.  This defaults to the value specified by 
        /// <see cref="HostingOptions.VmMemory"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmMemory", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmMemory", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VmMemory { get; set; } = null;

        /// <summary>
        /// The amount of disk space to allocate to this node when when provisioned on a hypervisor.  This is specified as a string
        /// that can be a byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, or <b>1TB</b>.  This defaults 
        /// to the value specified by <see cref="HostingOptions.VmDisk"/>.
        /// </summary>
        [JsonProperty(PropertyName = "VmDisk", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "vmDisk", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string VmDisk { get; set; } = null;

        /// <summary>
        /// Returns the maximum number processors to allocate for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The number of cores.</returns>
        public int GetVmProcessors(ClusterDefinition clusterDefinition)
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
        public long GetVmMemory(ClusterDefinition clusterDefinition)
        {
            if (!string.IsNullOrEmpty(VmMemory))
            {
                return ClusterDefinition.ValidateSize(VmMemory, this.GetType(), nameof(VmMemory));
            }
            else
            {
                return ClusterDefinition.ValidateSize(clusterDefinition.Hosting.VmMemory, clusterDefinition.Hosting.GetType(), nameof(clusterDefinition.Hosting.VmMemory));
            }
        }

        /// <summary>
        /// Returns the maximum number of bytes to disk allocate to for this node when
        /// hosted on a hypervisor.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes.</returns>
        public long GetVmDisk(ClusterDefinition clusterDefinition)
        {
            if (!string.IsNullOrEmpty(VmDisk))
            {
                return ClusterDefinition.ValidateSize(VmDisk, this.GetType(), nameof(VmDisk));
            }
            else
            {
                return ClusterDefinition.ValidateSize(clusterDefinition.Hosting.VmDisk, clusterDefinition.Hosting.GetType(), nameof(clusterDefinition.Hosting.VmDisk));
            }
        }

        /// <summary>
        /// Returns the size in bytes of the Ceph drive created for this node if 
        /// integrated Ceph storage cluster is enabled.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes or zero if Ceph is not enabled.</returns>
        public decimal GetCephOSDDriveSize(ClusterDefinition clusterDefinition)
        {
            if (!clusterDefinition.Ceph.Enabled)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(Labels.CephOSDDriveSize))
            {
                Labels.CephOSDDriveSize = clusterDefinition.Ceph.OSDDriveSize;
            }

            return ByteUnits.Parse(Labels.CephOSDDriveSize);
        }

        /// <summary>
        /// Returns the size in bytes of RAM to allocate to the OSD cache
        /// on this node integrated Ceph storage cluster is enabled and
        /// OSD is deployed to the node.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes or zero if Ceph is not enabled.</returns>
        public decimal GetCephOSDCacheSize(ClusterDefinition clusterDefinition)
        {
            if (!clusterDefinition.Ceph.Enabled)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(Labels.CephOSDCacheSize))
            {
                Labels.CephOSDCacheSize = clusterDefinition.Ceph.OSDCacheSize;
            }

            return ByteUnits.Parse(Labels.CephOSDCacheSize);
        }

        /// <summary>
        /// Returns the size in bytes of drive space to allocate to the
        /// OSD journal on this node integrated Ceph storage cluster is 
        /// enabled and OSD is deployed to the node.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes or zero if Ceph is not enabled.</returns>
        public decimal GetCephOSDJournalSize(ClusterDefinition clusterDefinition)
        {
            if (!clusterDefinition.Ceph.Enabled)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(Labels.CephOSDJournalSize))
            {
                Labels.CephOSDJournalSize = clusterDefinition.Ceph.OSDJournalSize;
            }

            return ByteUnits.Parse(Labels.CephOSDJournalSize);
        }

        /// <summary>
        /// Returns the size in bytes of RAM to allocate to the MDS cache
        /// on this node integrated Ceph storage cluster is enabled and
        /// MDS is deployed to the node.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The size in bytes or zero if Ceph is not enabled.</returns>
        public decimal GetCephMDSCacheSize(ClusterDefinition clusterDefinition)
        {
            if (!clusterDefinition.Ceph.Enabled)
            {
                return 0;
            }

            if (string.IsNullOrEmpty(Labels.CephMDSCacheSize))
            {
                Labels.CephMDSCacheSize = clusterDefinition.Ceph.MDSCacheSize;
            }

            return ByteUnits.Parse(Labels.CephMDSCacheSize);
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
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            // Ensure that the labels are wired up to the parent node.

            if (Labels == null)
            {
                Labels = new NodeLabels(this);
            }
            else
            {
                Labels.Node = this;
            }

            if (Name == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}] property is required.");
            }

            if (!ClusterDefinition.IsValidName(Name))
            {
                throw new ClusterDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (name == "localhost")
            {
                throw new ClusterDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}={Name}] property is not valid.  [localhost] is reserved.");
            }

            if (Name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ClusterDefinitionException($"The [{nameof(NodeDefinition)}.{nameof(Name)}={Name}] property is not valid because node names starting with [node-] are reserved.");
            }

            if (clusterDefinition.Hosting.IsOnPremiseProvider)
            {
                if (string.IsNullOrEmpty(PrivateAddress))
                {
                    throw new ClusterDefinitionException($"Node [{Name}] requires [{nameof(PrivateAddress)}] when hosting in an on-premise facility.");
                }

                if (!IPAddress.TryParse(PrivateAddress, out var nodeAddress))
                {
                    throw new ClusterDefinitionException($"Node [{Name}] has invalid IP address [{PrivateAddress}].");
                }
            }

            if (Azure != null)
            {
                Azure.Validate(clusterDefinition, this.Name);
            }

            if (clusterDefinition.Hosting.IsRemoteHypervisorProvider)
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
                ClusterDefinition.ValidateSize(VmMemory, this.GetType(), nameof(VmMemory));
            }

            if (VmDisk != null)
            {
                ClusterDefinition.ValidateSize(VmDisk, this.GetType(), nameof(VmDisk));
            }
        }
    }
}
