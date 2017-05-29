//-----------------------------------------------------------------------------
// FILE:	    NodeCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

namespace NeonTool
{
    /// <summary>
    /// Implements the <b>node</b> command.
    /// </summary>
    public class NodeCommand : CommandBase
    {
        private const string usage = @"
Commands to manage cluster nodes.

USAGE:

    neon prepare node [OPTIONS] SERVER1 [SERVER2...]

ARGUMENTS:

    SERVER1...      - IP addresses or FQDN of the servers
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "node" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }
        
        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            Console.WriteLine(usage);
            Program.Exit(1);
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: true);
        }
    }
}
