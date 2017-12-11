//-----------------------------------------------------------------------------
// FILE:	    NodeLabels.cs
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
using Neon.Diagnostics;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the standard neonCLUSTER and custom labels to be assigned to 
    /// a Docker node.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Labels are name/value properties that can be assigned to the Docker daemon
    /// managing each host node.  These labels can be used by Swarm as container
    /// scheduling criteria.
    /// </para>
    /// <para>
    /// By convention, label names should use a reverse domain name form using a
    /// DNS domain you control.  For example, neonCLUSTER related labels are prefixed
    /// with <b>"io.neon."</b>.  You should follow this convention for any
    /// custom labels you define.
    /// </para>
    /// <note>
    /// Docker reserves the use of labels without dots for itself.
    /// </note>
    /// <para>
    /// Label names must begin and end with a letter or digit and may include
    /// letters, digits, dashes and dots within.  Dots or dashes must not appear
    /// consecutively.
    /// </para>
    /// <note>
    /// Whitespace is not allowed in label values.  This was a bit of a surprise
    /// since Docker supports double quoting, but there it is.
    /// </note>
    /// <para>
    /// This class exposes several built-in neonCLUSTER properties.  You can use
    /// the <see cref="Custom"/> dictionary to add your own labels.
    /// </para>
    /// </remarks>
    public class NodeLabels
    {
        private INeonLogger     log = LogManager.Default.GetLogger<NodeLabels>();
        private NodeDefinition  node;    // The parent node

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parentNode">The parent node.</param>
        public NodeLabels(NodeDefinition parentNode)
        {
            Covenant.Requires<ArgumentNullException>(parentNode != null);

            this.node = parentNode;
        }

        //---------------------------------------------------------------------
        // Define global cluster and node definition labels.

        /// <summary>
        /// Reserved label name that identifies the datacenter.
        /// </summary>
        public const string LabelDatacenter = ClusterDefinition.ReservedLabelPrefix + ".cluster.datacenter";

        /// <summary>
        /// Reserved label name that identifies the cluster environment.
        /// </summary>
        public const string LabelEnvironment = ClusterDefinition.ReservedLabelPrefix + ".cluster.environment";

        /// <summary>
        /// Reserved label name that identifies the node's public IP address or FQDN.
        /// </summary>
        public const string LabelPublicAddress = ClusterDefinition.ReservedLabelPrefix + ".node.public_address";

        /// <summary>
        /// Reserved label name that identifies the node's private IP address.
        /// </summary>
        public const string LabelPrivateAddress = ClusterDefinition.ReservedLabelPrefix + ".node.private_address";

        /// <summary>
        /// Reserved label name that identifies the node role.
        /// </summary>
        public const string LabelRole = ClusterDefinition.ReservedLabelPrefix + ".node.role";

        /// <summary>
        /// Reserved label name that identifies the frontend port to be used to connect
        /// to a manager's VPN server (if it's hosting a VPN server).
        /// </summary>
        public const string LabelVpnFrontendPort = ClusterDefinition.ReservedLabelPrefix + ".node.vpn_frontend_port";

        /// <summary>
        /// Reserved label name that identifies a manager node's VPN return address 
        /// (if it's hosting a VPN server).
        /// </summary>
        public const string LabelVpnReturnAddress = ClusterDefinition.ReservedLabelPrefix + ".node.vpn_return_address";

        /// <summary>
        /// Reserved label name that identifies a manager node's VPN return subnet 
        /// (if it's hosting a VPN server).
        /// </summary>
        public const string LabelVpnReturnSubnet = ClusterDefinition.ReservedLabelPrefix + ".node.vpn_return_subnet";

        //---------------------------------------------------------------------
        // Azure hosting related labels.

        /// <summary>
        /// Reserved label name that identifies the node's Azure VM size.
        /// </summary>
        public const string LabelAzureVmSize = ClusterDefinition.ReservedLabelPrefix + ".azure.vm_size";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached storage type.
        /// </summary>
        public const string LabelAzureStorageType = ClusterDefinition.ReservedLabelPrefix + ".azure.storage_type";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached drive count.
        /// </summary>
        public const string LabelAzureDriveCount = ClusterDefinition.ReservedLabelPrefix + ".azure.drive_count";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached drive size in GB.
        /// </summary>
        public const string LabelAzureDriveSizeGB = ClusterDefinition.ReservedLabelPrefix + ".azure.drive_size_gb";

        //---------------------------------------------------------------------
        // Define the node storage related labels.

        /// <summary>
        /// Reserved label name for <see cref="StorageCapacityGB"/>.
        /// </summary>
        public const string LabelStorageCapacityGB = ClusterDefinition.ReservedLabelPrefix + ".storage.capacity_gb";

        /// <summary>
        /// Reserved label name for <see cref="StorageLocal"/>.
        /// </summary>
        public const string LabelStorageLocal = ClusterDefinition.ReservedLabelPrefix + ".storage.local";

        /// <summary>
        /// Reserved label name for <see cref="StorageSSD"/>.
        /// </summary>
        public const string LabelStorageSSD = ClusterDefinition.ReservedLabelPrefix + ".storage.ssd";

        /// <summary>
        /// Reserved label name for <see cref="StorageRedundant"/>.
        /// </summary>
        public const string LabelStorageRedundant = ClusterDefinition.ReservedLabelPrefix + ".storage.redundant";

        /// <summary>
        /// Reserved label name for <see cref="StorageEphemeral"/>.
        /// </summary>
        public const string LabelStorageEphemeral = ClusterDefinition.ReservedLabelPrefix + ".storage.ephemral";

        /// <summary>
        /// <b>io.neon.storage.capacity</b> [<c>int</c>]: Specifies the node storage capacity
        /// in gigabytes.  This defaults to <b>0</b> for on-premise clusters and is computed for
        /// cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageCapacityGB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(0)]
        public int StorageCapacityGB { get; set; } = 0;

        /// <summary>
        /// <b>io.neon.storage.local</b> [<c>bool</c>]: Specifies whether the node storage is hosted
        /// on the node itself or is mounted as a remote file system or block device.  This defaults
        /// to <c>true</c> for on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageLocal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(true)]
        public bool StorageLocal { get; set; } = true;

        /// <summary>
        /// <b>io.neon.storage.ssd</b> [<c>bool</c>]: Indicates that the storage is backed
        /// by SSDs as opposed to rotating hard drive.  This defaults to <c>false</c> for 
        /// on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageSSD", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool StorageSSD { get; set; } = false;

        /// <summary>
        /// <b>io.neon.storage.redundant</b> [<c>bool</c>]: Indicates that the storage is redundant.  This
        /// may be implemented locally using RAID1+ or remotely using network or cloud-based file systems.
        /// This defaults to <c>false</c> for on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageRedundant", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool StorageRedundant { get; set; } = false;

        /// <summary>
        /// <b>io.neon.storage.redundant</b> [<c>bool</c>]: Indicates that the storage is ephemeral.
        /// All data will be lost when the host is restarted.  This defaults to <c>false</c> for 
        /// on-premise clusters and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageEphemeral", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool StorageEphemeral { get; set; } = false;

        //---------------------------------------------------------------------
        // Define host compute related labels.

        /// <summary>
        /// Reserved label name for <see cref="ComputeCores"/>.
        /// </summary>
        public const string LabelComputeCores = ClusterDefinition.ReservedLabelPrefix + ".compute.cores";

        /// <summary>
        /// Reserved label name for <see cref="ComputeArchitecture"/>.
        /// </summary>
        public const string LabelComputeArchitecture = ClusterDefinition.ReservedLabelPrefix + ".compute.architecture";

        /// <summary>
        /// Reserved label name for <see cref="ComputeRamMB"/>.
        /// </summary>
        public const string LabelComputeRamMB = ClusterDefinition.ReservedLabelPrefix + ".compute.ram_mb";

        /// <summary>
        /// Reserved label name for <see cref="ComputeSwap"/>.
        /// </summary>
        public const string LabelComputeSwap = ClusterDefinition.ReservedLabelPrefix + ".compute.swap";

        /// <summary>
        /// <b>io.neon.compute.cores</b> [<c>int</c>]: Specifies the number of CPU cores.
        /// This defaults to <b>zero</b> for on-premise clusters and is computed for cloud
        /// deployments.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeCores", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(0)]
        public int ComputeCores { get; set; } = 0;

        /// <summary>
        /// <b>io.neon.compute.architecture</b> [<c>enum</c>]: Specifies the CPU architecture.
        /// This defaults to <see cref="CpuArchitecture.x64"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeArchitecture", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(CpuArchitecture.x64)]
        public CpuArchitecture ComputeArchitecture { get; set; } = CpuArchitecture.x64;

        /// <summary>
        /// <b>io.neon.compute.ram</b> [<c>int</c>]: Specifies the the available RAM in
        /// megabytes.  This defaults to <b>zero</b> for on-premise clusters and is computed
        /// for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeRamMB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(0)]
        public int ComputeRamMB { get; set; } = 0;

        /// <summary>
        /// <b>io.neon.compute.swap</b> [<c>bool</c>]: Specifies whether the node operating system may
        /// swap RAM to the file system.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeSwap", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool ComputeSwap { get; set; } = false;

        //---------------------------------------------------------------------
        // Define physical host labels.

        private string physicalFaultDomain = string.Empty;

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalLocation = ClusterDefinition.ReservedLabelPrefix + ".physical.location";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalMachine"/>.
        /// </summary>
        public const string LabelPhysicalMachine = ClusterDefinition.ReservedLabelPrefix + ".physical.machine";

        /// <summary>
        /// Reserved label name for <see cref="PhysicalFaultDomain"/>.
        /// </summary>
        public const string LabelPhysicalFaultDomain = ClusterDefinition.ReservedLabelPrefix + ".physical.faultdomain";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalPower = ClusterDefinition.ReservedLabelPrefix + ".physical.power";

        /// <summary>
        /// <b>io.neon.physical.location</b> [<c>string</c>]: A free format string describing the
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
        [DefaultValue("")]
        public string PhysicalLocation { get; set; } = string.Empty;

        /// <summary>
        /// <b>io.neon.physical.model</b> [<c>string</c>]: A free format string describing the
        /// physical server computer model (e.g. <b>Dell-PowerEdge-R220</b>).  This defaults to the <b>empty string</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PhysicalMachine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue("")]
        public string PhysicalMachine { get; set; } = string.Empty;

        /// <summary>
        /// <b>io.neon.physical.faultdomain</b> [<c>string</c>]: A free format string 
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
        [DefaultValue("")]
        public string PhysicalFaultDomain
        {
            get { return string.IsNullOrWhiteSpace(physicalFaultDomain) ? node.Name : physicalFaultDomain; }
            set { physicalFaultDomain = value; }
        }

        /// <summary>
        /// <b>io.neon.physical.power</b> [<c>string</c>]: Describes host the physical power
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
        [DefaultValue("")]
        public string PhysicalPower { get; set; } = string.Empty;

        // $todo(jeff.lill): Define the format of this string for APC PDUs.

        //---------------------------------------------------------------------
        // Build-in cluster logging related labels.

        /// <summary>
        /// Reserved label name for <see cref="LogEsData"/>.
        /// </summary>
        public const string LabelLogEsData = ClusterDefinition.ReservedLabelPrefix + ".log.esdata";

        /// <summary>
        /// <b>io.neon.log.esdata</b> [<c>bool</c>]: Indicates that the node should host an
        /// Elasticsearch node to be used to store cluster logging data. This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogEsData", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool LogEsData { get; set; } = false;

        /// <summary>
        /// Custom node labels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this property to define custom host node labels.
        /// </para>
        /// <note>
        /// The <b>io.neoncluster</b> label prefix is reserved.
        /// </note>
        /// <note>
        /// Labels names will be converted to lowercase when the Docker daemon is started
        /// on the host node.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Custom")]
        public Dictionary<string, string> Custom { get; set; } = new Dictionary<string, string>();

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Enumerates the neonCLUSTER standard Docker labels and values.
        /// </summary>
        [JsonIgnore]
        public IEnumerable<KeyValuePair<string, object>> Standard
        {
            get
            {
                // $note(jeff.lill): 
                //
                // This code will need to be updated whenever new standard labels are added or changed.

                var list = new List<KeyValuePair<string, object>>(20);

                // Standard labels from the parent node definition.

                list.Add(new KeyValuePair<string, object>(LabelPublicAddress,           node.PublicAddress));
                list.Add(new KeyValuePair<string, object>(LabelPrivateAddress,          node.PrivateAddress));
                list.Add(new KeyValuePair<string, object>(LabelRole,                    node.Role));
                list.Add(new KeyValuePair<string, object>(LabelVpnFrontendPort,         node.VpnFrontendPort));
                list.Add(new KeyValuePair<string, object>(LabelVpnReturnAddress,        node.VpnReturnAddress));
                list.Add(new KeyValuePair<string, object>(LabelVpnReturnSubnet,         node.VpnReturnSubnet));

                if (node.Azure != null)
                {
                    list.Add(new KeyValuePair<string, object>(LabelAzureVmSize,         node.Azure.VmSize));
                    list.Add(new KeyValuePair<string, object>(LabelAzureStorageType,    node.Azure.StorageType));
                    list.Add(new KeyValuePair<string, object>(LabelAzureDriveCount,     node.Azure.DriveCount));
                    list.Add(new KeyValuePair<string, object>(LabelAzureDriveSizeGB,    node.Azure.DriveSizeGB));
                }

                // Standard labels from this class.

                list.Add(new KeyValuePair<string, object>(LabelStorageCapacityGB,       StorageCapacityGB));
                list.Add(new KeyValuePair<string, object>(LabelStorageLocal,            StorageLocal));
                list.Add(new KeyValuePair<string, object>(LabelStorageSSD,              StorageSSD));
                list.Add(new KeyValuePair<string, object>(LabelStorageRedundant,        StorageRedundant));
                list.Add(new KeyValuePair<string, object>(LabelStorageEphemeral,        StorageEphemeral));
                list.Add(new KeyValuePair<string, object>(LabelComputeCores,            ComputeCores));
                list.Add(new KeyValuePair<string, object>(LabelComputeArchitecture,     ComputeArchitecture));
                list.Add(new KeyValuePair<string, object>(LabelComputeRamMB,            ComputeRamMB));
                list.Add(new KeyValuePair<string, object>(LabelComputeSwap,             ComputeSwap));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalLocation,        PhysicalLocation));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalMachine,         PhysicalMachine));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalFaultDomain,     PhysicalFaultDomain));
                list.Add(new KeyValuePair<string, object>(LabelPhysicalPower,           PhysicalPower));
                list.Add(new KeyValuePair<string, object>(LabelLogEsData,               LogEsData));

                return list;
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
                log.LogWarn(() => $"[node={node.Name}]: [{e.GetType().Name}] parsing [{label.Key}={label.Value}");
            }
        }

        /// <summary>
        /// Parses a dictionary of name/value labels by setting the appropriate
        /// properties of the parent node.
        /// </summary>
        /// <param name="labels">The label dictionary.</param>
        internal void Parse(Dictionary<string, string> labels)
        {
            foreach (var label in labels)
            {
                switch (label.Key)
                {
                    case LabelPublicAddress:    node.PublicAddress = label.Value; break;
                    case LabelPrivateAddress:   node.PrivateAddress = label.Value; break;
                    case LabelRole:             node.Role = label.Value; break;
                    case LabelVpnFrontendPort:  ParseCheck(label, () => { node.VpnFrontendPort = int.Parse(label.Value); }); break;
                    case LabelVpnReturnAddress: node.VpnReturnAddress = label.Value; break;
                    case LabelVpnReturnSubnet:  node.VpnReturnSubnet = label.Value; break;

                    case LabelAzureVmSize:
                    case LabelAzureStorageType:
                    case LabelAzureDriveCount:
                    case LabelAzureDriveSizeGB:

                        if (node.Azure == null)
                        {
                            node.Azure = new AzureNodeOptions();
                        }

                        switch (label.Key)
                        {
                            case LabelAzureVmSize:      ParseCheck(label, () => { node.Azure.VmSize = NeonHelper.ParseEnum<AzureVmSizes>(label.Value, ignoreCase: true); }); break;
                            case LabelAzureStorageType: ParseCheck(label, () => { node.Azure.StorageType = NeonHelper.ParseEnum<AzureStorageTypes>(label.Value, ignoreCase: true); }); break;
                            case LabelAzureDriveCount:  ParseCheck(label, () => { node.Azure.DriveCount = int.Parse(label.Value); }); break;
                            case LabelAzureDriveSizeGB: ParseCheck(label, () => { node.Azure.DriveSizeGB = int.Parse(label.Value); }); break;
                        }
                        break;

                    case LabelStorageCapacityGB:        ParseCheck(label, () => { node.Labels.StorageCapacityGB = int.Parse(label.Value); }); break;
                    case LabelStorageLocal:             node.Labels.StorageLocal = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageSSD:               node.Labels.StorageSSD = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageRedundant:         node.Labels.StorageRedundant = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelStorageEphemeral:         node.Labels.StorageEphemeral = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;

                    case LabelComputeCores:             ParseCheck(label, () => { node.Labels.ComputeCores = int.Parse(label.Value); }); break;
                    case LabelComputeArchitecture:      ParseCheck(label, () => { node.Labels.ComputeArchitecture = NeonHelper.ParseEnum<CpuArchitecture>(label.Value); }); break;
                    case LabelComputeRamMB:             ParseCheck(label, () => { node.Labels.ComputeRamMB = int.Parse(label.Value); }); break;
                    case LabelComputeSwap:              node.Labels.ComputeSwap = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;

                    case LabelPhysicalLocation:         node.Labels.PhysicalLocation = label.Value; break;
                    case LabelPhysicalMachine:          node.Labels.PhysicalMachine = label.Value;  break;
                    case LabelPhysicalFaultDomain:      node.Labels.PhysicalFaultDomain = label.Value; break;
                    case LabelPhysicalPower:            node.Labels.PhysicalPower = label.Value;  break;

                    case LabelLogEsData:                node.Labels.LogEsData = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;

                    case LabelDatacenter:
                    case LabelEnvironment:

                        // These labels don't currently map to node properties so
                        // we'll ignore them.

                        break;

                    default:

                        // Must be a custom label.

                        node.Labels.Custom.Add(label.Key, label.Value);
                        break;
                }
            }
        }

        /// <summary>
        /// Returns a clone of the current instance.
        /// </summary>
        /// <param name="parentNode">The cloned parent node.</param>
        /// <returns>The clone.</returns>
        public NodeLabels Clone(NodeDefinition parentNode)
        {
            Covenant.Requires<ArgumentNullException>(parentNode != null);

            var clone = new NodeLabels(parentNode);

            this.CopyTo(clone);

            return clone;
        }

        /// <summary>
        /// Performs a deep copy of the current cluster node to another instance.
        /// </summary>
        /// <param name="target">The target instance.</param>
        internal void CopyTo(NodeLabels target)
        {
            Covenant.Requires<ArgumentNullException>(target != null);

            target.StorageCapacityGB   = this.StorageCapacityGB;
            target.StorageLocal        = this.StorageLocal;
            target.StorageSSD          = this.StorageSSD;
            target.StorageRedundant    = this.StorageRedundant;
            target.StorageEphemeral    = this.StorageEphemeral;

            target.ComputeCores        = this.ComputeCores;
            target.ComputeArchitecture = this.ComputeArchitecture;
            target.ComputeRamMB        = this.ComputeRamMB;
            target.ComputeSwap         = this.ComputeSwap;

            target.PhysicalLocation    = this.PhysicalLocation;
            target.PhysicalMachine     = this.PhysicalMachine;
            target.PhysicalFaultDomain = this.PhysicalFaultDomain;
            target.PhysicalPower       = this.PhysicalPower;

            target.LogEsData           = this.LogEsData;

            foreach (var item in this.Custom)
            {
                target.Custom.Add(item.Key, item.Value);
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

            // Verify that custom node label names satisfy the 
            // following criteria:
            // 
            //      1. Be at least one character long.
            //      2. Start and end with an alpha numeric character.
            //      3. Include only alpha numeric characters, dashes,
            //         underscores or dots.
            //      4. Does not have consecutive dots or dashes.

            foreach (var item in Custom)
            {
                if (item.Key.Length == 0)
                {
                    throw new ClusterDefinitionException($"Custom node label for value [{item.Value}] has no label name.");
                }
                else if (item.Key.Contains(".."))
                {
                    throw new ClusterDefinitionException($"Custom node name [{item.Key}] has consecutive dots.");
                }
                else if (item.Key.Contains("--"))
                {
                    throw new ClusterDefinitionException($"Custom node name [{item.Key}] has consecutive dashes.");
                }
                else if (!char.IsLetterOrDigit(item.Key.First()))
                {
                    throw new ClusterDefinitionException($"Custom node name [{item.Key}] does not begin with a letter or digit.");
                }
                else if (!char.IsLetterOrDigit(item.Key.Last()))
                {
                    throw new ClusterDefinitionException($"Custom node name [{item.Key}] does not begin with a letter or digit.");
                }

                foreach (var ch in item.Key)
                {
                    if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_')
                    {
                        continue;
                    }

                    throw new ClusterDefinitionException($"Custom node name [{item.Key}] has an illegal character.  Only letters, digits, dash and dots are allowed.");
                }

                foreach (var ch in item.Value)
                {
                    if (char.IsWhiteSpace(ch))
                    {
                        throw new ClusterDefinitionException($"Whitespace in the value of [{item.Key}={item.Value}] is not allowed.");
                    }
                }
            }
        }
    }
}
