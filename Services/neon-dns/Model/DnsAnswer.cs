//-----------------------------------------------------------------------------
// FILE:	    DnsAnswer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeonDns
{
    /// <summary>
    /// Implements a DNS query answer returned by the Dynamic DNS service
    /// to the PowerDNS Authoritative service.
    /// </summary>
    public class DnsAnswer
    {
        /// <summary>
        /// The answer's time-to-live in seconds (defaults to <b>60</b>).
        /// </summary>
        [JsonProperty(PropertyName = "ttl")]
        public int Ttl { get; set; } = 60;

        /// <summary>
        /// Indicates that the answer is authoritative when set to <b>1</b> (the default).
        /// Set <b>0</b> for non-authoritative answers.
        /// </summary>
        [JsonProperty(PropertyName = "auth")]
        public int Auth { get; set; } = 1;

        /// <summary>
        /// The query name.
        /// </summary>
        [JsonProperty(PropertyName = "qname")]
        public string QName { get; set; } = string.Empty;

        /// <summary>
        /// The query type.
        /// </summary>
        [JsonProperty(PropertyName = "qtype")]
        public string QType { get; set; } = "A";

        /// <summary>
        /// The answer contents.
        /// </summary>
        [JsonProperty(PropertyName = "content")]
        public string Content { get; set; } = string.Empty;
    }
}
