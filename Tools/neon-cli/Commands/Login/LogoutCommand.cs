//-----------------------------------------------------------------------------
// FILE:	    LogoutCommand.cs
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
    /// Implements the <b>login</b> command.
    /// </summary>
    public class LogoutCommand : CommandBase
    {
        private const string usage = @"
Logs out of the current Kubernetes context.

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
            Console.WriteLine("");

            // Actually logout.

            if (KubeHelper.CurrentContext == null)
            {
                Console.WriteLine($"You're not logged into a cluster.");
                return;
            }

            Console.WriteLine($"Logging out of [{KubeHelper.CurrentContext.Name}].");
            KubeHelper.SetCurrentContext((string)null);
            Console.WriteLine("");
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
