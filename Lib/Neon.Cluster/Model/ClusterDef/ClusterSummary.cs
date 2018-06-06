//-----------------------------------------------------------------------------
// FILE:	    ClusterSummary.cs
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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// <para>
    /// Summarizes the state of a cluster.
    /// </para>
    /// <note>
    /// This does not include any sensitive information so it should be
    /// reasonable to be able to upload this automatically as telemetry
    /// and also to include in GitHub issues.
    /// </note>
    /// </summary>
    public class ClusterSummary
    {
        //---------------------------------------------------------------------
        // Local types

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the summary for the currently logged-in cluster.
        /// </summary>
        /// <returns>The <see cref="ClusterSummary"/>.</returns>
        public static ClusterSummary FromCluster()
        {
            Covenant.Assert(NeonClusterHelper.IsConnected, "Cluster is not connected.");

            var summary = new ClusterSummary();

            return summary;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterSummary()
        {
        }

        /// <summary>
        /// A dictionary including all of the cluster's global Consul settings and values.
        /// </summary>
        [JsonProperty(PropertyName = "Globals", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public Dictionary<string, string> Globals { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Returns the cluster's creation date (UTC).
        /// </summary>
        [JsonIgnore]
        public DateTime CreateDateUtc
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.CreateDateUtc, out string value))
                {
                    value = null;
                }

                if (!DateTime.TryParse("", out var date))
                {
                    date = default(DateTime);
                }

                return date;
            }
        }

        /// <summary>
        /// Returns the version of <b>neon-cli</b> that created or last upgraded the cluster.
        /// </summary>
        [JsonIgnore]
        public string NeonCliVersion
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.NeonCliVersion, out string value))
                {
                    value = null;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the minimum version of <b>neon-cli</b> allowed to manager the cluster.
        /// </summary>
        [JsonIgnore]
        public string NeonCliMinimumVersion
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.NeonCliVersionMinimum, out string value))
                {
                    value = null;
                }

                return value;
            }
        }

        /// <summary>
        /// Returns the cluster's unique ID.
        /// </summary>
        [JsonIgnore]
        public string Uuid
        {
            get
            {
                if (!Globals.TryGetValue(NeonClusterGlobals.Uuid, out string value))
                {
                    value = null;
                }

                return value;
            }
        }
    }
}
