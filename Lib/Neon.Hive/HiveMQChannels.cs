//-----------------------------------------------------------------------------
// FILE:	    HiveMQChannels.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
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
using Neon.HiveMQ;

namespace Neon.Hive
{
    /// <summary>
    /// Identifies and declares the built-in neonCLUSTER HiveMQ channels.
    /// </summary>
    public static class HiveMQChannels
    {
        /// <summary>
        /// <b>BROADCAST:</b> Used by the various neonHIVE proxy components and services to 
        /// coordinate their activities, especially for signaling that proxy configurations
        /// have changed.
        /// </summary>
        public const string ProxyNotify = "proxy-notify";
    }
}
