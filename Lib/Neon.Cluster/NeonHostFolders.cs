//-----------------------------------------------------------------------------
// FILE:	    HostFolders.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Cluster
{
    /// <summary>
    /// Enumerates the paths of important directories on neonCLUSTER 
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
    public static class NeonHostFolders
    {
        /// <summary>
        /// Path to the neonCLUSTER configuration directory.
        /// </summary>
        public const string Config = "/etc/neoncluster";

        /// <summary>
        /// Path to the neonCLUSTER secrets directory.
        /// </summary>
        public const string Secrets = "${HOME}/.secrets";

        /// <summary>
        /// Path to the neonCLUSTER archive directory.
        /// </summary>
        public const string Archive = "${HOME}/.archive";

        /// <summary>
        /// Path to the neonCLUSTER setup state directory.
        /// </summary>
        public const string State = "/var/local/neoncluster";

        /// <summary>
        /// Path to the neonCLUSTER setup scripts directory.
        /// </summary>
        public const string Setup = "/opt/neonsetup";

        /// <summary>
        /// Path to the neonCLUSTER tools directory.
        /// </summary>
        public const string Tools = "/opt/neontools";

        /// <summary>
        /// Path to the neonCLUSTER management scripts directory.
        /// </summary>
        public const string Scripts = "/opt/neonscripts";

        /// <summary>
        /// The folder where cluster tools can upload, unpack, and then
        /// execute <see cref="CommandBundle"/>s as well as store temporary
        /// command output files.
        /// </summary>
        public const string Exec = "/var/lib/neoncluster/exec";

        /// <summary>
        /// Root folder on the local tmpfs (shared memory) folder where 
        /// neonCLUSTER will persist misc temporary files.
        /// </summary>
        public const string ClusterTmpfs = "/dev/shm/neoncluster";

        /// <summary>
        /// The folder where Docker writes secrets provisioned to a container.
        /// </summary>
        public const string DockerSecrets = "/var/run/secrets";
    }
}
