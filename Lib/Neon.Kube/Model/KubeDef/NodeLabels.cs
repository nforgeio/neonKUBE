//-----------------------------------------------------------------------------
// FILE:	    NodeLabels.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Diagnostics;

namespace Neon.Kube
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
        private INeonLogger     log = LogManager.Default.GetLogger<NodeLabels>();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public NodeLabels()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public NodeLabels(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null);

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
        public const string LabelDatacenter = ClusterDefinition.ReservedLabelPrefix + "cluster.datacenter";

        /// <summary>
        /// Reserved label name that identifies the cluster environment.
        /// </summary>
        public const string LabelEnvironment = ClusterDefinition.ReservedLabelPrefix + "cluster.environment";

        /// <summary>
        /// Reserved label name that identifies the node's public IP address or FQDN.
        /// </summary>
        public const string LabelPublicAddress = ClusterDefinition.ReservedLabelPrefix + "node.public_address";

        /// <summary>
        /// Reserved label name that identifies the node's private IP address.
        /// </summary>
        public const string LabelPrivateAddress = ClusterDefinition.ReservedLabelPrefix + "node.private_address";

        /// <summary>
        /// Reserved label name that identifies the node role.
        /// </summary>
        public const string LabelRole = ClusterDefinition.ReservedLabelPrefix + "node.role";

        //---------------------------------------------------------------------
        // Azure hosting related labels.

        /// <summary>
        /// Reserved label name that identifies the node's Azure VM size.
        /// </summary>
        public const string LabelAzureVmSize = ClusterDefinition.ReservedLabelPrefix + "azure.vm_size";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached storage type.
        /// </summary>
        public const string LabelAzureStorageType = ClusterDefinition.ReservedLabelPrefix + "azure.storage_type";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached drive count.
        /// </summary>
        public const string LabelAzureDriveCount = ClusterDefinition.ReservedLabelPrefix + "azure.drive_count";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached drive size in GiB.
        /// </summary>
        public const string LabelAzureDriveSizeGiB = ClusterDefinition.ReservedLabelPrefix + "azure.drive_size_gib";

        //---------------------------------------------------------------------
        // Define the node storage related labels.

        /// <summary>
        /// Reserved label name for <see cref="StorageCapacityGiB"/>.
        /// </summary>
        public const string LabelStorageCapacityGiB = ClusterDefinition.ReservedLabelPrefix + "storage.capacity_gib";

        /// <summary>
        /// Reserved label name for <see cref="StorageLocal"/>.
        /// </summary>
        public const string LabelStorageLocal = ClusterDefinition.ReservedLabelPrefix + "storage.local";

        /// <summary>
        /// Reserved label name for <see cref="StorageHDD"/>.
        /// </summary>
        public const string LabelStorageHDD = ClusterDefinition.ReservedLabelPrefix + "storage.hdd";

        /// <summary>
        /// Reserved label name for <see cref="StorageRedundant"/>.
        /// </summary>
        public const string LabelStorageRedundant = ClusterDefinition.ReservedLabelPrefix + "storage.redundant";

        /// <summary>
        /// Reserved label name for <see cref="StorageEphemeral"/>.
        /// </summary>
        public const string LabelStorageEphemeral = ClusterDefinition.ReservedLabelPrefix + "storage.ephemral";

        /// <summary>
        /// <b>io.neonkube/storage.capacity_gb</b> [<c>int</c>]: Specifies the node primary drive 
        /// storage capacity in gigabytes.
        /// </summary>
        [JsonProperty(PropertyName = "StorageCapacityGiB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "StorageCapacityGiB", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int StorageCapacityGiB { get; set; } = 0;

        /// <summary>
        /// <b>io.neonkube/storage.local</b> [<c>bool</c>]: Specifies whether the node storage is hosted
        /// on the node itself or is mounted as a remote file system or block device.  This defaults
        /// to <c>true</c> for on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageLocal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "StorageLocal", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool StorageLocal { get; set; } = true;

        /// <summary>
        /// <b>io.neonkube/storage.hdd</b> [<c>bool</c>]: Indicates that the storage is backed
        /// by a spinning drive as opposed to a SSD.  This defaults to <c>false</c> for 
        /// on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageHDD", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "StorageHDD", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageHDD { get; set; } = false;

        /// <summary>
        /// <b>io.neonkube/storage.redundant</b> [<c>bool</c>]: Indicates that the storage is redundant.  This
        /// may be implemented locally using RAID1+ or remotely using network or cloud-based file systems.
        /// This defaults to <c>false</c> for on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageRedundant", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "StorageRedundant", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageRedundant { get; set; } = false;

        /// <summary>
        /// <b>io.neonkube/storage.redundant</b> [<c>bool</c>]: Indicates that the storage is ephemeral.
        /// All data will be lost when the host is restarted.  This defaults to <c>false</c> for 
        /// on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageEphemeral", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "StorageEphemeral", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool StorageEphemeral { get; set; } = false;

        //---------------------------------------------------------------------
        // Define host compute related labels.

        /// <summary>
        /// Reserved label name for <see cref="ComputeCores"/>.
        /// </summary>
        public const string LabelComputeCores = ClusterDefinition.ReservedLabelPrefix + "compute.cores";

        /// <summary>
        /// Reserved label name for <see cref="ComputeRamMiB"/>.
        /// </summary>
        public const string LabelComputeRamMiB = ClusterDefinition.ReservedLabelPrefix + "compute.ram_mib";

        /// <summary>
        /// <b>io.neonkube/compute.cores</b> [<c>int</c>]: Specifies the number of CPU cores.
        /// This defaults to <b>0</b> for <see cref="HostingEnvironments.Machine"/>
        /// and is initialized for cloud and Hypervisor based hosting environments.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeCores", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "ComputeCores", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ComputeCores { get; set; } = 0;

        /// <summary>
        /// <b>io.neonkube/compute.ram_mb</b> [<c>int</c>]: Specifies the available RAM in
        /// megabytes.  This defaults to <b>0</b> for <see cref="HostingEnvironments.Machine"/>
        /// and is initialized for cloud and Hypervisor based hosting environments.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeRamMiB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "ComputeRamMiB", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ComputeRamMiB { get; set; } = 0;

        //---------------------------------------------------------------------
        // Define physical host labels.

        private string physicalFaultDomain = string.Empty;

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalLocation = ClusterDefinition.ReservedLabelPrefix + "physical.location";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalMachine"/>.
        /// </summary>
        public const string LabelPhysicalMachine = ClusterDefinition.ReservedLabelPrefix + "physical.machine";

        /// <summary>
        /// Reserved label name for <see cref="PhysicalFaultDomain"/>.
        /// </summary>
        public const string LabelPhysicalFaultDomain = ClusterDefinition.ReservedLabelPrefix + "physical.faultdomain";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalPower = ClusterDefinition.ReservedLabelPrefix + "physical.power";

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
        [YamlMember(Alias = "PhysicalLocation", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalLocation { get; set; } = string.Empty;

        /// <summary>
        /// <b>io.neonkube/physical.model</b> [<c>string</c>]: A free format string describing the
        /// physical server computer model (e.g. <b>Dell-PowerEdge-R220</b>).  This defaults to the <b>empty string</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PhysicalMachine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "PhysicalMachine", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalMachine { get; set; } = string.Empty;

        /// <summary>
        /// <b>io.neonkube/physical.faultdomain</b> [<c>string</c>]: A free format string 
        /// grouping the host by the possibility of underlying hardware or software failures.
        /// This defaults to the <b>empty string</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The idea here is to identify broad possible failure scenarios and to assign hosts
        /// to fault domains in such a way that a failure for one domain will be unlikely
        /// to impact the hosts in another.  These groupings can be used to spread application
        /// containers across available fault domains such that an application has a reasonable 
        /// potential to continue operating in the face of hardware or network failures.
        /// </para>
        /// <para>
        /// Fault domains will be mapped to your specific hardware and networking architecture.
        /// Here are some example scenarios:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>VMs on one machine:</b></term>
        ///     <description>
        ///     <para>
        ///     This will be a common setup for development and test where every host
        ///     node is simply a virtual machine running locally.  In this case, the
        ///     fault domain could be set to the virtual machine name such that
        ///     failures can be tested by simply stopping a VM.
        ///     </para>
        ///     <note>
        ///     If no fault domain is specified for a node, then the fault domain
        ///     will default to the node name.
        ///     </note>
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Single Rack:</b></term>
        ///     <description>
        ///     For a cluster deployed to a single rack with a shared network connection,
        ///     the fault domain will typically be the physical machine such that the 
        ///     loss of a machine can be tolerated.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Multiple Racks:</b></term>
        ///     <description>
        ///     For clusters deployed to multiple racks, each with their own network
        ///     connection, the fault domain will typically be set at the rack
        ///     level, such that the loss of a rack or its network connectivity can
        ///     be tolerated.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Advanced:</b></term>
        ///     <description>
        ///     <para>
        ///     More advanced scenarios are possible.  For example, a datacenter may
        ///     have multiple pods, floors, or buildings that each have redundant 
        ///     infrastructure such as power and networking.  You could set the fault
        ///     domain at the pod or floor level.
        ///     </para>
        ///     <para>
        ///     For clusters that span physical datacenters, you could potentially map
        ///     each datacenter to an fault domain.
        ///     </para>
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "PhysicalFaultDomain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [YamlMember(Alias = "PhysicalFaultDomain", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalFaultDomain
        {
            get { return string.IsNullOrWhiteSpace(physicalFaultDomain) ? Node.Name : physicalFaultDomain; }
            set { physicalFaultDomain = value; }
        }

        /// <summary>
        /// <b>io.neonkube/physical.power</b> [<c>string</c>]: Describes host the physical power
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
        [YamlMember(Alias = "PhysicalPower", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string PhysicalPower { get; set; } = string.Empty;       // $todo(jeff.lill): Define the format of this string for APC PDUs.

        //---------------------------------------------------------------------

        /// <summary>
        /// Custom node labels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this property to define custom host node labels.
        /// </para>
        /// <note>
        /// The <b>io.neonkube/</b> label prefix is reserved.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Custom")]
        [YamlMember(Alias = "Custom", ApplyNamingConventions = false)]
        public Dictionary<string, string> Custom { get; set; } = new Dictionary<string, string>();

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Enumerates the node labels.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<KeyValuePair<string, object>> Standard
        {
            get
            {
                // WARNING: 
                //
                // This method will need to be updated whenever new standard labels are added or changed.

                var list = new List<KeyValuePair<string, object>>(50);

                // Standard labels from the parent node definition.

                list.Add(new KeyValuePair<string, object>(LabelPublicAddress,           Node.PublicAddress));
                list.Add(new KeyValuePair<string, object>(LabelPrivateAddress,          Node.PrivateAddress));
                list.Add(new KeyValuePair<string, object>(LabelRole,                    Node.Role));

                if (Node.Azure != null)
                {
                    list.Add(new KeyValuePair<string, object>(LabelAzureVmSize,         Node.Azure.VmSize));
                    list.Add(new KeyValuePair<string, object>(LabelAzureStorageType,    Node.Azure.StorageType));
                    list.Add(new KeyValuePair<string, object>(LabelAzureDriveCount,     Node.Azure.HardDriveCount));
                    list.Add(new KeyValuePair<string, object>(LabelAzureDriveSizeGiB,   Node.Azure.HardDriveSizeGiB));
                }

                // Standard labels from this class.

                list.Add(new KeyValuePair<string, object>(LabelStorageCapacityGiB,       StorageCapacityGiB));
                list.Add(new KeyValuePair<string, object>(LabelStorageLocal,            StorageLocal));
                list.Add(new KeyValuePair<string, object>(LabelStorageHDD,              StorageHDD));
                list.Add(new KeyValuePair<string, object>(LabelStorageRedundant,        StorageRedundant));
                list.Add(new KeyValuePair<string, object>(LabelStorageEphemeral,        StorageEphemeral));

                list.Add(new KeyValuePair<string, object>(LabelComputeCores,            ComputeCores));
                list.Add(new KeyValuePair<string, object>(LabelComputeRamMiB,           ComputeRamMiB));

                list.Add(new KeyValuePair<string, object>(LabelPhysicalLocation,        PhysicalLocation));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalMachine,         PhysicalMachine));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalFaultDomain,     PhysicalFaultDomain));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalPower,           PhysicalPower));

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
                log.LogWarn(() => $"[node={Node.Name}]: [{e.GetType().Name}] parsing [{label.Key}={label.Value}");
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
                    case LabelPublicAddress:                Node.PublicAddress = label.Value; break;
                    case LabelPrivateAddress:               Node.PrivateAddress = label.Value; break;
                    case LabelRole:                         Node.Role = label.Value; break;

                    case LabelAzureVmSize:
                    case LabelAzureStorageType:
                    case LabelAzureDriveCount:
                    case LabelAzureDriveSizeGiB:

                        if (Node.Azure == null)
                        {
                            Node.Azure = new AzureNodeOptions();
                        }

                        switch (label.Key)
                        {
                            case LabelAzureVmSize:          ParseCheck(label, () => { Node.Azure.VmSize = NeonHelper.ParseEnum<AzureVmSizes>(label.Value); }); break;
                            case LabelAzureStorageType:     ParseCheck(label, () => { Node.Azure.StorageType = NeonHelper.ParseEnum<AzureStorageTypes>(label.Value); }); break;
                            case LabelAzureDriveCount:      ParseCheck(label, () => { Node.Azure.HardDriveCount = int.Parse(label.Value); }); break;
                            case LabelAzureDriveSizeGiB:    ParseCheck(label, () => { Node.Azure.HardDriveSizeGiB = int.Parse(label.Value); }); break;
                        }
                        break;

                    case LabelStorageCapacityGiB:           ParseCheck(label, () => { Node.Labels.StorageCapacityGiB = int.Parse(label.Value); }); break;
                    case LabelStorageLocal:                 Node.Labels.StorageLocal = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageHDD:                   Node.Labels.StorageHDD = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageRedundant:             Node.Labels.StorageRedundant = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageEphemeral:             Node.Labels.StorageEphemeral = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;

                    case LabelComputeCores:                 ParseCheck(label, () => { Node.Labels.ComputeCores = int.Parse(label.Value); }); break;
                    case LabelComputeRamMiB:                ParseCheck(label, () => { Node.Labels.ComputeRamMiB = int.Parse(label.Value); }); break;

                    case LabelPhysicalLocation:             Node.Labels.PhysicalLocation = label.Value; break;
                    case LabelPhysicalMachine:              Node.Labels.PhysicalMachine = label.Value;  break;
                    case LabelPhysicalFaultDomain:          Node.Labels.PhysicalFaultDomain = label.Value; break;
                    case LabelPhysicalPower:                Node.Labels.PhysicalPower = label.Value;  break;

                    case LabelDatacenter:
                    case LabelEnvironment:

                        // These labels don't currently map to node properties so
                        // we'll ignore them.

                        break;

                    default:

                        // Must be a custom label.

                        Node.Labels.Custom.Add(label.Key, label.Value);
                        break;
                }
            }
        }

        /// <summary>
        /// Copies the label properties to another instance.
        /// </summary>
        /// <param name="target">The target instance.</param>
        internal void CopyTo(NodeLabels target)
        {
            Covenant.Requires<ArgumentNullException>(target != null);

            // WARNING: 
            //
            // This method will need to be updated whenever new standard labels are added or changed.

            target.StorageCapacityGiB   = this.StorageCapacityGiB;
            target.StorageLocal         = this.StorageLocal;
            target.StorageHDD           = this.StorageHDD;
            target.StorageRedundant     = this.StorageRedundant;
            target.StorageEphemeral     = this.StorageEphemeral;

            target.ComputeCores         = this.ComputeCores;
            target.ComputeRamMiB        = this.ComputeRamMiB;

            target.PhysicalLocation     = this.PhysicalLocation;
            target.PhysicalMachine      = this.PhysicalMachine;
            target.PhysicalFaultDomain  = this.PhysicalFaultDomain;
            target.PhysicalPower        = this.PhysicalPower;

            foreach (var item in this.Custom)
            {
                target.Custom.Add(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Validates the node labels.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            // Verify that custom node label names satisfy the 
            // following criteria:
            // 
            //      1. Have an optional reverse domain prefix.
            //      2. Be at least one character long.
            //      3. Start and end with an alpha numeric character.
            //      4. Include only alpha numeric characters, dashes,
            //         underscores or dots.
            //      5. Does not have consecutive dots or dashes.

            foreach (var item in Custom)
            {
                if (item.Key.Length == 0)
                {
                    throw new ClusterDefinitionException($"Custom node label for value [{item.Value}] has no label name.");
                }

                var pSlash = item.Key.IndexOf('/');
                var domain = pSlash == -1 ? null : item.Key.Substring(0, pSlash);
                var name   = pSlash == -1 ? item.Key : item.Key.Substring(pSlash + 1);

                if (domain != null)
                {
                    if (!ClusterDefinition.DnsHostRegex.IsMatch(domain))
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
            }
        }
    }
}
