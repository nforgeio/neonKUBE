//-----------------------------------------------------------------------------
// FILE:	    ClusterUpdateArgs.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Cluster update arguments.
    /// </summary>
    public class ClusterUpdateArgs
    {
        /// <summary>
        /// Indicates that the update should not actually be applied.
        /// </summary>
        public bool DryRun { get; set; } = false;

        /// <summary>
        /// Enables HashCorp Consul updates.
        /// </summary>
        public bool Consul { get; set; }

        /// <summary>
        /// The target Consul version.
        /// </summary>
        public string ConsulVersion { get; set; }

        /// <summary>
        /// Enables Docker daemon updates.
        /// </summary>
        public bool Docker { get; set; }

        /// <summary>
        /// The target Docker version.
        /// </summary>
        public string DockerVersion { get; set; }

        /// <summary>
        /// Enables Linux distribution updates.
        /// </summary>
        public bool Linux { get; set; }

        /// <summary>
        /// Enables HashiCorp Vault updates.
        /// </summary>
        public bool Vault { get; set; }

        /// <summary>
        /// The target Vault version.
        /// </summary>
        public string VaultVersion { get; set; }
    }
}
