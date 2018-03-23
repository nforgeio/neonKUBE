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

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>logins export</b> command.
    /// </summary>
    public class LoginExportCommand : CommandBase
    {
        private const string usage = @"
Exports a cluster login to the current directory.

USAGE:

    neon login export USER@CLUSTER

ARGUMENTS:

    USER@CLUSTER    - Specifies a cluster login username and cluster.
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
                Console.Error.WriteLine("*** ERROR: USER@CLUSTER is required.");
                Program.Exit(1);
            }

            var login = NeonClusterHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.Error.WriteLine($"*** ERROR: Invalid username/cluster [{commandLine.Arguments[0]}].  Expected something like: USER@CLUSTER");
                Program.Exit(1);
            }

            var username         = login.Username;
            var clusterName      = login.ClusterName;
            var clusterLoginPath = Program.GetClusterLoginPath(username, clusterName);

            if (File.Exists(clusterLoginPath))
            {
                var outputPath = Path.GetFullPath(Path.GetFileName(clusterLoginPath));
                var loginJson  = File.ReadAllText(clusterLoginPath);

                File.WriteAllText(outputPath, loginJson);
                Console.WriteLine($"Login exported to: {outputPath}");
            }
            else
            {
                Console.Error.WriteLine($"*** ERROR: Login [{login.Username}@{login.ClusterName}] does not exist.");
                return;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: false);
        }
    }
}
