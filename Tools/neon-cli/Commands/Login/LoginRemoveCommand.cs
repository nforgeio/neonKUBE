//-----------------------------------------------------------------------------
// FILE:	    LoginRemoveCommand.cs
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
    /// Implements the <b>login remove</b> command.
    /// </summary>
    public class LoginRemoveCommand : CommandBase
    {
        private const string usage = @"
Removes a Kubernetes context from the local computer.

USAGE:

    neon login rm       [--force] [ USER@CLUSTER[/NAMESPACE] ]
    neon login remove   [--force] [ USER@CLUSTER[/NAMESPACE] ]

ARGUMENTS:

    USER@CLUSTER[/NAMESPACE]    - Kubernetes user, cluster and optional namespace

OPTIONS:

    --force             - Don't prompt, just remove.

REMARKS:

By default, this comman will remove the current login when 
USER@CLUSTER[/NAMESPACE is not specified.
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
            KubeConfigContext   context     = null;
            KubeContextName     contextName = null;

            if (commandLine.Arguments.Length > 0)
            {
                contextName = KubeContextName.Parse(commandLine.Arguments.FirstOrDefault());
            }

            if (contextName != null)
            {
                context = KubeHelper.Config.GetContext(contextName);

                if (context == null)
                {
                    Console.Error.WriteLine($"*** ERROR: Context [{contextName}] not found.");
                    Program.Exit(1);
                }
            }
            else
            {
                context = KubeHelper.CurrentContext;

                if (context == null)
                {
                    Console.Error.WriteLine($"*** ERROR: You are not logged into a cluster.");
                    Program.Exit(1);
                }

                contextName = (KubeContextName)context.Name;
            }

            if (!commandLine.HasOption("--force") && !Program.PromptYesNo($"*** Are you sure you want to remove: {contextName}?"))
            {
                return;
            }

            if (KubeHelper.CurrentContextName == contextName)
            {
                Console.WriteLine($"Logging out of: {contextName}");
            }

            KubeHelper.Config.RemoveContext(context);
            Console.WriteLine($"Removed: {contextName}");
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
