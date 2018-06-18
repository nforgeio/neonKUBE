//-----------------------------------------------------------------------------
// FILE:	    ClusterUpdate.cs
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
            return $"[{FromVersion}-->{ToVersion}]";
        }

        /// <inheritdoc/>
        public ClusterProxy Cluster { get; set; }

        /// <inheritdoc/>
        public ClusterLogin ClusterLogin => Cluster?.ClusterLogin;

        /// <inheritdoc/>
        public string GetIdempotentTag(string operation)
        {
            return $"update/{ToVersion}/{operation}";
        }

        /// <inheritdoc/>
        public string GetStepLabel(string stepLabel)
        {
            stepLabel = stepLabel ?? string.Empty;

            return $"{this}: {stepLabel}";
        }

        /// <summary>
        /// Prepends any required generic cluster updates initialization steps 
        /// to a setup controller.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        protected void Initialize(SetupController<NodeDefinition> controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null);

            controller.AddStep(GetStepLabel("initialize"),
                (node, stepDelay) =>
                {
                    node.Status = "update state";

                    var updateFolder = LinuxPath.Combine(NeonHostFolders.State, "update", ToVersion.ToString());

                    node.SudoCommand("mkdir -p", updateFolder);
                    node.SudoCommand("chmod 770", updateFolder);
                },
                noParallelLimit: true,
                position: 0);
        }
    }
}
