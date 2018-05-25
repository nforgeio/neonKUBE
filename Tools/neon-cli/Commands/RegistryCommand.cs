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

using Neon.Cluster;
using Neon.Common;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>registry</b> commands.
    /// </summary>
    public class RegistryCommand : CommandBase
    {
        private const string usage = @"
Manages the neonCLUSTER Docker registry configuration.

USAGE:

    Manage cluster registry logins:
    -------------------------------

    neon registry login [REGISTRY] [USERNAME [PASSWORD|-]]
    neon registry logout [REGISTRY]

    Manage the optional local cluster registry:
    -------------------------------------------

    neon registry service deploy REGISTRY CERT-PATH SECRET [USERNAME [PASSWORD]]
    neon registry service prune
    neon registry service remove
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
Docker registries.  A neonCLUSTER is typically logged into the Docker
public registry without credentials by default.  You can use the 
[neon registry login] command to log into the public registry with
credentials or to log into other registries.

[neon registry login] ensures that all cluster nodes are logged in
to the target registry using the specified credentials.  You can
submit this command when credentials change.

[neon registry logout] logs all cluster nodes out of the target registry.

Registry Service
----------------

NOTE: The cluster registry service requires that the cluster was 
      deployed with the Ceph file system enabled.

A neonCLUSTER may deploy a locally hosted Docker registry.  This
runs as the [neon-registry] Docker service on the cluster manager
nodes with the registry data persisted to a shared [neon] volume
hosted on the cluster's Ceph file system.

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
The command also ensures that all cluster nodes are logged
into the registry with the specified credentials.

[neon registry service remove] removes the [neon-registry]
service if deployed including deleting all registry data
and then logs all cluster nodes out.

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

            // Parse the arguments.

            var command = commandLine.Arguments.ElementAtOrDefault(0);

            switch (command)
            {
                default:

                    Console.Error.WriteLine($"*** ERROR: Unexpected [{command}] command.");
                    Program.Exit(1);
                    break;
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: true);
        }
    }
}
