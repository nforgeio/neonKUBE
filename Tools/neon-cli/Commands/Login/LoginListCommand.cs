//-----------------------------------------------------------------------------
// FILE:	    LoginListCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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

                if (context.Extension.SetupDetails.SetupPending)
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
    }
}
