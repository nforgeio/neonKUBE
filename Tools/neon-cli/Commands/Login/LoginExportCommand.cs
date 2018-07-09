//-----------------------------------------------------------------------------
// FILE:	    LoginExportCommand.cs
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
    /// Implements the <b>logins export</b> command.
    /// </summary>
    public class LoginExportCommand : CommandBase
    {
        private const string usage = @"
Exports a hive login to the current directory.

USAGE:

    neon login export USER@HIVE

ARGUMENTS:

    USER@HIVE       - Specifies a hive login username and hive.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "login", "export" }; }
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
                Console.Error.WriteLine("*** ERROR: USER@HIVE is required.");
                Program.Exit(1);
            }

            var login = HiveHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.Error.WriteLine($"*** ERROR: Invalid username/hive [{commandLine.Arguments[0]}].  Expected something like: USER@HIVE");
                Program.Exit(1);
            }

            var username      = login.Username;
            var hiveName      = login.HiveName;
            var hiveLoginPath = Program.GetHiveLoginPath(username, hiveName);

            if (File.Exists(hiveLoginPath))
            {
                var outputPath = Path.GetFullPath(Path.GetFileName(hiveLoginPath));
                var loginJson  = File.ReadAllText(hiveLoginPath);

                File.WriteAllText(outputPath, loginJson);
                Console.Error.WriteLine($"Login exported to: {outputPath}");
            }
            else
            {
                Console.Error.WriteLine($"*** ERROR: Login [{login.Username}@{login.HiveName}] does not exist.");
                return;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
