//-----------------------------------------------------------------------------
// FILE:	    DnsTarget.cs
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
    /// Describes a DNS target domain to be served dynamically by the the neonCLUSTER 
    /// Dynamic DNS implementation.  These records are used by the <b>neon-dns-mon</b> 
    /// service to persist the <see cref="DnsAnswer"/> records to Consul for the
    /// healthy endpoints.
    /// </summary>
    public class DnsTarget
    {
        /// <summary>
        /// The target hostname to be resolved by the DNS.  This must be a simple
        /// hostname or a fully qualified domain name.
        /// </summary>
        [JsonProperty(PropertyName = "Hostname", Required = Required.Always)]
        public string Hostname { get; set; }

        /// <summary>
        /// Lists the domain endpoints.
        /// </summary>
        [JsonProperty(PropertyName = "Endpoints", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<DnsEndpoint> Endpoints { get; set; } = new List<DnsEndpoint>();

        /// <summary>
        /// Validates the DNS target.  Any warning/errors will be returned as a string list.
        /// </summary>
        /// <param name="clusterDefinition">The current cluster definition,</param>
        /// <param name="nodeGroups">The cluster node groups.</param>
        /// <returns>The list of warnings (if any).</returns>
        public List<string> Validate(ClusterDefinition clusterDefinition, Dictionary<string, List<NodeDefinition>> nodeGroups)
        {
            Covenant.Requires<ArgumentException>(clusterDefinition != null);
            Covenant.Requires<ArgumentException>(nodeGroups != null);

            var warnings = new List<string>();

            if (string.IsNullOrEmpty(Hostname))
            {
                warnings.Add($"Invalid [{nameof(DnsTarget)}.{nameof(Hostname)}={Hostname}].");
            }

            foreach (var endpoint in Endpoints)
            {
                endpoint.Validate(warnings, clusterDefinition, nodeGroups, Hostname);
            }

            return warnings;
        }
    }
}
