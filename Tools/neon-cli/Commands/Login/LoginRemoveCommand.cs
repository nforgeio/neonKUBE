//-----------------------------------------------------------------------------
// FILE:	    LoginRemoveCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
    /// Implements the <b>login remove</b> command.
    /// </summary>
    public class LoginRemoveCommand : CommandBase
    {
        private const string usage = @"
Removes a cluster login from the local computer.

USAGE:

    neon login rm       [--force] USER@CLUSTER
    neon login remove   [--force] USER@CLUSTER

ARGUMENTS:

    USER        - The operator's user name.
    CLUSTER     - The cluster name.

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
                Console.Error.WriteLine("*** ERROR: USER@CLUSTER is required.");
                Program.Exit(1);
            }

            var login = NeonClusterHelper.SplitLogin(commandLine.Arguments[0]);

            if (!login.IsOK)
            {
                Console.WriteLine($"*** ERROR: Invalid username and cluster [{commandLine.Arguments[0]}].  Expected something like: USER@CLUSTER");
                Program.Exit(1);
            }

            var username         = login.Username;
            var clusterName      = login.ClusterName;
            var clusterLoginPath = Program.GetClusterLoginPath(username, clusterName);

            if (File.Exists(clusterLoginPath))
            {
                if (!commandLine.HasOption("--force") && !Program.PromptYesNo($"*** Are you sure you want to remove the [{username}@{clusterName}] login?"))
                {
                    return;
                }

                File.Delete(clusterLoginPath);

                // Delete the backup and cached cluster definition files if present.

                var backupPath     = clusterLoginPath + ".bak";
                var definitionPath = NeonClusterHelper.GetCachedDefinitionPath(username, clusterName);

                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }

                if (File.Exists(definitionPath))
                {
                    File.Delete(definitionPath);
                }

                // Remove the [.current] file if this is the logged-in cluster.

                if (Program.ClusterLogin != null && 
                    string.Equals(Program.ClusterLogin.Username, username, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(Program.ClusterLogin.ClusterName, clusterName, StringComparison.OrdinalIgnoreCase))
                {
                    CurrentClusterLogin.Delete();
                    NeonClusterHelper.VpnClose(clusterName);
                }

                Console.WriteLine($"Removed [{username}@{clusterName}]");
            }
            else
            {
                Console.WriteLine($"*** ERROR: Login [{username}@{clusterName}] does not exist.");
                return;
            }

        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false, ensureConnection: false);
        }
    }
}
