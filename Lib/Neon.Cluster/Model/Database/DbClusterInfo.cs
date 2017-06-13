//-----------------------------------------------------------------------------
// FILE:	    DbClusterInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Used to persist information and status for a NeonCluster database to Consul at <b>neon/databases/DBNAME</b>.
    /// </summary>
    public class DbClusterInfo
    {
        /// <summary>
        /// Database specific information required for clients to establish a connection to the database.
        /// This is encoded as JSON.
        /// </summary>
        [JsonProperty(PropertyName = "ClientConfig", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ClientConfig { get; set; }

        /// <summary>
        /// The overall status of the database cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Status", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(DbStatus.Unknown)]
        public DbStatus Status { get; set;}

        /// <summary>
        /// Status information for indvidual database cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<DbNode> Nodes { get; set; } = new List<DbNode>();
    }
}
