//-----------------------------------------------------------------------------
// FILE:	    LoginRemoveCommand.cs
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
    /// Implements the <b>login remove</b> command.
    /// </summary>
    public class LoginRemoveCommand : CommandBase
    {
        private const string usage = @"
Removes a hive login from the local computer.

USAGE:

    neon login rm       [--force] USER@HIVE
    neon login remove   [--force] USER@HIVE

ARGUMENTS:

    USER        - The operator's user name.
    HIVE        - The hive name.

OPTIONS:

    --force             - Don't prompt, just remove.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "login", "remove" }; }
        }

        /// <inheritdoc/>
        public override string[] AltWords
        {
            get { return new string[] { "login", "rm" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--force" }; }
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
                if (!commandLine.HasOption("--force") && !Program.PromptYesNo($"*** Are you sure you want to remove the [{username}@{hiveName}] login?"))
                {
                    return;
                }

                File.Delete(hiveLoginPath);

                // Delete the backup and cached hive definition files if present.

                var backupPath     = hiveLoginPath + ".bak";
                var definitionPath = HiveHelper.GetCachedDefinitionPath(username, hiveName);

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                if (File.Exists(definitionPath))
                {
                    File.Delete(definitionPath);
                }

                // Remove the [.current] file if this is the logged-in hive.

                if (Program.HiveLogin != null && 
                    string.Equals(Program.HiveLogin.Username, username, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Program.HiveLogin.HiveName, hiveName, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentHiveLogin.Delete();
                    HiveHelper.VpnClose(hiveName);
                }

                Console.WriteLine($"Removed [{username}@{hiveName}]");
            }
            else
            {
                Console.Error.WriteLine($"*** ERROR: Login [{username}@{hiveName}] does not exist.");
                return;
            }

        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
