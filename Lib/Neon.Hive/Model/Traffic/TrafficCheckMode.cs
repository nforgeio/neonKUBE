//-----------------------------------------------------------------------------
// FILE:	    TrafficCheckMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Dynamic;
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
    /// Enumerates the health checking modes for testing load balanced endpoints.
    /// </summary>
    public enum TrafficCheckMode
    {
        /// <summary>
        /// Disables health checks against the endpoints.
        /// </summary>
        [EnumMember(Value = "disabled")]
        Disabled = 0,

        /// <summary>
        /// Performs TCP connection checks against <see cref="TrafficTcpRule"/> rules and
        /// HTTP checks against <see cref="TrafficTcpRule"/> rules.
        /// </summary>
        [EnumMember(Value = "default")]
        Default,

        /// <summary>
        /// Performs TCP connection checks for an <see cref="TrafficTcpRule"/> as well
        /// as <see cref="TrafficTcpRule"/> rules.
        /// </summary>
        [EnumMember(Value = "tcp")]
        Tcp,

        /// <summary>
        /// Performs HTTP connection checks for an <see cref="TrafficTcpRule"/> as well
        /// as <see cref="TrafficHttpRule"/> rules.
        /// </summary>
        [EnumMember(Value = "http")]
        Http
    }
}
