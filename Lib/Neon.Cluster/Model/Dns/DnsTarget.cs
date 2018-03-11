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
    /// Dynamic DNS implementation.  These records are used by the <b>neon-dns-health</b> 
    /// service to persist the <see cref="DnsAnswer"/> records to Consul for the
    /// healthy endpoints.
    /// </summary>
    public class DnsTarget
    {
        private string      hostname;
        private int         ttl;

        /// <summary>
        /// The target hostname.
        /// </summary>
        [JsonProperty(PropertyName = "Hostname", Required = Required.Always)]
        public string Hostname
        {
            get { return hostname; }
            
            set
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(value));

                hostname = value.ToLowerInvariant();
            }
        }

        /// <summary>
        /// The DNS TTL in seconds.
        /// </summary>
        [JsonProperty(PropertyName = "Ttl", Required = Required.Always)]
        public int Ttl
        {
            get { return ttl; }

            set
            {
                Covenant.Requires<ArgumentException>(value >= 0, $"DNS [TTL={value}] is not valid.");

                ttl = value;
            }
        }

        /// <summary>
        /// Lists the domain endpoints.
        /// </summary>
        [JsonProperty(PropertyName = "Endpoints", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<DnsEndpoint> Endpoints { get; set; } = new List<DnsEndpoint>();
    }
}
