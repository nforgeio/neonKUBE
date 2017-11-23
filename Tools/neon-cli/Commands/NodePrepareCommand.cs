//-----------------------------------------------------------------------------
// FILE:	    NodePrepareCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Net;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>node prepare</b> command.
    /// </summary>
    public class NodePrepareCommand : CommandBase
    {
        private const string usage = @"
Configures near pristine pyhsical Linux servers so that they are 
prepared to host a neonCLUSTER.  Pass the IP addresses or FQDNs 
of the nodes.

USAGE:

    neon node prepare [OPTIONS] SERVER1 [SERVER2...]

ARGUMENTS:

    CLUSTER-DEF     - Path to the cluster definition file
    SERVER1...      - IP addresses or FQDN of the servers

OPTIONS:

    --package-cache=CACHE-URI   - Optionally specifies an APT Package cache
                                  server to improve setup performance.

    --upgrade                   - Applies any pending Linux distribution
                                  package updates.

Server Requirements:
--------------------

    * Supported version of Linux (server)
    * Known root SSH credentials
    * OpenSSH installed (or another SSH server)
    * [sudo] elevates permissions without a password
";
        private string          packageCacheUri;
        private bool            upgrade;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "node", "prepare" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--package-cache" }; }
        }

        /// <inheritdoc/>
        public override bool NeedsCommandCredentials
        {
            get { return true; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }
        
        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            packageCacheUri = commandLine.GetOption("--package-cache");     // This overrides the cluster definition, if specified.
            upgrade         = commandLine.GetFlag("--upgrade");

            if (commandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine($"*** ERROR: At least one SERVER name or address is expected.");
                Program.Exit(1);
            }

            // Prepare

            var operationSummary = new List<string>();

            operationSummary.Add("node");
            operationSummary.Add("prepare");

            var clusterDefinition = (ClusterDefinition)null;
            var nodes             = new List<NodeProxy<NodeDefinition>>();

            foreach (var fqdn in commandLine.GetArguments(0))
            {
                operationSummary.Add(fqdn);

                var address = Dns.GetHostAddressesAsync(fqdn).Result.First();

                nodes.Add(Program.CreateNodeProxy<NodeDefinition>(fqdn, address.ToString(), address));
            }

            if (!string.IsNullOrEmpty(packageCacheUri))
            {
                clusterDefinition = new ClusterDefinition()
                {
                    PackageCache = packageCacheUri
                };
            }

            // Perform the setup operations.

            var controller = 
                new SetupController(operationSummary.ToArray(), nodes)
                {
                    ShowStatus  = !Program.Quiet,
                    MaxParallel = Program.MaxParallel
                };

            controller.AddWaitUntilOnlineStep();
            controller.AddStep("verify OS", n => CommonSteps.VerifyOS(n));
            controller.AddStep("prepare", server => CommonSteps.PrepareNode(server, clusterDefinition, shutdown: true, upgrade: upgrade));

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                Program.Exit(1);
            }
        }

        /// <inheritdoc/>
        public override ShimInfo Shim(DockerShim shim)
        {
            return new ShimInfo(isShimmed: false);
        }
    }
}
