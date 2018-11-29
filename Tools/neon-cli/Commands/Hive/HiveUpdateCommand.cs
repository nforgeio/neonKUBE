//-----------------------------------------------------------------------------
// FILE:	    HiveUpdateCommand.cs
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

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>hive update</b> command.
    /// </summary>
    public class HiveUpdateCommand : CommandBase
    {
        private const string usage = @"
Updates neonHIVE including host configuration, as well as neonHIVE
infrastructure related services and containers.

USAGE:

    neon hive update          [OPTIONS]         - updates hive components as well as
                                                  containers/services
    neon hive update check                      - checks for available updates 
    neon hive update consul   [OPTIONS] VERSION - updates HashiCorp Consul
    neon hive update docker   [OPTIONS] VERSION - updates the Docker engine
    neon hive update services [OPTIONS]         - updates only hive containers/services
    neon hive update linux    [OPTIONS]         - updates linux on hive nodes
    neon hive update vault    [OPTIONS] VERSION - updates HashiCorp Vault

OPTIONS:

    --force             - performs the update without prompting
    --image-tag=TAG     - overrides the default image tag for the [neon hive update]
                          and [neon hive update update services] commands
                          (typically for testing/development purposes)

REMARKS:

The current login must have ROOT PERMISSIONS to update the hive.
";

        private HiveLogin       hiveLogin;
        private HiveProxy       hive;
        private string          version;
        private string          dockerPackageUri;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "hive", "update" }; }
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

            hiveLogin = Program.ConnectHive();
            hive      = new HiveProxy(hiveLogin);

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
                case null:

                    UpdateHive(force, maxParallel, Program.DockerImageTag);
                    break;

                case "check":

                    CheckHive(maxParallel);
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

                case "services":

                    UpdateServices(force, maxParallel, Program.DockerImageTag);
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
            return new DockerShimInfo(shimability: DockerShimability.Optional, ensureConnection: true);
        }

        /// <summary>
        /// Ensures that the current login has root hive privileges.
        /// </summary>
        private void EnsureRootPivileges()
        {
            if (!hiveLogin.IsRoot)
            {
                Console.Error.WriteLine("*** ERROR: You must have root privileges to update a hive.");
                Program.Exit(1);
            }
        }

        /// <summary>
        /// Checks the hive for pending updates.
        /// </summary>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void CheckHive(int maxParallel)
        {
            EnsureRootPivileges();

            // Use a temporary controller to determine how  many hive
            // updates are pending.

            var controller = new SetupController<NodeDefinition>("hive update check", hive.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            var hiveUpdateCount = HiveUpdateManager.AddHiveUpdateSteps(hive, controller, out var restartRequired, serviceUpdateParallism: Program.MaxParallel);

            // Create another controller to actually scan the hive nodes to
            // count the pending Linux updates as well as the system containers
            // and services that need to be updated.

            // $todo(jeff.lill):
            //
            // We need to query a new image lookup service to get the images 
            // compatible with the hive and then determine whether any of 
            // these need updating on any node.  Right now, we're just checking
            // the Linux package updates.
            //
            // We should do something similar for the host services like:
            // consul, docker, powerdns, and vault.

            controller = new SetupController<NodeDefinition>("hive update check", hive.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            var syncLock           = new object();
            var maxUpdates         = 0;
            var maxSecurityUpdates = 0;
            var componentInfo      = hive.Headend.GetComponentInfo(hive.Globals.Version, ThisAssembly.Git.Branch);
            var dockerVersions     = new Dictionary<SemanticVersion, int>();    // Counts the numbers versions installed
            var consulVersions     = new Dictionary<SemanticVersion, int>();    // on hive nodes.
            var vaultVersions      = new Dictionary<SemanticVersion, int>();

            controller.AddStep("scan hive",
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
                    // on this node and tally the versions for the hive.  Note that
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

            var title = $"[{hive.Name}] hive";

            Console.WriteLine();
            Console.WriteLine(title);
            Console.WriteLine(new string('-', title.Length));

            var restartStatus       = restartRequired ? "    *** hive restart required ***" : string.Empty;
            var hiveStatus          = (hiveUpdateCount == 0 && maxUpdates == 0) ? "CURRENT" : hiveUpdateCount.ToString() + restartStatus;
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
            else if (dockerVersions.Keys.Min(v => v) < (SemanticVersion)componentInfo.Docker)
            {
                dockerStatus = "UPDATE AVAILABLE";
            }

            var dockerTitle = $"Docker Engine: {dockerStatus}";

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(dockerTitle);
            Console.WriteLine(new string('-', dockerTitle.Length));
            Console.WriteLine($"Current: {dockerVersionInfo}");
            Console.WriteLine($"Latest:  {componentInfo.Docker}");

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
            else if (consulVersions.Keys.Min(v => v) < (SemanticVersion)componentInfo.Consul)
            {
                consulStatus = "UPDATE AVAILABLE";
            }

            var consulTitle = $"HashiCorp Consul: {consulStatus}";

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(consulTitle);
            Console.WriteLine(new string('-', consulTitle.Length));
            Console.WriteLine($"Current: {consulVersionInfo}");
            Console.WriteLine($"Latest:  {componentInfo.Consul}");

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
            else if (vaultVersions.Keys.Min(v => v) < (SemanticVersion)componentInfo.Vault)
            {
                vaultStatus = "UPDATE AVAILABLE";
            }

            var vaultTitle = $"HashiCorp Vault: {vaultStatus}";

            Console.WriteLine();
            Console.WriteLine();
            Console.WriteLine(vaultTitle);
            Console.WriteLine(new string('-', vaultTitle.Length));
            Console.WriteLine($"Current: {vaultVersionInfo}");
            Console.WriteLine($"Latest:  {componentInfo.Vault}");
        }

        /// <summary>
        /// Updates the Linux distribution on all hive nodes and then reboots them
        /// one at a time, giving each of them some time to stabilize before rebooting
        /// the next node.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        private void UpdateLinux(bool force, int maxParallel)
        {
            EnsureRootPivileges();

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE LINUX on [{hive.Name}] hive nodes?"))
            {
                Program.Exit(0);
            }

            var firstManager = hive.FirstManager;
            
            var controller = new SetupController<NodeDefinition>("hive update linux", hive.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            controller.AddStep("fetch updates",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "run: safe-apt-get update";
                    node.SudoCommand("safe-apt-get update");
                });

            controller.AddStep("update managers",
                UpdateLinux,
                n => n.Metadata.IsManager,
                parallelLimit: 1);

            if (hive.Workers.Count() > 0)
            {
                controller.AddStep("update workers",
                    UpdateLinux,
                    n => n.Metadata.IsWorker,
                    parallelLimit: 1);
            }

            if (hive.Pets.Count() > 0)
            {
                controller.AddStep("update pets",
                    UpdateLinux,
                    n => n.Metadata.IsPet,
                    parallelLimit: 1);
            }

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more UPDATE steps failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("*** Linux packages was updated successfully.");
        }

        /// <summary>
        /// Updates Linux on a specific node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        private void UpdateLinux(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            if (node.Metadata.InSwarm)
            {
                node.Status = "swarm: drain services";
                hive.Docker.DrainNode(node.Name);
            }

            node.Status = "run: safe-apt-get dist-upgrade -yq";
            node.SudoCommand("safe-apt-get dist-upgrade -yq");

            node.Reboot();

            if (node.Metadata.InSwarm)
            {
                // Put the node back into ACTIVE mode (from DRAIN).

                node.Status = "swarm: activate";
                hive.Docker.ActivateNode(node.Name);
            }

            // Give the node a chance to become active again in the swarm 
            // for containers to restart and for service tasks to redeploy 

            node.Status = $"stabilizing ({Program.WaitSeconds}s)";
            Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
        }

        /// <summary>
        /// Updates the hive hive configuration, services and containers.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        /// <param name="imageTag">Optionally overrides the default image tag.</param>
        private void UpdateHive(bool force, int maxParallel, string imageTag = null)
        {
            EnsureRootPivileges();

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE HIVE components and services on [{hive.Name}]?"))
            {
                Program.Exit(0);
            }

            var controller = new SetupController<NodeDefinition>("hive update", hive.Nodes)
            {
                ShowStatus = !Program.Quiet
            };

            controller.MaxParallel = maxParallel;
            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            var hiveUpdateCount = HiveUpdateManager.AddHiveUpdateSteps(hive, controller, out var restartRequired, serviceUpdateParallism: Program.MaxParallel, imageTag: imageTag);

            if (controller.StepCount == 0)
            {
                Console.WriteLine("The hive is already up-to-date.");
                Program.Exit(0);
            }

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more UPDATE steps failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("*** Hive components, services, and containers were updated successfully.");

            if (hive.Globals.TryGetBool(HiveGlobals.UserDisableAutoUnseal, out var disableAutoUnseal) || !disableAutoUnseal)
            {
                Console.WriteLine();
                Console.WriteLine("*** WARNING: The hive Vault is probably sealed now because auto-unseal is disabled.");
                Console.WriteLine();
                Console.WriteLine("Use these command to check Vault status and manually unseal if necessary:");
                Console.WriteLine();
                Console.WriteLine("    neon vault -- status");
                Console.WriteLine("    Neon vault -- unseal");
                Console.WriteLine();
            }
        }

        /// <summary>
        /// Updates the hive services and containers.
        /// </summary>
        /// <param name="force"><c>true</c> to disable the update prompt.</param>
        /// <param name="maxParallel">Maximum number of parallel operations.</param>
        /// <param name="imageTag">Optionally overrides the default image tag.</param>
        private void UpdateServices(bool force, int maxParallel, string imageTag = null)
        {
            EnsureRootPivileges();

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE HIVE services on [{hive.Name}]?"))
            {
                Program.Exit(0);
            }

            var controller = new SetupController<NodeDefinition>("hive update images", hive.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = true
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            HiveUpdateManager.AddHiveUpdateSteps(hive, controller, out var restartRequired, servicesOnly: true, serviceUpdateParallism: Program.MaxParallel, imageTag: imageTag);

            if (controller.StepCount == 0)
            {
                Console.WriteLine("The hive is already up-to-date.");
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
        /// Updates the Docker engine on all hive nodes and then restarts them
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

            if (!hive.Headend.IsDockerCompatible(hive.Globals.Version, version, out var message))
            {
                Console.Error.WriteLine($"*** ERROR: {message}");
                Program.Exit(1);
            }

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE DOCKER on [{hive.Name}] hive nodes?"))
            {
                Program.Exit(0);
            }

            this.version          = version;
            this.dockerPackageUri = hive.Headend.GetDockerPackageUri(version, out message);

            var controller = new SetupController<NodeDefinition>($"hive update docker: {version}", hive.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            controller.AddStep("update managers",
                UpdateDocker,
                n => n.Metadata.IsManager,
                parallelLimit: 1);

            if (hive.Workers.Count() > 0)
            {
                controller.AddStep("update workers",
                    UpdateDocker,
                    n => n.Metadata.IsWorker,
                    parallelLimit: 1);
            }

            if (hive.Pets.Count() > 0)
            {
                controller.AddStep("update pets",
                    UpdateDocker,
                    n => n.Metadata.IsPet,
                    parallelLimit: 1);
            }

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more DOCKER UPDATE steps failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("*** Docker Engine was updated successfully.");
        }

        /// <summary>
        /// Updates Docker on a specific node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        private void UpdateDocker(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            if (node.GetDockerVersion() >= (SemanticVersion)version)
            {
                return;     // Already updated
            }

            if (node.Metadata.InSwarm)
            {
                node.Status = "swarm: drain services";
                hive.Docker.DrainNode(node.Name);
            }

            node.Status = "run: safe-apt-get update";
            node.SudoCommand("safe-apt-get update");

            node.Status = $"run: safe-apt-get install -yq {dockerPackageUri}";
            node.SudoCommand($"safe-apt-get install -yq {dockerPackageUri}");

            node.Status = $"restart: docker";
            node.SudoCommand("systemctl restart docker");

            if (node.Metadata.InSwarm)
            {
                // Put the node back into ACTIVE mode (from DRAIN).

                node.Status = "swarm: activate";
                hive.Docker.ActivateNode(node.Name);
            }

            node.Status = $"stabilizing ({Program.WaitSeconds}s)";
            Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
        }

        /// <summary>
        /// Updates HashiCorp Consul on all hive nodes and then restarts them
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

            if (!hive.Headend.IsConsulCompatible(hive.Globals.Version, version, out var message))
            {
                Console.Error.WriteLine($"*** ERROR: {message}");
                Program.Exit(1);
            }

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE CONSUL on [{hive.Name}] hive nodes?"))
            {
                Program.Exit(0);
            }

            this.version = version;

            var controller = new SetupController<NodeDefinition>($"hive update consul: {version}", hive.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            controller.AddStep("update managers",
                UpdateConsul,
                n => n.Metadata.IsManager,
                parallelLimit: 1);

            if (hive.Workers.Count() > 0)
            {
                controller.AddStep("update workers",
                    UpdateConsul,
                    n => n.Metadata.IsWorker,
                    parallelLimit: 1);
            }

            if (hive.Pets.Count() > 0)
            {
                controller.AddStep("update pets",
                    UpdateConsul,
                    n => n.Metadata.IsPet,
                    parallelLimit: 1);
            }

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more CONSUL UPDATE steps failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("*** Consul was updated successfully.");
        }

        /// <summary>
        /// Updates Consul on a specific node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        private void UpdateConsul(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
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
        }

        /// <summary>
        /// Updates HashiCorp Vault on all hive nodes and then restarts them
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

            if (!hive.Headend.IsVaultCompatible(hive.Globals.Version, version, out var message))
            {
                Console.Error.WriteLine($"*** ERROR: {message}");
                Program.Exit(1);
            }

            if (!force && !Program.PromptYesNo($"*** Are you sure you want to UPDATE VAULT on [{hive.Name}] hive node?"))
            {
                Program.Exit(0);
            }

            this.version = version;

            var controller = new SetupController<NodeDefinition>($"hive update vault: {version}", hive.Nodes)
            {
                MaxParallel = maxParallel,
                ShowStatus  = !Program.Quiet
            };

            controller.SetDefaultRunOptions(RunOptions.FaultOnError);

            controller.AddStep("update managers",
                UpdateVault,
                n => n.Metadata.IsManager,
                parallelLimit: 1);

            if (hive.Workers.Count() > 0)
            {
                controller.AddStep("update workers",
                    UpdateVault,
                    n => n.Metadata.IsWorker,
                    parallelLimit: 1);
            }

            if (hive.Pets.Count() > 0)
            {
                controller.AddStep("update pets",
                    UpdateVault,
                    n => n.Metadata.IsPet,
                    parallelLimit: 1);
            }

            if (!controller.Run())
            {
                Console.Error.WriteLine("*** ERROR: One or more VAULT UPDATE steps failed.");
                Program.Exit(1);
            }

            Console.WriteLine();
            Console.WriteLine("*** Vault was updated successfully.");
        }

        /// <summary>
        /// Updates Vault on a specific node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay.</param>
        private void UpdateVault(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
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
                hive.Vault.Unseal();

                node.Status = $"stabilizing ({Program.WaitSeconds}s)";
                Thread.Sleep(TimeSpan.FromSeconds(Program.WaitSeconds));
            }
        }
    }
}
