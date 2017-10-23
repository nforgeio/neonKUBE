//-----------------------------------------------------------------------------
// FILE:	    DnsResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeonDns
{
    /// <summary>
    /// The result returned to PowerDNS for successful DNS queries.
    /// </summary>
    public class DnsResult
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns a global invariant empty <see cref="DnsResult"/> instance.
        /// </summary>
        public static DnsEmptyResult Empty { get; private set; } = new DnsEmptyResult(success: true);

        /// <summary>
        /// Returns a global invariant <see cref="DnsEmptyResult"/> instance.
        /// </summary>
        public static DnsEmptyResult Fail { get; private set; } = new DnsEmptyResult(success: false);

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Returns the DNS records that answer the query.
        /// </summary>
        [JsonProperty(PropertyName = "result")]
        public List<DnsAnswer> Result { get; set; } = new List<DnsAnswer>();
    }
}
