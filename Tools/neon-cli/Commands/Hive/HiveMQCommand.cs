//-----------------------------------------------------------------------------
// FILE:	    HiveMQCommand.cs
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
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>queue</b> commands.
    /// </summary>
    public class HiveMQCommand : CommandBase
    {
        private const string usage = @"
Manages the built-in RabbitMQ messaging cluster via load-level CLI tools.

USAGE:

    neon [OPTIONS] queue -- rabbitmqctl [ARGS...]
    neon [OPTIONS] queue -- rabbitmqadmin [ARGS...]

OPTIONS :

    --help          - Prints this commands help.
    --node=NODE     - Specifies the target hive node.  The command selectS
                      a reasonable target if this is not specified.

COMMANDS:

    queue [OPTIONS] -- rabbitmqctl ARGS...
    ---------------------------------------------z
    Executes the [rabbitmqctl] management utility.

    queue [OPTIONS] -- rabbitmqadmin ARGS...
    ---------------------------------------------z
    Executes the [rabbitmqadmin] management utility.
";
        private HiveProxy hive;

        private const string remoteConsulPath = "/usr/local/bin/consul";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "hive", "queue" }; }
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
                try
                {
                    node = hive.GetNode(nodeName);
                }
                catch (KeyNotFoundException)
                {
                    Console.Error.WriteLine($"*** ERROR: Node [{nodeName}] does not exist.");
                    Program.Exit(1);
                }
            }
            else
            {
                node = hive.GetReachableNode(n => n.Metadata.Labels.HiveMQManager, ReachableHostMode.ReturnNull);

                if (node == null)
                {
                    Console.Error.WriteLine($"*** ERROR: None of the hive nodes hosting HiveMQ nodes appear to be online.");
                    Program.Exit(1);
                }
            }

            if (rightCommandLine == null)
            {
                Console.Error.WriteLine($"*** ERROR: This command requires the \"--\" argument.");
                Program.Exit(1);
            }

            var command = rightCommandLine.Arguments.FirstOrDefault();

            if (command == null)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var response = (CommandResponse)null;

            switch (command)
            {
                case "rabbitmqctl":

                    response = node.SudoCommand("docker exec neon-hivemq rabbitmqctl", RunOptions.None, rightCommandLine.Items.Skip(1));

                    Console.WriteLine(response.AllText);
                    Program.Exit(response.ExitCode);
                    break;

                case "rabbitmqadmin":

                    response = node.SudoCommand("docker exec neon-hivemq rabbitmqadmin", RunOptions.None, rightCommandLine.Items.Skip(1));

                    Console.WriteLine(response.AllText);
                    Program.Exit(response.ExitCode);
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: Unexpected [{command}] command.");
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
