//-----------------------------------------------------------------------------
// FILE:	    Update_1809_2_alpha_1809_3_alpha.cs
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

using ICSharpCode.SharpZipLib.Zip;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Updates a hive from version <b>18.9.2-alpha</b> to <b>18.9.3-alpha</b>.
    /// </summary>
    [HiveUpdate]
    public class Update_1809_2_alpha_1809_3_alpha : HiveUpdate
    {
        /// <inheritdoc/>
        public override SemanticVersion FromVersion { get; protected set; } = SemanticVersion.Parse("18.9.2-alpha");

        /// <inheritdoc/>
        public override SemanticVersion ToVersion { get; protected set; } = SemanticVersion.Parse("18.9.3-alpha");

        /// <inheritdoc/>
        public override void AddUpdateSteps(SetupController<NodeDefinition> controller)
        {
            base.Initialize(controller);

            controller.AddGlobalStep(GetStepLabel("hive version"), () => UpdateHiveVersion());
        }

        /// <summary>
        /// Updates the hive version.
        /// </summary>
        private void UpdateHiveVersion()
        {
            var firstManager = Hive.FirstManager;

            // HiveMQ Bootstrap settings: https://github.com/jefflill/NeonForge/issues/337

            firstManager.InvokeIdempotentAction(GetIdempotentTag("hivemq-bootstrap"),
                () =>
                {
                    firstManager.Status = "update: hivemq bootstrap";
                    Hive.HiveMQ.SaveBootstrapSettings();
                    firstManager.Status = string.Empty;
                });

            // Update the hive version.

            firstManager.InvokeIdempotentAction(GetIdempotentTag("hive-version"),
                () =>
                {
                    firstManager.Status = "update: hive version";
                    Hive.Globals.Set(HiveGlobals.Version,(string)ToVersion);
                    firstManager.Status = string.Empty;
                });
        }
    }
}
