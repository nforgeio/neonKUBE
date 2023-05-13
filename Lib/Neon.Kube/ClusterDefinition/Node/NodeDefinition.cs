//-----------------------------------------------------------------------------
// FILE:	    NodeDefinition.cs
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
using System.Net;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s.Models;

using Neon.Common;
using Neon.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using YamlDotNet.Serialization;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Describes a cluster node.
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
        /// also that all names will be converted to lowercase.
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
        /// The node's IP address or <c>null</c> if one has not been assigned yet.
        /// Note that an node's IP address cannot be changed once the node has
        /// been added to the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Address", Required = Required.Default)]
        [YamlMember(Alias = "address", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Address { get; set; } = null;

        /// <summary>
        /// Indicates that the node will act as a control-plane node (defaults to <c>false</c>).
        /// </summary>
        /// <remarks>
        /// <para>
        /// Control-plane nodes are reponsible for managing service discovery and coordinating 
        /// pod deployment across the cluster.
        /// </para>
        /// <para>
        /// An odd number of control-plane nodes must be deployed in a cluster (to help prevent
        /// split-brain).  One control-plane node may be deployed for non-production environments,
        /// but to enable high-availability, three or five control-plane nodes may be deployed.
        /// </para>
        /// </remarks>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsControlPane
        {
            get { return Role.Equals(NodeRole.ControlPlane, StringComparison.InvariantCultureIgnoreCase); }
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
        /// <para>
        /// Indicates whether this node should be configured to accept external network traffic
        /// on node ports and route that into the cluster.  This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// If all nodes have <see cref="Ingress"/> set to <c>false</c> and the cluster defines
        /// one or more <see cref="NetworkOptions.IngressRules"/> then NEONKUBE will choose a
        /// reasonable set of nodes to accept inbound traffic.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Ingress", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ingress", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Ingress { get; set; } = false;

        /// <summary>
        /// <para>
        /// Indicates that this node will provide a cStor block device for the cStorPool
        /// maintained by the cluster OpenEBS service that provides cloud optimized storage.
        /// This defaults to <c>false</c>
        /// </para>
        /// <note>
        /// If all nodes have <see cref="OpenEbsStorage"/> set to <c>false</c> then most NEONKUBE 
        /// hosting managers will automatically choose the nodes that will host the cStor
        /// block devices by configuring up to three nodes to do this, favoring worker nodes
        /// over control-plane nodes when possible.
        /// </note>
        /// <note>
        /// The <see cref="HostingEnvironment.BareMetal"/> hosting manager works a bit differently
        /// from the others.  It requires that at least one node have <see cref="OpenEbsStorage"/><c>=true</c>
        /// and that node must have an empty unpartitioned block device available to be provisoned
        /// as an cStor.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbsStorage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "openEbsStorage", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool OpenEbsStorage { get; set; } = false;

        /// <summary>
        /// Specifies the labels to be assigned to the cluster node.  These can describe
        /// details such as the host CPU, RAM, storage, etc.  <see cref="NodeLabels"/>
        /// for more information.
        /// </summary>
        [JsonProperty(PropertyName = "Labels", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "labels", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public NodeLabels Labels { get; set; }

        /// <summary>
        /// Specifies the taints to be assigned to the cluster node.  
        /// </summary>
        [JsonProperty(PropertyName = "Taints")]
        [YamlMember(Alias = "taints", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<V1Taint> Taints { get; set; }

        /// <summary>
        /// Hypervisor hosting related options for environments like Hyper-V and XenServer.
        /// </summary>
        [JsonProperty(PropertyName = "Hypervisor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hypervisor", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HypervisorNodeOptions Hypervisor { get; set; }

        /// <summary>
        /// Azure provisioning options for this node, or <c>null</c> to use reasonable defaults.
        /// </summary>
        [JsonProperty(PropertyName = "Azure", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "azure", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AzureNodeOptions Azure { get; set; }

        /// <summary>
        /// AWS provisioning options for this node, or <c>null</c> to use reasonable defaults.
        /// </summary>
        [JsonProperty(PropertyName = "Aws", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "aws", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AwsNodeOptions Aws { get; set; }

        /// <summary>
        /// Returns the size of the operating system boot disk as a string with optional
        /// <see cref="ByteUnits"/> unit suffix.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The disk size.</returns>
        public string GetOsDiskSize(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            switch (clusterDefinition.Hosting.Environment)
            {
                case HostingEnvironment.Aws:

                    return Aws.VolumeSize ?? clusterDefinition.Hosting.Aws.DefaultVolumeSize;

                case HostingEnvironment.Azure:

                    return Azure.DiskSize ?? clusterDefinition.Hosting.Azure.DefaultDiskSize;

                case HostingEnvironment.BareMetal:

                    throw new NotImplementedException();

                case HostingEnvironment.Google:

                    throw new NotImplementedException();

                case HostingEnvironment.HyperV:
                case HostingEnvironment.XenServer:

                    return Hypervisor.GetOsDisk(clusterDefinition).ToString();

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Returns the size of the data disk as a string with optional <see cref="ByteUnits"/> unit suffix.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <returns>The disk size or <c>null</c> when the node has no data disk.</returns>
        public string GetDataDiskSize(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            switch (clusterDefinition.Hosting.Environment)
            {
                case HostingEnvironment.Aws:

                    return Aws.OpenEbsVolumeSize ?? clusterDefinition.Hosting.Aws.DefaultOpenEbsVolumeSize;

                case HostingEnvironment.Azure:

                    return Azure.OpenEbsDiskSize ?? clusterDefinition.Hosting.Azure.DefaultOpenEbsDiskSize;

                case HostingEnvironment.BareMetal:

                    throw new NotImplementedException();

                case HostingEnvironment.Google:

                    throw new NotImplementedException();

                case HostingEnvironment.HyperV:
                case HostingEnvironment.XenServer:

                    return Hypervisor.GetOsDisk(clusterDefinition).ToString();

                default:

                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Validates the node definition.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            Hypervisor = Hypervisor ?? new HypervisorNodeOptions();

            var nodeDefinitionPrefix = $"{nameof(ClusterDefinition.NodeDefinitions)}";

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
                throw new ClusterDefinitionException($"The [{nodeDefinitionPrefix}.{nameof(Name)}] property is required.");
            }

            if (!ClusterDefinition.IsValidName(Name))
            {
                throw new ClusterDefinitionException($"The [{nodeDefinitionPrefix}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (name == "localhost")
            {
                throw new ClusterDefinitionException($"The [{nodeDefinitionPrefix}.{nameof(Name)}={Name}] property is not valid.  [localhost] is reserved.");
            }

            if (Name.StartsWith("neon-", StringComparison.InvariantCultureIgnoreCase) && !clusterDefinition.IsSpecialNeonCluster)
            {
                throw new ClusterDefinitionException($"The [{nodeDefinitionPrefix}.{nameof(Name)}={Name}] property is not valid because node names starting with [neon-] are reserved.");
            }

            if (name.Equals("cluster", StringComparison.InvariantCultureIgnoreCase))
            {
                // $hack(jefflill):
                //
                // The node name [cluster] is reserved because we want to persist the
                // global cluster log file as [cluster.log] and we don't want this to
                // conflict with any of the node log files.
                //
                // See: KubeConst.ClusterSetupLogName

                throw new ClusterDefinitionException($"The [{nodeDefinitionPrefix}.{nameof(Name)}={Name}] property is not valid because the node name [cluster] is reserved.");
            }

            if (string.IsNullOrEmpty(Role))
            {
                Role = NodeRole.Worker;
            }

            if (!Role.Equals(NodeRole.ControlPlane, StringComparison.InvariantCultureIgnoreCase) && !Role.Equals(NodeRole.Worker, StringComparison.InvariantCultureIgnoreCase))
            {
                throw new ClusterDefinitionException($"[{nodeDefinitionPrefix}.{nameof(Name)}={Name}] has invalid [{nameof(Role)}={Role}].  This must be [{NodeRole.ControlPlane}] or [{NodeRole.Worker}].");
            }

            // We don't need to check the node address for cloud providers.

            if (clusterDefinition.Hosting.IsOnPremiseProvider)
            {
                if (string.IsNullOrEmpty(Address))
                {
                    throw new ClusterDefinitionException($"[{nodeDefinitionPrefix}.{nameof(Name)}={Name}] requires [{nameof(Address)}] when hosting in an on-premise facility.");
                }

                if (!NetHelper.TryParseIPv4Address(Address, out var nodeAddress))
                {
                    throw new ClusterDefinitionException($"[{nodeDefinitionPrefix}.{nameof(Name)}={Name}] has invalid IP address [{Address}].");
                }
            }

            switch (clusterDefinition.Hosting.Environment)
            {
                case HostingEnvironment.Aws:

                    Aws = Aws ?? new AwsNodeOptions();
                    Aws.Validate(clusterDefinition, this.Name);
                    break;

                case HostingEnvironment.Azure:

                    Azure = Azure ?? new AzureNodeOptions();
                    Azure.Validate(clusterDefinition, this.Name);
                    break;

                case HostingEnvironment.BareMetal:

                    // No machine options to check at this time.
                    break;

                case HostingEnvironment.Google:

                    // $todo(jefflill: Implement this
                    break;

                case HostingEnvironment.HyperV:
                case HostingEnvironment.XenServer:

                    Hypervisor = Hypervisor ?? new HypervisorNodeOptions();
                    Hypervisor.Validate(clusterDefinition, this.Name);
                    break;

                default:

                    throw new NotImplementedException($"Hosting environment [{clusterDefinition.Hosting.Environment}] hosting option check is not implemented.");
            }
        }
    }
}
