//-----------------------------------------------------------------------------
// FILE:	    ClusterNodeStatus.cs
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
using Neon.Cryptography;

namespace Neon.Cluster
{
    /// <summary>
    /// Enumerates the possible cluster node status states.
    /// </summary>
    public enum ClusterNodeStatus
    {
        /// <summary>
        /// Node is in an unrecognized state.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Node is down due to Docker not running or network issues.
        /// </summary>
        [EnumMember(Value = "down")]
        Down,

        /// <summary>
        /// Node is ready.
        /// </summary>
        [EnumMember(Value = "ready")]
        Ready
    }
}
