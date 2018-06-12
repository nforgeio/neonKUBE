//-----------------------------------------------------------------------------
// FILE:	    ClusterUpdateCommand.cs
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
    /// Implements the <b>cluster update</b> command.
    /// </summary>
    public class ClusterUpdateCommand : CommandBase
    {
        private const string usage = @"
Updates a neonCLUSTER hosts, services, and containers.

USAGE:

    neon cluster update [OPTIONS]

OPTIONS:

    --dry-run           - show what will change without actually
                          doing any updating
    --max-parallel=#    - maximum number of host nodes to be updated
                          in parallel (defaults to 1)

    Update Options:
    ---------------
    --all               - updates everything (the default)
    --consul[=VERSION]  - updates HashiCorp Consul
    --docker[=VERSION]  - updates Docker daemon
    --linux             - updates Linux distribution
    --neon              - updates neonCLUSTER components and services
    --vault[=VERSION]   - updates HashiCorp Vault

REMARKS:

This command updates neonCLUSTER infrastructure related components including
the host node operating system, services, and packages.  The [--all] option
is assumed when no other update options are specified and you can use the
[--dry-run] option to see what changes would be made without actually doing
anything.

Some update options allow for a specific component version to be referenced.
These will default to the most recent known-good version at the time 
the current version of [neon-cli] was released.

You can use [--max-parallel=#] to specify the number of cluster host nodes
to be updated in parallel.  This defaults to 1.  For clusters with multiple
cluster managers and enough nodes and service replicas that the update should
have limited or no impact on the cluster workloads.  This will take some time
though for large clusters.  You can use [--max-parallel] to speed this up at
the cost of potentially impacting your workloads.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "update" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--dry-run", "--max-parallel", "--all", "--consul", "--docker", "--linux", "--neon", "--vault" }; }
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

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin);

        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(isShimmed: true, ensureConnection: true);
        }
    }
}
