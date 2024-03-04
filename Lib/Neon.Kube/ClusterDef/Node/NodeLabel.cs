//-----------------------------------------------------------------------------
// FILE:        NodeLabels.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Describes the standard cluster and custom labels to be assigned to 
    /// a cluster node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Labels are name/value properties that can be assigned to the cluster
    /// nodes for pod scheduling and other purposes.
    /// </para>
    /// <para>
    /// By convention, label names should use a reverse domain name prefix using a
    /// DNS domain you control.  For example, neonCLUSTER cluster related labels 
    /// are prefixed with <b>"neonkube.io/..."</b>.  You should follow this convention 
    /// for any custom labels you define.
    /// </para>
    /// <note>
    /// You may specify labels without a domain prefix if you're not concerned
    /// about potential conflicts.
    /// </note>
    /// <para>
    /// Label names must begin and end with a letter or digit and may include
    /// letters, digits, dashes and dots within.  Dots or dashes must not appear
    /// consecutively.
    /// </para>
    /// <note>
    /// Whitespace is not allowed in label values.
    /// </note>
    /// <para>
    /// This class exposes several built-in cluster properties.  You can use
    /// the <see cref="Custom"/> dictionary to add your own labels.
    /// </para>
    /// </remarks>
    public class NodeLabel
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NodeLabel()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="node">The node definition.</param>
        public NodeLabel(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            this.Node = node;
        }

        /// <summary>
        /// The parent node definition.
        /// </summary>
        internal NodeDefinition Node { get; set; }

        //---------------------------------------------------------------------
        // Define global cluster and node definition labels.

        /// <summary>
        /// Reserved label name that identifies the node's private IP address.
        /// </summary>
        public const string LabelAddress = ClusterDefinition.ReservedNodePrefix + "address";

        /// <summary>
        /// Reserved label name that identifies the node role.
        /// </summary>
        public const string LabelRole = ClusterDefinition.ReservedNodePrefix + "role";

        //---------------------------------------------------------------------
        // Define the node storage related labels.

        /// <summary>
        /// Reserved label name for <see cref="StorageOSDiskSize"/>.
        /// </summary>
        public const string LabelStorageOSDiskSize = ClusterDefinition.ReservedNodePrefix + "storage.osdisk.size";

        /// <summary>
        /// Reserved label name for <see cref="StorageOSDiskLocal"/>.
        /// </summary>
        public const string LabelStorageOSDiskLocal = ClusterDefinition.ReservedNodePrefix + "storage.osdisk.local";

        /// <summary>
        /// Reserved label name for <see cref="StorageOSDiskHDD"/>.
        /// </summary>
        public const string LabelStorageOSDiskHDD = ClusterDefinition.ReservedNodePrefix + "storage.osdisk.hdd";

        /// <summary>
        /// Reserved label name for <see cref="StorageOSDiskRedundant"/>.
        /// </summary>
        public const string LabelStorageOSDiskRedundant = ClusterDefinition.ReservedNodePrefix + "storage.osdisk.redundant";

        /// <summary>
        /// Reserved label name for <see cref="StorageOSDiskEphemeral"/>.
        /// </summary>
        public const string LabelStorageOSDiskEphemeral = ClusterDefinition.ReservedNodePrefix + "storage.osdisk.ephemeral";

        /// <summary>
        /// <b>node.neonkube.io/storage.osdisk.size</b> [<c>string</c>]: Specifies the node OS drive 
        /// storage capacity in bytes.
        /// </summary>
        [JsonProperty(PropertyName = "StorageOSDiskSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageOSDiskSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string StorageOSDiskSize { get; set; }

        /// <summary>
        /// <b>node.neonkube.io/storage.osdisklocal</b> [<c>bool</c>]: Specifies whether the node storage is hosted
        /// on the node itself or is mounted as a remote file system or block device.  This defaults
        /// to <c>true</c> for on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageOSDiskLocal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageOSDiskLocal", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool StorageOSDiskLocal { get; set; } = true;

        /// <summary>
        /// <b>node.neonkube.io/storage.osdisk.hdd</b> [<c>bool</c>]: Indicates that the storage
        /// is backed by a spinning drive as opposed to a SSD.  This defaults to <c>false</c> for 
        /// on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageOSDiskHDD", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageOSDiskHDD", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageOSDiskHDD { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/storage.osdisk.redundant</b> [<c>bool</c>]: Indicates that the storage is redundant.  This
        /// may be implemented locally using RAID1+ or remotely using network or cloud-based file systems.
        /// This defaults to <c>false</c> for on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageOSDiskRedundant", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageOSDiskRedundant", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageOSDiskRedundant { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/storage.osdisk.redundant</b> [<c>bool</c>]: Indicates that the storage is ephemeral.
        /// All data will be lost when the host is restarted.  This defaults to <c>false</c> for 
        /// on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageOSDiskEphemeral", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageOSDiskEphemeral", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageOSDiskEphemeral { get; set; } = false;

        //---------------------------------------------------------------------
        // Define physical host labels.

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalLocation = ClusterDefinition.ReservedNodePrefix + "physical.location";

        /// <summary>
        /// <b>node.neonkube.io/physical.location</b> [<c>string</c>]: A free format string describing the
        /// physical location of the server.  This defaults to the 
        /// <b>empty string</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You should use a consistent convention to describe a physical machine location.
        /// Here are some examples:
        /// </para>
        /// <list type="bullet">
        /// <item><i>rack-slot</i></item>
        /// <item><i>rack-number</i>/<i>rack-slot</i></item>
        /// <item><i>row</i>/<i>rack-number</i>/<i>rack-slot</i></item>
        /// <item><i>floor</i>/<i>row</i>/<i>rack-number</i>/<i>rack-slot</i></item>
        /// <item><i>building</i>/<i>floor</i>/<i>row</i>/<i>rack-number</i>/<i>rack-slot</i></item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "PhysicalLocation", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "physicalLocation", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalLocation { get; set; } = string.Empty;

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalMachine"/>.
        /// </summary>
        public const string LabelPhysicalMachine = ClusterDefinition.ReservedNodePrefix + "physical.machine";

        /// <summary>
        /// <b>node.neonkube.io/physical.model</b> [<c>string</c>]: A free format string describing the
        /// physical server computer model (e.g. <b>Dell-PowerEdge-R220</b>).  This defaults to the <b>empty string</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PhysicalMachine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "physicalMachine", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalMachine { get; set; } = string.Empty;

        /// <summary>
        /// Reserved label name for <see cref="PhysicalAvailabilitySet"/>.
        /// </summary>
        public const string LabelPhysicalAvailabilitytSet = ClusterDefinition.ReservedNodePrefix + "physical.availability-set";

        /// <summary>
        /// <para>
        /// <b>node.neonkube.io/physical.availability-set</b> [<c>string</c>]: Indicates that 
        /// the hosting environment will try to ensure that cluster VMs with the same
        /// availability set are deployed in a manner that reduces the possibility that
        /// more than one VM at a time will be taken offline for maintenance.
        /// </para>
        /// <para>
        /// This defaults to <b>control-plane</b> for cluster control-plane nodes and <b>worker</b>
        /// for worker nodes.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> Control-plane nodes should generally be located within their
        /// own availability set.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is typcally used for distributing pods across cluster nodes to 
        /// protect against more than one of them going down at once due to
        /// scheduled maintenance.
        /// </para>
        /// <para>
        /// On premise deployments don't currently support automatic provisioning by
        /// availability sets but that may happen in the future (e.g. by managing 
        /// clusters of XenServer host machines).  You'll need to manually specify 
        /// these labels to match your deployment and maintenance policies.
        /// </para>
        /// <para>
        /// Cloud deployments generally implement the concept of availability sets.
        /// These are used to group VMs together such that only one will be down
        /// for scheduled maintenance at any given moment and also that after a
        /// reboot, there will be a reasonable delay (like 30 minutes) to allow
        /// the VMs to collectively recover before rebooting the next VM.  NEONKUBE
        /// will provision node VMs that have the same <see cref="PhysicalAvailabilitySet"/> 
        /// into the same cloud availability set (for clouds that support this).
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PhysicalAvailabilitySet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "physicalAvailabilitySet", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PhysicalAvailabilitySet { get; set; } = null;

        /// <summary>
        /// <b>node.neonkube.io/physical.power</b> [<c>string</c>]: Describes the physical power
        /// connection for the node.  This defaults to the <b>empty string</b>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The format for this property is not currently defined.
        /// </note>
        /// <para>
        /// This field includes the information required to remotely control the power to
        /// the physical host machine via a Power Distribution Unit (PDU).
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PhysicalPower", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "physicalPower", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalPower { get; set; } = string.Empty;       // $todo(jefflill): Define the format of this string for APC PDUs.

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalPower = ClusterDefinition.ReservedNodePrefix + "physical.power";

        //---------------------------------------------------------------------
        // Define the neon-system related labels.

        /// <summary>
        /// Reserved label that an OpenEBS cStor/Mayastor block device should be deployed on the node.
        /// </summary>
        public const string LabelOpenEbsStorage = ClusterDefinition.ReservedNodePrefix + "storage.openebs-storage";

        /// <summary>
        /// <b>node.neonkube.io/system.openebs-storage</b> [<c>bool</c>]: Indicates that a NEONKUBE OpenEBS 
        /// block device will be deployed on this node.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This indicates that this node will provide a cStor block device for the cStorPool
        /// maintained by the cluster OpenEBS service that provides cloud optimized storage.
        /// This defaults to <c>false</c>
        /// </para>
        /// <note>
        /// If all nodes have <see cref="SystemOpenEbsStorage"/> set to <c>false</c> then most NEONKUBE 
        /// hosting managers will automatically choose the nodes that will host the cStor
        /// block devices by configuring up to three nodes to do this, favoring worker nodes
        /// over control-plane nodes when possible.
        /// </note>
        /// <note>
        /// The <see cref="HostingEnvironment.BareMetal"/> hosting manager works a bit differently
        /// from the others.  It requires that at least one node have <see cref="NodeLabel.SystemOpenEbsStorage"/><c>=true</c>
        /// and that node must have an empty unpartitioned block device available to be provisoned
        /// as an cStor.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "SystemOpenEbsStorage", Required = Required.Default)]
        [YamlMember(Alias = "systemOpenEbsStorage", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemOpenEbsStorage { get; set; } = false;

        /// <summary>
        /// Reserved label name for core NEONKUBE system components.
        /// </summary>
        public const string LabelSystemServices = ClusterDefinition.ReservedNodePrefix + "system.services";

        /// <summary>
        /// <para>
        /// <b>node.neonkube.io/system.services</b> [<c>bool</c>]: Indicates that general NEONKUBE 
        /// system services may be scheduled on this node.  This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// This currently only applies to deploying the <b>neon-cluster-operator</b>.  Use the
        /// <see cref="SystemIstioServices"/>, <see cref="SystemDbServices"/>, <see cref="SystemRegistryServices"/>,
        /// <see cref="SystemMinioServices"/>, <see cref="SystemRegistryServices"/>, <see cref="SystemLogServices"/>,
        /// <see cref="SystemLogServices"/>, <see cref="LabelSystemMetricServices"/> and <see cref="SystemTraceServices"/>
        /// properties to enable specific system services.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "systemServices", Required = Required.Default)]
        [YamlMember(Alias = "systemServices", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemServices { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="SystemIstioServices"/>.
        /// </summary>
        public const string LabelSystemIstioServices = ClusterDefinition.ReservedNodePrefix + "system.istio-services";

        /// <summary>
        /// <b>node.neonkube.io/system.istio-services,</b> [<c>bool</c>]: Indicates that NEONKUBE
        /// Istio services may be scheduled on this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "SystemIstioServices", Required = Required.Default)]
        [YamlMember(Alias = "systemIstioServices", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemIstioServices { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="LabelSystemDbServices"/>.
        /// </summary>
        public const string LabelSystemDbServices = ClusterDefinition.ReservedNodePrefix + "system.db-services";

        /// <summary>
        /// <b>node.neonkube.io/neon-system.db-services</b> [<c>bool</c>]: Indicates that the NEONKUBE 
        /// Citus/Postgresql database services may be scheduled on this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "SystemDbServices", Required = Required.Default)]
        [YamlMember(Alias = "systemDbServices", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemDbServices { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="LabelSystemDbServices"/>.
        /// </summary>
        public const string LabelSystemRegistryServices = ClusterDefinition.ReservedNodePrefix + "system.registry-services";

        /// <summary>
        /// <b>node.neonkube.io/system.registry-services</b> [<c>bool</c>]: Indicates that the NEONKUBE 
        /// Harbor registry may be scheduled on this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "SystemRegistryServices", Required = Required.Default)]
        [YamlMember(Alias = "systemRegistryServices", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemRegistryServices { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="SystemMinioServices"/>.
        /// </summary>
        public const string LabelSystemMinioServices = ClusterDefinition.ReservedNodePrefix + "system.minio-services";

        /// <summary>
        /// <b>node.neonkube.io/system.minio-services</b> [<c>bool</c>]: Indicates the NEONKUBE
        /// that Minio services can be scheduled on the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "SystemMinioServices", Required = Required.Default)]
        [YamlMember(Alias = "systeMinioServices", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemMinioServices { get; set; } = false;

        //---------------------------------------------------------------------
        // Define the monitoring related labels.

        /// <summary>
        /// Reserved label name for <see cref="SystemLogServices"/>.
        /// </summary>
        public const string LabelSystemLogServices = ClusterDefinition.ReservedNodePrefix + "system.monitor.log-services";

        /// <summary>
        /// <b>node.neonkube.io/monitor.log-services</b> [<c>bool</c>]: Indicates that 
        /// NEONKUBER that Loki logging services may be scheduled on the labeled node.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "SystemLogServices", Required = Required.Default)]
        [YamlMember(Alias = "systenmLogServices", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemLogServices { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="SystemMetricServices"/>.
        /// </summary>
        public const string LabelSystemMetricServices = ClusterDefinition.ReservedNodePrefix + "saystem.monitor.metric-services";

        /// <summary>
        /// <b>node.neonkube.io/monitor.metric-services</b> [<c>bool</c>]: Indicates NEONKUBE
        /// Mimir metrics services may be scheduled on the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "SystemMetricServices", Required = Required.Default)]
        [YamlMember(Alias = "systemMetricServices", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemMetricServices { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="SystemTraceServices"/>.
        /// </summary>
        public const string LabelTraceServices = ClusterDefinition.ReservedNodePrefix + "system.monitor.trace-services";

        /// <summary>
        /// <b>node.neonkube.io/monitor.trace-services</b> [<c>bool</c>]: Indicates that NEONKUBE
        /// Tempo tracing services may be scheduled on the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "SystemTraceServices", Required = Required.Default)]
        [YamlMember(Alias = "systemTraceServices", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool SystemTraceServices { get; set; } = false;

        //---------------------------------------------------------------------
        // Custom labels

        /// <summary>
        /// Custom node labels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this property to define custom cluster node labels.
        /// </para>
        /// <note>
        /// The <b>node.neonkube.io/</b> label prefix is reserved.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Custom")]
        [YamlMember(Alias = "custom", ApplyNamingConventions = false)]
        public Dictionary<string, string> Custom { get; set; } = new Dictionary<string, string>();

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Enumerates the standard Kubernetes/NEONKUBE node labels.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<KeyValuePair<string, object>> Standard
        {
            get
            {
                // WARNING: This method will need to be updated whenever standard labels are added or changed.

                var list = new List<KeyValuePair<string, object>>(50);

                // Standard labels from the parent node definition.

                list.Add(new KeyValuePair<string, object>(LabelAddress,                     Node.Address));
                list.Add(new KeyValuePair<string, object>(LabelRole,                        Node.Role));

                // Standard labels from this class.

                list.Add(new KeyValuePair<string, object>(LabelOpenEbsStorage, NeonHelper.ToBoolString(SystemOpenEbsStorage)));

                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskSize, ByteUnits.Parse(StorageOSDiskSize)));
                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskLocal,          NeonHelper.ToBoolString(StorageOSDiskLocal)));
                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskHDD,            NeonHelper.ToBoolString(StorageOSDiskHDD)));
                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskRedundant,      NeonHelper.ToBoolString(StorageOSDiskRedundant)));
                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskEphemeral,      NeonHelper.ToBoolString(StorageOSDiskEphemeral)));

                list.Add(new KeyValuePair<string, object>(LabelPhysicalLocation,            PhysicalLocation));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalMachine,             PhysicalMachine));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalAvailabilitytSet,    PhysicalAvailabilitySet));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalPower,               PhysicalPower));

                list.Add(new KeyValuePair<string, object>(LabelSystemDbServices,            NeonHelper.ToBoolString(SystemDbServices)));
                list.Add(new KeyValuePair<string, object>(LabelSystemRegistryServices,      NeonHelper.ToBoolString(SystemRegistryServices)));
                list.Add(new KeyValuePair<string, object>(LabelSystemIstioServices,         NeonHelper.ToBoolString(SystemIstioServices)));
                list.Add(new KeyValuePair<string, object>(LabelSystemLogServices,           NeonHelper.ToBoolString(SystemLogServices)));
                list.Add(new KeyValuePair<string, object>(LabelSystemMetricServices,        NeonHelper.ToBoolString(SystemMetricServices)));
                list.Add(new KeyValuePair<string, object>(LabelTraceServices,               NeonHelper.ToBoolString(SystemTraceServices)));
                list.Add(new KeyValuePair<string, object>(LabelSystemMinioServices,         NeonHelper.ToBoolString(SystemMinioServices)));

                return list;
            }
        }

        /// <summary>
        /// Enumerates all node labels.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<KeyValuePair<string, object>> All
        {
            get
            {
                var labels = (List<KeyValuePair<string, object>>)Standard;

                foreach (var label in Standard)
                {
                    labels.Add(new KeyValuePair<string, object>(label.Key, label.Value));
                }

                foreach (var label in Custom)
                {
                    labels.Add(new KeyValuePair<string, object>(label.Key, label.Value));
                }

                return labels;
            }
        }

        /// <summary>
        /// Validates the node labels.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            foreach (var label in Custom)
            {
                KubeHelper.ValidateKubernetesLabel("node label", label.Key, label.Value);
            }
        }
    }
}
