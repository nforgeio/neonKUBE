//-----------------------------------------------------------------------------
// FILE:	    LoginImportCommand.cs
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
    /// Implements the <b>login import</b> command.
    /// </summary>
    public class LoginImportCommand : CommandBase
    {
        private const string usage = @"
Imports a hive login from a file.

USAGE:

    neon login add PATH

ARGUMENTS:

    PATH        - Path to a hive login file including the 
                  hive definition and user credentials.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "login", "import" }; }
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
                Console.Error.WriteLine("*** ERROR: LOGIN-PATH is required.");
                Program.Exit(1);
            }

            var hiveLogin     = NeonHelper.JsonDeserialize<HiveLogin>(File.ReadAllText(commandLine.Arguments[0]));
            var hiveLoginPath = Program.GetHiveLoginPath(hiveLogin.Username, hiveLogin.HiveName);
            var exists        = File.Exists(hiveLoginPath);
            var newLoginJson  = NeonHelper.JsonSerialize(hiveLogin, Formatting.Indented);

            if (exists)
            {
                Console.Error.WriteLine($"*** ERROR: A login already exists for [{hiveLogin.LoginName}].");
                Console.Error.WriteLine($"           Use [neon login rm {hiveLogin.LoginName}] to delete this and then add the replacement.");
                Program.Exit(1);
            }

            File.WriteAllText(hiveLoginPath, newLoginJson);
            Console.Error.WriteLine($"Imported [{hiveLogin.LoginName}].");
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
