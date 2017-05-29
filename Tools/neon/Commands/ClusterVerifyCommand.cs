//-----------------------------------------------------------------------------
// FILE:	    ClusterVerifyCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;

namespace NeonTool
{
    /// <summary>
    /// Implements the <b>cluster verify</b> command.
    /// </summary>
    public class ClusterVerifyCommand : CommandBase
    {
        private const string usage = @"
Verifies a cluster definition file.

USAGE:

    neon cluster verify CLUSTER-DEF

ARGUMENTS:

    CLUSTER-DEF     - Path to the cluster definition file.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "verify" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: CLUSTER-DEF is required.");
                Program.Exit(1);
            }

            // Parse and validate the cluster definition.

            ClusterDefinition.FromFile(commandLine.Arguments[0]);

            Console.WriteLine("");
            Console.WriteLine("*** The cluster definition is OK.");
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            shim.AddFile(shim.CommandLine.Arguments.LastOrDefault());

            return new ShimInfo(isShimmed: true);
        }
    }
}
