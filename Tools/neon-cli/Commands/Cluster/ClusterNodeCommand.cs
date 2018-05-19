//-----------------------------------------------------------------------------
// FILE:	    ClusterNodeCommand.cs
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

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster node</b> command.
    /// </summary>
    public class ClusterNodeCommand : CommandBase
    {
        private const string usage = @"
Manages cluster nodes.

USAGE:

    neon cluster node list|ls   - lists cluster nodes
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "node" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin);
            var command      = commandLine.Arguments.ElementAtOrDefault(0);

            switch (command)
            {
                case "ls":
                case "list":

                    var maxNameLength = cluster.Definition.SortedNodes.Max(n => n.Name.Length);

                    foreach (var node in cluster.Definition.SortedNodes)
                    {
                        Console.WriteLine(node.Name + new string(' ', maxNameLength - node.Name.Length + 4) + node.PrivateAddress.ToString());
                    }
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unknown command: [{command}]");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
