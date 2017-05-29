//-----------------------------------------------------------------------------
// FILE:	    NodeHostFolders.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
    /// Enumerates the paths of important directories on NeonCluster 
    /// host servers.
    /// </summary>
    public static class NodeHostFolders
    {
        /// <summary>
        /// Path to the NeonCluster configuration directory.
        /// </summary>
        public const string Config = "/etc/neoncluster";

        /// <summary>
        /// Path to the NeonCluster secrets directory.
        /// </summary>
        public const string Secrets = "${HOME}/.secrets";

        /// <summary>
        /// Path to the NeonCluster archive directory.
        /// </summary>
        public const string Archive = "${HOME}/.archive";

        /// <summary>
        /// Path to the NeonCluster setup state directory.
        /// </summary>
        public const string State = "/var/local/neoncluster";

        /// <summary>
        /// Path to the NeonCluster setup scripts directory.
        /// </summary>
        public const string Setup = "/opt/neonsetup";

        /// <summary>
        /// Path to the NeonCluster tools directory.
        /// </summary>
        public const string Tools = "/opt/neontools";

        /// <summary>
        /// Path to the NeonCluster management scripts directory.
        /// </summary>
        public const string Scripts = "${HOME}/.scripts";

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
