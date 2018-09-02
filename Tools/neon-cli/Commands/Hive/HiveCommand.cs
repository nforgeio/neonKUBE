//-----------------------------------------------------------------------------
// FILE:	    HiveCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>hive</b> command.
    /// </summary>
    public class HiveCommand : CommandBase
    {
        private const string usage = @"
Performs basic hive provisioning and management.

USAGE:

    neon hive dns           - Manages the local hive DNS hosts
    neon hive example       - Outputs a sample hive definition
    neon hive get           - Gets hive variables and settings
    neon hive info          - Outputs hive information
    neon hive node          - Manages hive nodes
    neon hive prepare       - Prepares environment for hive setup
    neon hive registry      - Manages a local Docker registry
    neon hive set           - Sets a hive variable or setting
    neon hive setup         - Deploys a hive
    neon hive verify        - Verifies a hive definition
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "hive" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            Help();
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.Optional);
        }
    }
}
