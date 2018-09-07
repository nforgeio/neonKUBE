//-----------------------------------------------------------------------------
// FILE:	    QueueCommand.cs
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
    /// Implements the <b>queue</b> commands.
    /// </summary>
    public class QueueCommand : CommandBase
    {
        private const string usage = @"
Manages the built-in RabbitMQ messaging cluster.

USAGE:

    neon [OPTIONS] queue ctl -- [ARGS...]  - Invokes [rabbitmqctl]

OPTIONS :

    --help          - Prints this commands help.
    --node=NODE     - Specifies the target hive node.  The command select
                      a reasonable target if this is not specified.

COMMANDS:

    queue ctl -- ARGS...
    --------------------
    Executes the [rabbitmqctl] management utility on one of the RabbitMQ
    cluster nodes, passing any ARGS to the tool.  This can be used to 
    manage VHosts, users, permissions, etc.
";
        private HiveProxy hive;

        private const string remoteConsulPath = "/usr/local/bin/consul";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "queue" }; }
        }

        /// <inheritdoc/>
        public override string SplitItem
        {
            get { return "--"; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--node" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            var split            = commandLine.Split(SplitItem);
            var leftCommandLine  = split.Left;
            var rightCommandLine = split.Right;

            // Basic initialization.

            if (leftCommandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            // Initialize the hive.

            var hiveLogin = Program.ConnectHive();

            hive = new HiveProxy(hiveLogin);

            // Determine which node we're going to target.

            var node     = (SshProxy<NodeDefinition>)null;
            var nodeName = leftCommandLine.GetOption("--node", null);

            if (!string.IsNullOrEmpty(nodeName))
            {
                node = hive.GetNode(nodeName);
            }
            else
            {
                node = hive.GetHealthyManager();
            }

            var command = rightCommandLine.Arguments.FirstOrDefault();

            switch (command)
            {
                case "ctl":

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
