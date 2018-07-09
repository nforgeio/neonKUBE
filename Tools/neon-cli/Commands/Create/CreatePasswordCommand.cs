//-----------------------------------------------------------------------------
// FILE:	    CreatePasswordCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    /// Implements the <b>create password</b> command.
    /// </summary>
    public class CreatePasswordCommand : CommandBase
    {
        private const string usage = @"
Generates a cryptographically random password.

USAGE:

    neon create password [OPTIONS]

OPTIONS:

    --length=#      - The desired password length.  This defaults
                      to 20 characters.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "create", "password" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--length" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            var lengthOption = commandLine.GetOption("--length", "20");

            if (!int.TryParse(lengthOption, out var length) || length < 1 || length > 1024)
            {
                Console.Error.WriteLine($"*** ERROR: Length [{length}] is not valid.");
            }

            Console.Write(NeonHelper.GetRandomPassword(length));
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
