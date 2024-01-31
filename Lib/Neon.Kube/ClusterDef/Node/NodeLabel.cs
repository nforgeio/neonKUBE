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
        private ILogger log = TelemetryHub.CreateLogger<NodeLabel>();

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

        /// <summary>
        /// Reserved label name used to indicate that a node should route external traffic into the cluster.
        /// </summary>
        public const string LabelIngress = ClusterDefinition.ReservedNodePrefix + "system.ingress";

        /// <summary>
        /// Reserved label name used to indicate that a node hosts an OpenEBS cStor block device.
        /// </summary>
        public const string LabelOpenEbs = ClusterDefinition.ReservedNodePrefix + "system.openebs";

        /// <summary>
        /// <b>node.neonkube.io/openEbs.enabled</b> [<c>bool</c>]: Indicates that OpenEBS 
        /// will be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbs", Required = Required.Default)]
        [YamlMember(Alias = "openEbs", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool OpenEbs { get; set; } = false;

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
        /// Reserved label name for <see cref="LabelPhysicalMachine"/>.
        /// </summary>
        public const string LabelPhysicalMachine = ClusterDefinition.ReservedNodePrefix + "physical.machine";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalLocation = ClusterDefinition.ReservedNodePrefix + "physical.location";

        /// <summary>
        /// Reserved label name for <see cref="PhysicalAvailabilitySet"/>.
        /// </summary>
        public const string LabelPhysicalAvailabilitytSet = ClusterDefinition.ReservedNodePrefix + "physical.availability-set";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalPower = ClusterDefinition.ReservedNodePrefix + "physical.power";

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
        /// <b>node.neonkube.io/physical.model</b> [<c>string</c>]: A free format string describing the
        /// physical server computer model (e.g. <b>Dell-PowerEdge-R220</b>).  This defaults to the <b>empty string</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PhysicalMachine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "physicalMachine", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalMachine { get; set; } = string.Empty;

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

        //---------------------------------------------------------------------
        // Define the neon-system related labels.

        /// <summary>
        /// Reserved label name for core NEONKUBE system components.
        /// </summary>
        public const string LabelNeonSystem = ClusterDefinition.ReservedNodePrefix + "system";

        /// <summary>
        /// <b>node.neonkube.io/neon-system</b> [<c>bool</c>]: Indicates that general neon-system 
        /// services may be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "NeonSystem", Required = Required.Default)]
        [YamlMember(Alias = "neonSystem", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool NeonSystem { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="Istio"/>.
        /// </summary>
        public const string LabelIstio = ClusterDefinition.ReservedNodePrefix + "system.istio";

        /// <summary>
        /// <b>node.neonkube.io/istio.enabled</b> [<c>bool</c>]: Indicates that Istio 
        /// may be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Istio", Required = Required.Default)]
        [YamlMember(Alias = "istio", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Istio { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="LabelNeonSystemDb"/>.
        /// </summary>
        public const string LabelNeonSystemDb = ClusterDefinition.ReservedNodePrefix + "system.db";

        /// <summary>
        /// <b>node.neonkube.io/neon-system.db</b> [<c>bool</c>]: Indicates that the neon-system 
        /// Citus/Postgresql database may be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "NeonSystemDb", Required = Required.Default)]
        [YamlMember(Alias = "neonSystemDb", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool NeonSystemDb { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="LabelNeonSystemDb"/>.
        /// </summary>
        public const string LabelNeonSystemRegistry = ClusterDefinition.ReservedNodePrefix + "system.registry";

        /// <summary>
        /// <b>node.neonkube.io/neon-system.registry</b> [<c>bool</c>]: Indicates that the neon-system 
        /// Harbor registry may be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "NeonSystemRegistry", Required = Required.Default)]
        [YamlMember(Alias = "neonSystemRegistry", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool NeonSystemRegistry { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/system.minio-internal</b> [<c>bool</c>]: Indicates the user has specified
        /// that Minio should be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Minio", Required = Required.Default)]
        [YamlMember(Alias = "minio", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Minio { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/system.minio-internal</b> [<c>bool</c>]: Indicates that Minio
        /// will be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "MinioInternal", Required = Required.Default)]
        [YamlMember(Alias = "minioInternal", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool MinioInternal { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="Minio"/>.
        /// </summary>
        public const string LabelMinio = ClusterDefinition.ReservedNodePrefix + "system.minio";

        /// <summary>
        /// Reserved label name for <see cref="MinioInternal"/>.
        /// </summary>
        public const string LabelMinioInternal = ClusterDefinition.ReservedNodePrefix + "system.minio-internal";

        //---------------------------------------------------------------------
        // Define the logging related labels.

        /// <summary>
        /// Reserved label name for <see cref="Logs"/>.
        /// </summary>
        public const string LabelLogs = ClusterDefinition.ReservedNodePrefix + "monitor.logs";

        /// <summary>
        /// Reserved label name for <see cref="LogsInternal"/>.
        /// </summary>
        public const string LabelLogsInternal = ClusterDefinition.ReservedNodePrefix + "monitor.logs-internal";

        /// <summary>
        /// Reserved label name for <see cref="Metrics"/>.
        /// </summary>
        public const string LabelMetrics = ClusterDefinition.ReservedNodePrefix + "monitor.metrics";

        /// <summary>
        /// Reserved label name for <see cref="MetricsInternal"/>.
        /// </summary>
        public const string LabelMetricsInternal = ClusterDefinition.ReservedNodePrefix + "monitor.metrics-internal";

        /// <summary>
        /// Reserved label name for <see cref="Traces"/>.
        /// </summary>
        public const string LabelTraces = ClusterDefinition.ReservedNodePrefix + "monitor.traces";

        /// <summary>
        /// Reserved label name for <see cref="TracesInternal"/>.
        /// </summary>
        public const string LabelTracesInternal = ClusterDefinition.ReservedNodePrefix + "monitor.traces-internal";

        /// <summary>
        /// <b>node.neonkube.io/monitor.logs</b> [<c>bool</c>]: Indicates the user has 
        /// specified that Loki logging should be deployed to the labeled node.  This 
        /// defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Logs", Required = Required.Default)]
        [YamlMember(Alias = "logs", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Logs { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/monitor.logs-internal</b> [<c>bool</c>]: Indicates that Liko
        /// logging will be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogsInternal", Required = Required.Default)]
        [YamlMember(Alias = "logsInternal", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool LogsInternal { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/monitor.metrics</b> [<c>bool</c>]: Indicates the user has specified
        /// that Mimir metrics should be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Metrics", Required = Required.Default)]
        [YamlMember(Alias = "metrics", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Metrics { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/monitor.metrics-internal</b> [<c>bool</c>]: Indicates that Mirmir
        /// metrics will be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "MetricsInternal", Required = Required.Default)]
        [YamlMember(Alias = "metricsInternal", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool MetricsInternal { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/monitor.traces</b> [<c>bool</c>]: Indicates that the user has specified
        /// that Tempo traces should be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Traces", Required = Required.Default)]
        [YamlMember(Alias = "traces", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Traces { get; set; } = false;

        /// <summary>
        /// <b>node.neonkube.io/monitor.traces-internal</b> [<c>bool</c>]: Indicates that Tempo
        /// traces will be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "TracesInternal", Required = Required.Default)]
        [YamlMember(Alias = "tracesInternal", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool TracesInternal { get; set; } = false;

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
                // WARNING: 
                //
                // This method will need to be updated whenever standard labels are added or changed.

                var list = new List<KeyValuePair<string, object>>(50);

                // Standard labels from the parent node definition.

                list.Add(new KeyValuePair<string, object>(LabelAddress,                     Node.Address));
                list.Add(new KeyValuePair<string, object>(LabelRole,                        Node.Role));
                list.Add(new KeyValuePair<string, object>(LabelIngress,                     NeonHelper.ToBoolString(Node.Ingress)));
                list.Add(new KeyValuePair<string, object>(LabelOpenEbs,                     NeonHelper.ToBoolString(Node.OpenEbsStorage)));

                // Standard labels from this class.

                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskSize,           ByteUnits.Parse(StorageOSDiskSize)));
                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskLocal,          NeonHelper.ToBoolString(StorageOSDiskLocal)));
                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskHDD,            NeonHelper.ToBoolString(StorageOSDiskHDD)));
                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskRedundant,      NeonHelper.ToBoolString(StorageOSDiskRedundant)));
                list.Add(new KeyValuePair<string, object>(LabelStorageOSDiskEphemeral,      NeonHelper.ToBoolString(StorageOSDiskEphemeral)));

                list.Add(new KeyValuePair<string, object>(LabelPhysicalLocation,            PhysicalLocation));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalMachine,             PhysicalMachine));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalAvailabilitytSet,    PhysicalAvailabilitySet));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalPower,               PhysicalPower));

                list.Add(new KeyValuePair<string, object>(LabelNeonSystemDb,                NeonHelper.ToBoolString(NeonSystemDb)));
                list.Add(new KeyValuePair<string, object>(LabelNeonSystemRegistry,          NeonHelper.ToBoolString(NeonSystemRegistry)));

                list.Add(new KeyValuePair<string, object>(LabelIstio,                       NeonHelper.ToBoolString(Istio)));

                list.Add(new KeyValuePair<string, object>(LabelLogs,                        NeonHelper.ToBoolString(Logs)));
                list.Add(new KeyValuePair<string, object>(LabelLogsInternal,                NeonHelper.ToBoolString(LogsInternal)));

                list.Add(new KeyValuePair<string, object>(LabelMetrics,                     NeonHelper.ToBoolString(Metrics)));
                list.Add(new KeyValuePair<string, object>(LabelMetricsInternal,             NeonHelper.ToBoolString(MetricsInternal)));

                list.Add(new KeyValuePair<string, object>(LabelTraces,                      NeonHelper.ToBoolString(Traces)));
                list.Add(new KeyValuePair<string, object>(LabelTracesInternal,              NeonHelper.ToBoolString(TracesInternal)));

                list.Add(new KeyValuePair<string, object>(LabelMinio,                       NeonHelper.ToBoolString(Minio)));
                list.Add(new KeyValuePair<string, object>(LabelMinioInternal,               NeonHelper.ToBoolString(MinioInternal)));

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

                foreach (var label in Custom)
                {
                    labels.Add(new KeyValuePair<string, object>(label.Key, label.Value));
                }

                return labels;
            }
        }

        /// <summary>
        /// Logs a warning if a label field parse action fails.
        /// </summary>
        /// <param name="label">The label being parsed.</param>
        /// <param name="parseAction">The parse action.</param>
        private void ParseCheck(KeyValuePair<string, string> label, Action parseAction)
        {
            try
            {
                parseAction();
            }
            catch (Exception e)
            {
                log.LogWarningEx(() => $"[node={Node.Name}]: [{e.GetType().Name}] parsing [{label.Key}={label.Value}");
            }
        }

        /// <summary>
        /// Parses a dictionary of name/value labels by setting the appropriate
        /// properties of the parent node.
        /// </summary>
        /// <param name="labels">The label dictionary.</param>
        internal void Parse(Dictionary<string, string> labels)
        {
            // WARNING: 
            //
            // This method will need to be updated whenever new standard labels are added or changed.

            foreach (var label in labels)
            {
                switch (label.Key)
                {
                    case LabelAddress:                      Node.Address = label.Value; break;
                    case LabelRole:                         Node.Role    = label.Value; break;
                    case LabelIngress:                      ParseCheck(label, () => { Node.Ingress = NeonHelper.ParseBool(label.Value); }); break; 
                    case LabelOpenEbs:                      ParseCheck(label, () => { Node.OpenEbsStorage = NeonHelper.ParseBool(label.Value); }); break; 

                    case LabelStorageOSDiskSize:                  ParseCheck(label, () => { Node.Labels.StorageOSDiskSize = ByteUnits.Parse(label.Value).ToString(); }); break;
                    case LabelStorageOSDiskLocal:                 Node.Labels.StorageOSDiskLocal            = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageOSDiskHDD:                   Node.Labels.StorageOSDiskHDD              = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageOSDiskRedundant:             Node.Labels.StorageOSDiskRedundant        = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageOSDiskEphemeral:             Node.Labels.StorageOSDiskEphemeral        = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;

                    case LabelPhysicalMachine:              Node.Labels.PhysicalMachine         = label.Value; break;
                    case LabelPhysicalLocation:             Node.Labels.PhysicalLocation        = label.Value; break;
                    case LabelPhysicalAvailabilitytSet:     Node.Labels.PhysicalAvailabilitySet = label.Value; break;
                    case LabelPhysicalPower:                Node.Labels.PhysicalPower           = label.Value; break;

                    default:

                        // Must be a custom label.

                        Node.Labels.Custom.Add(label.Key, label.Value);
                        break;
                }
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
