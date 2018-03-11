//-----------------------------------------------------------------------------
// FILE:	    DnsRecord.cs
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
    /// Holds information about a DNS record persisted to Consul as part of
    /// the neonCLUSTER Dynamic DNS implementation.  These records hold the
    /// answers that will be returned by the <b>neon-dns</b> service configured
    /// as a PowerDNS backend.
    /// </summary>
    public class DnsRecord
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
        /// The record contents.  For A records, this will simply be an IP address.
        /// For CNAME, this will be the referenced domain and for MX records, this
        /// will be the referenced domain followed by the priority.
        /// </summary>
        public string Contents { get; set; }

        /// <summary>
        /// The DNS TTL in seconds.
        /// </summary>
        public int Ttl { get; set; }
    }
}
