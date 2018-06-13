//-----------------------------------------------------------------------------
// FILE:	    ClusterUpdateContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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
    public class ClusterUpdateContext
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The target cluster.</param>
        public ClusterUpdateContext(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.Cluster = cluster;
        }

        /// <summary>
        /// Returns a proxy for the cluster being updated.
        /// </summary>
        public ClusterProxy Cluster { get; private set; }

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

        /// <summary>
        /// Returns update output strings.
        /// </summary>
        public List<string> Output { get; private set; } = new List<string>();

        /// <summary>
        /// Writes a line of text to the update output.
        /// </summary>
        /// <param name="text">The output text or <c>null</c>.</param>
        public void WriteLine(string text = null)
        {
            Output.Add(text ?? string.Empty);
        }
    }
}
