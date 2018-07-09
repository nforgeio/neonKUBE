//-----------------------------------------------------------------------------
// FILE:	    HiveNodeCommand.cs
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

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>hive node</b> command.
    /// </summary>
    public class HiveNodeCommand : CommandBase
    {
        private const string usage = @"
Manages hive nodes.

USAGE:

    neon hive node list|ls      - lists hive nodes
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "hive", "node" }; }
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

            var hiveLogin = Program.ConnectHive();
            var hive      = new HiveProxy(hiveLogin);
            var command   = commandLine.Arguments.ElementAtOrDefault(0);

            switch (command)
            {
                case "ls":
                case "list":

                    var maxNameLength = hive.Definition.SortedNodes.Max(n => n.Name.Length);

                    foreach (var node in hive.Definition.SortedNodes)
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
            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
        }
    }
}
