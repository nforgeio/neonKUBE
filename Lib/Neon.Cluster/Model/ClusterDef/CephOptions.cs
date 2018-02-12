//-----------------------------------------------------------------------------
// FILE:	    CephOptions.cs
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

using Neon.Common;
using Neon.IO;

namespace Neon.Cluster
{
    /// <summary>
    /// Specifies the options for configuring the cluster integrated <a href="https://ceph.com/">Ceph</a>
    /// distributed storage cluster.  This is disabled by default.
    /// </summary>
    public class CephOptions
    {
        private const string defaultVersion            = "luminous";
        private const string defaultOSDDriveSize       = "128GB";
        private const string defaultOSDCacheSize       = "1GB";
        private const string defaultOSDJournalSize     = "5GB";
        private const string defaultOSDObjectSizeMax   = "5GB";
        private const int    defaultOSDPlacementGroups = 100;
        private const string defaultMDSCacheSize       = "1GB";

        /// <summary>
        /// The fudge factor to apply to Ceph cache sizes before actually
        /// configuring the services.
        /// </summary>
        public const double CacheSizeFudge = 1.0/1.5;

        /// <summary>
        /// Indicates whether Ceph storage is to be enabled for the cluster.  
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default)]
        [DefaultValue(false)]
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Returns the Linux user for the Ceph components.
        /// </summary>
        [JsonIgnore]
        public string Username
        {
            get { return "ceph"; }
        }

        /// <summary>
        /// <para>
        /// Specifies the Ceph software release name and optional version, formatted
        /// like <b>luminous</b> or <b>luminous/12.2.2</b>.  The Ceph software releases
        /// are documented <a href="http://docs.ceph.com/docs/master/releases/">here</a>.
        /// This defaults to a reasonable recent release without a version number.
        /// </para>
        /// <note>
        /// The version number is currently ignored but may be honored in future
        /// neonCLUSTER releases such that you can install a specific version of
        /// a Ceph release.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default)]
        [DefaultValue(defaultVersion)]
        public string Version { get; set; } = defaultVersion;

        /// <summary>
        /// Specifies the default size of the Ceph OSD drives created for cloud and
        /// hypervisor based environments.  This can be a long byte count or a long
        /// with units like <b>512MB</b> or <b>2GB</b>.  This can be overridden 
        /// for specific nodes.  This defaults to <b>128GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OSDDriveSize", Required = Required.Default)]
        [DefaultValue(defaultOSDDriveSize)]
        public string OSDDriveSize { get; set; } = defaultOSDDriveSize;

        /// <summary>
        /// <para>
        /// Specifies the default amount of RAM to allocate to Ceph OSD processes for 
        /// caching.  This can be a long byte count or a long with units like <b>512MB</b> 
        /// or <b>2GB</b>.  This can be overridden for specific nodes.  This defaults
        /// to <b>1GB</b> (which is probably too small for production clusters).
        /// </para>
        /// <note>
        /// <para>
        /// The <a href="https://ceph.com/community/new-luminous-bluestore/">Ceph documentation</a>
        /// states that OSD may tend to underestimate the RAM it's using by up to 1.5 times.
        /// To avoid potential memory issues, neonCLUSTER  will adjust this value by dividing it 
        /// by 1.5 to when actually configuring OSD services.
        /// </para>
        /// <para>
        /// You should also take care to leave 1-2GB of RAM for the host Linux operating system
        /// as well as the OSD non-cache related memory when you're configuring this property.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "OSDCacheSize", Required = Required.Default)]
        [DefaultValue(defaultOSDCacheSize)]
        public string OSDCacheSize { get; set; } = defaultOSDCacheSize;

        /// <summary>
        /// Specifies the default size to allocate for the OSD journals.  This can be a 
        /// long byte count or a long with units like <b>512MB</b> or <b>2GB</b>.  This 
        /// can be overridden for specific nodes.  This defaults to <b>5GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OSDJournalSize", Required = Required.Default)]
        [DefaultValue(defaultOSDJournalSize)]
        public string OSDJournalSize { get; set; } = defaultOSDJournalSize;

        /// <summary>
        /// Specifies the maximum size of a Ceph RADOS object in bytes.  This can be a 
        /// long byte count or a long with units like <b>512MB</b> or <b>2GB</b>.  This 
        /// can be overridden for specific nodes.  This defaults to <b>5GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OSDObjectSizeMax", Required = Required.Default)]
        [DefaultValue(defaultOSDObjectSizeMax)]
        public string OSDObjectSizeMax { get; set; } = defaultOSDObjectSizeMax;

        /// <summary>
        /// Specifies the default number of object replicas to be stored in the cluster.
        /// This defaults to the minimum of 3 or the number of OSD nodes provisioned
        /// in the cluster.
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
        /// caching.  This can be a long byte count or a long with units like <b>512MB</b> 
        /// or <b>2GB</b>.  This can be overridden for specific nodes.  This defaults
        /// to <b>1GB</b> (which is probably too small for production clusters).
        /// </para>
        /// <note>
        /// <para>
        /// The Ceph documentation states that MDS may tend to underestimate the RAM it's 
        /// using by up to 1.5 times.  To avoid potential memory issues, neonCLUSTER  will 
        /// adjust this value by dividing it  by 1.5 to when actually configuring the 
        /// MDS services.
        /// </para>
        /// <para>
        /// You should also take care to leave 1-2GB of RAM for the host Linux operating system
        /// as well as the OSD non-cache related memory when you're configuring this property.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "MDSCacheSize", Required = Required.Default)]
        [DefaultValue(defaultMDSCacheSize)]
        public string MDSCacheSize { get; set; } = defaultMDSCacheSize;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (!Enabled)
            {
                return;
            }

            // Examine the Ceph related labels for the cluster nodes to verify that any 
            // specified Ceph service assignments are reasonable.  We will also try to
            // automatically assign Ceph services to nodes when there are no explicit
            // assignments.

            // $hack(jeff.lill):
            //
            // It's not super clean to be doing this here but it's easy and I believe
            // I've already done this sort of thing already.

            var cephMonitorCount = clusterDefinition.Nodes.Count(n => n.Labels.CephMonitor);
            var cephOSDCount     = clusterDefinition.Nodes.Count(n => n.Labels.CephOSD);
            var cephMDSCount     = clusterDefinition.Nodes.Count(n => n.Labels.CephMDS);

            if (cephMonitorCount == 0)
            {
                // No Ceph monitor/manager nodes are explicitly assigned so we're going to
                // automatically place these on the cluster managers.

                foreach (var node in clusterDefinition.Nodes.Where(n => n.IsManager))
                {
                    node.Labels.CephMonitor = true;
                }
            }

            if (cephOSDCount == 0)
            {
                // No Ceph OSD nodes are explicitly assigned.
                //
                // If the cluster has at least three workers, we'll provision the
                // OSDs on all of the workers.
                //
                // If there are fewer than three workers, we'll provision ODSs on
                // all managers and all workers (AKA the Swarm nodes).

                if (clusterDefinition.Workers.Count() >= 3)
                {
                    foreach (var node in clusterDefinition.Workers)
                    {
                        node.Labels.CephOSD = true;
                    }
                }
                else
                {
                    foreach (var node in clusterDefinition.Swarm)
                    {
                        node.Labels.CephOSD = true;
                    }
                }
            }

            if (cephMonitorCount == 0)
            {
                // No Ceph MSD nodes are explicitly assigned so we're going to provision
                // these on the OSD servers.

                foreach (var node in clusterDefinition.Nodes.Where(n => n.Labels.CephOSD))
                {
                    node.Labels.CephMDS = true;
                }
            }

            // Recount the Ceph component instances to account for any the automatic
            // provisioning assignments that may have been performed above.

            cephMonitorCount = clusterDefinition.Nodes.Count(n => n.Labels.CephMonitor);
            cephOSDCount     = clusterDefinition.Nodes.Count(n => n.Labels.CephOSD);
            cephMDSCount     = clusterDefinition.Nodes.Count(n => n.Labels.CephMDS);

            // Validate the properties.

            if (string.IsNullOrWhiteSpace(Version))
            {
                Version = defaultVersion;
            }

            if (Version == string.Empty)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(Version)}={Version}] is not a valid.  Please use something like [{defaultVersion}].");
            }

            if (ClusterDefinition.ValidateSize(OSDDriveSize, this.GetType(), nameof(OSDDriveSize)) < NeonHelper.Giga)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDDriveSize)}={OSDDriveSize}] cannot be less than [1GB].");
            }

            if (ClusterDefinition.ValidateSize(OSDCacheSize, this.GetType(), nameof(OSDCacheSize)) < 100 * NeonHelper.Mega)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDCacheSize)}={OSDCacheSize}] cannot be less than [100MB].");
            }

            if (ClusterDefinition.ValidateSize(OSDJournalSize, this.GetType(), nameof(OSDJournalSize)) < 100 * NeonHelper.Mega)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDJournalSize)}={OSDJournalSize}] cannot be less than [100MB].");
            }

            if (ClusterDefinition.ValidateSize(OSDObjectSizeMax, this.GetType(), nameof(OSDObjectSizeMax)) < 100 * NeonHelper.Mega)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDObjectSizeMax)}={OSDObjectSizeMax}] cannot be less than [100MB].");
            }

            if (ClusterDefinition.ValidateSize(MDSCacheSize, this.GetType(), nameof(MDSCacheSize)) < 100 * NeonHelper.Mega)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(MDSCacheSize)}={MDSCacheSize}] cannot be less than [100MB].");
            }

            if (cephMonitorCount == 0)
            {
                throw new ClusterDefinitionException($"Ceph storage cluster requires at least one monitor node.");
            }

            if (cephOSDCount == 0)
            {
                throw new ClusterDefinitionException($"Ceph storage cluster requires at least one OSD (data) node.");
            }

            if (cephMDSCount == 0)
            {
                throw new ClusterDefinitionException($"Ceph storage cluster requires at least one MDS (metadata) node.");
            }

            if (OSDReplicaCount == 0)
            {
                // Set a reasonable default.

                OSDReplicaCount = Math.Min(3, cephOSDCount);
            }

            if (OSDReplicaCount < 0)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDReplicaCount)}={OSDReplicaCount}] cannot be less than zero.");
            }

            if (OSDReplicaCount > cephOSDCount)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDReplicaCount)}={OSDReplicaCount}] cannot be greater than the number of OSD nodes [{cephOSDCount}].");
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
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDReplicaCountMin)}={OSDReplicaCountMin}] cannot be less than zero.");
            }

            if (OSDReplicaCountMin > OSDReplicaCount)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDReplicaCountMin)}={OSDReplicaCountMin}] cannot be less than [{nameof(OSDReplicaCount)}={OSDReplicaCount}].");
            }

            if (OSDReplicaCountMin > cephOSDCount)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDReplicaCountMin)}={OSDReplicaCountMin}] cannot be greater than the number of OSD nodes [{cephOSDCount}].");
            }

            if (OSDPlacementGroups < 8)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDPlacementGroups)}={OSDPlacementGroups}] cannot be less than [8].");
            }
        }
    }
}
