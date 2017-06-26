//-----------------------------------------------------------------------------
// FILE:	    CouchbaseSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
