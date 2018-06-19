//-----------------------------------------------------------------------------
// FILE:	    ClusterNodeAvailability.cs
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
    /// Enumerates the possible cluster node swarm availability states.
    /// </summary>
    public enum ClusterNodeAvailability
    {
        /// <summary>
        /// Node is in an unrecognized state.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Node is available to schedule swarm tasks.
        /// </summary>
        [EnumMember(Value = "active")]
        Active,

        /// <summary>
        /// Node is paused.
        /// </summary>
        [EnumMember(Value = "pause")]
        Pause,

        /// <summary>
        /// Node is draining swarm tasks.
        /// </summary>
        [EnumMember(Value = "drain")]
        Drain
    }
}
