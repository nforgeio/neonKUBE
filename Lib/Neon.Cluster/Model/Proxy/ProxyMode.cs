//-----------------------------------------------------------------------------
// FILE:	    ProxyMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Cluster
{
    /// <summary>
    /// Indicates whether TCP or HTTP connections should be proxied.
    /// </summary>
    public enum ProxyMode
    {
        /// <summary>
        /// Proxy mode is undefined.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Proxy HTTP connections.
        /// </summary>
        [EnumMember(Value = "http")]
        Http,

        /// <summary>
        /// Proxy TCP connections.
        /// </summary>
        [EnumMember(Value = "tcp")]
        Tcp
    }
}
