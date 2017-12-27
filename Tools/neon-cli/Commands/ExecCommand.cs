//-----------------------------------------------------------------------------
// FILE:	    ExecCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

// $todo(jeff.lill):
//
// It would be cool to introduce some kind of target node filtering
// based in contraint expressions.

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>exec</b> command.
    /// </summary>
    public class ExecCommand : CommandBase
    {
        private const string usage = @"
Executes a Bash command or script on one or more cluster host nodes.

USAGE:

    neon [OPTIONS] exec COMMAND [ARGS]

ARGUMENTS:

    COMMAND         - The command and arguments to be executed.

OPTIONS:

    --node          - Zero are more target node names (separated by commas)
                      or a plus (+) symbol to target to all nodes.  Executes 
                      on the first cluster manager if no node is specified.

    --text=PATH     - Text file to be uploaded to the node(s) before
                      executing the command.  Multiple files are allowed.

    --data=PATH     - Binary file to be uploaded to the node(s) before
                      executing the command.  Multiple files are allowed.

    --script=PATH   - Script file to be uploaded to the node(s).
                      Uploaded scripts will have 700 permissions.
                      Multiple files are allowed.
NOTES:

    * Any files specified by [--text, --data, --script] options will be
      uploaded to a temporary directory first and then the command will
      be executed with that as the current working directory.  The 
      temporary directory will removed after the command completes.

    * Other than the above, commands should make no assumption about
      the current working directory.

    * If the command targets a single node, the command output will be
      written to standard output and [neon-cli] will return the command
      exit code.

    * If the command targets multiple nodes, the command output will
      be written to the node log files and [neon-cli] will return a
      [0] exit code if all of the node commands returned [0], otherwise [1].

    * Commands are executed with [sudo] privileges. 

    * If the command is being run on more than one node, the [-w/--wait] 
      option specifies the number of seconds to wait for each node to stablize
      after the command completes.  This defaults to 60 seconds.

    * The [-m=COUNT/--max-parallel] option specifies the number
      of nodes to reboot in parallel.  This defaults to one for this 
      command.

EXAMPLES:

List the Docker nodes on a cluster manager:

    neon exec docker node ls

Upgrade Linux packages on all nodes:

    neon exec --node=+ apt-get update && apt-get dist-upgrade -yq

Upload the [foo.sh] script and the [bar.txt] text file and then execute
the script on two specified nodes:

    neon exec --script=foo.sh --text=bar.txt --node=mynode-1,mynode-2 .\foo.sh

It is not possible to upload and execute a Linux binary directly but
you can change its permissions first and then execute it.  This command
does this on the first manager node:

    neon exec --data=myapp chmod 700 myapp && ./myapp
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "exec" }; }
        }

        /// <inheritdoc/>
        public override string SplitItem
        {
            get { return "exec"; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--node", "--text", "--data", "--script" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            // Split the command line on "exec".

            var split = commandLine.Split("exec");

            var leftCommandLine  = split.Left;
            var rightCommandLine = split.Right;

            // Basic initialization.

            if (leftCommandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var clusterLogin = Program.ConnectCluster();

            // Process the nodes.

            var nodeDefinitions = new List<NodeDefinition>();
            var nodeOption      = leftCommandLine.GetOption("--node", null);

            if (string.IsNullOrWhiteSpace(nodeOption))
            {
                nodeDefinitions.Add(clusterLogin.Definition.Managers.First());
            }
            else if (nodeOption == "+")
            {
                foreach (var manager in clusterLogin.Definition.SortedManagers)
                {
                    nodeDefinitions.Add(manager);
                }

                foreach (var worker in clusterLogin.Definition.SortedWorkers)
                {
                    nodeDefinitions.Add(worker);
                }
            }
            else
            {
                foreach (var name in nodeOption.Split(','))
                {
                    var trimmedName = name.Trim();

                    NodeDefinition node;

                    if (!clusterLogin.Definition.NodeDefinitions.TryGetValue(trimmedName, out node))
                    {
                        Console.WriteLine($"*** ERROR: Node [{trimmedName}] is not present in the cluster.");
                        Program.Exit(1);
                    }

                    nodeDefinitions.Add(node);
                }
            }

            // Create the command bundle by appending the right command.

            if (rightCommandLine == null)
            {
                Console.WriteLine($"*** ERROR: A command is expected after [exec].");
                Program.Exit(1);
            }

            string  command = rightCommandLine.Items.First();
            var     args    = rightCommandLine.Items.Skip(1).ToArray();

            var bundle = new CommandBundle(command, args.ToArray());

            // Append any script, text, or data files to the bundle.

            foreach (var scriptPath in leftCommandLine.GetOptionValues("--script"))
            {
                if (!File.Exists(scriptPath))
                {
                    Console.WriteLine($"*** ERROR: Script [{scriptPath}] does not exist.");
                    Program.Exit(1);
                }

                bundle.AddFile(Path.GetFileName(scriptPath), File.ReadAllText(scriptPath), isExecutable: true);
            }

            foreach (var textPath in leftCommandLine.GetOptionValues("--text"))
            {
                if (!File.Exists(textPath))
                {
                    Console.WriteLine($"*** ERROR: Text file [{textPath}] does not exist.");
                    Program.Exit(1);
                }

                bundle.AddFile(Path.GetFileName(textPath), File.ReadAllText(textPath));
            }

            foreach (var dataPath in leftCommandLine.GetOptionValues("--data"))
            {
                if (!File.Exists(dataPath))
                {
                    Console.WriteLine($"*** ERROR: Data file [{dataPath}] does not exist.");
                    Program.Exit(1);
                }

                bundle.AddFile(Path.GetFileName(dataPath), File.ReadAllBytes(dataPath));
            }

            // Perform the operation.

            var cluster = new ClusterProxy(clusterLogin, Program.CreateNodeProxy<NodeDefinition>);

            if (nodeDefinitions.Count == 1)
            {
                // Run the command on a single node and return the output and exit code.

                var node     = cluster.GetNode(nodeDefinitions.First().Name);
                var response = node.SudoCommand(bundle);

                Console.WriteLine(response.OutputText);

                Program.Exit(response.ExitCode);
            }
            else
            {
                // Run the command on multiple nodes and return an overall exit code.

                var operation = 
                    new SetupController(Program.SafeCommandLine, cluster.Nodes.Where(n => nodeDefinitions.Exists(nd => nd.Name == n.Name)))
                    {
                        ShowStatus  = !Program.Quiet,
                        MaxParallel = Program.MaxParallel
                    };

                operation.AddWaitUntilOnlineStep();
                operation.AddStep($"run: {bundle.Command}",
                    node =>
                    {
                        node.Status = "running";
                        node.SudoCommand(bundle, RunOptions.FaultOnError | RunOptions.LogOutput);

                        if (Program.WaitSeconds > 0)
                        {
                            node.Status = $"stabilize ({Program.WaitSeconds}s)";
                            Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
                        }
                    });

                if (!operation.Run())
                {
                    Console.Error.WriteLine("*** ERROR: [exec] on one or more nodes failed.");
                    Program.Exit(1);
                }
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            // We need to copy any of the files referenced by [--script]
            // [--text] and/or [--data] options to the shim directory.

            var commandLine    = shim.CommandLine;
            var optionPatterns = new string[]
                {
                    "--script=",
                    "--text=",
                    "--data="
                };

            for (int i = 0; i < commandLine.Items.Length; i++)
            {
                var item = commandLine.Items[i];

                foreach (var pattern in optionPatterns)
                {
                    if (item.StartsWith(pattern))
                    {
                        var shimPath = shim.AddFile(item.Substring(pattern.Length), dontReplace: true, noGuid: true);

                        commandLine.Items[i] = $"{pattern}{shimPath}";
                        break;
                    }
                }
            }

            return new ShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
