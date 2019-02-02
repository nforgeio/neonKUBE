//-----------------------------------------------------------------------------
// FILE:	    PasswordGenerateCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>password generate</b> command.
    /// </summary>
    public class PasswordGenerateCommand : CommandBase
    {
        private const string usage = @"
Generates a cryptographically secure password.

USAGE:

    neon password generate|gen [LENGTH]

ARGUMENTS:

    LENGTH      - Length of the desired password (default=20)

REMARKS:

The generated password will be written to standard output.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "password", "generate" }; }
        }

        /// <inheritdoc/>
        public override string[] AltWords
        {
            get { return new string[] { "password", "gen" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var lengthArg = commandLine.Arguments.ElementAtOrDefault(0);
            var length    = 20;

            if (lengthArg != null)
            {
                if (!int.TryParse(lengthArg, out length) || length < 8 || length > 100)
                {
                    Console.Error.WriteLine($"*** ERROR: [LENGTH={lengthArg}] is invalid.  Expected an integere between [8..100].");
                    Program.Exit(1);
                }
            }

            Console.Write(NeonHelper.GetRandomPassword(length));
            Program.Exit(0);
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
