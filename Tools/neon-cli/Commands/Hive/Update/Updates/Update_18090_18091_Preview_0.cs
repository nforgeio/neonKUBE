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

using ICSharpCode.SharpZipLib.Zip;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Updates a hive from version <b>18.9.0-preview.0</b> to <b>18.9.1-preview.0</b>.
    /// </summary>
    public class Update_18090_18091_Preview_0 : HiveUpdate
    {
        /// <inheritdoc/>
        public override SemanticVersion FromVersion { get; protected set; } = SemanticVersion.Parse("18.9.0-preview.0");

        /// <inheritdoc/>
        public override SemanticVersion ToVersion { get; protected set; } = SemanticVersion.Parse("18.9.1-preview.0");

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
