//-----------------------------------------------------------------------------
// FILE:	    Update_010297_010298.cs
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
    /// Updates a cluster from version <b>1.2.97</b> to <b>1.2.98</b>.
    /// </summary>
    public class Update_010297_010298 : ClusterUpdate
    {
        /// <inheritdoc/>
        public override SemanticVersion FromVersion { get; protected set; } = SemanticVersion.Parse("1.2.97");

        /// <inheritdoc/>
        public override SemanticVersion ToVersion { get; protected set; } = SemanticVersion.Parse("1.2.98");

        /// <inheritdoc/>
        public override void AddUpdateSteps(SetupController<NodeDefinition> controller)
        {
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Update [{FromVersion}] --> [{ToVersion}]";
        }
    }
}
