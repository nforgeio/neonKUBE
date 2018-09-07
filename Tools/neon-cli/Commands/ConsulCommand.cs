//-----------------------------------------------------------------------------
// FILE:	    ConsulCommand.cs
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
    /// Implements the <b>consul</b> commands.
    /// </summary>
    public class ConsulCommand : CommandBase
    {
        private const string usage = @"
Runs a HashiCorp Consul command on the hive.  All command line arguments
and options as well are passed through to the Consul CLI.

USAGE:

    neon [OPTIONS] consul [ARGS...]     - Invokes a Consul command

ARGUMENTS:

    ARGS    - The standard HashCorp Consul command arguments and options

OPTIONS :

    --help          - Prints this commands help.
    --node=NODE     - Specifies the target node.  The Consul command 
                      will be executed on the first manager node when  
                      this isn't specified.

NOTE: [neon consul watch] command is not supported.

NOTE: [neon consul snapshot ...] commands reads or writes files on the remote
      hive host, not the local workstation and you'll need to specify
      a fully qualified path.
";
        private HiveProxy hive;

        private const string remoteConsulPath = "/usr/local/bin/consul";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "consul" }; }
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
                node = hive.GetReachableManager();
            }

            if (rightCommandLine == null)
            {
                Console.Error.WriteLine("*** ERROR: The [consul] command requires the \"--\" argument.");
                Program.Exit(1);
            }

            var command = rightCommandLine.Arguments.FirstOrDefault();

            switch (command)
            {
                case "watch":

                    Console.Error.WriteLine("*** ERROR: [neon consul watch] is not supported.");
                    Program.Exit(1);
                    break;

                case "monitor":

                    // We'll just relay the output we receive from the remote command
                    // until the user kills this process.

                    using (var shell = node.CreateSudoShell())
                    {
                        shell.WriteLine($"sudo {remoteConsulPath} {rightCommandLine}");

                        while (true)
                        {
                            var line = shell.ReadLine();

                            if (line == null)
                            {
                                break; // Just being defensive
                            }

                            Console.WriteLine(line);
                        }
                    }
                    break;

                default:

                    if (rightCommandLine.Items.LastOrDefault() == "-")
                    {
                        // This is the special case where we need to pipe the standard input sent
                        // to this command on to Consul on the remote machine.  We're going to use
                        // a CommandBundle by uploading the standard input data as a file.

                        var bundle = new CommandBundle($"cat stdin.dat | {remoteConsulPath} {rightCommandLine}");

                        bundle.AddFile("stdin.dat", NeonHelper.ReadStandardInputBytes());

                        var response = node.SudoCommand(bundle, RunOptions.IgnoreRemotePath);

                        Console.WriteLine(response.AllText);
                        Program.Exit(response.ExitCode);
                    }
                    else if (rightCommandLine.StartsWithArgs("kv", "put") && rightCommandLine.Arguments.Length == 4 && rightCommandLine.Arguments[3].StartsWith("@"))
                    {
                        // We're going to special case PUT when saving a file
                        // whose name is prefixed with "@".

                        var filePath = rightCommandLine.Arguments[3].Substring(1);
                        var bundle   = new CommandBundle($"{remoteConsulPath} {rightCommandLine}");

                        bundle.AddFile(filePath, File.ReadAllBytes(filePath));

                        var response = node.SudoCommand(bundle, RunOptions.IgnoreRemotePath);

                        Console.Write(response.AllText);
                        Program.Exit(response.ExitCode);
                    }
                    else
                    {
                        // All we need to do is to execute the command remotely.  We're going to special case
                        // the [consul kv get ...] command to process the result as binary.

                        CommandResponse response;

                        if (rightCommandLine.ToString().StartsWith("kv get"))
                        {
                            response = node.SudoCommand($"{remoteConsulPath} {rightCommandLine}", RunOptions.IgnoreRemotePath | RunOptions.BinaryOutput);

                            using (var remoteStandardOutput = response.OpenOutputBinaryStream())
                            {
                                if (response.ExitCode != 0)
                                {
                                    // Looks like Consul writes its errors to standard output, so 
                                    // I'm going to open a text reader and write those lines
                                    // to standard error.

                                    using (var reader = new StreamReader(remoteStandardOutput))
                                    {
                                        foreach (var line in reader.Lines())
                                        {
                                            Console.Error.WriteLine(line);
                                        }
                                    }
                                }
                                else
                                {
                                    // Write the remote binary output to standard output.

                                    using (var output = Console.OpenStandardOutput())
                                    {
                                        var buffer = new byte[8192];
                                        int cb;

                                        while (true)
                                        {
                                            cb = remoteStandardOutput.Read(buffer, 0, buffer.Length);

                                            if (cb == 0)
                                            {
                                                break;
                                            }

                                            output.Write(buffer, 0, cb);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            response = node.SudoCommand($"{remoteConsulPath} {rightCommandLine}", RunOptions.IgnoreRemotePath);

                            Console.WriteLine(response.AllText);
                        }

                        Program.Exit(response.ExitCode);
                    }
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            Program.LogPath = null;

            var commandLine = shim.CommandLine;

            // Handle the case where we need to pipe the standard input to 
            // to the container.

            if (commandLine.Items.LastOrDefault() == "-")
            {
                shim.AddStdin();
            }
            else if (commandLine.StartsWithArgs("consul", "kv", "put") && commandLine.Arguments.Length == 5 && commandLine.Arguments[4].StartsWith("@"))
            {
                // We're going to special case PUT when saving a file
                // whose name is prefixed with "@".

                var fileArg  = commandLine.Arguments[4];
                var filePath = fileArg.Substring(1);
                var shimFile = shim.AddFile(filePath, dontReplace: true);

                shim.ReplaceItem(fileArg, "@" + shimFile);
            }

            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
        }
    }
}
