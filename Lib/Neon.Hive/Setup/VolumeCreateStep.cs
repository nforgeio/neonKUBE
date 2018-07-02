//-----------------------------------------------------------------------------
// FILE:	    VolumeCreateStep.cs
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

namespace Neon.Hive
{
    /// <summary>
    /// Ensures that a Docker volume exists on a node.
    /// </summary>
    public class VolumeCreateStep : ConfigStep
    {
        private string      nodeName;
        private string      volumeName;

        /// <summary>
        /// Constructs a configuration step that ensures that a Docker volume has
        /// been created on a specific Docker node.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="volumeName">The volume name (case sensitive).</param>
        public VolumeCreateStep(string nodeName, string volumeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(volumeName));

            this.nodeName   = nodeName;
            this.volumeName = volumeName;
        }

        /// <inheritdoc/>
        public override void Run(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            var node = hive.GetNode(nodeName);

            node.SudoCommand("docker-volume-create", volumeName);

            node.Status = string.Empty;
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"volume-create node={nodeName} volume={volumeName}";
        }
    }
}
