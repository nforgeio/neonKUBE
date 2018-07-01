//-----------------------------------------------------------------------------
// FILE:	    VolumeRemoveStep.cs
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
    /// Ensures that a Docker volume is not present on a node.
    /// </summary>
    public class VolumeRemoveStep : ConfigStep
    {
        private string      nodeName;
        private string      volumeName;

        /// <summary>
        /// Constructs a configuration step that removes a named volume from a Docker node
        /// if the volume exists.
        /// </summary>
        /// <param name="nodeName">The Docker node name.</param>
        /// <param name="volumeName">The volume name (case sensitive).</param>
        public VolumeRemoveStep(string nodeName, string volumeName)
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

            node.SudoCommand("docker-volume-rm", volumeName);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"volume-remove node={nodeName} volume={volumeName}";
        }
    }
}
