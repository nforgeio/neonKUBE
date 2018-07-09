//-----------------------------------------------------------------------------
// FILE:	    HiveVerifyCommand.cs
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
    /// Implements the <b>hive verify</b> command.
    /// </summary>
    public class HiveVerifyCommand : CommandBase
    {
        private const string usage = @"
Verifies a hive definition file.

USAGE:

    neon hive verify HIVE-DEF

ARGUMENTS:

    HIVE-DEF    - Path to the hive definition file.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "hive", "verify" }; }
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
                Console.Error.WriteLine("*** ERROR: HIVE-DEF is required.");
                Program.Exit(1);
            }

            // Parse and validate the hive definition.

            HiveDefinition.FromFile(commandLine.Arguments[0], strict: true);

            Console.WriteLine("");
            Console.WriteLine("*** The hive definition is OK.");
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            shim.AddFile(shim.CommandLine.Arguments.LastOrDefault());

            return new DockerShimInfo(shimability: DockerShimability.Optional);
        }
    }
}
