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
        private const string defaultDriveSize = "128GB";
        private const string defaultCacheSize = "1GB";

        /// <summary>
        /// Indicates whether Ceph storage is to be enabled for the cluster.  
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default)]
        [DefaultValue(false)]
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Specifies the default size of the Ceph OSD drives created for cloud and
        /// hypervisor based environments.  This can be an long byte count or a long
        /// with units like <b>512MB</b> or <b>2GB</b>.  This can be overridden 
        /// for specific nodes.  This defaults to <b>128GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "DriveSize", Required = Required.Default)]
        [DefaultValue(defaultDriveSize)]
        public string DriveSize { get; set; } = defaultDriveSize;

        /// <summary>
        /// Specifies the default amount of RAM to allocate to Ceph OSD processes for 
        /// caching.  This can be an long byte count or a long with units like <b>512MB</b> 
        /// or <b>2GB</b>.  This can be overridden for specific nodes.  This defaults
        /// to <b>1GB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CacheSize", Required = Required.Default)]
        [DefaultValue(defaultCacheSize)]
        public string CacheSize { get; set; } = defaultCacheSize;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (!Enabled)
            {
                return;
            }

            if (ClusterDefinition.ValidateSize(DriveSize, this.GetType(), nameof(DriveSize)) < NeonHelper.Giga)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(DriveSize)}={DriveSize}] is cannot be less than [1GB].");
            }

            if (ClusterDefinition.ValidateSize(CacheSize, this.GetType(), nameof(CacheSize)) < 100 * NeonHelper.Mega)
            {
                throw new ClusterDefinitionException($"[{nameof(CephOptions)}.{nameof(CacheSize)}={CacheSize}] is cannot be less than [100MB].");
            }
        }
    }
}
