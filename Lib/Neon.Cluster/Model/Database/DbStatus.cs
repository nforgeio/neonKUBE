//-----------------------------------------------------------------------------
// FILE:	    DbStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Cryptography;
using System.Runtime.Serialization;
using System.Text;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Enumerates the NeonCluster database status indicators used for indicating the
    /// health of a database cluster and individual cluster nodes.
    /// </summary>
    public enum DbStatus
    {
        /// <summary>
        /// The database or node status is unknown.
        /// </summary>
        [EnumMember(Value = "unknown")]
        Unknown = 0,

        /// <summary>
        /// The database or node is offline.
        /// </summary>
        [EnumMember(Value = "offline")]
        Offline,

        /// <summary>
        /// The database or node is being configured.
        /// </summary>
        [EnumMember(Value = "setup")]
        Setup,

        /// <summary>
        /// The database or node is in a faulted state.
        /// </summary>
        [EnumMember(Value = "fault")]
        Fault,

        /// <summary>
        /// The database or node is operational but inhibited (e.g. a cluster node is offline).
        /// </summary>
        [EnumMember(Value = "warning")]
        Warning,

        /// <summary>
        /// The database or node is online and ready.
        /// </summary>
        [EnumMember(Value = "ready")]
        Ready
    }
}
