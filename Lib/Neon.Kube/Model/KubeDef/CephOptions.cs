//-----------------------------------------------------------------------------
// FILE:	    CephOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// Specifies the options for configuring the cluster integrated
    /// <a href="https://ceph.com/">Ceph Distributed Storage</a>
    /// cluster.
    /// </summary>
    public class CephOptions
    {
        private const string    defaultRelease             = "mimic";
        private const string    defaultOSDDriveSize        = "16Gi";
        private const string    defaultOSDCacheSize        = "256Mi";
        private const string    defaultOSDJournalSize      = "1Gi";
        private const string    defaultOSDObjectSizeMax    = "5Gi";
        private const int       defaultOSDPlacementGroups  = 100;
        private const string    defaultMDSCacheSize        = "64Mi";
        private const string    defaultVolumePluginPackage = "https://s3-us-west-2.amazonaws.com/neonforge/neoncluster/neon-volume-plugin_latest.deb";

        /// <summary>
        /// Returns the names of the supported Ceph releases.
        /// </summary>
        private IEnumerable<string> SupportedReleases =
            new string[]
            {
                "mimic"
            };

        /// <summary>
        /// The fudge factor to apply to Ceph cache sizes before actually
        /// configuring the services.
        /// </summary>
        public const double CacheSizeFudge = 1.0 / 1.5;

        /// <summary>
        /// Indicates whether Ceph storage is to be enabled for the cluster.  
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default)]
        [YamlMember(Alias = "enabled", ApplyNamingConventions = false)]
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
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "Release", Required = Required.Default)]
        [YamlMember(Alias = "release", ApplyNamingConventions = false)]
        [DefaultValue(defaultRelease)]
        public string Release { get; set; } = defaultRelease;

        /// <summary>
        /// <para>
        /// Specifies the default size of the Ceph OSD drives created for cloud and
        /// hypervisor based environments (<see cref="ByteUnits"/>).  This can be 
        /// overridden  for specific nodes.  This defaults to <b>16Gi</b>.
        /// </para>
        /// <note>
        /// The default may be too small for production environments
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "OSDDriveSize", Required = Required.Default)]
        [YamlMember(Alias = "osdDriveSize", ApplyNamingConventions = false)]
        [DefaultValue(defaultOSDDriveSize)]
        public string OSDDriveSize { get; set; } = defaultOSDDriveSize;

        /// <summary>
        /// <para>
        /// Specifies the default amount of RAM to allocate to Ceph OSD processes for 
        /// caching (<see cref="ByteUnits"/>).  his can be overridden for specific nodes. 
        /// This defaults to <b>256Mi</b>.
        /// </para>
        /// <note>
        /// <para>
        /// The <a href="https://ceph.com/community/new-luminous-bluestore/">Ceph documentation</a>
        /// states that OSD may tend to underestimate the RAM it's using by up to 1.5 times.
        /// To avoid potential memory issues, neonKUBE  will adjust this value by dividing it 
        /// by 1.5 to when actually configuring OSD services.
        /// </para>
        /// <para>
        /// You should also take care to leave 1-2GB of RAM for the host Linux operating system
        /// as well as the OSD non-cache related memory when you're configuring this property.
        /// </para>
        /// <note>
        /// The default may be too small for production environments
        /// </note>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "OSDCacheSize", Required = Required.Default)]
        [YamlMember(Alias = "osdCacheSize", ApplyNamingConventions = false)]
        [DefaultValue(defaultOSDCacheSize)]
        public string OSDCacheSize { get; set; } = defaultOSDCacheSize;

        /// <summary>
        /// <para>
        /// Specifies the default size to allocate for the OSD journals  (<see cref="ByteUnits"/>).  
        /// This  can be overridden for specific nodes.  This defaults to <b>1Gi</b>.
        /// </para>
        /// <note>
        /// The default may be too small for production environments
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "OSDJournalSize", Required = Required.Default)]
        [YamlMember(Alias = "osdJournalSize", ApplyNamingConventions = false)]
        [DefaultValue(defaultOSDJournalSize)]
        public string OSDJournalSize { get; set; } = defaultOSDJournalSize;

        /// <summary>
        /// Specifies the maximum size of a Ceph RADOS object in bytes (<see cref="ByteUnits"/>).  
        /// This can be overridden for specific nodes.  This defaults to <b>5Gi</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OSDObjectSizeMax", Required = Required.Default)]
        [YamlMember(Alias = "osdObjectSizeMax", ApplyNamingConventions = false)]
        [DefaultValue(defaultOSDObjectSizeMax)]
        public string OSDObjectSizeMax { get; set; } = defaultOSDObjectSizeMax;

        /// <summary>
        /// Specifies the default number of object replicas to be stored in the cluster.
        /// This defaults to the minimum of 3 or the number of OSD nodes provisioned
        /// in the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "OSDReplicaCount", Required = Required.Default)]
        [YamlMember(Alias = "osdReplicaCount", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int OSDReplicaCount { get; set; } = 0;

        /// <summary>
        /// Specifies the minimum number of objects replicas required when the
        /// Ceph storage cluster is operating in a degraded state.  This defaults
        /// to <see cref="OSDReplicaCount"/><b>-1</b> unless <see cref="OSDReplicaCount"/><b>==1</b>
        /// in which case this will also default to 1.
        /// </summary>
        [JsonProperty(PropertyName = "OSDReplicaCountMin", Required = Required.Default)]
        [YamlMember(Alias = "osdReplicaCountMin", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int OSDReplicaCountMin { get; set; }

        /// <summary>
        /// Specifies the default number of placement groups assigned to each OSD.
        /// This defaults to <b>100</b>.
        /// </summary>
        [JsonProperty(PropertyName = "OSDPlacementGroups", Required = Required.Default)]
        [YamlMember(Alias = "osdPlacementGroups", ApplyNamingConventions = false)]
        [DefaultValue(defaultOSDPlacementGroups)]
        public int OSDPlacementGroups { get; set; } = defaultOSDPlacementGroups;

        /// <summary>
        /// <para>
        /// Specifies the default amount of RAM to allocate to Ceph MDS processes for 
        /// caching (<see cref="ByteUnits"/>). This can be overridden for specific nodes. 
        /// This defaults to <b>64Mi</b>.
        /// </para>
        /// <note>
        /// <para>
        /// The Ceph documentation states that MDS may tend to underestimate the RAM it's 
        /// using by up to 1.5 times.  To avoid potential memory issues, neonKUBE will 
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
        [YamlMember(Alias = "mdsCacheSize", ApplyNamingConventions = false)]
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

            Release = Release ?? defaultRelease;
            Release = Release.ToLowerInvariant();

            if (!SupportedReleases.Contains(Release))
            {
                throw new ClusterDefinitionException($"[{Release}] is not a supported Ceph release.");
            }

            // Examine the Ceph related labels for the cluster nodes to verify that any 
            // specified Ceph service assignments are reasonable.  We will also try to
            // automatically assign Ceph services to nodes when there are no explicit
            // assignments.

            var cephMONCount = clusterDefinition.Nodes.Count(n => n.Labels.CephMON);
            var cephOSDCount = clusterDefinition.Nodes.Count(n => n.Labels.CephOSD);
            var cephMDSCount = clusterDefinition.Nodes.Count(n => n.Labels.CephMDS);
            var cephMGRCount = clusterDefinition.Nodes.Count(n => n.Labels.CephMGR);

            if (cephMONCount == 0)
            {
                // No Ceph monitor/manager nodes are explicitly assigned so we're going to
                // automatically place these on the cluster managers.

                foreach (var node in clusterDefinition.Nodes.Where(n => n.IsMaster))
                {
                    node.Labels.CephMON = true;
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
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.CephOSD = true;
                    }
                }
            }

            if (cephMDSCount == 0)
            {
                // No Ceph MDS nodes are explicitly assigned so we're going to provision
                // these on the Ceph Monitor servers.

                foreach (var node in clusterDefinition.Nodes.Where(n => n.Labels.CephMON))
                {
                    node.Labels.CephMDS = true;
                }
            }

            if (cephMGRCount == 0)
            {
                // No Ceph MGR nodes are explicitly assigned so we're going to provision
                // these on the Ceph Monitor servers.

                foreach (var node in clusterDefinition.Nodes.Where(n => n.Labels.CephMON))
                {
                    node.Labels.CephMGR = true;
                }
            }

            // Recount the Ceph component instances to account for any the automatic
            // provisioning assignments that may have been performed above.

            cephMONCount = clusterDefinition.Nodes.Count(n => n.Labels.CephMON);
            cephOSDCount = clusterDefinition.Nodes.Count(n => n.Labels.CephOSD);
            cephMDSCount = clusterDefinition.Nodes.Count(n => n.Labels.CephMDS);

            // Validate the properties.

            if (string.IsNullOrWhiteSpace(Release))
            {
                Release = defaultRelease;
            }

            if (Release == string.Empty)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(Release)}={Release}] is not valid.  Please specify something like [{defaultRelease}].");
            }

            if (ClusterDefinition.ValidateSize(OSDDriveSize, this.GetType(), nameof(OSDDriveSize)) < ByteUnits.GibiBytes)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDDriveSize)}={OSDDriveSize}] cannot be less than [1Gi].");
            }

            if (ClusterDefinition.ValidateSize(OSDCacheSize, this.GetType(), nameof(OSDCacheSize)) < 64 * ByteUnits.MebiBytes)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDCacheSize)}={OSDCacheSize}] cannot be less than [64Mi].");
            }

            if (ClusterDefinition.ValidateSize(OSDJournalSize, this.GetType(), nameof(OSDJournalSize)) < 64 * ByteUnits.MebiBytes)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDJournalSize)}={OSDJournalSize}] cannot be less than [64Mi].");
            }

            if (ClusterDefinition.ValidateSize(OSDObjectSizeMax, this.GetType(), nameof(OSDObjectSizeMax)) < 64 * ByteUnits.MebiBytes)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(OSDObjectSizeMax)}={OSDObjectSizeMax}] cannot be less than [64Mi].");
            }

            if (ClusterDefinition.ValidateSize(MDSCacheSize, this.GetType(), nameof(MDSCacheSize)) < 64 * ByteUnits.MebiBytes)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(MDSCacheSize)}={MDSCacheSize}] cannot be less than [64Mi].");
            }

            if (cephMONCount == 0)
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

            //if (cephMGRCount == 0)
            //{
            //    throw new ClusterDefinitionException($"Ceph storage cluster requires at least one MDS (metadata) node.");
            //}

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
