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
        private const string defaultVersion             = "luminous";
        private const string defaultDriveSize           = "128GB";
        private const string defaultCacheSize           = "1GB";
        private const string defaultJournalSize         = "5GB";
        private const string defaultObjectSizeMax       = "5GB";
        private const int    defaultPlacementGroupCount = 100;

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
        [JsonProperty(PropertyName = "DriveSize", Required = Required.Default)]
        [DefaultValue(defaultDriveSize)]
        public string DriveSize { get; set; } = defaultDriveSize;

        /// <summary>
        /// Specifies the default amount of RAM to allocate to Ceph OSD processes for 
        /// caching.  This can be a long byte count or a long with units like <b>512MB</b> 
        /// or <b>2GB</b>.  This can be overridden for specific nodes.  This defaults
        /// to <b>1GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CacheSize", Required = Required.Default)]
        [DefaultValue(defaultCacheSize)]
        public string CacheSize { get; set; } = defaultCacheSize;

        /// <summary>
        /// Specifies the default size to allocate for the OSD journals.  This can be a 
        /// long byte count or a long with units like <b>512MB</b> or <b>2GB</b>.  This 
        /// can be overridden for specific nodes.  This defaults to <b>5GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "JournalSize", Required = Required.Default)]
        [DefaultValue(defaultJournalSize)]
        public string JournalSize { get; set; } = defaultJournalSize;

        /// <summary>
        /// Specifies the maximum size of a Ceph RADOS object in bytes.  This can be a 
        /// long byte count or a long with units like <b>512MB</b> or <b>2GB</b>.  This 
        /// can be overridden for specific nodes.  This defaults to <b>5GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ObjectSizeMax", Required = Required.Default)]
        [DefaultValue(defaultObjectSizeMax)]
        public string ObjectSizeMax { get; set; } = defaultObjectSizeMax;

        /// <summary>
        /// Specifies the default number of object replicas to be stored in the cluster.
        /// This defaults to the minimum of 3 or the number of OSD nodes provisioned
        /// in the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "ReplicaCount", Required = Required.Default)]
        [DefaultValue(0)]
        public int ReplicaCount { get; set; } = 0;

        /// <summary>
        /// Specifies the minimum number of objects replicas required when the
        /// Ceph storage cluster is operating in a degraded state.  This defaults
        /// to <see cref="ReplicaCount"/><b>-1</b> unless <see cref="ReplicaCount"/><b>==1</b>
        /// in which case this will also default to 1.
        /// </summary>
        [JsonProperty(PropertyName = "ReplicaCountMin", Required = Required.Default)]
        [DefaultValue(0)]
        public int ReplicaCountMin { get; set; }

        /// <summary>
        /// Specifies the default number of placement groups assigned to each OSD.
        /// This defaults to <b>100</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PlacementGroupCount", Required = Required.Default)]
        [DefaultValue(defaultPlacementGroupCount)]
        public int PlacementGroupCount { get; set; } = defaultPlacementGroupCount;

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

            if (ClusterDefinition.ValidateSize(DriveSize, this.GetType(), nameof(DriveSize)) < NeonHelper.Giga)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(DriveSize)}={DriveSize}] cannot be less than [1GB].");
            }

            if (ClusterDefinition.ValidateSize(CacheSize, this.GetType(), nameof(CacheSize)) < 100 * NeonHelper.Mega)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(CacheSize)}={CacheSize}] cannot be less than [100MB].");
            }

            if (ClusterDefinition.ValidateSize(JournalSize, this.GetType(), nameof(JournalSize)) < 100 * NeonHelper.Mega)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(JournalSize)}={JournalSize}] cannot be less than [100MB].");
            }

            if (ClusterDefinition.ValidateSize(ObjectSizeMax, this.GetType(), nameof(ObjectSizeMax)) < 100 * NeonHelper.Mega)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(ObjectSizeMax)}={ObjectSizeMax}] cannot be less than [100MB].");
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

            if (ReplicaCount == 0)
            {
                // Set a reasonable default.

                ReplicaCount = Math.Min(3, cephOSDCount);
            }

            if (ReplicaCount < 0)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(ReplicaCount)}={ReplicaCount}] cannot be less than zero.");
            }

            if (ReplicaCount > cephOSDCount)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(ReplicaCount)}={ReplicaCount}] cannot be greater than the number of OSD nodes [{cephOSDCount}].");
            }

            if (ReplicaCountMin == 0)
            {
                // Set a reasonable default.

                if (ReplicaCount == 1)
                {
                    ReplicaCountMin = 1;
                }
                else
                {
                    ReplicaCountMin = ReplicaCount - 1;
                }
            }

            if (ReplicaCountMin < 0)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(ReplicaCountMin)}={ReplicaCountMin}] cannot be less than zero.");
            }

            if (ReplicaCountMin > ReplicaCount)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(ReplicaCountMin)}={ReplicaCountMin}] cannot be less than [{nameof(ReplicaCount)}={ReplicaCount}].");
            }

            if (ReplicaCountMin > cephOSDCount)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(ReplicaCountMin)}={ReplicaCountMin}] cannot be greater than the number of OSD nodes [{cephOSDCount}].");
            }

            if (PlacementGroupCount < 8)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(PlacementGroupCount)}={PlacementGroupCount}] cannot be less than [8].");
            }
        }
    }
}
