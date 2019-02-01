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
Exports an extended Kubernetes context to standard output.

USAGE:

    neon login export [ USER@CLUSTER[/NAMESPACE] ]

ARGUMENTS:

    USER@CLUSTER[/NAMESPACE]    - Kubernetes user, cluster and optional namespace

REMARKS:

    The output includes the Kubernetes context information along
    with additional neonKUBE extensions.  This is intended to be
    used for distributing a context to other cluster operators
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
            KubeContextName contextName = null;

            var rawName = commandLine.Arguments.FirstOrDefault();

            if (rawName != null)
            {
                contextName = KubeContextName.Parse(rawName);
            }
            else
            {
                contextName = KubeHelper.CurrentContextName;

                if (contextName == null)
                {
                    Console.Error.WriteLine($"*** ERROR: You are not logged into a cluster.");
                    Program.Exit(1);
                }
            }

            var context = KubeHelper.Config.GetContext(contextName);
            var cluster = KubeHelper.Config.GetCluster(context.Properties.Cluster);
            var user    = KubeHelper.Config.GetUser(context.Properties.User);

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                Program.Exit(1);
            }

            if (user == null)
            {
                Console.Error.WriteLine($"*** ERROR: User [{context.Properties.User}] not found.");
                Program.Exit(1);
            }

            var login = new KubeLogin()
            {
                Cluster    = cluster,
                Context    = context,
                Extensions = KubeHelper.GetContextExtension(contextName),
                User       = user
            };

            var yaml = NeonHelper.YamlSerialize(login);

            Console.WriteLine(yaml);
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
