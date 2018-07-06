//-----------------------------------------------------------------------------
// FILE:	    CephConfig.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Hive
{
    /// <summary>
    /// Holds Ceph Storage Cluster configuration information.
    /// </summary>
    public class CephConfig
    {
        /// <summary>
        /// The hive's unique identifier (also known as its file system identifier).
        /// This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Fsid", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Fsid { get; set; } = null;

        /// <summary>
        /// The hive name.  This defaults to <b>ceph</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("ceph")]
        public string Name { get; set; } = "ceph";

        /// <summary>
        /// The Ceph cluster monitor keyring.  Monitors communicate with each other via a secret key.
        /// This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "MonitorKeyring", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string MonitorKeyring { get; set; } = null;

        /// <summary>
        /// The cluster administrator keyring for the <b>client.admin</b> user.  This defaults
        /// to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "AdminKeyring", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string AdminKeyring { get; set; } = null;

        /// <summary>
        /// The bootstrap OSD keyring.  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "OSDKeyring", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string OSDKeyring { get; set; } = null;

        /// <summary>
        /// The monitor map (byte array).  This defaults to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "MonitorMap", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public byte[] MonitorMap { get; set; } = null;
    }
}
