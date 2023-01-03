//-----------------------------------------------------------------------------
// FILE:	    NodeLabels.cs
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
    /// are prefixed with <b>"io.neonkube/..."</b>.  You should follow this convention 
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
    public class NodeLabels
    {
        private ILogger log = TelemetryHub.CreateLogger<NodeLabels>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public NodeLabels()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="node">The node definition.</param>
        public NodeLabels(NodeDefinition node)
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
        /// Reserved label name that identifies the datacenter.
        /// </summary>
        public const string LabelDatacenter = ClusterDefinition.ReservedPrefix + "cluster.datacenter";

        /// <summary>
        /// Reserved label name that identifies the cluster environment.
        /// </summary>
        public const string LabelEnvironment = ClusterDefinition.ReservedPrefix + "cluster.environment";

        /// <summary>
        /// Reserved label name that identifies the node's private IP address.
        /// </summary>
        public const string LabelAddress = ClusterDefinition.ReservedPrefix + "node.private_address";

        /// <summary>
        /// Reserved label name that identifies the node role.
        /// </summary>
        public const string LabelRole = ClusterDefinition.ReservedPrefix + "node.role";

        /// <summary>
        /// Reserved label name used to indicate that a node should route external traffic into the cluster.
        /// </summary>
        public const string LabelIngress = ClusterDefinition.ReservedPrefix + "node.ingress";

        /// <summary>
        /// Reserved label name used to indicate that a node hosts an OpenEBS cStor block device.
        /// </summary>
        public const string LabelOpenEbs = ClusterDefinition.ReservedPrefix + "node.openebs";

        /// <summary>
        /// <b>neonkube.io/openEbs.enabled</b> [<c>bool</c>]: Indicates that OpenEBS 
        /// will be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbs", Required = Required.Default)]
        [YamlMember(Alias = "openEbs", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool OpenEbs { get; set; } = false;

        //---------------------------------------------------------------------
        // Azure hosting related labels.

        /// <summary>
        /// Reserved label name that identifies the node's Azure VM size.
        /// </summary>
        public const string LabelAzureVmSize = ClusterDefinition.ReservedPrefix + "azure.vm_size";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached storage type.
        /// </summary>
        public const string LabelAzureStorageType = ClusterDefinition.ReservedPrefix + "azure.storage-type";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached drive size.
        /// </summary>
        public const string LabelAzureDriveSize = ClusterDefinition.ReservedPrefix + "azure.drive-size";

        //---------------------------------------------------------------------
        // Define the node storage related labels.

        /// <summary>
        /// Reserved label name for <see cref="StorageSize"/>.
        /// </summary>
        public const string LabelStorageSize = ClusterDefinition.ReservedPrefix + "storage.size";

        /// <summary>
        /// Reserved label name for <see cref="StorageLocal"/>.
        /// </summary>
        public const string LabelStorageLocal = ClusterDefinition.ReservedPrefix + "storage.local";

        /// <summary>
        /// Reserved label name for <see cref="StorageHDD"/>.
        /// </summary>
        public const string LabelStorageHDD = ClusterDefinition.ReservedPrefix + "storage.hdd";

        /// <summary>
        /// Reserved label name for <see cref="StorageRedundant"/>.
        /// </summary>
        public const string LabelStorageRedundant = ClusterDefinition.ReservedPrefix + "storage.redundant";

        /// <summary>
        /// Reserved label name for <see cref="StorageEphemeral"/>.
        /// </summary>
        public const string LabelStorageEphemeral = ClusterDefinition.ReservedPrefix + "storage.ephemral";

        /// <summary>
        /// <b>io.neonkube/storage.size</b> [<c>string</c>]: Specifies the node OS drive 
        /// storage capacity in bytes.
        /// </summary>
        [JsonProperty(PropertyName = "StorageSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "StorageSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string StorageSize { get; set; }

        /// <summary>
        /// <b>io.neonkube/storage.local</b> [<c>bool</c>]: Specifies whether the node storage is hosted
        /// on the node itself or is mounted as a remote file system or block device.  This defaults
        /// to <c>true</c> for on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageLocal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageLocal", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool StorageLocal { get; set; } = true;

        /// <summary>
        /// <b>io.neonkube/storage.hdd</b> [<c>bool</c>]: Indicates that the storage is backed
        /// by a spinning drive as opposed to a SSD.  This defaults to <c>false</c> for 
        /// on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageHDD", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageHDD", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageHDD { get; set; } = false;

        /// <summary>
        /// <b>io.neonkube/storage.redundant</b> [<c>bool</c>]: Indicates that the storage is redundant.  This
        /// may be implemented locally using RAID1+ or remotely using network or cloud-based file systems.
        /// This defaults to <c>false</c> for on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageRedundant", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageRedundant", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageRedundant { get; set; } = false;

        /// <summary>
        /// <b>io.neonkube/storage.redundant</b> [<c>bool</c>]: Indicates that the storage is ephemeral.
        /// All data will be lost when the host is restarted.  This defaults to <c>false</c> for 
        /// on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageEphemeral", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "storageEphemeral", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageEphemeral { get; set; } = false;

        //---------------------------------------------------------------------
        // Define host compute related labels.

        /// <summary>
        /// Reserved label name for <see cref="ComputeCores"/>.
        /// </summary>
        public const string LabelComputeCores = ClusterDefinition.ReservedPrefix + "compute.cores";

        /// <summary>
        /// Reserved label name for <see cref="ComputeRam"/>.
        /// </summary>
        public const string LabelComputeRamMiB = ClusterDefinition.ReservedPrefix + "compute.ram-mib";

        /// <summary>
        /// <b>io.neonkube/compute.cores</b> [<c>int</c>]: Specifies the number of CPU cores.
        /// This defaults to <b>0</b> for <see cref="HostingEnvironment.BareMetal"/>
        /// and is initialized for cloud and Hypervisor based hosting environments.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeCores", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "ComputeCores", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ComputeCores { get; set; } = 0;

        /// <summary>
        /// <b>io.neonkube/compute.ram_mb</b> [<c>int</c>]: Specifies the available RAM in
        /// megabytes.  This defaults to <b>0</b> for <see cref="HostingEnvironment.BareMetal"/>
        /// and is initialized for cloud and Hypervisor based hosting environments.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeRamMiB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "ComputeRamMiB", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ComputeRam { get; set; } = 0;

        //---------------------------------------------------------------------
        // Define physical host labels.

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalMachine"/>.
        /// </summary>
        public const string LabelPhysicalMachine = ClusterDefinition.ReservedPrefix + "physical.machine";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalLocation = ClusterDefinition.ReservedPrefix + "physical.location";

        /// <summary>
        /// Reserved label name for <see cref="PhysicalAvailabilitySet"/>.
        /// </summary>
        public const string LabelPhysicalAvailabilitytSet = ClusterDefinition.ReservedPrefix + "physical.availability-set";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalPower = ClusterDefinition.ReservedPrefix + "physical.power";

        /// <summary>
        /// <b>io.neonkube/physical.location</b> [<c>string</c>]: A free format string describing the
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
        /// <b>io.neonkube/physical.model</b> [<c>string</c>]: A free format string describing the
        /// physical server computer model (e.g. <b>Dell-PowerEdge-R220</b>).  This defaults to the <b>empty string</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PhysicalMachine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "physicalMachine", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalMachine { get; set; } = string.Empty;

        /// <summary>
        /// <para>
        /// <b>io.neonkube/physical.availability-set</b> [<c>string</c>]: Indicates that 
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
        /// the VMs to collectively recover before rebooting the next VM.  neonKUBE
        /// will provision node VMs that have the same <see cref="PhysicalAvailabilitySet"/> 
        /// into the same cloud availability set (for clouds that support this).
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PhysicalAvailabilitySet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "physicalAvailabilitySet", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PhysicalAvailabilitySet { get; set; } = null;

        /// <summary>
        /// <b>io.neonkube/physical.power</b> [<c>string</c>]: Describes the physical power
        /// to the server may be controlled.  This defaults to the <b>empty string</b>.
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
        // Define the K8s related labels.

        /// <summary>
        /// Reserved label name for <see cref="Istio"/>.
        /// </summary>
        public const string LabelIstio = ClusterDefinition.ReservedPrefix + "istio";

        /// <summary>
        /// <b>neonkube.io/istio.enabled</b> [<c>bool</c>]: Indicates that Istio 
        /// will be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Istio", Required = Required.Default)]
        [YamlMember(Alias = "istio", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Istio { get; set; } = false;

        //---------------------------------------------------------------------
        // Define the neon-system related labels.

        /// <summary>
        /// Reserved label name for core neonKUBE system components.
        /// </summary>
        public const string LabelNeonSystem = ClusterDefinition.ReservedPrefix + "neon-system";

        /// <summary>
        /// <b>neonkube.io/neon-system</b> [<c>bool</c>]: Indicates that general neon-system 
        /// services may be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "NeonSystem", Required = Required.Default)]
        [YamlMember(Alias = "neonSystem", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool NeonSystem { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="LabelNeonSystemDb"/>.
        /// </summary>
        public const string LabelNeonSystemDb = ClusterDefinition.ReservedPrefix + "neon-system.db";

        /// <summary>
        /// <b>neonkube.io/neon-system.db</b> [<c>bool</c>]: Indicates that the neon-system 
        /// Citus/Postgresql database may be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "NeonSystemDb", Required = Required.Default)]
        [YamlMember(Alias = "neonSystemDb", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool NeonSystemDb { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="LabelNeonSystemDb"/>.
        /// </summary>
        public const string LabelNeonSystemRegistry = ClusterDefinition.ReservedPrefix + "neon-system.registry";

        /// <summary>
        /// <b>neonkube.io/neon-system.registry</b> [<c>bool</c>]: Indicates that the neon-system 
        /// Harbor registry may be deployed to this node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "NeonSystemRegistry", Required = Required.Default)]
        [YamlMember(Alias = "neonSystemRegistry", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool NeonSystemRegistry { get; set; } = false;

        /// <summary>
        /// <b>neonkube.io/system.minio-internal</b> [<c>bool</c>]: Indicates the user has specified
        /// that Minio should be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Minio", Required = Required.Default)]
        [YamlMember(Alias = "minio", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Minio { get; set; } = false;

        /// <summary>
        /// <b>neonkube.io/system.minio-internal</b> [<c>bool</c>]: Indicates that Minio
        /// will be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "MinioInternal", Required = Required.Default)]
        [YamlMember(Alias = "minioInternal", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool MinioInternal { get; set; } = false;

        /// <summary>
        /// Reserved label name for <see cref="Minio"/>.
        /// </summary>
        public const string LabelMinio = ClusterDefinition.ReservedPrefix + "system.minio";

        /// <summary>
        /// Reserved label name for <see cref="MinioInternal"/>.
        /// </summary>
        public const string LabelMinioInternal = ClusterDefinition.ReservedPrefix + "system.minio-internal";

        //---------------------------------------------------------------------
        // Define the logging related labels.

        /// <summary>
        /// Reserved label name for <see cref="Logs"/>.
        /// </summary>
        public const string LabelLogs = ClusterDefinition.ReservedPrefix + "monitor.logs";

        /// <summary>
        /// Reserved label name for <see cref="LogsInternal"/>.
        /// </summary>
        public const string LabelLogsInternal = ClusterDefinition.ReservedPrefix + "monitor.logs-internal";

        /// <summary>
        /// Reserved label name for <see cref="Metrics"/>.
        /// </summary>
        public const string LabelMetrics = ClusterDefinition.ReservedPrefix + "monitor.metrics";

        /// <summary>
        /// Reserved label name for <see cref="MetricsInternal"/>.
        /// </summary>
        public const string LabelMetricsInternal = ClusterDefinition.ReservedPrefix + "monitor.metrics-internal";

        /// <summary>
        /// Reserved label name for <see cref="Traces"/>.
        /// </summary>
        public const string LabelTraces = ClusterDefinition.ReservedPrefix + "monitor.traces";

        /// <summary>
        /// Reserved label name for <see cref="TracesInternal"/>.
        /// </summary>
        public const string LabelTracesInternal = ClusterDefinition.ReservedPrefix + "monitor.traces-internal";

        /// <summary>
        /// <b>neonkube.io/monitor.logs</b> [<c>bool</c>]: Indicates the user has 
        /// specified that Loki logging should be deployed to the labeled node.  This 
        /// defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Logs", Required = Required.Default)]
        [YamlMember(Alias = "logs", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Logs { get; set; } = false;

        /// <summary>
        /// <b>neonkube.io/monitor.logs-internal</b> [<c>bool</c>]: Indicates that Liko
        /// logging will be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogsInternal", Required = Required.Default)]
        [YamlMember(Alias = "logsInternal", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool LogsInternal { get; set; } = false;

        /// <summary>
        /// <b>neonkube.io/monitor.metrics</b> [<c>bool</c>]: Indicates the user has specified
        /// that Mimir metrics should be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Metrics", Required = Required.Default)]
        [YamlMember(Alias = "metrics", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Metrics { get; set; } = false;

        /// <summary>
        /// <b>neonkube.io/monitor.metrics-internal</b> [<c>bool</c>]: Indicates that Mirmir
        /// metrics will be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "MetricsInternal", Required = Required.Default)]
        [YamlMember(Alias = "metricsInternal", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool MetricsInternal { get; set; } = false;

        /// <summary>
        /// <b>neonkube.io/monitor.traces</b> [<c>bool</c>]: Indicates the user has specified
        /// that Tempo traces should be deployed to the labeled node.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Traces", Required = Required.Default)]
        [YamlMember(Alias = "traces", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Traces { get; set; } = false;

        /// <summary>
        /// <b>neonkube.io/monitor.traces-internal</b> [<c>bool</c>]: Indicates that Tempo
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
        /// The <b>io.neonkube/</b> label prefix is reserved.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Custom")]
        [YamlMember(Alias = "custom", ApplyNamingConventions = false)]
        public Dictionary<string, string> Custom { get; set; } = new Dictionary<string, string>();

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Enumerates the standard neonKUBE node labels.
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
                list.Add(new KeyValuePair<string, object>(LabelIngress,                     Node.Ingress));
                list.Add(new KeyValuePair<string, object>(LabelOpenEbs,                     Node.OpenEbsStorage));

                if (Node.Azure != null)
                {
                    list.Add(new KeyValuePair<string, object>(LabelAzureVmSize,             Node.Azure.VmSize));
                    list.Add(new KeyValuePair<string, object>(LabelAzureStorageType,        Node.Azure.StorageType));
                    list.Add(new KeyValuePair<string, object>(LabelAzureDriveSize,          ByteUnits.Parse(Node.Azure.DiskSize)));
                }

                // Standard labels from this class.

                list.Add(new KeyValuePair<string, object>(LabelStorageSize,                 ByteUnits.Parse(StorageSize)));
                list.Add(new KeyValuePair<string, object>(LabelStorageLocal,                StorageLocal));
                list.Add(new KeyValuePair<string, object>(LabelStorageHDD,                  NeonHelper.ToBoolString(StorageHDD)));
                list.Add(new KeyValuePair<string, object>(LabelStorageRedundant,            NeonHelper.ToBoolString(StorageRedundant)));
                list.Add(new KeyValuePair<string, object>(LabelStorageEphemeral,            NeonHelper.ToBoolString(StorageEphemeral)));

                list.Add(new KeyValuePair<string, object>(LabelComputeCores,                ComputeCores));
                list.Add(new KeyValuePair<string, object>(LabelComputeRamMiB,               ComputeRam));

                list.Add(new KeyValuePair<string, object>(LabelPhysicalLocation,            PhysicalLocation));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalMachine,             PhysicalMachine));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalAvailabilitytSet,    PhysicalAvailabilitySet));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalPower,               PhysicalPower));

                list.Add(new KeyValuePair<string, object>(LabelNeonSystem,                  NeonHelper.ToBoolString(NeonSystem)));
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

                    case LabelAzureVmSize:
                    case LabelAzureStorageType:
                    case LabelAzureDriveSize:

                        if (Node.Azure == null)
                        {
                            Node.Azure = new AzureNodeOptions();
                        }

                        switch (label.Key)
                        {
                            case LabelAzureVmSize:          Node.Azure.VmSize   = label.Value; break;
                            case LabelAzureDriveSize:       Node.Azure.DiskSize = label.Value; break;
                            case LabelAzureStorageType:     ParseCheck(label, () => { Node.Azure.StorageType = NeonHelper.ParseEnum<AzureStorageType>(label.Value); }); break;
                        }
                        break;

                    case LabelStorageSize:                  ParseCheck(label, () => { Node.Labels.StorageSize = ByteUnits.Parse(label.Value).ToString(); }); break;
                    case LabelStorageLocal:                 Node.Labels.StorageLocal            = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageHDD:                   Node.Labels.StorageHDD              = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageRedundant:             Node.Labels.StorageRedundant        = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageEphemeral:             Node.Labels.StorageEphemeral        = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;

                    case LabelComputeCores:                 ParseCheck(label, () => { Node.Labels.ComputeCores = int.Parse(label.Value); }); break;
                    case LabelComputeRamMiB:                ParseCheck(label, () => { Node.Labels.ComputeRam = int.Parse(label.Value); }); break;

                    case LabelPhysicalMachine:              Node.Labels.PhysicalMachine         = label.Value; break;
                    case LabelPhysicalLocation:             Node.Labels.PhysicalLocation        = label.Value; break;
                    case LabelPhysicalAvailabilitytSet:     Node.Labels.PhysicalAvailabilitySet = label.Value; break;
                    case LabelPhysicalPower:                Node.Labels.PhysicalPower           = label.Value; break;

                    case LabelDatacenter:
                    case LabelEnvironment:

                        // These labels don't currently map to node properties so we'll ignore them.

                        break;

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

            // Verify that custom node label names and values satisfy the 
            // following criteria:
            // 
            // NAMES:
            //
            //      1. Have an optional reverse domain prefix.
            //      2. Be at least one character long.
            //      3. Start and end with an alpha numeric character.
            //      4. Include only alpha numeric characters, dashes,
            //         underscores or dots.
            //      5. Does not have consecutive dots or dashes.
            //
            // VALUES:
            //
            //      1. Must start or end with an alphnumeric character.
            //      2. May include alphanumerics, dashes, underscores or dots
            //         between the beginning and ending characters.
            //      3. Values can be empty.
            //      4. Maximum length is 63 characters.

            foreach (var item in Custom)
            {
                if (item.Key.Length == 0)
                {
                    throw new ClusterDefinitionException($"Custom node label for value [{item.Value}] has no label name.");
                }

                var pSlash = item.Key.IndexOf('/');
                var domain = pSlash == -1 ? null : item.Key.Substring(0, pSlash);
                var name   = pSlash == -1 ? item.Key : item.Key.Substring(pSlash + 1);
                var value  = item.Value;

                // Validate the NAME:

                if (domain != null)
                {
                    if (!NetHelper.IsValidHost(domain))
                    {
                        throw new ClusterDefinitionException($"Custom node label [{item.Key}] has an invalid reverse domain prefix.");
                    }

                    if (domain.Length > 253)
                    {
                        throw new ClusterDefinitionException($"Custom node label [{item.Key}] has a reverse domain prefix that's longer than 253 characters.");
                    }
                }

                if (name.Length == 0)
                {
                    throw new ClusterDefinitionException($"Custom node label name in [{item.Key}] is empty.");
                }
                else if (name.Contains(".."))
                {
                    throw new ClusterDefinitionException($"Custom node label name in [{item.Key}] has consecutive dots.");
                }
                else if (name.Contains("--"))
                {
                    throw new ClusterDefinitionException($"Custom node label name in [{item.Key}] has consecutive dashes.");
                }
                else if (name.Contains("__"))
                {
                    throw new ClusterDefinitionException($"Custom node label name in [{item.Key}] has consecutive underscores.");
                }
                else if (!char.IsLetterOrDigit(name.First()))
                {
                    throw new ClusterDefinitionException($"Custom node label name in [{item.Key}] does not begin with a letter or digit.");
                }
                else if (!char.IsLetterOrDigit(name.Last()))
                {
                    throw new ClusterDefinitionException($"Custom node label name in [{item.Key}] does not end with a letter or digit.");
                }

                foreach (var ch in name)
                {
                    if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_')
                    {
                        continue;
                    }

                    throw new ClusterDefinitionException($"Custom node label name in [{item.Key}] has an illegal character.  Only letters, digits, dashs, underscores and dots are allowed.");
                }

                // Validate the VALUE:

                if (value == string.Empty)
                {
                    continue;
                }

                if (!char.IsLetterOrDigit(value.First()) || !char.IsLetterOrDigit(value.First()))
                {
                    throw new ClusterDefinitionException($"Custom node label value in [{item.Key}={item.Value}] has an illegal value.  Values must start and end with a letter or digit.");
                }

                foreach (var ch in value)
                {
                    if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_')
                    {
                        continue;
                    }

                    throw new ClusterDefinitionException($"Custom node label value in [{item.Key}={item.Value}] has an illegal character.  Only letters, digits, dashs, underscores and dots are allowed.");
                }

                if (value.Length > 63)
                {
                    throw new ClusterDefinitionException($"Custom node label value in [{item.Key}={item.Value}] is too long.  Values can have a maximum of 63 characters.");
                }
            }
        }
    }
}
