//-----------------------------------------------------------------------------
// FILE:	    HiveDashboard.cs
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

namespace Neon.Hive
{
    /// <summary>
    /// Describes a hive dashboard.
    /// </summary>
    public class HiveDashboard
    {
        private string title;

        /// <summary>
        /// Identifies the dashboard.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The title to be used for this dashboard when displayed in the global 
        /// hive dashboard.
        /// This defaults to <see cref="Name"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Title", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Title
        {
            get { return title ?? Name; }
            set { title = value; }
        }

        /// <summary>
        /// Optionally specifies the 
        /// </summary>
        [JsonProperty(PropertyName = "Folder", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Folder { get; set; }

        /// <summary>
        /// <para>
        /// The dashboard URL.
        /// </para>
        /// <note>
        /// You may set the URL hostname to <b>reachable-manager</b> to target
        /// the private IP address of the first reachable hive manager node.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Url", Required = Required.Always)]
        public string Url { get; set; }

        /// <summary>
        /// Optional dashboard description.
        /// </summary>
        [JsonProperty(PropertyName = "Description", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Description { get; set; }

        /// <summary>
        /// Validates the dashboard.  Any warning/errors will be returned as a string list.
        /// </summary>
        /// <param name="hiveDefinition">The current hive definition,</param>
        /// <returns>The list of warnings (if any).</returns>
        public List<string> Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentException>(hiveDefinition != null);

            var warnings = new List<string>();

            if (string.IsNullOrEmpty(Name))
            {
                warnings.Add($"Invalid [{nameof(HiveDashboard)}.{nameof(Name)}={Name}].");
            }

            if (string.IsNullOrEmpty(Url) || !Uri.TryCreate(Url, UriKind.Absolute, out var uri))
            {
                warnings.Add($"Invalid [{nameof(HiveDashboard)}.{nameof(Url)}={Url}].");
            }

            return warnings;
        }
    }
}
