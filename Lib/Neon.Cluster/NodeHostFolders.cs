//-----------------------------------------------------------------------------
// FILE:	    NodeHostFolders.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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
    public static class NodeHostFolders
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
        public const string Exec = "${HOME}/.exec";

        /// <summary>
        /// The folder where Docker writes secrets provisioned to a container.
        /// </summary>
        public const string DockerSecrets = "/var/run/secrets";
    }
}
