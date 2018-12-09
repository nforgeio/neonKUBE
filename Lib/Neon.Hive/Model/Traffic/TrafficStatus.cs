//-----------------------------------------------------------------------------
// FILE:	    TrafficStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Describes the route status for a traffic manager.
    /// </summary>
    public class TrafficStatus
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public TrafficStatus()
        {
            this.TimestampUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// The last time the <b>neon-proxy-manager</b> finished processing rules for the traffic manager.
        /// </summary>
        [JsonProperty(PropertyName = "TimestampUtc", Required = Required.Always)]
        public DateTime TimestampUtc { get; set; }

        /// <summary>
        /// Logs generated when the rules were processed.
        /// </summary>
        [JsonProperty(PropertyName = "Status", Required = Required.Always)]
        public string Status { get; set; }
    }
}
