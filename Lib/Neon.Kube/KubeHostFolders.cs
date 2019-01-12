//-----------------------------------------------------------------------------
// FILE:	    KubeHostFolders.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the paths of important directories on cluster 
    /// host servers.
    /// </summary>
    /// <remarks>
    /// <note>
    /// Although these constants are referenced by C# code, Linux scripts 
    /// are likely to hardcode these strings.  You should do a search and
    /// replace whenever you change any of these values.
    /// </note>
    /// <note>
    /// Changing any of these will likely break [neon-cli] interactions
    /// with existing clusters that use the previous folder path.  Be
    /// ver sure you know what you're doing when you make changes.
    /// </note>
    /// </remarks>
    public static class KubeHostFolders
    {
        /// <summary>
        /// Path to the cluster archive directory.
        /// </summary>
        public const string Archive = "${HOME}/.archive";

        /// <summary>
        /// Path to the cluster executable files directory.
        /// </summary>
        public const string Bin = "/lib/neon/bin";

        /// <summary>
        /// Path to the cluster configuration directory.
        /// </summary>
        public const string Config = "/etc/neon";

        /// <summary>
        /// The folder where cluster tools can upload, unpack, and then
        /// execute <see cref="CommandBundle"/>s as well as store temporary
        /// command output files.
        /// </summary>
        public const string Exec = "${HOME}/.exec";

        /// <summary>
        /// Path to the cluster management scripts directory.
        /// </summary>
        public const string Scripts = "/lib/neon/scripts";

        /// <summary>
        /// Path to the cluster secrets directory.
        /// </summary>
        public const string Secrets = "${HOME}/.secrets";

        /// <summary>
        /// Path to the cluster setup scripts directory.
        /// </summary>
        public const string Setup = "/lib/neon/setup";

        /// <summary>
        /// Path to the cluster source code directory.
        /// </summary>
        public const string Source = "/lib/neon/src";

        /// <summary>
        /// Path to the cluster setup state directory.
        /// </summary>
        public const string State = "/var/local/neon";

        /// <summary>
        /// Root folder on the local tmpfs (shared memory) folder where 
        /// cluster will persist misc temporary files.
        /// </summary>
        public const string Tmpfs = "/dev/shm/neon";

        /// <summary>
        /// Path to the cluster tools directory.
        /// </summary>
        public const string Tools = "/lib/neon/tools";
    }
}
