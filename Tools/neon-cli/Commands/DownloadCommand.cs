//-----------------------------------------------------------------------------
// FILE:	    DownloadCommand.cs
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
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>download</b> command.
    /// </summary>
    public class DownloadCommand : CommandBase
    {
        private const string usage = @"
Downloads a file from a cluster node and writes it to standard output.

USAGE:

    neon download SOURCE [NODE]

ARGUMENTS:

    SOURCE      - Path to the source file on the remote node.
    NODE        - Optionally specifies the source node otherwise
                  defaults to the first manager node.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "download" }; }
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

            // Process the command arguments.

            SshProxy<NodeDefinition>   node;
            string                      source;

            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: SOURCE file was not specified.");
                Program.Exit(1);
            }

            source = commandLine.Arguments[0];

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>);

            if (commandLine.Arguments.Length == 1)
            {
                node = cluster.GetHealthyManager();
            }
            else if (commandLine.Arguments.Length == 2)
            {
                node = cluster.GetNode(commandLine.Arguments[1]);
            }
            else
            {
                Console.Error.WriteLine("*** ERROR: A maximum of one node can be specified.");
                Program.Exit(1);
                return;
            }

            // Perform the download.

            node.Download(source, "/tmp/download");

            using (var download = new FileStream("/tmp/download", FileMode.Open, FileAccess.Read))
            {
                using (var output = Console.OpenStandardOutput())
                {
                    var buffer = new byte[8192];

                    while (true)
                    {
                        var cb = download.Read(buffer, 0, buffer.Length);

                        if (cb == 0)
                        {
                            break;
                        }

                        output.Write(buffer, 0, cb);
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            var commandLine = shim.CommandLine;

            if (commandLine.Arguments.Length >= 3)
            {
                // We need to have the container command write the target file
                // to the [/shim] folder and then add a post action that copies
                // the target file to the specified location on the operator's
                // workstation.

                var externalTarget = commandLine.Arguments[2];
                var internalTarget = "__target";

                shim.ReplaceItem(externalTarget, internalTarget);

                shim.SetPostAction(
                    exitCode =>
                    {
                        if (exitCode == 0)
                        {
                            File.Copy(Path.Combine(shim.ShimExternalFolder, internalTarget), externalTarget);
                        }
                    });
            }

            return new ShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
