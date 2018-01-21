//-----------------------------------------------------------------------------
// FILE:	    CouchbaseSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;

using Neon.Common;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.Data
{
    /// <summary>
    /// Settings used to connect a Couchbase client to a Couchbase bucket.
    /// </summary>
    public class CouchbaseSettings
    {
        /// <summary>
        /// One or more Couchbase server URIs.
        /// </summary>
        /// <remarks>
        /// You must specify the URI for at least one operating Couchbase node.  The Couchbase
        /// client will use this to discover the remaining nodes.  It is a best practice to
        /// specify multiple nodes in a clustered environment to avoid initial connection
        /// problems if any single node is down.
        /// </remarks>
        [JsonProperty(PropertyName = "Servers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<Uri> Servers { get; set; } = new List<Uri>();

        /// <summary>
        /// Optionally specifies the name of the target Couchbase bucket.
        /// </summary>
        [JsonProperty(PropertyName = "Bucket", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Bucket { get; set; }

        /// <summary>
        /// Maximum time (milliseconds) to wait to establish a server connection (defaults to <b>10 seconds</b>).
        /// </summary>
        [JsonProperty(PropertyName = "ConnectTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(10000)]
        public int ConnectTimeout { get; set; } = 10000;

        /// <summary>
        /// Maximum time (milliseconds) to wait to transmit a server request (defaults to <b>10 seconds</b>).
        /// </summary>
        [JsonProperty(PropertyName = "SendTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(10000)]
        public int SendTimeout { get; set; } = 10000;

        /// <summary>
        /// Maximum time (milliseconds) to wait for an operation to complete (defaults to <b>10 seconds</b>).
        /// </summary>
        [JsonProperty(PropertyName = "OperationTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(10000)]
        public int OperationTimeout { get; set; } = 10000;

        /// <summary>
        /// Maximum time (milliseconds) to wait for a query to complete (defaults to 75 seconds).
        /// </summary>
        [JsonProperty(PropertyName = "QueryRequestTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(75000)]
        public int QueryRequestTimeout { get; set; } = 75000;

        /// <summary>
        /// Maximum number of pooled connections to a server bucket (defaults to <b>5</b>).
        /// </summary>
        [JsonProperty(PropertyName = "MaxPoolConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5)]
        public int MaxPoolConnections { get; set; } = 5;

        /// <summary>
        /// Minimum number of pooled connections to a server bucket (defaults to <b>2</b>).
        /// </summary>
        [JsonProperty(PropertyName = "MinPoolConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(2)]
        public int MinPoolConnections { get; set; } = 2;

        /// <summary>
        /// Returns <c>true</c> if the settings are valid.
        /// </summary>
        [JsonIgnore]
        public bool IsValid
        {
            get
            {
                if (Servers == null || Servers.Count == 0)
                {
                    return false;
                }

                foreach (var server in Servers)
                {
                    if (server == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
