//-----------------------------------------------------------------------------
// FILE:	    PasswordCommand.cs
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
    /// Implements the <b>password</b> command.
    /// </summary>
    public class PasswordCommand : CommandBase
    {
        private const string usage = @"
Manages neonKUBE passwords.

USAGE:

    neon password
    neon password export PATH NAME...
    neon password export PATH *
    neon password generate [LENGTH]
    neon password import PATH
    neon password list|ls
    neon password remove|rm NAME
    neon password remove|rm *
    neon password set NAME [PATH|-]

ARGUMENTS:

    LENGTH      - Length of the desired password (default=20)
    NAME        - Password name
    PATH        - Input or output file path
    -           - Read from standard input
    *           - Process all passwords
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "password" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            var command = commandLine.Arguments.ElementAtOrDefault(0);

            if (command != null)
            {
                Console.Error.WriteLine($"*** ERROR: Unknown command: {command}");
                Program.Exit(1);
            }

            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
