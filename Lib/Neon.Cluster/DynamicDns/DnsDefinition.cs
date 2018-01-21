//-----------------------------------------------------------------------------
// FILE:	    DnsDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Net;

namespace Neon.Cluster.DynamicDns
{
    /// <summary>
    /// Holds definitions for the DNS record persisted to Consul as part of
    /// the neonCLUSTER Dynamic DNS implementation.  These records are used
    /// by the <b>neon-dns-health</b> service to persist the <see cref="DnsRecord"/>
    /// records to Consul for the healthy endpoints.
    /// </summary>
    public class DnsDefinition
    {
        /// <summary>
        /// The domain name without the terminating period.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The DNS record type (uppercase).
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// Lists endpoints for the name.
        /// </summary>
        public List<DnsEndpoint> References { get; set; } = new List<DnsEndpoint>();
    }
}
