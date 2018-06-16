//-----------------------------------------------------------------------------
// FILE:	    ClusterUpdate.cs
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
    /// Cluster update base class.
    /// </summary>
    public abstract class ClusterUpdate : IClusterUpdate
    {
        /// <inheritdoc/>
        public abstract SemanticVersion FromVersion { get; protected set; }

        /// <inheritdoc/>
        public abstract SemanticVersion ToVersion { get; protected set; }

        /// <inheritdoc/>
        public abstract void AddUpdateSteps(SetupController<NodeDefinition> controller);

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"Update [{FromVersion}] --> [{ToVersion}]";
        }

        /// <inheritdoc/>
        public ClusterProxy Cluster { get; set; }

        /// <inheritdoc/>
        public ClusterLogin ClusterLogin => Cluster?.ClusterLogin;

        /// <inheritdoc/>
        public virtual string IdempotentPrefix => $"{ToVersion}-";

        /// <inheritdoc/>
        public string GetItempotentTag(string operation)
        {
            return $"update/{ToVersion}/{IdempotentPrefix}{operation}";
        }
    }
}
