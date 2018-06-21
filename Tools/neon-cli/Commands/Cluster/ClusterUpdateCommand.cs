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

    neon cluster update [OPTIONS]                   - updates cluster and containers/services

    neon cluster update check                       - checks for available updates 
    neon cluster update consul [OPTIONS] VERSION    - updates HashiCorp Consul
    neon cluster update docker [OPTIONS] VERSION    - updates the Docker engine
    neon cluster update images [OPTIONS]            - updates neon containers/services
    neon cluster update linux [OPTIONS]             - updates linux on cluster nodes
    neon cluster update vault [OPTIONS] VERSION     - updates HashiCorp Vault

OPTIONS:

    --force     - performs the update without prompting

REMARKS:

The current login must have ROOT PERMISSIONS to update the cluster.
";

        private ClusterLogin    clusterLogin;
        private ClusterProxy    cluster;

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

            clusterLogin = Program.ConnectCluster();
            cluster      = new ClusterProxy(clusterLogin);

            var command     = commandLine.Arguments.ElementAtOrDefault(0);
            var force       = commandLine.HasOption("--force");
            var maxParallel = Program.MaxParallel;

            // $todo(jeff.lill):
            //
            // We're eventually going to need a command to update Ceph services too.

            switch (command)
            {
                case null:

                    UpdateCluster(force, maxParallel);
                    break;

                case "check":

                    CheckCluster(maxParallel);
                    break;

                case "consul":

                    throw new NotImplementedException("$todo(jeff.lill): Implement this");

                case "docker":

                    throw new NotImplementedException("$todo(jeff.lill): Implement this");

                case "images":

                    UpdateImages(force, maxParallel);
                    break;

                case "linux":

                    UpdateLinux(force, maxParallel);
                    break;

                case "vault":

                    throw new NotImplementedException("$todo(jeff.lill): Implement this");

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
        /// Ensures that the current login has root cluster privileges.
        /// </summary>
        private void EnsureRootPivileges()
        {
            if (!clusterLogin.IsRoot)
            {
                Console.Error.WriteLine("*** ERROR: You must have root privileges to update a cluster.");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// Checks the cluster for pending updates.
        /// </summary>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void CheckCluster(int maxParallel)
        {
            EnsureRootPivileges();

            // Use a temporary controller to determine how  many cluster
            // updates are pending.

            var controller = new SetupController<NodeDefinition>("cluster status", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            var pendingUpdateCount = ClusterUpdateManager.AddUpdateSteps(cluster, controller, serviceUpdateParallism: Program.MaxParallel);

            // Create another controller to actually scan the cluster nodes to
            // count the pending Linux updates as well as the system containers
            // and services that need to be updated.

            // $todo(jeff.lill):
            //
            // We need to query a new image lookup service to get the images 
            // compatible with the cluster and then determine whether any of 
            // these need updating on any node.  Right now, we're just checking
            // the Linux package updates.
            //
            // We should do something similar for the host services like:
            // consul, docker, powerdns, and vault.

            controller = new SetupController<NodeDefinition>("cluster status", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            var syncLock           = new object();
            var maxUpdates         = 0;
            var maxSecurityUpdates = 0;

            controller.AddStep("get pending linux updates",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "run: apt-get update";
                    node.SudoCommand("apt-get update");

                    node.Status  = "run: apt-check";
                    var response = node.SudoCommand("/usr/lib/update-notifier/apt-check");

                    // This command returns the total number of updates and
                    // the security updates like: TOTAL;SECURITY.

                    var fields = response.ErrorText.Trim().Split(';');

                    if (fields.Length < 2 || !int.TryParse(fields[0], out var updates) || !int.TryParse(fields[1], out var securityUpdates))
                    {
                        node.Fault($"Unexpected update response: {response.OutputText}");
                        return;
                    }

                    lock (syncLock)
                    {
                        maxUpdates         = Math.Max(maxUpdates, updates);
                        maxSecurityUpdates = Math.Max(maxSecurityUpdates, securityUpdates);
                    }
                });

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more CHECK steps failed.");
                Program.Exit(1);
            }

            // Output the results.

            var title = $"[{cluster.Name}] cluster";

            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine(new string('-', title.Length));

            if (pendingUpdateCount == 0 && maxUpdates == 0)
            {
                Console.WriteLine("Cluster is up to date.");
            }
            else
            {
                Console.WriteLine($"neonCLUSTER updates:    {pendingUpdateCount}");
                Console.WriteLine($"Linux total updates:    {maxUpdates}");
                Console.WriteLine($"Linux security updates: {maxSecurityUpdates}");
            }

            Program.Exit(0);
        }

        /// <summary>
        /// Updates the Linux distribution on all cluster nodes and then reboots them
        /// one at a time, giving each of them some time to stabilize before rebooting
        /// the next node.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateLinux(bool force, int maxParallel)
        {
            EnsureRootPivileges();

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE Linux on [{cluster.Name}] cluster nodes?"))
            {
                Program.Exit(0);
            }

            var controller = new SetupController<NodeDefinition>("cluster linux update", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.AddStep("update nodes",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "run: apt-get update";
                    node.SudoCommand("apt-get update");

                    node.Status = "run: apt-get dist-upgrade -yq";
                    node.SudoCommand("apt-get dist-upgrade -yq");
                });

            controller.AddStep("reboot nodes",
                (node, stepDelay) =>
                {
                    if (node.Metadata.InSwarm)
                    {
                        // Give Swarm the chance to DRAIN any service tasks running
                        // on this node.  Ideally, we'd wait for all of the service 
                        // tasks to stop but it appears that there's no easy way to
                        // check for this other than listing all of the cluster services
                        // and then doing a [docker service ps SERVICE] for each until
                        // none report running on this node.
                        //
                        // We're just going to hardcode a wait for 30 seconds which
                        // should be OK since it'll take some time to actually install
                        // the updates before we reboot and task draining can proceed
                        // during the update.

                        node.Status = "swarm: drain service tasks";
                        node.SudoCommand($"docker node update --availability drain {node.Name}");
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                    }

                    node.Reboot();

                    if (node.Metadata.InSwarm)
                    {
                        // Put the node back into ACTIVE mode (from DRAIN).

                        node.Status = "swarm: activate";
                        node.SudoCommand($"docker node update --availability active {node.Name}");
                    }

                    // Give the node a chance to become active again in the swarm 
                    // for containers to restart and for service tasks to redeploy 

                    node.Status = $"stabilizing ({Program.WaitSeconds}s)";
                    Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
                },
                parallelLimit: 1);  // Reboot the nodes one at a time.

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more UPDATE steps failed.");
                Program.Exit(1);
            }

            Program.Exit(0);
        }

        /// <summary>
        /// Updates the cluster.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateCluster(bool force, int maxParallel)
        {
            EnsureRootPivileges();

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE cluster [{cluster.Name}]?"))
            {
                Program.Exit(0);
            }

            var controller = new SetupController<NodeDefinition>("cluster update", cluster.Nodes)
            {
                ShowStatus = !Program.Quiet
            };

            controller.MaxParallel = maxParallel;

            var pendingUpdateCount = ClusterUpdateManager.AddUpdateSteps(cluster, controller, serviceUpdateParallism: Program.MaxParallel);

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

            Console.WriteLine();
            Console.WriteLine("*** Cluster was updated successfully.");
        }

        /// <summary>
        /// Updates the cluster images.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateImages(bool force, int maxParallel)
        {
            EnsureRootPivileges();

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE this cluster's images?"))
            {
                Program.Exit(0);
            }

            var controller = new SetupController<NodeDefinition>("cluster images", cluster.Nodes);

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

            Console.WriteLine();
            Console.WriteLine("*** Cluster service and container images were updated successfully.");
        }
    }
}
