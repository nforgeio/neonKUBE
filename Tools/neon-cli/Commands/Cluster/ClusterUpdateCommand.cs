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
Updates neonCLUSTER including host configuration, as well as neonCLUSTER
infrastructure related services and containers.

USAGE:

    neon cluster update cluster [OPTIONS]
    neon cluster update images [OPTIONS]

OPTIONS:

    --force             - performs the update without prompting
    --max-parallel=#    - maximum number of host nodes or service instances
                          to be updated in parallel (defaults to 1)

REMARKS:

[update cluster] updates the cluster configuration including any neonCLUSTER
related Docker services and containers.

[update images] updates only the neonCLUSTER related Docker services and 
container images.

You can use [--max-parallel=#] to specify the number of cluster host nodes
or service instances to be updated in parallel.  This defaults to 1.

For clusters with multiple cluster managers and enough nodes and service
replicas, the update should have limited or no impact on the cluster 
workloads.  This will take some time though for very large clusters.  
You can use [--max-parallel] to speed this up at the cost of potentially
impacting your workloads.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "update" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--force" }; }
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

            Console.WriteLine();

            var command     = commandLine.Arguments.ElementAtOrDefault(1);
            var force       = commandLine.HasOption("--force");
            var maxParallel = Program.MaxParallel;

            if (command == null)
            {
                Console.Error.WriteLine(usage);
                Program.Exit(1);
            }

            switch (command)
            {
                case "cluster":

                    UpdateCluster(force, maxParallel);
                    break;

                case "images":

                    UpdateImages(force, maxParallel);
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

        /// <summary>
        /// Updates the cluster.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateCluster(bool force, int maxParallel)
        {
            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE this cluster?"))
            {
                Program.Exit(0);
            }

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin);
            var controller   = new SetupController<NodeDefinition>("cluster update", cluster.Nodes);

            controller.MaxParallel = maxParallel;

            ClusterUpdateManager.AddUpdateSteps(cluster, controller, serviceUpdateParallism: Program.MaxParallel);

            if (controller.StepCount == 0)
            {
                Console.WriteLine("The cluster is already up-to-date.");
                Program.Exit(0);
            }

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more UPDATE steps failed.");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// Updates the cluster images.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateImages(bool force, int maxParallel)
        {
            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE this cluster's images?"))
            {
                Program.Exit(0);
            }

            var clusterLogin = Program.ConnectCluster();
            var cluster      = new ClusterProxy(clusterLogin);
            var controller   = new SetupController<NodeDefinition>("cluster images", cluster.Nodes);

            controller.MaxParallel = maxParallel;

            ClusterUpdateManager.AddUpdateSteps(cluster, controller, imagesOnly: true, serviceUpdateParallism: Program.MaxParallel);

            if (controller.StepCount == 0)
            {
                Console.WriteLine("The cluster is already up-to-date.");
                Program.Exit(0);
            }

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more UPDATE steps failed.");
                Program.Exit(1);
            }
        }
    }
}
