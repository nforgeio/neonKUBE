//-----------------------------------------------------------------------------
// FILE:	    DnsDomain.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a DNS domain to be served dynamically by the the neonCLUSTER 
    /// Dynamic DNS implementation.  These records are used by the <b>neon-dns-health</b> 
    /// service to persist the <see cref="DnsRecord"/> records to Consul for the
    /// healthy endpoints.
    /// </summary>
    public class DnsDomain
    {
        /// <summary>
        /// The domain name without the terminating period.
        /// </summary>
        public string Domain { get; set; }

        /// <summary>
        /// The record type in uppercase (e.g. "A", "CNAME", "MX",...).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Lists the domain endpoints.
        /// </summary>
        public List<DnsEndpoint> Endpoints { get; set; } = new List<DnsEndpoint>();
    }
}
