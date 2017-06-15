//-----------------------------------------------------------------------------
// FILE:	    DbClusterInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Collections.Generic;
using System.ComponentModel;

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
        [JsonProperty(PropertyName = "ClientSettings", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string ClientSettings { get; set; }

        /// <summary>
        /// Identifies the underlying service type.
        /// </summary>
        [JsonProperty(PropertyName = "ServiceType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue("")]
        public string ServiceType { get; set; }

        /// <summary>
        /// The overall status of the database cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Status", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(DbStatus.Unknown)]
        public DbStatus Status { get; set;}

        /// <summary>
        /// A status or error message for the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Message", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue("")]
        public string Message { get; set; }

        /// <summary>
        /// Status information for indvidual database cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public List<DbNode> Nodes { get; set; } = new List<DbNode>();

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            var other = obj as DbClusterInfo;

            if (other == null)
            {
                return false;
            }

            return NeonHelper.JsonSerialize(this) == NeonHelper.JsonSerialize(other);
        }
        /// <inheritdoc/>

        public override int GetHashCode()
        {
            return NeonHelper.JsonSerialize(this).GetHashCode();
        }
    }
}
