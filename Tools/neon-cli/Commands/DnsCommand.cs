//-----------------------------------------------------------------------------
// FILE:	    DnsCommand.cs
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

using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>dns</b> command.
    /// </summary>
    public class DnsCommand : CommandBase
    {
        private const string usage = @"
Manages cluster DNS records.

USAGE:

    neon dns ls|list            - Lists the DNS targets
    neon dns rm|remove TARGET   - Removes a DNS target
    neon dns set TARGET PATH    - Adds/updates a DNS target from a file
    neon dns set TARGET -       - Adds/updates a DNS target from stdin
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "dns" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length != 1)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>);
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: false, ensureConnection: true);
        }
    }
}
