//-----------------------------------------------------------------------------
// FILE:	    OsUpgrade.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Hive
{
    /// <summary>
    /// Enumerates the possible host node operating system upgrade options.
    /// </summary>
    public enum OsUpgrade
    {
        /// <summary>
        /// Perform no operating system upgrade.
        /// </summary>
        [EnumMember(Value = "none")]
        None,

        /// <summary>
        /// Upgrades many but not all components.  This is equivalent to performing: <b>apt-get upgrade</b>
        /// </summary>
        [EnumMember(Value = "partial")]
        Partial,

        /// <summary>
        /// Upgrades all components.  This is equivalent to performing: <b>apt-get dist-upgrade</b>
        /// </summary>
        [EnumMember(Value = "full")]
        Full
    }
}
