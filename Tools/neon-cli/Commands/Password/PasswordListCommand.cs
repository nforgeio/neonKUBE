//-----------------------------------------------------------------------------
// FILE:	    PasswordListCommand.cs
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
using Neon.Cryptography;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>password list</b> command.
    /// </summary>
    public class PasswordListCommand : CommandBase
    {
        private const string usage = @"
Lists the named passwords.

USAGE:

    neon password list|ls
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "password", "list" }; }
        }

        /// <inheritdoc/>
        public override string[] AltWords
        {
            get { return new string[] { "password", "ls" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            foreach (var path in Directory.GetFiles(KubeHelper.PasswordsFolder).OrderBy(p => p.ToLowerInvariant()))
            {
                Console.WriteLine(Path.GetFileName(path));
            }

            Program.Exit(0);
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: false);
        }
    }
}
