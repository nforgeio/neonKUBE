//-----------------------------------------------------------------------------
// FILE:	    ClusterDashboard.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a cluster dashboard.
    /// </summary>
    public class ClusterDashboard
    {
        /// <summary>
        /// Identifies the dashboard.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The dashboard URL.
        /// </summary>
        [JsonProperty(PropertyName = "Url", Required = Required.Always)]
        public string Url { get; set; }

        /// <summary>
        /// Validates the dashboard.  Any warning/errors will be returned as a string list.
        /// </summary>
        /// <param name="clusterDefinition">The current cluster definition,</param>
        /// <returns>The list of warnings (if any).</returns>
        public List<string> Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentException>(clusterDefinition != null);

            var warnings = new List<string>();

            if (string.IsNullOrEmpty(Name))
            {
                warnings.Add($"Invalid [{nameof(ClusterDashboard)}.{nameof(Name)}={Name}].");
            }

            if (string.IsNullOrEmpty(Url) || !Uri.TryCreate(Url, UriKind.Absolute, out var uri))
            {
                warnings.Add($"Invalid [{nameof(ClusterDashboard)}.{nameof(Url)}={Url}].");
            }

            return warnings;
        }
    }
}
