//-----------------------------------------------------------------------------
// FILE:	    LogoutCommand.cs
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
    /// Implements the <b>login</b> command.
    /// </summary>
    public class LogoutCommand : CommandBase
    {
        private const string usage = @"
Logs out of a hive.

USAGE:

    neon logout 
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "logout" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            var hiveLogin = Program.HiveLogin;

            Console.WriteLine("");

            // Close all VPN connections even if we're not officially logged in.
            //
            // We're passing NULL to close all hive VPN connections to ensure that 
            // we're only connected to one at a time.  It's very possible for a operator
            // to have to manage multiple disconnnected hives that share the same
            // IP address space.

            HiveHelper.VpnClose(null); 

            // Actually logout.

            if (hiveLogin == null)
            {
                return; // Not logged in.
            }

            Console.WriteLine($"Logging out of [{hiveLogin.HiveName}].");
            Console.WriteLine("");

            CurrentHiveLogin.Delete();
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
