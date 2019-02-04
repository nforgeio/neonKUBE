//-----------------------------------------------------------------------------
// FILE:	    LoginListCommand.cs
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
    /// Implements the <b>logins list</b> command.
    /// </summary>
    public class LoginListCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        private class LoginInfo
        {
            public LoginInfo(KubeConfigContext context)
            {
                this.Name = context.Name;

                var sbInfo = new StringBuilder();

                if (context.Extensions.SetupDetails.SetupPending)
                {
                    sbInfo.AppendWithSeparator("setup");
                }

                this.Info = sbInfo.ToString();
            }

            public string Name;
            public string Info;
        }

        //---------------------------------------------------------------------
        // Implementation
        
        private const string usage = @"
Lists the Kubernetes contexts available on the local computer.

USAGE:

    neon login list
    neon login ls
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "login", "list" }; }
        }

        /// <inheritdoc/>
        public override string[] AltWords
        {
            get { return new string[] { "login", "ls" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            var current = KubeHelper.CurrentContext;
            var logins  = new List<LoginInfo>();

            foreach (var context in KubeHelper.Config.Contexts.OrderBy(c => c.Name))
            {
                logins.Add(new LoginInfo(context));
            }

            Console.WriteLine();

            if (logins.Count == 0)
            {
                Console.Error.WriteLine("*** No Kubernetes contexts.");
            }
            else
            {
                var maxLoginNameWidth = logins.Max(l => l.Name.Length);

                foreach (var login in logins.OrderBy(c => c.Name))
                {
                    if (current != null && login.Name == current.Name)
                    {
                        Console.Write(" --> ");
                    }
                    else
                    {
                        Console.Write("     ");
                    }

                    var padding = new string(' ', maxLoginNameWidth - login.Name.Length);

                    Console.Write($"{login.Name}{padding}    {login.Info}");
                }

                Console.WriteLine();
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}
