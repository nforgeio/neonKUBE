//-----------------------------------------------------------------------------
// FILE:	    PdnsDnsEmptyResult.cs
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
    /// The result returned to PowerDNS for unsuccessful DNS queries or queries 
    /// that return no results.
    /// </summary>
    public class PdnsDnsEmptyResult
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="success">Optionally used to indicate success or failure (defaults to <c>true</c>).</param>
        public PdnsDnsEmptyResult(bool success = true)
        {
            this.Result = success;
        }

        /// <summary>
        /// Set to <c>true</c> or <c>false</c> to indicate whether the query
        /// was considtered to be successful with no results.
        /// </summary>
        [JsonProperty(PropertyName = "result")]
        public bool Result { get; private set; }
    }
}
