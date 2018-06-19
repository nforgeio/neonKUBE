//-----------------------------------------------------------------------------
// FILE:	    ClusterNodeManagerStatus.cs
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
    /// Enumerates the possible cluster node manager status states.
    /// </summary>
    public enum ClusterNodeManagerStatus
    {
        /// <summary>
        /// Node is in an unrecognized state.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// Node is not a cluster manager.
        /// </summary>
        [EnumMember(Value = "not-manager")]
        NotManager,

        /// <summary>
        /// Node is a healthy standby manager.
        /// </summary>
        [EnumMember(Value = "reachable")]
        Reachable,

        /// <summary>
        /// Node is an unhealthy standby manager.
        /// </summary>
        [EnumMember(Value = "reachable")]
        Unreachable,

        /// <summary>
        /// Node is the healthy swarm manager.
        /// </summary>
        [EnumMember(Value = "leader")]
        Leader
    }
}
