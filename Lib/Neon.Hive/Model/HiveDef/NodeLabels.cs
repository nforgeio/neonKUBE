//-----------------------------------------------------------------------------
// FILE:	    NodeLabels.cs
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
using Neon.Diagnostics;

namespace Neon.Hive
{
    /// <summary>
    /// Describes the standard neonHIVE and custom labels to be assigned to 
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
    /// DNS domain you control.  For example, neonHIVE related labels are prefixed
    /// with <b>"io.neonhive."</b>.  You should follow this convention for any
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
    /// This class exposes several built-in neonHIVE properties.  You can use
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
        // Define global hive and node definition labels.

        /// <summary>
        /// Reserved label name that identifies the datacenter.
        /// </summary>
        public const string LabelDatacenter = HiveDefinition.ReservedLabelPrefix + ".hive.datacenter";

        /// <summary>
        /// Reserved label name that identifies the hive environment.
        /// </summary>
        public const string LabelEnvironment = HiveDefinition.ReservedLabelPrefix + ".hive.environment";

        /// <summary>
        /// Reserved label name that identifies the node's public IP address or FQDN.
        /// </summary>
        public const string LabelPublicAddress = HiveDefinition.ReservedLabelPrefix + ".node.public_address";

        /// <summary>
        /// Reserved label name that identifies the node's private IP address.
        /// </summary>
        public const string LabelPrivateAddress = HiveDefinition.ReservedLabelPrefix + ".node.private_address";

        /// <summary>
        /// Reserved label name that identifies the node role.
        /// </summary>
        public const string LabelRole = HiveDefinition.ReservedLabelPrefix + ".node.role";

        /// <summary>
        /// Reserved label name that identifies the frontend port to be used to connect
        /// to a manager's VPN server (if it's hosting a VPN server).
        /// </summary>
        public const string LabelVpnFrontendPort = HiveDefinition.ReservedLabelPrefix + ".node.vpn_frontend_port";

        /// <summary>
        /// Reserved label name that identifies a manager node's VPN pool address 
        /// (if it's hosting a VPN server).
        /// </summary>
        public const string LabelVpnPoolAddress = HiveDefinition.ReservedLabelPrefix + ".node.vpn_pool_address";

        /// <summary>
        /// Reserved label name that identifies a manager node's VPN address pool subnet 
        /// (if it's hosting a VPN server).
        /// </summary>
        public const string LabelVpnPoolSubnet = HiveDefinition.ReservedLabelPrefix + ".node.vpn_pool_subnet";

        //---------------------------------------------------------------------
        // Azure hosting related labels.

        /// <summary>
        /// Reserved label name that identifies the node's Azure VM size.
        /// </summary>
        public const string LabelAzureVmSize = HiveDefinition.ReservedLabelPrefix + ".azure.vm_size";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached storage type.
        /// </summary>
        public const string LabelAzureStorageType = HiveDefinition.ReservedLabelPrefix + ".azure.storage_type";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached drive count.
        /// </summary>
        public const string LabelAzureDriveCount = HiveDefinition.ReservedLabelPrefix + ".azure.drive_count";

        /// <summary>
        /// Reserved label name that identifies the node's Azure attached drive size in GB.
        /// </summary>
        public const string LabelAzureDriveSizeGB = HiveDefinition.ReservedLabelPrefix + ".azure.drive_size_gb";

        //---------------------------------------------------------------------
        // Define the node storage related labels.

        /// <summary>
        /// Reserved label name for <see cref="StorageCapacityGB"/>.
        /// </summary>
        public const string LabelStorageCapacityGB = HiveDefinition.ReservedLabelPrefix + ".storage.capacity_gb";

        /// <summary>
        /// Reserved label name for <see cref="StorageLocal"/>.
        /// </summary>
        public const string LabelStorageLocal = HiveDefinition.ReservedLabelPrefix + ".storage.local";

        /// <summary>
        /// Reserved label name for <see cref="StorageSSD"/>.
        /// </summary>
        public const string LabelStorageSSD = HiveDefinition.ReservedLabelPrefix + ".storage.ssd";

        /// <summary>
        /// Reserved label name for <see cref="StorageRedundant"/>.
        /// </summary>
        public const string LabelStorageRedundant = HiveDefinition.ReservedLabelPrefix + ".storage.redundant";

        /// <summary>
        /// Reserved label name for <see cref="StorageEphemeral"/>.
        /// </summary>
        public const string LabelStorageEphemeral = HiveDefinition.ReservedLabelPrefix + ".storage.ephemral";

        /// <summary>
        /// <b>io.neonhive.storage.capacity_gb</b> [<c>int</c>]: Specifies the node primary drive 
        /// storage capacity in gigabytes.
        /// </summary>
        [JsonProperty(PropertyName = "StorageCapacityGB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(0)]
        public int StorageCapacityGB { get; set; } = 0;

        /// <summary>
        /// <b>io.neonhive.storage.local</b> [<c>bool</c>]: Specifies whether the node storage is hosted
        /// on the node itself or is mounted as a remote file system or block device.  This defaults
        /// to <c>true</c> for on-premise hives and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageLocal", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(true)]
        public bool StorageLocal { get; set; } = true;

        /// <summary>
        /// <b>io.neonhive.storage.ssd</b> [<c>bool</c>]: Indicates that the storage is backed
        /// by SSDs as opposed to rotating hard drive.  This defaults to <c>false</c> for 
        /// on-premise hives and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageSSD", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool StorageSSD { get; set; } = false;

        /// <summary>
        /// <b>io.neonhive.storage.redundant</b> [<c>bool</c>]: Indicates that the storage is redundant.  This
        /// may be implemented locally using RAID1+ or remotely using network or cloud-based file systems.
        /// This defaults to <c>false</c> for on-premise hives and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageRedundant", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool StorageRedundant { get; set; } = false;

        /// <summary>
        /// <b>io.neonhive.storage.redundant</b> [<c>bool</c>]: Indicates that the storage is ephemeral.
        /// All data will be lost when the host is restarted.  This defaults to <c>false</c> for 
        /// on-premise clusthivesers and is computed for cloud deployments.
        /// </summary>
        [JsonProperty(PropertyName = "StorageEphemeral", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool StorageEphemeral { get; set; } = false;

        //---------------------------------------------------------------------
        // Define host compute related labels.

        /// <summary>
        /// Reserved label name for <see cref="ComputeCores"/>.
        /// </summary>
        public const string LabelComputeCores = HiveDefinition.ReservedLabelPrefix + ".compute.cores";

        /// <summary>
        /// Reserved label name for <see cref="ComputeArchitecture"/>.
        /// </summary>
        public const string LabelComputeArchitecture = HiveDefinition.ReservedLabelPrefix + ".compute.architecture";

        /// <summary>
        /// Reserved label name for <see cref="ComputeRamMB"/>.
        /// </summary>
        public const string LabelComputeRamMB = HiveDefinition.ReservedLabelPrefix + ".compute.ram_mb";

        /// <summary>
        /// Reserved label name for <see cref="ComputeSwap"/>.
        /// </summary>
        public const string LabelComputeSwap = HiveDefinition.ReservedLabelPrefix + ".compute.swap";

        /// <summary>
        /// <b>io.neonhive.compute.cores</b> [<c>int</c>]: Specifies the number of CPU cores.
        /// This defaults to <b>0</b> for <see cref="HostingEnvironments.Machine"/>
        /// and is initialized for cloud and Hypervisor based hosting environments.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeCores", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(0)]
        public int ComputeCores { get; set; } = 0;

        /// <summary>
        /// <b>io.neonhive.compute.architecture</b> [<c>enum</c>]: Specifies the CPU architecture.
        /// This defaults to <see cref="CpuArchitecture.x64"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeArchitecture", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(CpuArchitecture.x64)]
        public CpuArchitecture ComputeArchitecture { get; set; } = CpuArchitecture.x64;

        /// <summary>
        /// <b>io.neonhive.compute.ram_mb</b> [<c>int</c>]: Specifies the available RAM in
        /// megabytes.  This defaults to <b>0</b> for <see cref="HostingEnvironments.Machine"/>
        /// and is initialized for cloud and Hypervisor based hosting environments.
        /// </summary>
        [JsonProperty(PropertyName = "ComputeRamMB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(0)]
        public int ComputeRamMB { get; set; } = 0;

        /// <summary>
        /// <b>io.neonhive.compute.swap</b> [<c>bool</c>]: Specifies whether the node operating system may
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
        public const string LabelPhysicalLocation = HiveDefinition.ReservedLabelPrefix + ".physical.location";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalMachine"/>.
        /// </summary>
        public const string LabelPhysicalMachine = HiveDefinition.ReservedLabelPrefix + ".physical.machine";

        /// <summary>
        /// Reserved label name for <see cref="PhysicalFaultDomain"/>.
        /// </summary>
        public const string LabelPhysicalFaultDomain = HiveDefinition.ReservedLabelPrefix + ".physical.faultdomain";

        /// <summary>
        /// Reserved label name for <see cref="LabelPhysicalPower"/>.
        /// </summary>
        public const string LabelPhysicalPower = HiveDefinition.ReservedLabelPrefix + ".physical.power";

        /// <summary>
        /// <b>io.neonhive.physical.location</b> [<c>string</c>]: A free format string describing the
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
        /// <b>io.neonhive.physical.model</b> [<c>string</c>]: A free format string describing the
        /// physical server computer model (e.g. <b>Dell-PowerEdge-R220</b>).  This defaults to the <b>empty string</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PhysicalMachine", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue("")]
        public string PhysicalMachine { get; set; } = string.Empty;

        /// <summary>
        /// <b>io.neonhive.physical.faultdomain</b> [<c>string</c>]: A free format string 
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
        ///     For a hive deployed to a single rack with a shared network connection,
        ///     the fault domain will typically be the physical machine such that the 
        ///     loss of a machine can be tolerated.
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>Multiple Racks:</b></term>
        ///     <description>
        ///     For hives deployed to multiple racks, each with their own network
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
        ///     For hives that span physical datacenters, you could potentially map
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
        /// <b>io.neonhive.physical.power</b> [<c>string</c>]: Describes host the physical power
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
        // Build-in hive logging related labels.

        /// <summary>
        /// Reserved label name for <see cref="LogEsData"/>.
        /// </summary>
        public const string LabelLogEsData = HiveDefinition.ReservedLabelPrefix + ".log.esdata";

        /// <summary>
        /// <b>io.neonhive.log.esdata</b> [<c>bool</c>]: Indicates that the node should host an
        /// Elasticsearch node to be used to store hive logging data. This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogEsData", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool LogEsData { get; set; } = false;

        //---------------------------------------------------------------------
        // Build-in hive HiveMQ queue related labels.

        /// <summary>
        /// Reserved label name for <see cref="HiveMQ"/>.
        /// </summary>
        public const string LabelHiveMQ = HiveDefinition.ReservedLabelPrefix + ".hivemq";

        /// <summary>
        /// Reserved label name for <see cref="HiveMQManager"/>.
        /// </summary>
        public const string LabelHiveMQManager = HiveDefinition.ReservedLabelPrefix + ".hivemq.manager";

        /// <summary>
        /// <b>io.neonhive.hivemq</b> [<c>bool</c>]: Indicates that the node should host a
        /// RabbitMQ instance. This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "HiveMQ", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool HiveMQ { get; set; } = false;

        /// <summary>
        /// <para>
        /// <b>io.neonhive.hivemq.manager</b> [<c>bool</c>]: Indicates that the node should
        /// host a RabbitMQ instance with the management plugin when <see cref="HiveMQ"/><c>=true</c> .
        /// This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// Setting this to <c>true</c> implicitly sets <see cref="HiveMQ"/> to <c>true</c>.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "HiveMQManager", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(false)]
        public bool HiveMQManager { get; set; } = false;

        //---------------------------------------------------------------------
        // Ceph Storage Cluster related labels.

        /// <summary>
        /// Reserved label name for <see cref="CephMON"/>.
        /// </summary>
        public const string LabelCephMON = HiveDefinition.ReservedLabelPrefix + ".ceph.mon";

        /// <summary>
        /// Reserved label name for <see cref="CephOSD"/>.
        /// </summary>
        public const string LabelCephOSD = HiveDefinition.ReservedLabelPrefix + ".ceph.osd";

        /// <summary>
        /// Reserved label name for <see cref="CephOSDDevice"/>.
        /// </summary>
        public const string LabelCephOSDDevice = HiveDefinition.ReservedLabelPrefix + ".ceph.osd_device";

        /// <summary>
        /// Reserved label name for <see cref="CephMDS"/>.
        /// </summary>
        public const string LabelCephMDS = HiveDefinition.ReservedLabelPrefix + ".ceph.mds";

        /// <summary>
        /// Reserved label name for <see cref="CephOSDDriveSizeGB"/>.
        /// </summary>
        public const string LabelCephOSDDriveSizeGB = HiveDefinition.ReservedLabelPrefix + ".ceph.osd_drivesize_gb";

        /// <summary>
        /// Reserved label name for <see cref="CephOSDCacheSizeMB"/>.
        /// </summary>
        public const string LabelCephOSDCacheSizeMB = HiveDefinition.ReservedLabelPrefix + ".ceph.osd_cachesize_mb";

        /// <summary>
        /// Reserved label name for <see cref="CephOSDJournalSizeMB"/>.
        /// </summary>
        public const string LabelCephOSDJournalSizeMB = HiveDefinition.ReservedLabelPrefix + ".ceph.osd_journalsize_mb";

        /// <summary>
        /// Reserved label name for <see cref="CephOSDJournalSizeMB"/>.
        /// </summary>
        public const string LabelCephMDSCacheSizeMB = HiveDefinition.ReservedLabelPrefix + ".ceph.mds_cachesize_mb";

        /// <summary>
        /// <b>io.neonhive.ceph.monitor</b> [<c>bool</c>]: Indicates that the Ceph 
        /// monitor and manager services  will be deployed to this node if 
        /// <see cref="HiveFSOptions.Enabled"/> is <c>true</c>.  This defaults 
        /// to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Monitors maintain maps of the hive state, including the monitor map, 
        /// manager map, the OSD map, and the CRUSH map. These maps are critical 
        /// cluster state required for Ceph daemons to coordinate with each other. 
        /// Monitors are also responsible for managing authentication between 
        /// daemons and clients. At least three monitors are normally required 
        /// for redundancy and high availability.
        /// </para>
        /// <para>
        /// Managers are responsible for keeping track of runtime metrics and the
        /// current state of the Ceph cluster, including storage utilization, 
        /// current performance metrics, and system load. The Ceph Manager daemons
        /// also host python-based plugins to manage and expose Ceph cluster information,
        /// including a web-based dashboard and REST API. At least two managers are
        /// normally required for high availability.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "CephMON", Required = Required.Default)]
        [DefaultValue(false)]
        public bool CephMON { get; set; } = false;

        /// <summary>
        /// <b>io.neonhive.ceph.osd</b> [<c>bool</c>]: Indicates that a Ceph OSD 
        /// (object storage daemon) will be deployed to this node if 
        /// <see cref="HiveFSOptions.Enabled"/> is <c>true</c>.  
        /// This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// OSDs store data, handles data replication, recovery, rebalancing, and
        /// provides some monitoring information to Ceph Monitors and Managers by 
        /// checking other Ceph OSD Daemons for a heartbeat. At least 3 Ceph OSDs 
        /// are normally required for redundancy and high availability.
        /// </remarks>
        [JsonProperty(PropertyName = "CephOSD", Required = Required.Default)]
        [DefaultValue(false)]
        public bool CephOSD { get; set; } = false;

        /// <summary>
        /// <b>io.neonhive.ceph.osd_device</b> (<c>string</c>]: The path to the block
        /// device where the OSD data will be persisted (like: <b>/dev/sdb</b>)
        /// when <see cref="CephOSD"/> is <c>true</c>.  This will be initialized 
        /// automatically for most hosting environments but will need to be specified 
        /// explicitly for <see cref="HostingEnvironments.Machine"/>.
        /// </summary>
        public string CephOSDDevice { get; set; } = null;

        /// <summary>
        /// <b>io.neonhive.ceph.mds</b> [<c>bool</c>]: Indicates that a Ceph MDS 
        /// (metadata server) will be deployed to this node if <see cref="HiveFSOptions.Enabled"/> 
        /// is <c>true</c>.  This defaults to <c>false</c>.
        /// </summary>
        /// <remarks>
        /// Metadata servers store metadata on behalf of the Ceph Filesystem 
        /// (e.g. Ceph Block Devices and Ceph Object Storage do not use MDS). 
        /// Ceph Metadata Servers allow POSIX file system users to execute basic 
        /// commands (like ls, find, etc.) without placing an enormous burden on
        /// the Ceph Storage Cluster.
        /// </remarks>
        [JsonProperty(PropertyName = "CephMDS", Required = Required.Default)]
        [DefaultValue(false)]
        public bool CephMDS { get; set; } = false;

        /// <summary>
        /// <b>io.neonhive.ceph.drivesize_gb</b> [<c>int</c>]: Specifies the size in gigabytes
        /// of the Ceph OSD drive created for cloud and hypervisor based environments if the
        /// integrated Ceph storage cluster is enabled and <see cref="CephOSD"/>
        /// is <c>true</c> for this node.  This defaults to <see cref="HiveFSOptions.OSDDriveSize"/>
        /// (<b>128GB</b>).
        /// </summary>
        [JsonProperty(PropertyName = "CephOSDDriveSizeGB", Required = Required.Default)]
        [DefaultValue(0)]
        public int CephOSDDriveSizeGB { get; set; } = 0;

        /// <summary>
        /// <para>
        /// <b>io.neonhive.ceph.cachesize_mb</b> [<c>int</c>]: Specifies the RAM in megabytes
        /// to assign to the Ceph OSDs for caching if the integrated Ceph storage cluster 
        /// is enabled and <see cref="CephOSD"/> is <c>true</c> for this node.
        /// This defaults to <see cref="HiveFSOptions.OSDCacheSize"/> (<b>1GB</b>) (which is 
        /// probably too small for production hives).
        /// </para>
        /// <note>
        /// <para>
        /// The <a href="https://ceph.com/community/new-luminous-bluestore/">Ceph documentation</a>
        /// states that OSDs may tend to underestimate the RAM it's using by up to 1.5 times.
        /// To avoid potential memory issues, neonHIVE  will adjust this value by dividing it 
        /// by 1.5 to when actually configuring the OSD services.
        /// </para>
        /// <para>
        /// You should also take care to leave 1-2GB of RAM for the host Linux operating system
        /// as well as the OSD non-cache related memory when you're configuring this property.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "CephOSDCacheSizeMB", Required = Required.Default)]
        [DefaultValue(0)]
        public int CephOSDCacheSizeMB { get; set; } = 0;

        /// <summary>
        /// <b>io.neonhive.ceph.journalsize_mb</b> [<c>int</c>]: Specifies the disk capacity
        /// in megabytes to assign to the Ceph OSD journal if the integrated Ceph storage 
        /// cluster is enabled and <see cref="CephOSD"/> is <c>true</c> for this node.
        /// This defaults to <see cref="HiveFSOptions.OSDJournalSize"/>.
        /// </summary>
        [JsonProperty(PropertyName = "CephOSDJournalSizeMB", Required = Required.Default)]
        [DefaultValue(0)]
        public int CephOSDJournalSizeMB { get; set; } = 0;

        /// <summary>
        /// <para>
        /// <b>io.neonhive.ceph.mds_cachesize_mb</b> [<c>int</c>]: Specifies the RAM in megabytes
        /// to assign to the Ceph MDS services for caching if the integrated Ceph storage cluster 
        /// is enabled and <see cref="CephMDS"/> is <c>true</c> for this node.
        /// This defaults to <see cref="HiveFSOptions.MDSCacheSize"/> (<b>1GB</b>) (which is 
        /// probably too small for production hives).
        /// </para>
        /// <note>
        /// <para>
        /// The Ceph documentation states that OSDs may tend to underestimate the RAM it's using
        /// by up to 1.5 times.  To avoid potential memory issues, neonHIVE  will adjust this
        /// value by dividing it  by 1.5 to when actually configuring the MDS services.
        /// </para>
        /// <para>
        /// You should also take care to leave 1-2GB of RAM for the host Linux operating system
        /// as well as the OSD non-cache related memory when you're configuring this property.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "CephMDSCacheSizeMB", Required = Required.Default)]
        [DefaultValue(0)]
        public int CephMDSCacheSizeMB { get; set; } = 0;

        //---------------------------------------------------------------------

        /// <summary>
        /// Custom node labels.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this property to define custom host node labels.
        /// </para>
        /// <note>
        /// The <b>io.neonhive.</b> label prefix is reserved.
        /// </note>
        /// <note>
        /// Labels names will be converted to lowercase when the Docker daemon is started
        /// on the host node.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Custom")]
        public Dictionary<string, object> Custom { get; set; } = new Dictionary<string, object>();

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Enumerates the neonHIVE standard Docker labels and values.
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
                // The [nhive/neon-hive-manager] and [nhive/neon-proxy-manager] service images will also 
                // need to be rebuilt.

                var list = new List<KeyValuePair<string, object>>(20);

                // Standard labels from the parent node definition.

                list.Add(new KeyValuePair<string, object>(LabelPublicAddress,           node.PublicAddress));
                list.Add(new KeyValuePair<string, object>(LabelPrivateAddress,          node.PrivateAddress));
                list.Add(new KeyValuePair<string, object>(LabelRole,                    node.Role));
                list.Add(new KeyValuePair<string, object>(LabelVpnFrontendPort,         node.VpnFrontendPort));
                list.Add(new KeyValuePair<string, object>(LabelVpnPoolAddress,          node.VpnPoolAddress));
                list.Add(new KeyValuePair<string, object>(LabelVpnPoolSubnet,           node.VpnPoolSubnet));

                if (node.Azure != null)
                {
                    list.Add(new KeyValuePair<string, object>(LabelAzureVmSize,         node.Azure.VmSize));
                    list.Add(new KeyValuePair<string, object>(LabelAzureStorageType,    node.Azure.StorageType));
                    list.Add(new KeyValuePair<string, object>(LabelAzureDriveCount,     node.Azure.HardDriveCount));
                    list.Add(new KeyValuePair<string, object>(LabelAzureDriveSizeGB,    node.Azure.HardDriveSizeGB));
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

                list.Add(new KeyValuePair<string, object>(LabelHiveMQ,                  HiveMQ));
                list.Add(new KeyValuePair<string, object>(LabelHiveMQManager,           HiveMQManager));

                list.Add(new KeyValuePair<string, object>(LabelCephMON,                 CephMON));
                list.Add(new KeyValuePair<string, object>(LabelCephOSD,                 CephOSD));
                list.Add(new KeyValuePair<string, object>(LabelCephOSDDevice,           CephOSDDevice));
                list.Add(new KeyValuePair<string, object>(LabelCephMDS,                 CephMDS));
                list.Add(new KeyValuePair<string, object>(LabelCephOSDDriveSizeGB,      CephOSDDriveSizeGB));
                list.Add(new KeyValuePair<string, object>(LabelCephOSDCacheSizeMB,      CephOSDCacheSizeMB));
                list.Add(new KeyValuePair<string, object>(LabelCephOSDJournalSizeMB,    CephOSDJournalSizeMB));
                list.Add(new KeyValuePair<string, object>(LabelCephMDSCacheSizeMB,      CephMDSCacheSizeMB));

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
            // WARNING: 
            //
            // This method will need to be updated whenever new standard labels are added or changed.
            // The [nhive/neon-hive-manager] and [nhive/neon-proxy-manager] service images will also 
            // need to be rebuilt.

            foreach (var label in labels)
            {
                switch (label.Key)
                {
                    case LabelPublicAddress:    node.PublicAddress = label.Value; break;
                    case LabelPrivateAddress:   node.PrivateAddress = label.Value; break;
                    case LabelRole:             node.Role = label.Value; break;
                    case LabelVpnFrontendPort:  ParseCheck(label, () => { node.VpnFrontendPort = int.Parse(label.Value); }); break;
                    case LabelVpnPoolAddress:   node.VpnPoolAddress = label.Value; break;
                    case LabelVpnPoolSubnet:    node.VpnPoolSubnet = label.Value; break;

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
                            case LabelAzureVmSize:      ParseCheck(label, () => { node.Azure.VmSize = NeonHelper.ParseEnum<AzureVmSizes>(label.Value); }); break;
                            case LabelAzureStorageType: ParseCheck(label, () => { node.Azure.StorageType = NeonHelper.ParseEnum<AzureStorageTypes>(label.Value); }); break;
                            case LabelAzureDriveCount:  ParseCheck(label, () => { node.Azure.HardDriveCount = int.Parse(label.Value); }); break;
                            case LabelAzureDriveSizeGB: ParseCheck(label, () => { node.Azure.HardDriveSizeGB = int.Parse(label.Value); }); break;
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

                    case LabelHiveMQ:                   node.Labels.HiveMQ = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelHiveMQManager:            node.Labels.HiveMQManager = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;

                    case LabelCephMON:                  node.Labels.CephMON = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelCephOSD:                  node.Labels.CephOSD = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelCephOSDDevice:            node.Labels.CephOSDDevice = label.Value; break;
                    case LabelCephMDS:                  node.Labels.CephMDS = label.Value.Equals("true", StringComparison.OrdinalIgnoreCase); break;
                    case LabelCephOSDDriveSizeGB:       ParseCheck(label, () => { node.Labels.CephOSDDriveSizeGB = int.Parse(label.Value); }); break;
                    case LabelCephOSDCacheSizeMB:       ParseCheck(label, () => { node.Labels.CephOSDCacheSizeMB = int.Parse(label.Value); }); break;
                    case LabelCephOSDJournalSizeMB:     ParseCheck(label, () => { node.Labels.CephOSDJournalSizeMB = int.Parse(label.Value); }); break;
                    case LabelCephMDSCacheSizeMB:       ParseCheck(label, () => { node.Labels.CephMDSCacheSizeMB = int.Parse(label.Value); }); break;

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
        /// Copies the label properties to another instance.
        /// </summary>
        /// <param name="target">The target instance.</param>
        internal void CopyTo(NodeLabels target)
        {
            Covenant.Requires<ArgumentNullException>(target != null);

            // WARNING: 
            //
            // This method will need to be updated whenever new standard labels are added or changed.
            // The [nhive/neon-hive-manager] and [nhive/neon-proxy-manager] service images will also 
            // need to be rebuilt.

            target.StorageCapacityGB    = this.StorageCapacityGB;
            target.StorageLocal         = this.StorageLocal;
            target.StorageSSD           = this.StorageSSD;
            target.StorageRedundant     = this.StorageRedundant;
            target.StorageEphemeral     = this.StorageEphemeral;

            target.ComputeCores         = this.ComputeCores;
            target.ComputeArchitecture  = this.ComputeArchitecture;
            target.ComputeRamMB         = this.ComputeRamMB;
            target.ComputeSwap          = this.ComputeSwap;

            target.PhysicalLocation     = this.PhysicalLocation;
            target.PhysicalMachine      = this.PhysicalMachine;
            target.PhysicalFaultDomain  = this.PhysicalFaultDomain;
            target.PhysicalPower        = this.PhysicalPower;

            target.LogEsData            = this.LogEsData;

            target.CephMON              = this.CephMON;
            target.CephOSD              = this.CephOSD;
            target.CephOSDDevice        = this.CephOSDDevice;
            target.CephMDS              = this.CephMDS;
            target.CephOSDDriveSizeGB   = this.CephOSDDriveSizeGB;
            target.CephOSDCacheSizeMB   = this.CephOSDCacheSizeMB;
            target.CephOSDJournalSizeMB = this.CephOSDJournalSizeMB;
            target.CephMDSCacheSizeMB   = this.CephMDSCacheSizeMB;

            target.HiveMQ               = this.HiveMQ;
            target.HiveMQManager        = this.HiveMQManager;

            foreach (var item in this.Custom)
            {
                target.Custom.Add(item.Key, item.Value);
            }
        }

        /// <summary>
        /// Validates the node labels.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            if (hiveDefinition.HiveFS.Enabled)
            {
                if (CephOSDDriveSizeGB > 0 && CephOSDDriveSizeGB < 1)
                {
                    throw new HiveDefinitionException($"[{nameof(NodeLabels)}.{nameof(CephOSDDriveSizeGB)}={CephOSDDriveSizeGB}] is cannot be less than [1GB].");
                }

                if (CephOSDCacheSizeMB > 0 && CephOSDCacheSizeMB < 64)
                {
                    throw new HiveDefinitionException($"[{nameof(NodeLabels)}.{nameof(CephOSDCacheSizeMB)}={CephOSDCacheSizeMB}] is cannot be less than [64MB].");
                }

                if (CephOSDJournalSizeMB > 0 && CephOSDJournalSizeMB < 64)
                {
                    throw new HiveDefinitionException($"[{nameof(NodeLabels)}.{nameof(CephOSDCacheSizeMB)}={CephOSDCacheSizeMB}] is cannot be less than [64MB].");
                }
            }

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
                    throw new HiveDefinitionException($"Custom node label for value [{item.Value}] has no label name.");
                }
                else if (item.Key.Contains(".."))
                {
                    throw new HiveDefinitionException($"Custom node name [{item.Key}] has consecutive dots.");
                }
                else if (item.Key.Contains("--"))
                {
                    throw new HiveDefinitionException($"Custom node name [{item.Key}] has consecutive dashes.");
                }
                else if (!char.IsLetterOrDigit(item.Key.First()))
                {
                    throw new HiveDefinitionException($"Custom node name [{item.Key}] does not begin with a letter or digit.");
                }
                else if (!char.IsLetterOrDigit(item.Key.Last()))
                {
                    throw new HiveDefinitionException($"Custom node name [{item.Key}] does not begin with a letter or digit.");
                }

                foreach (var ch in item.Key)
                {
                    if (char.IsLetterOrDigit(ch) || ch == '.' || ch == '-' || ch == '_')
                    {
                        continue;
                    }

                    throw new HiveDefinitionException($"Custom node name [{item.Key}] has an illegal character.  Only letters, digits, dash and dots are allowed.");
                }

                foreach (var ch in item.Value.ToString())
                {
                    if (char.IsWhiteSpace(ch))
                    {
                        throw new HiveDefinitionException($"Whitespace in the value of [{item.Key}={item.Value}] is not allowed.");
                    }
                }
            }
        }
    }
}
