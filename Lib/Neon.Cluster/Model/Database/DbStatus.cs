//-----------------------------------------------------------------------------
// FILE:	    DbStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using ICSharpCode.SharpZipLib.Zip;
using Neon.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Security.Cryptography;
using System.Text;

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
        Unknown = 0,

        /// <summary>
        /// The database or node is offline.
        /// </summary>
        Offline,

        /// <summary>
        /// The database or node is being configured.
        /// </summary>
        Configuring,

        /// <summary>
        /// The database or node is in a faulted state.
        /// </summary>
        Fault,

        /// <summary>
        /// The database or node is operational but inhibited (e.g. a cluster node is offline).
        /// </summary>
        Warning,

        /// <summary>
        /// The database or node is online and ready.
        /// </summary>
        Ready
    }
}
