//-----------------------------------------------------------------------------
// FILE:	    LoginExportCommand.cs
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
    /// Implements the <b>logins export</b> command.
    /// </summary>
    public class LoginExportCommand : CommandBase
    {
        private const string usage = @"
Exports an extended Kubernetes context to standard output or to a file.

USAGE:

    neon login export USER@CLUSTER[/NAMESPACE] [PATH]

ARGUMENTS:

    USER@CLUSTER[/NAMESPACE]    - Kubernetes user, cluster and optional namespace
    PATH                        - Optional output file path

REMARKS:

    The file written includes the Kubernetes context information
    along with additional neonKUBE extensions.  This is intended
    to be used for distributing a context to other cluster operators
    who can use the [neon login import] command to add the context
    to their workstation.
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
                Console.Error.WriteLine("*** ERROR: USER@CLUSTER[/NAMESPACE] is required.");
                Program.Exit(1);
            }

            var contextName = KubeContextName.Parse(commandLine.Arguments.First());
            var context = KubeHelper.Config.GetContext(contextName);

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                Program.Exit(1);
            }

            var yaml = NeonHelper.YamlSerialize(context);

            if (commandLine.Arguments.Length < 2)
            {
                Console.WriteLine(yaml);
            }
            else
            {
                File.WriteAllText(commandLine.Arguments[1], yaml);
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
