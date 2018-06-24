//-----------------------------------------------------------------------------
// FILE:	    ClusterUpdateCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
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

    neon cluster update check                       - checks for available updates 
    neon cluster update consul   [OPTIONS] VERSION  - updates HashiCorp Consul
    neon cluster update docker   [OPTIONS] VERSION  - updates the Docker engine
    neon cluster update hive     [OPTIONS]          - updates neonHIVE and containers/services
    neon cluster update services [OPTIONS]          - updates neonHIVE containers/services
    neon cluster update linux    [OPTIONS]          - updates linux on cluster nodes
    neon cluster update vault    [OPTIONS] VERSION  - updates HashiCorp Vault

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

            if (commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            Console.WriteLine();

            clusterLogin = Program.ConnectCluster();
            cluster      = new ClusterProxy(clusterLogin);

            var command     = commandLine.Arguments.ElementAtOrDefault(0);
            var force       = commandLine.HasOption("--force");
            var maxParallel = Program.MaxParallel;
            var version     = (string)null;

            // $todo(jeff.lill):
            //
            // We're eventually going to need a command to update Ceph 
            // service for major releases (revision updates are handled
            // by the Linux package manager).

            Console.WriteLine();

            switch (command)
            {
                case "check":

                    CheckCluster(maxParallel);
                    break;

                case "consul":

                    version = commandLine.Arguments.ElementAtOrDefault(1);

                    if (string.IsNullOrEmpty(version))
                    {
                        Console.Error.WriteLine("*** ERROR: VERSON argument is required.");
                        Program.Exit(1);
                    }

                    UpdateConsul(force, version, maxParallel);
                    break;

                case "docker":

                    version = commandLine.Arguments.ElementAtOrDefault(1);

                    if (string.IsNullOrEmpty(version))
                    {
                        Console.Error.WriteLine("*** ERROR: VERSON argument is required.");
                        Program.Exit(1);
                    }

                    UpdateDocker(force, version, maxParallel);
                    break;

                case "hive":

                    UpdateHive(force, maxParallel);
                    break;

                case "services":

                    UpdateServices(force, maxParallel);
                    break;

                case "linux":

                    UpdateLinux(force, maxParallel);
                    break;

                case "vault":

                    version = commandLine.Arguments.ElementAtOrDefault(1);

                    if (string.IsNullOrEmpty(version))
                    {
                        Console.Error.WriteLine("*** ERROR: VERSON argument is required.");
                        Program.Exit(1);
                    }

                    UpdateVault(force, version, maxParallel);
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

            var controller = new SetupController<NodeDefinition>("cluster check", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            var pendingUpdateCount = ClusterUpdateManager.AddHiveUpdateSteps(cluster, controller, serviceUpdateParallism: Program.MaxParallel);

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

            controller = new SetupController<NodeDefinition>("cluster check", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            var syncLock           = new object();
            var maxUpdates         = 0;
            var maxSecurityUpdates = 0;
            var componentVersions  = cluster.Headend.GetComponentVersions(cluster.Globals.Version);
            var dockerVersions     = new Dictionary<SemanticVersion, int>();    // Counts the numbers versions installed
            var consulVersions     = new Dictionary<SemanticVersion, int>();    // on cluster nodes.
            var vaultVersions      = new Dictionary<SemanticVersion, int>();

            controller.AddStep("scan cluster",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);

                    //---------------------------------------------------------
                    // Look for Linux package updates.

                    node.Status = "run: safe-apt-get update";
                    node.SudoCommand("safe-apt-get update");

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

                    //---------------------------------------------------------
                    // Determine the versions of Docker, Consul, and Vault installed
                    // on this node and tally the versions for the cluster.  Note that
                    // it's possible for multiple versions of a compontent to be
                    // installed on different nodes if a previous update did not
                    // run until completion.

                    node.Status       = "docker version";
                    var dockerVersion = node.GetDockerVersion(faultIfNotInstalled: true);

                    node.Status       = "consul version";
                    var consulVersion = node.GetConsulVersion(faultIfNotInstalled: true);

                    node.Status       = "vault version";
                    var vaultVersion  = node.GetVaultVersion(faultIfNotInstalled: true);

                    if (!node.IsFaulted)
                    {
                        lock (syncLock)
                        {
                            int count;

                            if (!dockerVersions.TryGetValue(dockerVersion, out count))
                            {
                                count = 0;
                            }

                            dockerVersions[dockerVersion] = count + 1;

                            if (!consulVersions.TryGetValue(consulVersion, out count))
                            {
                                count = 0;
                            }

                            consulVersions[consulVersion] = count + 1;

                            if (!vaultVersions.TryGetValue(vaultVersion, out count))
                            {
                                count = 0;
                            }

                            vaultVersions[vaultVersion] = count + 1;
                        }
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

            var hiveStatus          = (pendingUpdateCount == 0 && maxUpdates == 0) ? "CURRENT" : pendingUpdateCount.ToString();
            var linuxPackageStatus  = (maxUpdates == 0) ? "CURRENT" : maxUpdates.ToString();
            var linuxSecurityStatus = (maxSecurityUpdates == 0) ? "CURRENT" : maxSecurityUpdates.ToString();

            Console.WriteLine($"neonHIVE updates:       {hiveStatus}");
            Console.WriteLine($"Linux package updates:  {linuxPackageStatus}");
            Console.WriteLine($"Linux security updates: {linuxSecurityStatus}");

            //-------------------------------------------------------------
            // Docker status

            string dockerVersionInfo;

            if (dockerVersions.Count == 0)
            {
                dockerVersionInfo = "*** ERROR: Docker is not installed.";
            }
            else if (dockerVersions.Count == 1)
            {
                dockerVersionInfo = (string)dockerVersions.Keys.First();
            }
            else
            {
                var sb = new StringBuilder();

                foreach (var version in dockerVersions.Keys.OrderBy(v => v))
                {
                    sb.AppendWithSeparator((string)version, ", ");
                }

                dockerVersionInfo = sb.ToString();
            }

            var dockerStatus = "CURRENT";

            if (dockerVersions.Count == 0)
            {
                dockerStatus = "ERROR: cannot detect version";
            }
            else if (dockerVersions.Count > 1)
            {
                dockerStatus = "WARNING: multiple versions installed";
            }
            else if (dockerVersions.Keys.Min(v => v) < (SemanticVersion)componentVersions.Docker)
            {
                dockerStatus = "UPDATE AVAILABLE";
            }

            var dockerTitle = $"Docker Engine: {dockerStatus}";

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(dockerTitle);
            Console.WriteLine(new string('-', dockerTitle.Length));
            Console.WriteLine($"Current: {dockerVersionInfo}");
            Console.WriteLine($"Latest:  {componentVersions.Docker}");

            //-------------------------------------------------------------
            // Consul status

            string consulVersionInfo;

            if (consulVersions.Count == 0)
            {
                consulVersionInfo = "*** ERROR: Consul is not installed.";
            }
            else if (consulVersions.Count == 1)
            {
                consulVersionInfo = (string)consulVersions.Keys.First();
            }
            else
            {
                var sb = new StringBuilder();

                foreach (var version in consulVersions.Keys.OrderBy(v => v))
                {
                    sb.AppendWithSeparator((string)version, ", ");
                }

                consulVersionInfo = sb.ToString();
            }

            var consulStatus = "CURRENT";

            if (consulVersions.Count == 0)
            {
                consulStatus = "ERROR: cannot detect version";
            }
            else if (consulVersions.Count > 1)
            {
                consulStatus = "WARNING: multiple versions installed";
            }
            else if (consulVersions.Keys.Min(v => v) < (SemanticVersion)componentVersions.Consul)
            {
                consulStatus = "UPDATE AVAILABLE";
            }

            var consulTitle = $"HashiCorp Consul: {consulStatus}";

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(consulTitle);
            Console.WriteLine(new string('-', consulTitle.Length));
            Console.WriteLine($"Current: {consulVersionInfo}");
            Console.WriteLine($"Latest:  {componentVersions.Consul}");

            //-------------------------------------------------------------
            // Vault status

            string vaultVersionInfo;

            if (consulVersions.Count == 0)
            {
                vaultVersionInfo = "*** ERROR: Vault is not installed.";
            }
            else if (consulVersions.Count == 1)
            {
                vaultVersionInfo = (string)vaultVersions.Keys.First();
            }
            else
            {
                var sb = new StringBuilder();

                foreach (var version in vaultVersions.Keys.OrderBy(v => v))
                {
                    sb.AppendWithSeparator((string)version, ", ");
                }

                vaultVersionInfo = sb.ToString();
            }

            var vaultStatus = "CURRENT";

            if (vaultVersions.Count == 0)
            {
                vaultStatus = "ERROR: cannot detect version";
            }
            else if (vaultVersions.Count > 1)
            {
                vaultStatus = "WARNING: multiple versions installed";
            }
            else if (vaultVersions.Keys.Min(v => v) < (SemanticVersion)componentVersions.Vault)
            {
                vaultStatus = "UPDATE AVAILABLE";
            }

            var vaultTitle = $"HashiCorp Vault: {vaultStatus}";

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(vaultTitle);
            Console.WriteLine(new string('-', vaultTitle.Length));
            Console.WriteLine($"Current: {vaultVersionInfo}");
            Console.WriteLine($"Latest:  {componentVersions.Vault}");
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

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE LINUX on [{cluster.Name}] cluster nodes?"))
            {
                Program.Exit(0);
            }

            var firstManager = cluster.FirstManager;
            
            var controller = new SetupController<NodeDefinition>("cluster linux update", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.AddStep("update nodes",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "run: safe-apt-get update";
                    node.SudoCommand("safe-apt-get update");

                    node.Status = "run: safe-apt-get dist-upgrade -yq";
                    node.SudoCommand("safe-apt-get dist-upgrade -yq");
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

                        node.Status = "swarm: drain services";
                        cluster.Docker.DrainNode(node.Name);
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                    }

                    node.Reboot();

                    if (node.Metadata.InSwarm)
                    {
                        // Put the node back into ACTIVE mode (from DRAIN).

                        node.Status = "swarm: activate";
                        cluster.Docker.ActivateNode(node.Name);
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

            Console.WriteLine();
            Console.WriteLine("*** Linux packages was updated successfully.");
        }

        /// <summary>
        /// Updates the cluster hive configuration, services and containers.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateHive(bool force, int maxParallel)
        {
            EnsureRootPivileges();

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE HIVE components and services on [{cluster.Name}]?"))
            {
                Program.Exit(0);
            }

            var controller = new SetupController<NodeDefinition>("cluster update", cluster.Nodes)
            {
                ShowStatus = !Program.Quiet
            };

            controller.MaxParallel = maxParallel;

            var pendingUpdateCount = ClusterUpdateManager.AddHiveUpdateSteps(cluster, controller, serviceUpdateParallism: Program.MaxParallel);

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
            Console.WriteLine("*** Hive coponents, services, and containers were updated successfully.");
        }

        /// <summary>
        /// Updates the cluster services and containers.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateServices(bool force, int maxParallel)
        {
            EnsureRootPivileges();

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE HIVE services on [{cluster.Name}]?"))
            {
                Program.Exit(0);
            }

            var controller = new SetupController<NodeDefinition>("cluster images", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = true
            };

            ClusterUpdateManager.AddHiveUpdateSteps(cluster, controller, servicesOnly: true, serviceUpdateParallism: Program.MaxParallel);

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
            Console.WriteLine("*** Hive services and containers were updated successfully.");
        }

        /// <summary>
        /// Updates the Docker engine on all cluster nodes and then restarts them
        /// one at a time, giving each of them some time to stabilize before 
        /// updating the next node.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="version">The Docker version to install.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateDocker(bool force, string version, int maxParallel)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version));

            EnsureRootPivileges();

            if (!cluster.Headend.IsDockerCompatible(cluster.Globals.Version, version, out var message))
            {
                Console.Error.WriteLine($"*** ERROR: {message}");
                Program.Exit(1);
            }

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE DOCKER on [{cluster.Name}] cluster nodes?"))
            {
                Program.Exit(0);
            }

            var firstManager = cluster.FirstManager;
            var package      = cluster.Headend.GetDockerPackage(version, out message);

            var controller = new SetupController<NodeDefinition>($"cluster docker update: {version}", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.AddStep("update docker",
                (node, stepDelay) =>
                {
                    if (node.GetDockerVersion() >= (SemanticVersion)version)
                    {
                        return;     // Already updated
                    }

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

                        node.Status = "swarm: drain services";
                        cluster.Docker.DrainNode(node.Name);
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                    }

                    node.Status = "run: safe-apt-get update";
                    node.SudoCommand("safe-apt-get update");

                    node.Status = $"run: safe-apt-get install -yq {package}";
                    node.SudoCommand($"safe-apt-get install -yq {package}");

                    node.Status = $"restart: docker";
                    node.SudoCommand("systemctl restart docker");

                    if (node.Metadata.InSwarm)
                    {
                        // Put the node back into ACTIVE mode (from DRAIN).

                        node.Status = "swarm: activate";
                        cluster.Docker.ActivateNode(node.Name);
                    }

                    node.Status = $"stabilizing ({Program.WaitSeconds}s)";
                    Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
                },
                parallelLimit: 1);  // Update the nodes one at a time.

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more DOCKER UPDATE steps failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("*** Docker Engine was updated successfully.");
        }

        /// <summary>
        /// Updates HashiCorp Consul on all cluster nodes and then restarts them
        /// one at a time, giving each of them some time to stabilize before 
        /// updating the next node.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="version">The Docker version to install.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateConsul(bool force, string version, int maxParallel)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version));

            EnsureRootPivileges();

            if (!cluster.Headend.IsConsulCompatible(cluster.Globals.Version, version, out var message))
            {
                Console.Error.WriteLine($"*** ERROR: {message}");
                Program.Exit(1);
            }

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE CONSUL on [{cluster.Name}] cluster nodes?"))
            {
                Program.Exit(0);
            }

            var firstManager = cluster.FirstManager;

            var controller = new SetupController<NodeDefinition>($"cluster consul update: {version}", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.AddStep("update consul",
                (node, stepDelay) =>
                {
                    if (node.GetConsulVersion() >= (SemanticVersion)version)
                    {
                        return;     // Already updated
                    }

                    node.Status = $"stop: consul";
                    node.SudoCommand("systemctl stop consul");

                    node.Status = $"update: consul";

                    var bundle = new CommandBundle("./install.sh", version);

                    bundle.AddFile("install.sh",
$@"#!/bin/bash

set -euo pipefail

curl {Program.CurlOptions} https://releases.hashicorp.com/consul/$1/consul_$1_linux_amd64.zip -o /tmp/consul.zip 1>&2
unzip -u /tmp/consul.zip -d /tmp
cp /tmp/consul /usr/local/bin
chmod 770 /usr/local/bin/consul

rm /tmp/consul.zip
rm /tmp/consul 
",
                        isExecutable: true);

                    node.SudoCommand(bundle);

                    node.Status = $"restart: consul";
                    node.SudoCommand("systemctl restart consul");

                    if (node.Metadata.IsManager)
                    {
                        node.Status = $"stabilizing ({Program.WaitSeconds}s)";
                        Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
                    }
                },
                parallelLimit: 1);  // Update the nodes one at a time.

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more CONSUL UPDATE steps failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("*** Consul was updated successfully.");
        }

        /// <summary>
        /// Updates HashiCorp Vault on all cluster nodes and then restarts them
        /// one at a time, giving each of them some time to stabilize before 
        /// updating the next node.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="version">The Docker version to install.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateVault(bool force, string version, int maxParallel)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(version));

            EnsureRootPivileges();

            if (!cluster.Headend.IsVaultCompatible(cluster.Globals.Version, version, out var message))
            {
                Console.Error.WriteLine($"*** ERROR: {message}");
                Program.Exit(1);
            }

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE VAULT on [{cluster.Name}] cluster node?"))
            {
                Program.Exit(0);
            }

            var firstManager = cluster.FirstManager;

            var controller = new SetupController<NodeDefinition>($"cluster vault update: {version}", cluster.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.AddStep("update vault",
                (node, stepDelay) =>
                {
                    if (node.GetVaultVersion() >= (SemanticVersion)version)
                    {
                        return;     // Already updated
                    }

                    node.Status = $"update: vault";

                    var bundle = new CommandBundle("./install.sh", version);

                    bundle.AddFile("install.sh",
$@"#!/bin/bash

set -euo pipefail

curl {Program.CurlOptions} https://releases.hashicorp.com/vault/$1/vault_$1_linux_amd64.zip -o /tmp/vault.zip 1>&2
unzip -o /tmp/vault.zip -d /tmp
rm /tmp/vault.zip

mv /tmp/vault /usr/local/bin/vault
chmod 700 /usr/local/bin/vault
",
                    isExecutable: true);

                    node.SudoCommand(bundle);

                    if (node.Metadata.IsManager)
                    {
                        node.Status = $"restart: vault";
                        node.SudoCommand("systemctl restart vault");

                        node.Status = $"unseal: vault";
                        cluster.Vault.Unseal();

                        node.Status = $"stabilizing ({Program.WaitSeconds}s)";
                        Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
                    }
                },
                parallelLimit: 1);  // Update the nodes one at a time.

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more VAULT UPDATE steps failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("*** Vault was updated successfully.");
        }
    }
}
