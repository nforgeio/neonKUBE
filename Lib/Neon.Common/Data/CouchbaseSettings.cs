//-----------------------------------------------------------------------------
// FILE:	    CouchbaseSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;

using Neon.Common;
using Neon.Data;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.Common
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
        public List<Uri> Servers { get; set; } = new List<Uri>();

        /// <summary>
        /// Optionally specifies the name of the target Couchbase bucket.
        /// </summary>
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
