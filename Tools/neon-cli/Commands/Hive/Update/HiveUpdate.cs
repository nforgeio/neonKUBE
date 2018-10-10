//-----------------------------------------------------------------------------
// FILE:	    HiveUpdate.cs
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

using Neon.Common;
using Neon.IO;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Hive update base class.
    /// </summary>
    public abstract class HiveUpdate : IHiveUpdate
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
            return $"[{ToVersion}]";
        }

        /// <inheritdoc/>
        public HiveProxy Hive { get; set; }

        /// <inheritdoc/>
        public HiveLogin HiveLogin => Hive?.HiveLogin;

        /// <inheritdoc/>
        public virtual bool RestartRequired => false;

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
        /// Prepends any required generic hive updates initialization steps 
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

                    var updateFolder = LinuxPath.Combine(HiveHostFolders.State, "update", ToVersion.ToString());

                    node.SudoCommand("mkdir -p", updateFolder);
                    node.SudoCommand("chmod 770", updateFolder);
                },
                noParallelLimit: true,
                position: 0);
        }

        /// <summary>
        /// <para>
        /// Updates the hive version.  This should be added as the last update step to
        /// a <see cref="SetupController{NodeMetadata}"/> as a <b>global step</b> by
        /// hive update implementations.
        /// </para>
        /// <note>
        /// Only builds from the <b>PROD</b> branch actually update the hive version
        /// as a convenience.  This makes it easy to progressively apply new updates
        /// to intermediate builds as updates are being developed.
        /// </note>
        /// </summary>
        protected void UpdateHiveVersion()
        {
            if (Program.IsProd)
            {
                var firstManager = Hive.FirstManager;

                // Update the hive version.  Note that we're not making this operation
                // idempotent so that it'll be easier to manually change the hive version
                // in Consul when testing hive update code.

                firstManager.Status = "update: hive version";
                Hive.Globals.Set(HiveGlobals.Version, (string)ToVersion);
                firstManager.Status = string.Empty;
            }
        }
    }
}
