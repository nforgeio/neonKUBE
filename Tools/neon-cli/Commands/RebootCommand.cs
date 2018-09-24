//-----------------------------------------------------------------------------
// FILE:	    RebootCommand.cs
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
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>reboot</b> command.
    /// </summary>
    public class RebootCommand : CommandBase
    {
        private const string usage = @"
Reboots one or more hive host nodes.

USAGE:

    neon reboot [OPTIONS] NODE...

ARGUMENTS:

    NODE        - One or more target node names, or the plus (+)
                  sign to reboot all nodes.

NOTES:

The [-w/--wait] option specifies the number of seconds to wait
for each node to stablize after it has successfully rebooted.  
This defaults to 60 seconds.

The [-m=COUNT/--max-parallel] option specifies the number
of nodes to reboot in parallel.  This defaults to one for this 
command.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "reboot" }; }
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

            var hiveLogin = Program.ConnectHive();

            // Process the command arguments.

            var nodeDefinitions = new List<NodeDefinition>();

            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: At least one NODE must be specified.");
                Program.Exit(1);
            }

            if (commandLine.Arguments.Length == 1 && commandLine.Arguments[0] == "+")
            {
                foreach (var manager in hiveLogin.Definition.SortedManagers)
                {
                    nodeDefinitions.Add(manager);
                }

                foreach (var worker in hiveLogin.Definition.SortedWorkers)
                {
                    nodeDefinitions.Add(worker);
                }
            }
            else
            {
                foreach (var name in commandLine.Arguments)
                {
                    NodeDefinition node;

                    if (!hiveLogin.Definition.NodeDefinitions.TryGetValue(name, out node))
                    {
                        Console.Error.WriteLine($"*** Error: Node [{name}] is not present in the hive.");
                        Program.Exit(1);
                    }

                    nodeDefinitions.Add(node);
                }
            }

            // Perform the reboots.

            var hive       = new HiveProxy(hiveLogin);
            var controller = new SetupController<NodeDefinition>(Program.SafeCommandLine, hive.Nodes.Where(n => nodeDefinitions.Exists(nd => nd.Name == n.Name)))
            {
                ShowStatus  = !Program.Quiet,
                MaxParallel = Program.MaxParallel
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            controller.AddWaitUntilOnlineStep();
            controller.AddStep("reboot nodes",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "rebooting";
                    node.Reboot(wait: true);

                    node.Status = $"stablizing ({Program.WaitSeconds}s)";
                    Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
                });

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: The reboot for one or more nodes failed.");
                Program.Exit(1);
            }
        }
        
        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            var commandLine = shim.CommandLine;

            if (commandLine.Arguments.LastOrDefault() == "-")
            {
                shim.AddStdin(text: true);
            }
            else if (commandLine.Arguments.Length == 4)
            {
                switch (commandLine.Arguments[2])
                {
                    case "add":
                    case "settings":

                        shim.AddFile(commandLine.Arguments[3]);
                        break;
                }
            }

            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
        }
    }
}
