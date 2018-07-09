//-----------------------------------------------------------------------------
// FILE:	    RegistryCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>registry</b> commands.
    /// </summary>
    public class RegistryCommand : CommandBase
    {
        private const string usage = @"
Manages the neonHIVE Docker registry configuration.

USAGE:

    Manage hive registry logins:
    ----------------------------
    neon registry login [REGISTRY [USERNAME [PASSWORD|-]]]
    neon registry logout [REGISTRY]

    Manage an optional local hive registry:
    ---------------------------------------
    neon registry service create REGISTRY CERT-PATH SECRET [USERNAME [PASSWORD|-]]
    neon registry service prune
    neon registry service remove|rm
    neon registry service status

ARGUMENTS:

    REGISTRY        - optional registry hostname 
                      (defaults to the Docker public registry)
    USERNAME        - optional username
    PASSWORD        - optional password
    -               - password will be read from STDIN
    CERT-PATH       - path to the PEM encoded certificate and private
                      key for the REGISTRY hostname
    SECRET          - password used to help prevent spoofing

REMARKS:

Login/Logout
------------
The login/logout commands are used to manage the credentials for remote
Docker registries.  A neonHIVE is typically logged into the Docker
public registry without credentials by default.  You can use the 
[neon registry login] command to log into the public registry with
credentials or to log into other registries.

[neon registry login] ensures that all hive nodes are logged in
to the target registry using the specified credentials.  You can
submit this command when credentials change.

[neon registry logout] logs all hive nodes out of the target registry.

NOTE: If REGISTRY is deployed within the same datacenter and its
      DNS points to your router's public Internet address, you'll
      likely need to configure a hive DNS hostname that overrides
      this to point the local network address for the registry
      because most routers don't allow network traffic to loop
      back into the datacenter.

Registry Service
----------------

NOTE: The hive registry service requires that the hive was 
      deployed with the Ceph file system enabled.

A neonHIVE may deploy a locally hosted Docker registry.  This
runs as the [neon-registry] Docker service on the hive manager
nodes with the registry data persisted to a shared [neon] volume
hosted on the hive's Ceph file system.

The [neon-registry] service is not deployed by default.  You can
use the [neon registry server add] command to do this or use
the [neon_docker_registry] Ansible module.  For either technique,
you'll need:

    * a registered hostname like: REGISTRY.MYDOMAIN.COM

    * the USERNAME/PASSWORD to be used to secure the registry

    * a SECRET password used behind the scenes to help 
      registry clients detected if they're being spoofed
      by a rogue registry

    * a real (non-self-signed) PEM encoded certificate covering
      the REGISTRY domain including any required intermediate
      certificates and the private key.

[neon registry add] deploys the local registry if it's not
already running otherwise it updates the registry credentials.
The command also ensures that all hive nodes are logged
into the registry with the specified credentials.

[neon registry service remove] removes the [neon-registry]
service if deployed including deleting all registry data
and then logs all hive nodes out.

[neon registry prune] temporarily puts [neon-registry] into
READ-ONLY mode while it garbage collects unreferenced image
layers.

[neon registry status] displays some information about the
[neon-registry] service and returns EXITCODE=0 if the service
is running or EXITCODE=1 if it's not.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "registry" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length == 0 || commandLine.HasHelpOption)
            {
                Help();
                Program.Exit(0);
            }

            Program.ConnectHive();

            var hive = HiveHelper.Hive;

            // Parse the arguments.

            var command = commandLine.Arguments.ElementAtOrDefault(0);

            if (string.IsNullOrEmpty(command))
            {
                Console.Error.WriteLine($"*** ERROR: COMMAND expected.");
                Program.Exit(1);
            }

            var registry = string.Empty;
            var username = string.Empty;
            var password = string.Empty;
            var certPath = string.Empty;
            var secret   = string.Empty;

            switch (command)
            {
                case "login":

                    // Read the arguments.

                    registry = commandLine.Arguments.ElementAtOrDefault(1);

                    if (!string.IsNullOrEmpty(registry))
                    {
                        if (!HiveDefinition.NameRegex.IsMatch(registry))
                        {
                            Console.Error.WriteLine($"*** ERROR: [{registry}] is not a valid hostname.");
                            Program.Exit(1);
                        }

                        username = commandLine.Arguments.ElementAtOrDefault(1);

                        if (!string.IsNullOrEmpty(username))
                        {
                            password = commandLine.Arguments.ElementAtOrDefault(2);

                            if (password == "-")
                            {
                                password = NeonHelper.ReadStandardInputText().Trim();

                                if (string.IsNullOrEmpty(password))
                                {
                                    Console.Error.WriteLine("*** ERROR: No password was read from STDIN.");
                                    Program.Exit(1);
                                }
                            }
                        }
                    }

                    // Execute the command.

                    hive.Registry.Login(registry, username, password);
                    break;

                case "logout":

                    // Read the arguments.

                    registry = commandLine.Arguments.ElementAtOrDefault(1);

                    if (!string.IsNullOrEmpty(registry) && !HiveDefinition.NameRegex.IsMatch(registry))
                    {
                        Console.Error.WriteLine($"*** ERROR: [{registry}] is not a valid hostname.");
                        Program.Exit(1);
                    }

                    // Execute the command.

                    hive.Registry.Logout(registry);
                    break;

                case "service":

                    var serviceCommand = commandLine.Arguments.ElementAtOrDefault(1);

                    if (string.IsNullOrEmpty(serviceCommand))
                    {
                        Console.Error.WriteLine($"*** ERROR: service COMMAND expected.");
                        Program.Exit(1);
                    }

                    switch (serviceCommand)
                    {
                        case "create":

                            registry = commandLine.Arguments.ElementAtOrDefault(2);

                            if (string.IsNullOrEmpty(registry))
                            {
                                Console.Error.WriteLine($"*** ERROR: Expected HOSTNAME.");
                                Program.Exit(1);
                            }

                            if (!HiveDefinition.NameRegex.IsMatch(registry))
                            {
                                Console.Error.WriteLine($"*** ERROR: [{registry}] is not a valid hostname.");
                                Program.Exit(1);
                            }

                            certPath = commandLine.Arguments.ElementAtOrDefault(3);

                            if (string.IsNullOrEmpty(certPath))
                            {
                                Console.Error.WriteLine($"*** ERROR: Expected CERT-PATH.");
                                Program.Exit(1);
                            }

                            if (!File.Exists(certPath))
                            {
                                Console.Error.WriteLine($"*** ERROR: Cannot load certificate from [{certPath}].");
                                Program.Exit(1);
                            }

                            var certificate = TlsCertificate.Load(certPath);

                            certificate.Parse();

                            if (!certificate.IsValidDate())
                            {
                                Console.Error.WriteLine($"*** ERROR: The certificate expired on [{certificate.ValidUntil}].");
                                Program.Exit(1);
                            }

                            if (!certificate.IsValidHost(registry))
                            {
                                Console.Error.WriteLine($"*** ERROR: The certificate does not cover [{registry}].");
                                Program.Exit(1);
                            }

                            secret = commandLine.Arguments.ElementAtOrDefault(4);

                            if (string.IsNullOrEmpty(secret))
                            {
                                Console.Error.WriteLine($"*** ERROR: Expected SECRET.");
                                Program.Exit(1);
                            }

                            username = commandLine.Arguments.ElementAtOrDefault(5);

                            if (!string.IsNullOrEmpty(username))
                            {
                                password = commandLine.Arguments.ElementAtOrDefault(6);

                                if (password == "-")
                                {
                                    password = NeonHelper.ReadStandardInputText().Trim();

                                    if (string.IsNullOrEmpty(password))
                                    {
                                        Console.Error.WriteLine("*** ERROR: No password was read from STDIN.");
                                        Program.Exit(1);
                                    }
                                }
                            }

                            // Execute the command.

                            Console.WriteLine();
                            hive.Registry.CreateLocalRegistry(registry, username, password, secret, certificate,
                                progress:  message => Console.WriteLine(message));

                            break;

                        case "prune":

                            if (!hive.Registry.HasLocalRegistry)
                            {
                                Console.Error.WriteLine($"*** ERROR: The [{hive.Name}] hive does not have a local registry deployed.");
                                Program.Exit(1);
                            }

                            Console.WriteLine();
                            hive.Registry.PruneLocalRegistry(progress: message => Console.WriteLine(message));
                            break;

                        case "remove":
                        case "rm":

                            if (!hive.Registry.HasLocalRegistry)
                            {
                                Console.Error.WriteLine($"*** ERROR: The [{hive.Name}] hive does not have a local registry deployed.");
                                Program.Exit(1);
                            }

                            Console.WriteLine();
                            hive.Registry.RemoveLocalRegistry(progress: message => Console.WriteLine(message));
                            break;

                        case "status":

                            Console.WriteLine();

                            if (hive.Registry.HasLocalRegistry)
                            {
                                Console.WriteLine($"Local Docker registry is deployed on [{hive.Name}].");
                                Program.Exit(0);
                            }
                            else
                            {
                                Console.WriteLine($"No local Docker registry is deployed on [{hive.Name}].");
                                Program.Exit(1);
                            }
                            break;

                        default:

                            Console.Error.WriteLine($"*** ERROR: Unexpected [{serviceCommand}] command.");
                            Program.Exit(1);
                            break;
                    }
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
            return new DockerShimInfo(shimability: DockerShimability.Optional);
        }
    }
}
