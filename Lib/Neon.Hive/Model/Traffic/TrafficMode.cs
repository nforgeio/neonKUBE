//-----------------------------------------------------------------------------
// FILE:	    TrafficMode.cs
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
    /// Indicates whether TCP or HTTP connections should be load balanced.
    /// </summary>
    public enum TrafficMode
    {
        /// <summary>
        /// Load balancer mode is undefined.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Load balance HTTP connections.
        /// </summary>
        [EnumMember(Value = "http")]
        Http,

        /// <summary>
        /// Load balance TCP connections.
        /// </summary>
        [EnumMember(Value = "tcp")]
        Tcp
    }
}
