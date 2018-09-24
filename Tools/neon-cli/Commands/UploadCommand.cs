//-----------------------------------------------------------------------------
// FILE:	    UploadCommand.cs
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
using Neon.IO;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>upload</b> command.
    /// </summary>
    public class UploadCommand : CommandBase
    {
        private const string usage = @"
Uploads a file to one or more hive hosts.

USAGE:

    neon upload [OPTIONS] SOURCE TARGET [NODE...]

ARGUMENTS:

    SOURCE              - Path to the source file on the local workstation.
    TARGET              - Path to the destination file on the nodes.
    NODE                - Zero are more target node names, an plus (+)
                          symbol to upload to all nodes, or to the 
                          first manager if no node is specified.
OPTIONS:

    --text              - Converts TABs to spaces and line endings to Linux
    --chmod=PERMISSIONS - Linux target file permissions

NOTES:

    * Any required destination folders will be created if missing.
    * TARGET must be the full destination path including the file name.
    * Files will be uploaded with 440 permissions if [--chmod] is not present.
";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "upload" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--text", "--chmod" }; }
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

            // Process the command options.

            var isText      = false;
            var permissions = new LinuxPermissions("440");

            if (commandLine.GetOption("--text") != null)
            {
                isText = true;
            }

            var chmod = commandLine.GetOption("--chmod");

            if (!string.IsNullOrEmpty(chmod))
            {
                if (!LinuxPermissions.TryParse(chmod, out permissions))
                {
                    Console.Error.WriteLine("*** ERROR: Invalid Linux file permissions.");
                    Program.Exit(1);
                }
            }

            // Process the command arguments.

            List<NodeDefinition>    nodeDefinitions = new List<NodeDefinition>();
            string                  source;
            string                  target;

            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: SOURCE file was not specified.");
                Program.Exit(1);
            }

            source = commandLine.Arguments[0];

            if (commandLine.Arguments.Length < 2)
            {
                Console.Error.WriteLine("*** ERROR: TARGET file was not specified.");
                Program.Exit(1);
            }

            target = commandLine.Arguments[1];

            if (commandLine.Arguments.Length == 2)
            {
                nodeDefinitions.Add(hiveLogin.Definition.Managers.First());
            }
            else if (commandLine.Arguments.Length == 3 && commandLine.Arguments[2] == "+")
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
                foreach (var name in commandLine.Shift(2).Arguments)
                {
                    NodeDefinition node;

                    if (!hiveLogin.Definition.NodeDefinitions.TryGetValue(name, out node))
                    {
                        Console.Error.WriteLine($"*** ERROR: Node [{name}] is not present in the hive.");
                        Program.Exit(1);
                    }

                    nodeDefinitions.Add(node);
                }
            }

            if (!File.Exists(source))
            {
                Console.Error.WriteLine($"*** ERROR: File [{source}] does not exist.");
                Program.Exit(1);
            }

            // Perform the upload.

            var hive       = new HiveProxy(hiveLogin);
            var controller = new SetupController<NodeDefinition>(Program.SafeCommandLine, hive.Nodes.Where(n => nodeDefinitions.Exists(nd => nd.Name == n.Name)))
            {
                ShowStatus  = !Program.Quiet,
                MaxParallel = Program.MaxParallel
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            controller.AddWaitUntilOnlineStep();
            controller.AddStep("upload",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "uploading";

                    if (isText)
                    {
                        node.UploadText(target, File.ReadAllText(source, Encoding.UTF8), tabStop: 4, outputEncoding: Encoding.UTF8);
                    }
                    else
                    {
                        using (var stream = new FileStream(source, FileMode.Open, FileAccess.Read))
                        {
                            node.Upload(target, stream);
                        }
                    }

                    node.Status = $"set permissions: {permissions}";
                    node.SudoCommand("chmod", permissions, target);
                });

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: The upload to one or more nodes failed.");
                Program.Exit(1);
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
        }
    }
}
