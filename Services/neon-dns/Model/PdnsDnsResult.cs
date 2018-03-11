//-----------------------------------------------------------------------------
// FILE:	    PdnsDnsResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    public class PdnsDnsResult
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns a global invariant empty <see cref="PdnsDnsResult"/> instance.
        /// </summary>
        public static PdnsDnsEmptyResult Empty { get; private set; } = new PdnsDnsEmptyResult(success: true);

        /// <summary>
        /// Returns a global invariant <see cref="PdnsDnsEmptyResult"/> instance.
        /// </summary>
        public static PdnsDnsEmptyResult Fail { get; private set; } = new PdnsDnsEmptyResult(success: false);

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Returns the DNS records that answer the query.
        /// </summary>
        [JsonProperty(PropertyName = "result")]
        public List<PdnsDnsAnswer> Result { get; set; } = new List<PdnsDnsAnswer>();
    }
}
