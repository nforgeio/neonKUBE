//-----------------------------------------------------------------------------
// FILE:	    ClusterRegistryCommand.cs
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
    /// Implements the <b>cluster registry</b> command.
    /// </summary>
    public class ClusterRegistryCommand : CommandBase
    {
        private const string usage = @"
Manages the Docker registries attached to the cluster.

USAGE:

    neon cluster registry list|ls
    neon cluster registry login REGISTRY [USERNAME [PASSWORD|-]]
    neon cluster registry logout REGISTRY

AWGUMENTS:

    REGISTRY    - Registry hostname (e.g. registry-1.docker.io)
    USERNAME    - Optional username
    PASSWORD    - Optional password or ""-"" to read from STDIN

Note that the [login] command will prompt for the username and password
when these aren't specified.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "registry" }; }
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
            var registry     = commandLine.Arguments.ElementAtOrDefault(1);

            List<RegistryCredentials> registries;

            switch (command)
            {
                case "ls":
                case "list":

                    registries = cluster.Registry.List();

                    // Special-case the Docker public registry if it's not
                    // set explicitly.  All neonCLUSTERs implicitly reference
                    // the public registry.

                    if (!registries.Exists(r => NeonClusterHelper.IsDockerPublicRegistry(r.Registry)))
                    {
                        registries.Add(
                            new RegistryCredentials()
                            {
                                Registry = NeonClusterConst.DockerPublicRegistry
                            });
                    }

                    var maxRegistryLength = registries.Max(r => r.Registry.Length);

                    foreach (var item in registries)
                    {
                        var spacer      = new string(' ', maxRegistryLength - item.Registry.Length);
                        var credentials = string.Empty;

                        if (!string.IsNullOrEmpty(item.Username))
                        {
                            credentials = $"{item.Username}/{item.Password ?? string.Empty}";
                        }

                        Console.WriteLine($"{item.Registry}{spacer} - {credentials}");
                    }
                    break;

                case "login":

                    if (string.IsNullOrEmpty(registry))
                    {
                        Console.Error.WriteLine("***ERROR: REGISTRY argument expected.");
                        Program.Exit(1);
                    }

                    if (!ClusterDefinition.DnsHostRegex.IsMatch(registry))
                    {
                        Console.Error.WriteLine($"***ERROR: [{registry}] is not a valid registry hostname.");
                        Program.Exit(1);
                    }

                    // Get the credentials.

                    var username = commandLine.Arguments.ElementAtOrDefault(2);
                    var password = commandLine.Arguments.ElementAtOrDefault(3);

                    if (password == "-")
                    {
                        password = NeonHelper.ReadStandardInputText();
                    }

                    if (string.IsNullOrEmpty(username))
                    {
                        Console.Write("username: ");
                        username = Console.ReadLine().Trim();
                    }
                    else if (string.IsNullOrEmpty(password) && !string.IsNullOrEmpty(username))
                    {
                        Console.Write("password: ");
                        password = Console.ReadLine();
                    }

                    username = username ?? string.Empty;
                    password = password ?? string.Empty;

                    // Verify the credentials on a single node first.

                    var manager = cluster.GetHealthyManager();

                    Console.WriteLine($"Verifying registry credentials on [{manager.Name}].");

                    if (!manager.RegistryLogin(registry, username, password))
                    {
                        Console.Error.WriteLine($"*** ERROR: Registry login failed on [{manager.Name}].");
                        Program.Exit(1);
                    }

                    Console.WriteLine($"Registry credentials are valid.");

                    // Login all of the nodes.

                    var sbFailedNodes = new StringBuilder();

                    Console.WriteLine($"Logging the cluster into the [{registry}] registry.");
                    cluster.Registry.Login(registry, username, password);

                    // Restart the registry cache containers running on the managers
                    // with the new credentials if we're updating credentials for the 
                    // Docker public registry and the cache is enabled.

                    cluster.Registry.RestartCache(registry, username, password);
                    break;

                case "logout":

                    if (string.IsNullOrEmpty(registry))
                    {
                        Console.Error.WriteLine("***ERROR: REGISTRY argument expected.");
                        Program.Exit(1);
                    }

                    if (!ClusterDefinition.DnsHostRegex.IsMatch(registry))
                    {
                        Console.Error.WriteLine($"***ERROR: [{registry}] is not a valid registry hostname.");
                        Program.Exit(1);
                    }

                    // $todo(jeff.lill):
                    //
                    // Complete this implementation.  Note that we also need to update
                    // registry cache credentials when we're logging out of the Docker
                    // public registry.
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
