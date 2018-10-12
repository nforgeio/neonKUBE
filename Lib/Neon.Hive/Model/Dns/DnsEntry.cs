//-----------------------------------------------------------------------------
// FILE:	    DnsEntry.cs
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
    /// Describes a DNS domain to be served dynamically by the neonHIVE 
    /// Local DNS implementation.  These records are used by the <b>neon-dns-mon</b> 
    /// service to persist the <see cref="DnsAnswer"/> records to Consul for the
    /// healthy endpoints.
    /// </summary>
    public class DnsEntry
    {
        /// <summary>
        /// The hostname to be resolved by the DNS.  This must be a simple
        /// hostname or a fully qualified domain name.
        /// </summary>
        [JsonProperty(PropertyName = "Hostname", Required = Required.Always)]
        public string Hostname { get; set; }

        /// <summary>
        /// Indicates that this is a built-in system entry.
        /// </summary>
        [JsonProperty(PropertyName = "IsSystem", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool IsSystem { get; set; }

        /// <summary>
        /// Lists the domain endpoints.
        /// </summary>
        [JsonProperty(PropertyName = "Endpoints", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<DnsEndpoint> Endpoints { get; set; } = new List<DnsEndpoint>();

        /// <summary>
        /// Validates the DNS entry.  Any warning/errors will be returned as a string list.
        /// </summary>
        /// <param name="hiveDefinition">The current hive definition,</param>
        /// <param name="nodeGroups">The hive node groups.</param>
        /// <returns>The list of warnings (if any).</returns>
        public List<string> Validate(HiveDefinition hiveDefinition, Dictionary<string, List<NodeDefinition>> nodeGroups)
        {
            Covenant.Requires<ArgumentException>(hiveDefinition != null);
            Covenant.Requires<ArgumentException>(nodeGroups != null);

            var warnings = new List<string>();

            if (string.IsNullOrEmpty(Hostname) || !HiveDefinition.IsValidName(Hostname))
            {
                warnings.Add($"Invalid [{nameof(DnsEntry)}.{nameof(Hostname)}={Hostname}].");
            }

            foreach (var endpoint in Endpoints)
            {
                endpoint.Validate(warnings, hiveDefinition, nodeGroups, Hostname);
            }

            return warnings;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Hostname;
        }
    }
}
