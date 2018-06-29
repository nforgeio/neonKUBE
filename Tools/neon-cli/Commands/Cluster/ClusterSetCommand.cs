//-----------------------------------------------------------------------------
// FILE:	    ClusterSetCommand.cs
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
    /// Implements the <b>cluster set</b> command.
    /// </summary>
    public class ClusterSetCommand : CommandBase
    {
        private const string usage = @"
Modifies a global cluster setting.

USAGE:

    neon cluster set [--no-verify] SETTING=VALUE

ARGUMENTS:

    SETTING         - identifies the setting
    VALUE           - the value to set

OPTIONS:

    --no-verify     - don't validate the arguments

REMARKS:

This command sets a cluster global setting.  By default, only settings
considered to be user modifiable can be changed:

    allow-unit-testing      - enable ClusterFixture unit testing (bool)
    disable-auto-unseal     - disables automatic Vault unsealing (bool)
    log-retention-days      - number of days of logs to keep (int > 0)

Use the [--no-verify] option to change any global without restrictions.
Note that THIS CAN BE DANGEROUS, so be sure you know what you're doing.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "set" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--no-verify" }; }
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

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin);

            if (commandLine.Arguments.Length != 1)
            {
                Console.Error.WriteLine("*** ERROR: SETTING=VALUE expected.");
                Console.Error.WriteLine();
                Console.Error.WriteLine(usage);
                Program.Exit(1);
            }

            var noVerify   = commandLine.HasOption("--no-verify");
            var assignment = commandLine.Arguments[0];
            var fields     = assignment.Split(new char[] { '=' }, 2);

            if (fields.Length != 2)
            {
                Console.Error.WriteLine("*** ERROR: SETTING=VALUE expected.");
                Console.Error.WriteLine();
                Console.Error.WriteLine(usage);
                Program.Exit(1);
            }

            var setting = fields[0].ToLowerInvariant();
            var value   = fields[1];

            if (noVerify)
            {
                cluster.Globals.Set(setting, value);
            }
            else
            {
                try
                {
                    cluster.Globals.SetUser(setting, value);
                }
                catch (Exception e)
                {
                    Console.Error.WriteLine($"*** ERROR: {e.Message}");
                    Program.Exit(1);
                }
            }

            Console.WriteLine();
            Console.WriteLine($"* updated: {setting}");
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
