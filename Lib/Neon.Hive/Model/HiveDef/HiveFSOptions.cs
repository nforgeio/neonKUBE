//-----------------------------------------------------------------------------
// FILE:	    HiveFSOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Hive
{
    /// <summary>
    /// Specifies the options for configuring the hive integrated
    /// <a href="https://ceph.com/">Ceph Distributed Storage</a>
    /// cluster.
    /// </summary>
    public class HiveFSOptions
    {
        private const string defaultRelease             = "mimic";
        private const string defaultOSDDriveSize        = "16GB";
        private const string defaultOSDCacheSize        = "256MB";
        private const string defaultOSDJournalSize      = "1GB";
        private const string defaultOSDObjectSizeMax    = "5GB";
        private const int    defaultOSDPlacementGroups  = 100;
        private const string defaultMDSCacheSize        = "64MB";
        private const string defaultVolumePluginPackage = "https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/neon-volume-plugin_latest.deb";

        /// <summary>
        /// Returns the names of the supported Ceph releases.
        /// </summary>
        private IEnumerable<string> SupportedReleases =
            new string[]
            {
                "luminous",
                "mimic"
            };

        /// <summary>
        /// The fudge factor to apply to Ceph cache sizes before actually
        /// configuring the services.
        /// </summary>
        public const double CacheSizeFudge = 1.0/1.5;

        /// <summary>
        /// Indicates whether Ceph storage is to be enabled for the hive.  
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default)]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Returns the Linux user for the Ceph components.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string Username
        {
            get { return "ceph"; }
        }

        /// <summary>
        /// Specifies the Ceph software major release name. This defaults to <b>mimic</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The following major Ceph releases are supported:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>mimic</b></term>
        ///     <description>
        ///     Released 07-2018 (<b>default</b>)
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>luminous</b></term>
        ///     <description>
        ///     Released 08-2017
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "Release", Required = Required.Default)]
        [DefaultValue(defaultRelease)]
        public string Release { get; set; } = defaultRelease;

        /// <summary>
        /// <para>
        /// Specifies the default size of the Ceph OSD drives created for cloud and
        /// hypervisor based environments.  This can be a long byte count or a long
        /// with units like <b>512MB</b> or <b>2GB</b>.  This can be overridden 
        /// for specific nodes.  This defaults to <b>16GB</b>.
        /// </para>
        /// <note>
        /// The default is probably too small for production environments
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "OSDDriveSize", Required = Required.Default)]
        [DefaultValue(defaultOSDDriveSize)]
        public string OSDDriveSize { get; set; } = defaultOSDDriveSize;

        /// <summary>
        /// <para>
        /// Specifies the default amount of RAM to allocate to Ceph OSD processes for 
        /// caching.  This can be a long byte count or a long with units like <b>512MB</b>,
        /// <b>2GB</b>, or <b>1TB</b>.  This can be overridden for specific nodes.  This defaults
        /// to <b>256MB</b>.
        /// </para>
        /// <note>
        /// <para>
        /// The <a href="https://ceph.com/community/new-luminous-bluestore/">Ceph documentation</a>
        /// states that OSD may tend to underestimate the RAM it's using by up to 1.5 times.
        /// To avoid potential memory issues, neonHIVE  will adjust this value by dividing it 
        /// by 1.5 to when actually configuring OSD services.
        /// </para>
        /// <para>
        /// You should also take care to leave 1-2GB of RAM for the host Linux operating system
        /// as well as the OSD non-cache related memory when you're configuring this property.
        /// </para>
        /// <note>
        /// The default is probably too small for production environments
        /// </note>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "OSDCacheSize", Required = Required.Default)]
        [DefaultValue(defaultOSDCacheSize)]
        public string OSDCacheSize { get; set; } = defaultOSDCacheSize;

        /// <summary>
        /// <para>
        /// Specifies the default size to allocate for the OSD journals.  This can be a 
        /// byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, 
        /// or <b>1TB</b>.  This  can be overridden for specific nodes.  This defaults to <b>1GB</b>.
        /// </para>
        /// <note>
        /// The default is probably too small for production environments
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "OSDJournalSize", Required = Required.Default)]
        [DefaultValue(defaultOSDJournalSize)]
        public string OSDJournalSize { get; set; } = defaultOSDJournalSize;

        /// <summary>
        /// Specifies the maximum size of a Ceph RADOS object in bytes.  This can be a 
        /// byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, <b>2GB</b>, 
        /// or <b>1TB</b>.  This can be overridden for specific nodes.  This defaults to 
        /// <b>5GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OSDObjectSizeMax", Required = Required.Default)]
        [DefaultValue(defaultOSDObjectSizeMax)]
        public string OSDObjectSizeMax { get; set; } = defaultOSDObjectSizeMax;

        /// <summary>
        /// Specifies the default number of object replicas to be stored in the hive.
        /// This defaults to the minimum of 3 or the number of OSD nodes provisioned
        /// in the hive.
        /// </summary>
        [JsonProperty(PropertyName = "OSDReplicaCount", Required = Required.Default)]
        [DefaultValue(0)]
        public int OSDReplicaCount { get; set; } = 0;

        /// <summary>
        /// Specifies the minimum number of objects replicas required when the
        /// Ceph storage cluster is operating in a degraded state.  This defaults
        /// to <see cref="OSDReplicaCount"/><b>-1</b> unless <see cref="OSDReplicaCount"/><b>==1</b>
        /// in which case this will also default to 1.
        /// </summary>
        [JsonProperty(PropertyName = "OSDReplicaCountMin", Required = Required.Default)]
        [DefaultValue(0)]
        public int OSDReplicaCountMin { get; set; }

        /// <summary>
        /// Specifies the default number of placement groups assigned to each OSD.
        /// This defaults to <b>100</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OSDPlacementGroups", Required = Required.Default)]
        [DefaultValue(defaultOSDPlacementGroups)]
        public int OSDPlacementGroups { get; set; } = defaultOSDPlacementGroups;

        /// <summary>
        /// <para>
        /// Specifies the default amount of RAM to allocate to Ceph MDS processes for 
        /// caching.  byte count or a number with units like <b>512MB</b>, <b>0.5GB</b>, 
        /// <b>2GB</b>, or <b>1TB</b>.  This can be overridden for specific nodes.  This
        /// defaults to <b>64MB</b>.
        /// </para>
        /// <note>
        /// <para>
        /// The Ceph documentation states that MDS may tend to underestimate the RAM it's 
        /// using by up to 1.5 times.  To avoid potential memory issues, neonHIVE will 
        /// adjust this value by dividing it by 1.5 to when actually configuring the 
        /// MDS services.
        /// </para>
        /// <para>
        /// You should also take care to leave 1-2GB of RAM for the host Linux operating system
        /// as well as the OSD non-cache related memory when you're configuring this property.
        /// </para>
        /// </note>
        /// <note>
        /// The default is probably too small for production environments
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "MDSCacheSize", Required = Required.Default)]
        [DefaultValue(defaultMDSCacheSize)]
        public string MDSCacheSize { get; set; } = defaultMDSCacheSize;

        /// <summary>
        /// URL for the Docker volume plugin package to be installed on all hive
        /// nodes when Ceph is enabled.  This defaults to the latest released version.
        /// </summary>
        [JsonProperty(PropertyName = "VolumePluginPackage", Required = Required.Default)]
        [DefaultValue(defaultVolumePluginPackage)]
        public string VolumePluginPackage { get; set; } = defaultVolumePluginPackage;

        /// <summary>
        /// Indicates whether the Ceph dashboard requires TLS.
        /// </summary>
        /// <remarks>
        /// <note>
        /// All Ceph versions after <b>luminous</b> require TLS.
        /// </note>
        /// </remarks>
        [JsonIgnore]
        [YamlIgnore]
        public bool DashboardTls => Release != "luminous";

        /// <summary>
        /// Returns the TCP port for the Ceph dashboard.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The older <b>luminous</b> release hardcodes the dashboard to port <b>7000</b>
        /// and the new releases will be listening at <see cref="HiveHostPorts.CephDashboard"/>.
        /// </note>
        /// </remarks>
        [JsonIgnore]
        [YamlIgnore]
        public int DashboardPort => Release == "luminous" ? 7000 : HiveHostPorts.CephDashboard;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(HiveDefinition hiveDefinition)
        {
            if (!Enabled)
            {
                return;
            }

            VolumePluginPackage = VolumePluginPackage ?? defaultVolumePluginPackage;

            if (string.IsNullOrEmpty(VolumePluginPackage) || !Uri.TryCreate(VolumePluginPackage, UriKind.Absolute, out var uri))
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(VolumePluginPackage)}={VolumePluginPackage}] must be set to a valid package URL.");
            }

            Release = Release ?? defaultRelease;
            Release = Release.ToLowerInvariant();

            if (!SupportedReleases.Contains(Release))
            {
                throw new HiveDefinitionException($"[{Release}] is not a supported Ceph release.");
            }

            // Examine the Ceph related labels for the hive nodes to verify that any 
            // specified Ceph service assignments are reasonable.  We will also try to
            // automatically assign Ceph services to nodes when there are no explicit
            // assignments.

            // $hack(jeff.lill):
            //
            // It's not super clean to be doing this here but it's easy and I believe
            // I've already done this sort of thing elsewhere.

            var cephMONCount = hiveDefinition.Nodes.Count(n => n.Labels.CephMON);
            var cephOSDCount = hiveDefinition.Nodes.Count(n => n.Labels.CephOSD);
            var cephMDSCount = hiveDefinition.Nodes.Count(n => n.Labels.CephMDS);

            if (cephMONCount == 0)
            {
                // No Ceph monitor/manager nodes are explicitly assigned so we're going to
                // automatically place these on the hive managers.

                foreach (var node in hiveDefinition.Nodes.Where(n => n.IsManager))
                {
                    node.Labels.CephMON = true;
                }
            }

            if (cephOSDCount == 0)
            {
                // No Ceph OSD nodes are explicitly assigned.
                //
                // If the hive has at least three workers, we'll provision the
                // OSDs on all of the workers.
                //
                // If there are fewer than three workers, we'll provision ODSs on
                // all managers and all workers (AKA the Swarm nodes).

                if (hiveDefinition.Workers.Count() >= 3)
                {
                    foreach (var node in hiveDefinition.Workers)
                    {
                        node.Labels.CephOSD = true;
                    }
                }
                else
                {
                    foreach (var node in hiveDefinition.Swarm)
                    {
                        node.Labels.CephOSD = true;
                    }
                }
            }

            if (cephMONCount == 0)
            {
                // No Ceph MDS nodes are explicitly assigned so we're going to provision
                // these on the Ceph Monitor servers.

                foreach (var node in hiveDefinition.Nodes.Where(n => n.Labels.CephMON))
                {
                    node.Labels.CephMDS = true;
                }
            }

            // Recount the Ceph component instances to account for any the automatic
            // provisioning assignments that may have been performed above.

            cephMONCount = hiveDefinition.Nodes.Count(n => n.Labels.CephMON);
            cephOSDCount = hiveDefinition.Nodes.Count(n => n.Labels.CephOSD);
            cephMDSCount = hiveDefinition.Nodes.Count(n => n.Labels.CephMDS);

            // Validate the properties.

            if (string.IsNullOrWhiteSpace(Release))
            {
                Release = defaultRelease;
            }

            if (Release == string.Empty)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(Release)}={Release}] is not valid.  Please specify something like [{defaultRelease}].");
            }

            if (HiveDefinition.ValidateSize(OSDDriveSize, this.GetType(), nameof(OSDDriveSize)) < NeonHelper.Giga)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDDriveSize)}={OSDDriveSize}] cannot be less than [1GB].");
            }

            if (HiveDefinition.ValidateSize(OSDCacheSize, this.GetType(), nameof(OSDCacheSize)) < 64 * NeonHelper.Mega)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDCacheSize)}={OSDCacheSize}] cannot be less than [64MB].");
            }

            if (HiveDefinition.ValidateSize(OSDJournalSize, this.GetType(), nameof(OSDJournalSize)) < 64 * NeonHelper.Mega)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDJournalSize)}={OSDJournalSize}] cannot be less than [64MB].");
            }

            if (HiveDefinition.ValidateSize(OSDObjectSizeMax, this.GetType(), nameof(OSDObjectSizeMax)) < 64 * NeonHelper.Mega)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDObjectSizeMax)}={OSDObjectSizeMax}] cannot be less than [64MB].");
            }

            if (HiveDefinition.ValidateSize(MDSCacheSize, this.GetType(), nameof(MDSCacheSize)) < 64 * NeonHelper.Mega)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(MDSCacheSize)}={MDSCacheSize}] cannot be less than [64MB].");
            }

            if (cephMONCount == 0)
            {
                throw new HiveDefinitionException($"Ceph storage cluster requires at least one monitor node.");
            }

            if (cephOSDCount == 0)
            {
                throw new HiveDefinitionException($"Ceph storage cluster requires at least one OSD (data) node.");
            }

            if (cephMDSCount == 0)
            {
                throw new HiveDefinitionException($"Ceph storage cluster requires at least one MDS (metadata) node.");
            }

            if (OSDReplicaCount == 0)
            {
                // Set a reasonable default.

                OSDReplicaCount = Math.Min(3, cephOSDCount);
            }

            if (OSDReplicaCount < 0)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDReplicaCount)}={OSDReplicaCount}] cannot be less than zero.");
            }

            if (OSDReplicaCount > cephOSDCount)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDReplicaCount)}={OSDReplicaCount}] cannot be greater than the number of OSD nodes [{cephOSDCount}].");
            }

            if (OSDReplicaCountMin == 0)
            {
                // Set a reasonable default.

                if (OSDReplicaCount == 1)
                {
                    OSDReplicaCountMin = 1;
                }
                else
                {
                    OSDReplicaCountMin = OSDReplicaCount - 1;
                }
            }

            if (OSDReplicaCountMin < 0)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDReplicaCountMin)}={OSDReplicaCountMin}] cannot be less than zero.");
            }

            if (OSDReplicaCountMin > OSDReplicaCount)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDReplicaCountMin)}={OSDReplicaCountMin}] cannot be less than [{nameof(OSDReplicaCount)}={OSDReplicaCount}].");
            }

            if (OSDReplicaCountMin > cephOSDCount)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDReplicaCountMin)}={OSDReplicaCountMin}] cannot be greater than the number of OSD nodes [{cephOSDCount}].");
            }

            if (OSDPlacementGroups < 8)
            {
                throw new HiveDefinitionException($"[{nameof(HiveFSOptions)}.{nameof(OSDPlacementGroups)}={OSDPlacementGroups}] cannot be less than [8].");
            }
        }
    }
}
