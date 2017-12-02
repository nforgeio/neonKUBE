//-----------------------------------------------------------------------------
// FILE:	    LoginImportCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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
    /// Implements the <b>login import</b> command.
    /// </summary>
    public class LoginImportCommand : CommandBase
    {
        private const string usage = @"
Imports a cluster login from a file.

USAGE:

    neon login add PATH

ARGUMENTS:

    PATH        - Path to a cluster login file including the 
                  cluster definition and user credentials.
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

            var clusterLogin     = NeonHelper.JsonDeserialize<ClusterLogin>(File.ReadAllText(commandLine.Arguments[0]));
            var clusterLoginPath = Program.GetClusterLoginPath(clusterLogin.Username, clusterLogin.ClusterName);
            var exists           = File.Exists(clusterLoginPath);
            var newLoginJson     = NeonHelper.JsonSerialize(clusterLogin, Formatting.Indented);

            if (exists)
            {
                Console.WriteLine($"*** ERROR: A login already exists for [{clusterLogin.LoginName}].");
                Console.WriteLine($"           Use [neon logins rm {clusterLogin.LoginName}] to delete this and then add the replacement.");
                Program.Exit(1);
            }

            File.WriteAllText(clusterLoginPath, newLoginJson);
            Console.WriteLine($"Imported [{clusterLogin.LoginName}].");
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false);
        }
    }
}
