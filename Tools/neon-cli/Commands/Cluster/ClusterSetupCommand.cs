//-----------------------------------------------------------------------------
// FILE:	    ClusterSetupCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster setup</b> command.
    /// </summary>
    public class ClusterSetupCommand : CommandBase
    {
        private const string usage = @"
Configures a neonHIVE as described in the hive definition file.

USAGE: 

    neon cluster setup [OPTIONS] root@CLUSTER-NAME  

OPTIONS:

    --unredacted        - Runs Vault and other commands with potential
                          secrets without redacting logs.  This is useful 
                          for debugging cluster setup  issues.  Do not
                          use for production hives.
";
        private const string logBeginMarker = "# CLUSTER-BEGIN-SETUP ############################################################";
        private const string logEndMarker = "# CLUSTER-END-SETUP-SUCCESS ######################################################";
        private const string logFailedMarker = "# CLUSTER-END-SETUP-FAILED #######################################################";

        private string contextName;
        private KubeConfig kubeConfig;
        private KubeConfigContext kubeContext;
        private KubeContextExtension kubeContextExtension;
        private ClusterProxy cluster;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "setup" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--unredacted" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length < 1)
            {
                Console.Error.WriteLine("*** ERROR: [root@CLUSTER-NAME] argument is required.");
                Program.Exit(1);
            }

            kubeConfig = KubeConfig.Load();

            var contextName = KubeConfigName.Parse(commandLine.Arguments[0]);

            if (kubeConfig.GetCluster(contextName.Cluster) != null)
            {
                Console.Error.WriteLine($"*** ERROR: You already have a deployed cluster named [{contextName.Cluster}].");
                Program.Exit(1);
            }

            var username = contextName.User;
            var clusterName = contextName.Cluster;

            kubeContextExtension = KubeHelper.GetContextExtension(contextName);

            if (kubeContextExtension == null)
            {
                Console.Error.WriteLine($"*** ERROR: Be sure to prepare the hive first using [neon cluster prepare...].");
                Program.Exit(1);
            }

            if (!kubeContextExtension.SetupPending)
            {
                Console.Error.WriteLine($"*** ERROR: Cluster [{contextName.Cluster}] has already been setup.");
            }

            kubeContext = new KubeConfigContext();
            kubeContext.Properties.Extension = kubeContextExtension;

            KubeHelper.SetKubeContext(kubeContext);

            // Note that cluster setup appends to existing log files.

            cluster = new ClusterProxy(kubeContextExtension, Program.CreateNodeProxy<NodeDefinition>, appendLog: true, useBootstrap: true, defaultRunOptions: RunOptions.LogOutput | RunOptions.FaultOnError);

            // Configure global options.

            if (commandLine.HasOption("--unredacted"))
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            // Perform the setup operations.

            var controller =
                new SetupController<NodeDefinition>(new string[] { "cluster", "setup", $"[{cluster.Name}]" }, cluster.Nodes)
                {
                    ShowStatus = !Program.Quiet,
                    MaxParallel = Program.MaxParallel
                };

            controller.AddWaitUntilOnlineStep("connect");

            controller.AddStep("ssh client cert",
                (node, stepDelay) =>
                {
                    GenerateClientSshKey(node, stepDelay);
                },
                node => node == cluster.FirstMaster);

            controller.AddStep("verify OS",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    CommonSteps.VerifyOS(node);
                });

            // Write the operation begin marker to all hive node logs.

            cluster.LogLine(logBeginMarker);

            // Perform common configuration for all cluster nodes.

            controller.AddStep("configure nodes",
                (node, stepDelay) =>
                {
                    ConfigureCommon(node, stepDelay);
                    node.InvokeIdempotentAction("setup/common-restart", () => RebootAndWait(node));
                    ConfigureNode(node);
                },
                node => true,
                stepStaggerSeconds: cluster.Definition.Setup.StepStaggerSeconds);

            // Create the Swarm.

            controller.AddStep("swarm create",
                (node, stepDelay) =>
                {
                    CreateSwarm(node, stepDelay);
                },
                node => node == cluster.FirstMaster);

            controller.AddStep("swarm join",
                (node, stepDelay) =>
                {
                    JoinSwarm(node, stepDelay);
                },
                node => node != cluster.FirstMaster && !node.Metadata.IsPet);

            // Continue with the configuration.

            controller.AddStep("check masters",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    HiveDiagnostics.CheckMaster(node, cluster.Definition);
                },
                node => node.Metadata.IsMaster);

            controller.AddStep("check workers",
                (node, stepDelay) =>
                {
                    Thread.Sleep(stepDelay);
                    HiveDiagnostics.CheckWorker(node, cluster.Definition);
                },
                node => node.Metadata.IsWorker);

            // Change the root account's password to something very strong.  
            // This step should be very close to the last one so it will still be
            // possible to log into nodes with the old password to diagnose
            // setup issues.
            //
            // Note that the if statement verifies that we haven't already generated
            // the strong password in a previous setup run that failed.  This prevents
            // us from generating a new password, perhaps resulting in hive nodes
            // having different passwords.

            if (!hiveLogin.HasStrongSshPassword)
            {
                if (cluster.Definition.HiveNode.PasswordLength > 0)
                {
                    hiveLogin.SshPassword = NeonHelper.GetRandomPassword(cluster.Definition.HiveNode.PasswordLength);
                    hiveLogin.HasStrongSshPassword = true;
                }
                else
                {
                    hiveLogin.SshPassword = Program.MachinePassword;
                }

                hiveLogin.Save();
            }

            if (cluster.Definition.HiveNode.PasswordLength > 0)
            {
                // $todo(jeff.lill):
                //
                // Note that this step isn't entirely idempotent.  The problem happens
                // when the password change fails on one or more of the nodes and succeeds
                // on others.  This will result in SSH connection failures for the nodes
                // that had their passwords changes.
                //
                // A possible workaround would be to try both the provisioning and new 
                // password when connecting to nodes, but I'm going to defer this.
                //
                //      https://github.com/jefflill/NeonForge/issues/397

                controller.AddStep("strong password",
                    (node, stepDelay) =>
                    {
                        SetStrongPassword(node, stepDelay);
                    });
            }

            controller.AddGlobalStep("passwords set", () =>
                {
                    // This hidden step sets the SSH provisioning password to NULL to 
                    // indicate that the finaly password has been set for all of the nodes.

                    hiveLogin.SshProvisionPassword = null;
                    hiveLogin.Save();
                },
                quiet: true);

            controller.AddGlobalStep("ssh certs", () => ConfigureSshCerts());

            // This needs to be run last because it will likely disable
            // SSH username/password authentication which may block
            // connection attempts.
            //
            // It's also handy to do this last so it'll be possible to 
            // manually login with the original credentials to diagnose
            // setup issues.

            controller.AddStep("ssh secured",
                (node, stepDelay) =>
                {
                    ConfigureSsh(node, stepDelay);
                });

            controller.AddGlobalStep("finish up",
                () =>
                {
                    // Some services (like [neon-hive-manager]) don't perform some
                    // activities until hive setup has completed, so we'll indicate
                    // that we're done.

                    cluster.Globals.Set(HiveGlobals.SetupPending, false);
                });

            // Start setup.

            if (!controller.Run())
            {
                // Write the operation end/failed to all hive node logs.

                cluster.LogLine(logFailedMarker);

                Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                Program.Exit(1);
            }

            // Update the hive login file.

            hiveLogin.SetupPending = false;
            hiveLogin.IsRoot = true;
            hiveLogin.Username = HiveConst.RootUser;
            hiveLogin.Definition = cluster.Definition;

            if (cluster.Definition.Vpn.Enabled)
            {
                // We don't need to save the certificate authority files any more
                // because they've been stored in the hive Vault.

                hiveLogin.VpnCredentials.CaZip = null;
            }

            hiveLogin.Save();

            // Write the operation end marker to all hive node logs.

            cluster.LogLine(logEndMarker);

            Console.WriteLine($"*** Logging into [{HiveConst.RootUser}@{cluster.Definition.Name}].");

            // Note that we're going to login via the VPN for cloud environments
            // but not for local hosting since the operator had to be on-premise
            // to have just completed hive setup.
            var currentLogin =
                new CurrentHiveLogin()
                {
                    Login = $"{HiveConst.RootUser}@{cluster.Definition.Name}",
                    ViaVpn = hiveLogin.Definition.Hosting.Environment != HostingEnvironments.Machine
                };

            currentLogin.Save();

            Console.WriteLine();
        }

        /// <summary>
        /// Basic configuration that will happen every time if DEBUG setup
        /// mode is ENABLED or else will be invoked idempotently (if that's 
        /// a word).
        /// </summary>
        /// <param name="node">The target hive node.</param>
        private void ConfigureBasic(SshProxy<NodeDefinition> node)
        {
            // Configure the node's environment variables.

            CommonSteps.ConfigureEnvironmentVariables(node, cluster.Definition);

            // Upload the setup and configuration files.

            node.CreateHostFolders();
            node.UploadConfigFiles(cluster.Definition);
            node.UploadResources(cluster.Definition);
        }

        /// <summary>
        /// Performs common node configuration.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void ConfigureCommon(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            //-----------------------------------------------------------------
            // NOTE: 
            //
            // We're going to perform the following steps outside of the
            // idempotent check to make it easier to debug and modify 
            // scripts and tools when hive setup has been partially
            // completed.  These steps are implicitly idempotent and
            // complete pretty quickly.

            if (Program.Debug)
            {
                ConfigureBasic(node);
            }

            //-----------------------------------------------------------------
            // Ensure the following steps are executed only once.

            node.InvokeIdempotentAction("setup/common",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    if (!Program.Debug)
                    {
                        ConfigureBasic(node);
                    }

            // Ensure that the node has been prepared for setup.

            CommonSteps.PrepareNode(node, cluster.Definition);

            // Create the [/mnt-data] folder if it doesn't already exist.  This folder
            // is where we're going to host the Docker containers and volumes that should
            // have been initialized to link to any data drives attached to the machine
            // or simply be located on the OS drive.  This may not be initialized for
            // some prepared nodes, so we'll create this on the OS drive if necessary.

            if (!node.DirectoryExists("/mnt-data"))
                    {
                        node.SudoCommand("mkdir -p /mnt-data");
                    }

            // Configure the APT proxy server settings early.

            node.Status = "run: setup-apt-proxy.sh";
                    node.SudoCommand("setup-apt-proxy.sh");

            // Perform basic node setup including changing the hostname.

            UploadHostsFile(node);

                    node.Status = "run: setup-node.sh";
                    node.SudoCommand("setup-node.sh");

            // Tune Linux for SSDs, if enabled.

            node.Status = "run: setup-ssd.sh";
                    node.SudoCommand("setup-ssd.sh");
                });
        }

        /// <summary>
        /// Performs basic node configuration.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ConfigureNode(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction($"setup/{node.Metadata.Role}",
                () =>
                {
                    // Configure the APT package proxy on the managers
                    // and configure the proxy selector for all nodes.

                    node.Status = "run: setup-apt-proxy.sh";
                    node.SudoCommand("setup-apt-proxy.sh");

                    // Upgrade Linux packages if requested.  We're doing this after
                    // deploying the APT package proxy so it'll be faster.

                    switch (cluster.Definition.NodeOptions.Upgrade)
                    {
                        case OsUpgrade.Partial:

                            node.Status = "package upgrade (partial)";

                            node.SudoCommand("safe-apt-get upgrade -yq");
                            break;

                        case OsUpgrade.Full:

                            node.Status = "package upgrade (full)";

                            node.SudoCommand("safe-apt-get dist-upgrade -yq");
                            break;
                    }

                    // Check to see whether the upgrade requires a reboot and
                    // do that now if necessary.

                    if (node.FileExists("/var/run/reboot-required"))
                    {
                        node.Status = "reboot after update";
                        node.Reboot();
                    }

                    // Setup NTP.

                    node.Status = "run: setup-ntp.sh";
                    node.SudoCommand("setup-ntp.sh");

                    // Setup Docker.

                    node.Status = "setup docker";

                    var dockerRetry = new LinearRetryPolicy(typeof(TransientException), maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(5));

                    dockerRetry.InvokeAsync(
                        async () =>
                        {
                            var response = node.SudoCommand("setup-docker.sh", node.DefaultRunOptions & ~RunOptions.FaultOnError);

                            if (response.ExitCode != 0)
                            {
                                throw new TransientException(response.ErrorText);
                            }

                            await Task.CompletedTask;

                        }).Wait();

                    // Clean up any cached APT files.

                    node.Status = "clean up";
                    node.SudoCommand("safe-apt-get clean -yq");
                    node.SudoCommand("rm -rf /var/lib/apt/lists");
                });
        }

        /// <summary>
        /// Reboots the cluster nodes.
        /// </summary>
        /// <param name="node">The hive node.</param>
        private void RebootAndWait(SshProxy<NodeDefinition> node)
        {
            node.Status = "restarting...";
            node.Reboot(wait: true);
        }

        /// <summary>
        /// Generates and uploads the <b>/etc/hosts</b> file for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UploadHostsFile(SshProxy<NodeDefinition> node)
        {
            var sbHosts = new StringBuilder();

            var nodeAddress = node.PrivateAddress.ToString();
            var separator = new string(' ', Math.Max(16 - nodeAddress.Length, 1));

            sbHosts.Append(
$@"
127.0.0.1	    localhost
{nodeAddress}{separator}{node.Name}
::1             localhost ip6-localhost ip6-loopback
ff02::1         ip6-allnodes
ff02::2         ip6-allrouters
");
            node.UploadText("/etc/hosts", sbHosts.ToString(), 4, Encoding.UTF8);
        }

        /// <summary>
        /// Creates the initial swarm on the bootstrap manager node passed and 
        /// captures the manager and worker swarm tokens required to join additional
        /// nodes to the hive.
        /// </summary>
        /// <param name="bootstrapManager">The target bootstrap manager server.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CreateSwarm(SshProxy<NodeDefinition> bootstrapManager, TimeSpan stepDelay)
        {
            if (hiveLogin.SwarmManagerToken != null && hiveLogin.SwarmWorkerToken != null)
            {
                return; // Swarm has already been created.
            }

            Thread.Sleep(stepDelay);

            bootstrapManager.Status = "create swarm";
            bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm init --advertise-addr {bootstrapManager.Metadata.PrivateAddress}:{cluster.Definition.Docker.SwarmPort}");

            var response = bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm join-token manager");

            hiveLogin.SwarmManagerToken = ExtractSwarmToken(response.OutputText);

            response = bootstrapManager.DockerCommand(RunOptions.FaultOnError, $"docker swarm join-token worker");

            hiveLogin.SwarmWorkerToken = ExtractSwarmToken(response.OutputText);

            // Persist the swarm tokens into the hive login.

            hiveLogin.Save();
        }

        /// <summary>
        /// Extracts the Swarm token from a <b>docker swarm join-token [manager|worker]</b> 
        /// command.  The token returned can be used when adding additional nodes to the hive.
        /// </summary>
        /// <param name="commandResponse">The command response string.</param>
        /// <returns>The swarm token.</returns>
        private string ExtractSwarmToken(string commandResponse)
        {
            const string tokenOpt = "--token ";

            var startPos = commandResponse.IndexOf(tokenOpt);
            var errorMsg = $"Cannot extract swarm token from:\r\n\r\n{commandResponse}";

            if (startPos == -1)
            {
                throw new HiveException(errorMsg);
            }

            if (startPos == -1)
            {
                throw new HiveException(errorMsg);
            }

            startPos += tokenOpt.Length;

            // It looks like the format for output has changed.  Older releases
            // like [17.03-ce] have a backslash to continue the example command.
            // Newer versions have a space and additional arguments on the same
            // line.  We're going to handle both.

            var endPos = commandResponse.IndexOf("\\", startPos);

            if (endPos == -1)
            {
                endPos = commandResponse.IndexOfAny(new char[] { ' ', '\r', '\n' }, startPos);
            }

            if (endPos == -1)
            {
                throw new HiveException($"Cannot extract swarm token from:\r\n\r\n{commandResponse}");
            }

            return commandResponse.Substring(startPos, endPos - startPos).Trim();
        }

        /// <summary>
        /// Creates the standard hive overlay networks.
        /// </summary>
        /// <param name="manager">The manager node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void ConfigureHiveNetworks(SshProxy<NodeDefinition> manager, TimeSpan stepDelay)
        {
            Thread.Sleep(stepDelay);

            manager.InvokeIdempotentAction("setup/ingress-network",
                () =>
                {
                    // We need to delete and recreate the Docker ingress network so we can
                    // reduce the MTU from 1500 to 1492.  I believe this is what was causing
                    // timeouts and perhaps poor performance when hive nodes are deployed as
                    // Hyper-V and XEN virtual machines.

                    var ingressScript =
$@"
# Delete the [ingress] network.

docker network rm ingress << EOF
y
EOF

# Give the network a chance to actually be deleted.

sleep 10

# Recreate the [ingress] network with the new settings.

docker network create \
   --driver overlay \
   --ingress \
   --subnet={cluster.Definition.Network.IngressSubnet} \
   --gateway={cluster.Definition.Network.IngressGateway} \
   --opt com.docker.network.mtu={cluster.Definition.Network.IngressMTU} \
   ingress
";
                    var bundle = new CommandBundle(". ./ingress.sh");

                    bundle.AddFile("ingress.sh", ingressScript, isExecutable: true);

                    manager.Status = "network: ingress MTU and subnet";
                    manager.SudoCommand(bundle);
                });

            manager.InvokeIdempotentAction("setup/neon-public-network",
                () =>
                {
                    manager.Status = "network: neon-public";
                    manager.DockerCommand(
                        "docker network create",
                        "--driver", "overlay",
                        "--subnet", cluster.Definition.Network.PublicSubnet,
                        "--opt", "encrypt",
                        cluster.Definition.Network.PublicAttachable ? "--attachable" : null,
                        HiveConst.PublicNetwork);
                });

            manager.InvokeIdempotentAction("setup/neon-private-network",
                () =>
                {
                    manager.Status = "network: neon-private";
                    manager.DockerCommand(
                        "docker network create",
                        "--driver", "overlay",
                        "--subnet", cluster.Definition.Network.PrivateSubnet,
                        "--opt", "encrypt",
                        cluster.Definition.Network.PrivateAttachable ? "--attachable" : null,
                        HiveConst.PrivateNetwork);
                });
        }

        /// <summary>
        /// Adds the node labels.
        /// </summary>
        /// <param name="manager">The manager node.</param>
        private void AddNodeLabels(SshProxy<NodeDefinition> manager)
        {
            manager.InvokeIdempotentAction("setup/node-labels",
                () =>
                {
                    manager.Status = "labeling";

                    foreach (var node in cluster.Nodes.Where(n => n.Metadata.InSwarm))
                    {
                        var labelDefinitions = new List<string>();

                        labelDefinitions.Add($"{NodeLabels.LabelDatacenter}={cluster.Definition.Datacenter.ToLowerInvariant()}");
                        labelDefinitions.Add($"{NodeLabels.LabelEnvironment}={cluster.Definition.Environment.ToString().ToLowerInvariant()}");

                        foreach (var item in node.Metadata.Labels.Standard)
                        {
                            var value = item.Value;

                            if (value == null)
                            {
                                value = string.Empty;
                            }
                            else
                            {
                                value = value.ToString().ToLowerInvariant();
                            }

                            labelDefinitions.Add($"{item.Key.ToLowerInvariant()}={value}");
                        }

                        foreach (var item in node.Metadata.Labels.Custom)
                        {
                            var value = item.Value;

                            if (value == null)
                            {
                                value = string.Empty;
                            }
                            else
                            {
                                value = value.ToString();
                            }

                            labelDefinitions.Add($"{item.Key.ToLowerInvariant()}={value}");
                        }

                        if (labelDefinitions.Count == 0)
                        {
                            // This should never happen but it's better to be safe.

                            continue;
                        }

                        // Generate a script that adds the required labels to each in one shot.

                        var labelAdds = new StringBuilder();

                        foreach (var labelDefinition in labelDefinitions)
                        {
                            labelAdds.AppendWithSeparator($"--label-add \"{labelDefinition}\"");
                        }

                        var labelCommand = $"docker node update {labelAdds} \"{node.Name}\"";

                        // We occasionaly see [update out of sequence] errors from labeling operations.
                        // These seem to be transient, so we're going to retry a few times before
                        // actually giving up.

                        var retry = new LinearRetryPolicy(e => e is HiveException, maxAttempts: 10, retryInterval: TimeSpan.FromSeconds(5));

                        retry.InvokeAsync(
                            async () =>
                            {
                                var bundle = new CommandBundle("./set-labels.sh");

                                bundle.AddFile("set-labels.sh", labelCommand, isExecutable: true);

                                var response = manager.SudoCommand(bundle, RunOptions.Defaults & ~RunOptions.FaultOnError);

                                if (response.ExitCode != 0)
                                {
                                    throw new TransientException(response.ErrorSummary);
                                }

                                await Task.CompletedTask;

                            }).Wait();

                        // We're going to wait two seconds for the hive manager to propagate the
                        // changes in the hope of proactively avoiding the [update out of sequence] 
                        // errors

                        Thread.Sleep(TimeSpan.FromSeconds(2));
                    }
                });
        }

        /// <summary>
        /// Pulls common images to the node.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="pullAll">
        /// Optionally specifies that all hive images should be pulled to the
        /// node regardless of the node properties.  This is used to pull images
        /// into the cache.
        /// </param>
        private void PullImages(SshProxy<NodeDefinition> node, bool pullAll = false)
        {
            node.InvokeIdempotentAction("setup/pull-images",
                () =>
                {
                    var images = new List<string>()
                    {
                        Program.ResolveDockerImage("nhive/ubuntu-16.04"),
                        Program.ResolveDockerImage("nhive/ubuntu-16.04-dotnet"),
                        Program.ResolveDockerImage(cluster.Definition.Image.Proxy),
                        Program.ResolveDockerImage(cluster.Definition.Image.ProxyVault)
                    };

                    if (node.Metadata.IsManager)
                    {
                        images.Add(Program.ResolveDockerImage(cluster.Definition.Image.HiveManager));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.Image.ProxyManager));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.Image.Dns));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.Image.DnsMon));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.Image.SecretRetriever));
                    }

                    if (cluster.Definition.Log.Enabled)
                    {
                        // All nodes pull these images:

                        images.Add(Program.ResolveDockerImage(cluster.Definition.Image.LogHost));
                        images.Add(Program.ResolveDockerImage(cluster.Definition.Image.Metricbeat));

                        // [neon-log-collector] only runs on managers.

                        if (pullAll || node.Metadata.IsManager)
                        {
                            images.Add(Program.ResolveDockerImage(cluster.Definition.Image.LogCollector));
                        }

                        // [elasticsearch] only runs on designated nodes.

                        if (pullAll || node.Metadata.Labels.LogEsData)
                        {
                            images.Add(Program.ResolveDockerImage(cluster.Definition.Image.Elasticsearch));
                        }
                    }

                    if (pullAll || node.Metadata.Labels.HiveMQ)
                    {
                        images.Add(Program.ResolveDockerImage(cluster.Definition.Image.HiveMQ));
                    }

                    foreach (var image in images)
                    {
                        var command = $"docker pull {image}";

                        node.Status = $"run: {command}";
                        node.DockerCommand(command);
                    }
                });
        }

        /// <summary>
        /// Adds the node to the swarm cluster.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void JoinSwarm(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            if (node == cluster.FirstMaster)
            {
                // This node is implictly joined to the hive.

                node.Status = "joined";
                return;
            }

            node.InvokeIdempotentAction("setup/swarm-join",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "joining";

                    if (node.Metadata.IsManager)
                    {
                        node.DockerCommand(cluster.SecureRunOptions | RunOptions.FaultOnError, $"docker swarm join --token {hiveLogin.SwarmManagerToken} {cluster.FirstMaster.Metadata.PrivateAddress}:2377");
                    }
                    else
                    {
                        // Must be a worker node.

                        node.DockerCommand(cluster.SecureRunOptions | RunOptions.FaultOnError, $"docker swarm join --token {hiveLogin.SwarmWorkerToken} {cluster.FirstMaster.Metadata.PrivateAddress}:2377");
                    }
                });

            node.Status = "joined";
        }

        /// <summary>
        /// Returns systemd unit settings that will effectively have the unit
        /// try to restart forever on failure.
        /// </summary>
        private string UnitRestartSettings
        {
            get
            {
                return
@"# These settings configure the service to restart always after
# waiting 5 seconds between attempts for up to a 365 days (effectively 
# forever).  [StartLimitIntervalSec] is set to the number of seconds 
# in a year and [StartLimitBurst] is set to the number of 5 second 
# intervals in [StartLimitIntervalSec].

Restart=always
RestartSec=5
StartLimitIntervalSec=31536000 
StartLimitBurst=6307200";
            }
        }

        /// <summary>
        /// Installs the required Ceph Storage Cluster packages on the node without
        /// configuring them.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CephPackages(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            if (!cluster.Definition.HiveFS.Enabled)
            {
                return;
            }

            // IMPLEMENTATION NOTE:
            //
            // We're going to configure the Ceph release key, repository and packages
            // on all nodes to make it easier to reconfigure the Ceph cluster manually or 
            // via tools in the future.  This also installs [ceph-common] which is
            // required to actually mount the file system, which will also probably be 
            // required on all nodes.
            //
            // Note that the full Ceph install consumes about 411MB of disk space
            // where as the common components add about 100MB.  So the full install
            // consumes an additional 311MB.  I'm not going to worry about allowing
            // operators tune what's installed.  Perhaps something for the future.

            // $todo(jeff.lill): Consider allowing customization of which Ceph components are installed.

            node.InvokeIdempotentAction("setup/ceph-packages",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "ceph package";

                    // Extract the Ceph release and version from the configuration.
                    // Note that the version is optional and is currently ignored.

                    var parts = cluster.Definition.HiveFS.Release.Split('/');
                    var cephRelease = parts[0].Trim().ToLowerInvariant();

                    // Configure the Ceph Debian package repositories and install
                    // the required packages.

                    // $todo(jeff.lill): 
                    //
                    // Do we need to pin to the Ceph version to avoid automatically updating 
                    // to a new release via [apt-get dist-upgrade]?

                    var linuxRelease = node.SudoCommand("lsb_release -sc").OutputText;

                    node.SudoCommand($"wget -q -O- https://download.ceph.com/keys/release.asc | sudo apt-key add -");
                    node.SudoCommand($"apt-add-repository \"deb https://download.ceph.com/debian-{cephRelease}/ {linuxRelease} main\"");
                    node.SudoCommand($"safe-apt-get update");
                    node.SudoCommand($"safe-apt-get install -yq ceph");

                    // We also need need support for extended file system attributes
                    // so we can set the maximum size in bytes and/or maximum number
                    // files in a directory via [setfattr] and [getfat].

                    node.SudoCommand($"safe-apt-get install -yq attr");

                    //---------------------------------------------------------
                    // The default Ceph service systemd unit files don't try very
                    // hard to restart after failures so we're going to upload
                    // systemd unit drop-in files with our changes.

                    string unitPath;

                    // Ceph-MDS

                    unitPath = "/etc/systemd/system/ceph-mds@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph metadata server daemon
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
PartOf=ceph-mds.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-mds -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecReload=/bin/kill -HUP $MAINPIDk
PrivateDevices=yes
ProtectHome=true
ProtectSystem=full
PrivateTmp=true
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-mds.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");

                    // Ceph-MGR

                    unitPath = "/etc/systemd/system/ceph-mgr@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph cluster manager daemon
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
PartOf=ceph-mgr.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph

ExecStart=/usr/bin/ceph-mgr -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecReload=/bin/kill -HUP $MAINPID

{UnitRestartSettings}

[Install]
WantedBy=ceph-mgr.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");

                    // Ceph-MGRS

                    unitPath = "/etc/systemd/system/ceph-mgrs@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph cluster manager daemon
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
PartOf=ceph-mgr.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph

ExecStart=/usr/bin/ceph-mgr -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecReload=/bin/kill -HUP $MAINPID

{UnitRestartSettings}

[Install]
WantedBy=ceph-mgr.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");
                    node.SudoCommand("systemctl daemon-reload");

                    // Ceph-MON

                    unitPath = "/etc/systemd/system/ceph-mon@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph cluster monitor daemon

# According to:
# http://www.freedesktop.org/wiki/Software/systemd/NetworkTarget
# these can be removed once ceph-mon will dynamically change network
# configuration.
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target

PartOf=ceph-mon.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-mon -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecReload=/bin/kill -HUP $MAINPID
PrivateDevices=yes
ProtectHome=true
ProtectSystem=full
PrivateTmp=true
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-mon.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");

                    // Ceph-OSD

                    unitPath = "/etc/systemd/system/ceph-osd@.service";

                    node.UploadText(unitPath,
$@"[Unit]
Description=Ceph object storage daemon osd.%i
After=network-online.target local-fs.target time-sync.target ceph-mon.target
Wants=network-online.target local-fs.target time-sync.target
PartOf=ceph-osd.target

[Service]
LimitNOFILE=1048576
LimitNPROC=1048576
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-osd -f --cluster ${{CLUSTER}} --id %i --setuser ceph --setgroup ceph
ExecStartPre=/usr/lib/ceph/ceph-osd-prestart.sh --cluster ${{CLUSTER}} --id %i
ExecReload=/bin/kill -HUP $MAINPID
ProtectHome=true
ProtectSystem=full
PrivateTmp=true
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-osd.target
");
                    node.SudoCommand($"chmod 644 {unitPath}");

                    // Reload the systemd configuration changes.

                    node.SudoCommand("systemctl daemon-reload");
                });
        }

        /// <summary>
        /// Generates the Ceph related configuration settings as a global step.  This
        /// assumes that <see cref="CephPackages"/> has already been completed.
        /// </summary>
        private void CephSettings()
        {
            if (!cluster.Definition.HiveFS.Enabled)
            {
                return;
            }

            if (hiveLogin.Ceph != null)
            {
                return; // We've already done this.
            }

            // IMPLEMENTATION NOTE:
            //
            // We're going to follow the steps keyring generation steps from the link
            // below to obtain the monitor, client admin, and OSD keyrings in addition
            // to the monitor map which we'll then persist in the cluser login so 
            // we'll be able to complete Ceph configuration in subsequent setup steps.
            //
            //      http://docs.ceph.com/docs/master/install/manual-deployment/

            // Use the first manager node to access the Ceph admin tools.

            var manager = cluster.FirstMaster;
            var cephConfig = new CephConfig();
            var tmpPath = "/tmp/ceph";
            var monKeyringPath = LinuxPath.Combine(tmpPath, "mon.keyring");
            var adminKeyringPath = LinuxPath.Combine(tmpPath, "admin.keyring");
            var osdKeyringPath = LinuxPath.Combine(tmpPath, "osd.keyring");
            var monMapPath = LinuxPath.Combine(tmpPath, "monmap");
            var runOptions = RunOptions.Defaults | RunOptions.FaultOnError;

            hiveLogin.Ceph = cephConfig;

            cephConfig.Name = "ceph";
            cephConfig.Fsid = Guid.NewGuid().ToString("D");

            manager.SudoCommand($"mkdir -p {tmpPath}", RunOptions.Defaults | RunOptions.FaultOnError);

            // Generate an initial monitor keyring (we'll be adding additional keys below).

            manager.SudoCommand($"ceph-authtool --create-keyring {monKeyringPath} --gen-key -n mon. --cap mon \"allow *\"", runOptions);

            // Generate the client admin keyring. 

            manager.SudoCommand($"ceph-authtool --create-keyring {adminKeyringPath} --gen-key -n client.admin --set-uid=0 --cap mon \"allow *\" --cap osd \"allow *\" --cap mds \"allow *\" --cap mgr \"allow *\"", runOptions);
            manager.SudoCommand($"chmod 666 {adminKeyringPath}", runOptions);

            cephConfig.AdminKeyring = manager.DownloadText(adminKeyringPath);

            // Generate the bootstrap OSD keyring.

            manager.SudoCommand($"ceph-authtool --create-keyring {osdKeyringPath} --gen-key -n client.bootstrap-osd --cap mon \"profile bootstrap-osd\"", runOptions);
            manager.SudoCommand($"chmod 666 {osdKeyringPath}", runOptions);

            cephConfig.OSDKeyring = manager.DownloadText(osdKeyringPath);

            // Add the client admin and OSD bootstrap keyrings to the monitor keyring
            // and then download the result.

            manager.SudoCommand($"ceph-authtool {monKeyringPath} --import-keyring {adminKeyringPath}", runOptions);
            manager.SudoCommand($"ceph-authtool {monKeyringPath} --import-keyring {osdKeyringPath}", runOptions);
            manager.SudoCommand($"chmod 666 {monKeyringPath}", runOptions);

            cephConfig.MonitorKeyring = manager.DownloadText(monKeyringPath);

            // Generate the monitor map.

            var sbAddOptions = new StringBuilder();

            foreach (var monNode in cluster.Nodes.Where(n => n.Metadata.Labels.CephMON))
            {
                sbAddOptions.AppendWithSeparator($"--add {monNode.Name} {monNode.PrivateAddress}");
            }

            manager.SudoCommand($"monmaptool --create {sbAddOptions} --fsid {cephConfig.Fsid} {monMapPath}", runOptions);
            manager.SudoCommand($"chmod 666 {monMapPath}", runOptions);

            cephConfig.MonitorMap = manager.DownloadBytes(monMapPath);

            // Make sure we've deleted any temporary files.

            manager.SudoCommand($"rm -r {tmpPath}", runOptions);

            // Persist the hive login so we'll have the keyrings and 
            // monitor map in case we have to restart setup.

            hiveLogin.Save();
        }

        /// <summary>
        /// Generates and uploads the Ceph configuration file to a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="configOnly">Optionally specifies that only the <b>ceph.conf</b> file is to be uploaded.</param>
        private void UploadCephConf(SshProxy<NodeDefinition> node, bool configOnly = false)
        {
            node.Status = "ceph config";

            var sbHostNames = new StringBuilder();
            var sbHostAddresses = new StringBuilder();

            foreach (var monitorNode in cluster.Definition.SortedNodes.Where(n => n.Labels.CephMON))
            {
                sbHostNames.AppendWithSeparator(monitorNode.Name, ", ");
                sbHostAddresses.AppendWithSeparator(monitorNode.PrivateAddress, ", ");
            }

            var hiveSubnet =
                new HostingManagerFactory().IsCloudEnvironment(cluster.Definition.Hosting.Environment)
                    ? cluster.Definition.Network.CloudSubnet
                    : cluster.Definition.Network.PremiseSubnet;

            node.SudoCommand("mkdir -p /etc/ceph");

            var mdsConf = string.Empty;

            if (node.Metadata.Labels.CephMDS)
            {
                mdsConf =
$@"
[mds.{node.Name}]
host = {node.Name}
mds cache memory limit = {(int)(node.Metadata.GetCephMDSCacheSize(cluster.Definition) * HiveFSOptions.CacheSizeFudge)}
mds_standby_replay = true
";
            }

            node.UploadText("/etc/ceph/ceph.conf",
$@"[global]
fsid = {hiveLogin.Ceph.Fsid}
mgr initial modules = dashboard
mon initial members = {sbHostNames}
mon host = {sbHostAddresses}
mon health preluminous compat warning = false
public network = {hiveSubnet}
auth cluster required = cephx
auth service required = cephx
auth client required = cephx
osd journal size = {HiveDefinition.ValidateSize(cluster.Definition.HiveFS.OSDJournalSize, cluster.Definition.HiveFS.GetType(), nameof(cluster.Definition.HiveFS.OSDJournalSize)) / NeonHelper.Mega}
osd pool default size = {cluster.Definition.HiveFS.OSDReplicaCount}
osd pool default min size = {cluster.Definition.HiveFS.OSDReplicaCountMin}
osd pool default pg num = {cluster.Definition.HiveFS.OSDPlacementGroups}
osd pool default pgp num = {cluster.Definition.HiveFS.OSDPlacementGroups}
osd crush chooseleaf type = 1
bluestore_cache_size = {(int)(node.Metadata.GetCephOSDCacheSize(cluster.Definition) * HiveFSOptions.CacheSizeFudge) / NeonHelper.Mega}
{mdsConf}
");
            if (!configOnly)
            {
                var cephUser = cluster.Definition.HiveFS.Username;
                var adminKeyringPath = "/etc/ceph/ceph.client.admin.keyring";

                node.UploadText(adminKeyringPath, hiveLogin.Ceph.AdminKeyring);
                node.SudoCommand($"chown {cephUser}:{cephUser} {adminKeyringPath}");
                node.SudoCommand($"chmod 640 {adminKeyringPath}");
            }
        }

        /// <summary>
        /// Generates the Ceph manager service keyring for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The keyring.</returns>
        private string GetManagerKeyring(SshProxy<NodeDefinition> node)
        {
            var result = node.SudoCommand($"ceph auth get-or-create mgr.{node.Name} mon \"allow profile mgr\" osd \"allow *\" mds \"allow *\"");

            if (result.ExitCode == 0)
            {
                return result.OutputText;
            }
            else
            {
                // We shouldn't ever get here.

                return string.Empty;
            }
        }

        /// <summary>
        /// Generates the Ceph MDS service keyring for a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The keyring.</returns>
        private string GetMDSKeyring(SshProxy<NodeDefinition> node)
        {
            var result = node.SudoCommand($"ceph auth get-or-create mds.{node.Name} mds \"allow *\" osd \"allow *\" mon \"allow rwx\"");

            if (result.ExitCode == 0)
            {
                return result.OutputText;
            }
            else
            {
                // We shouldn't ever get here.

                return string.Empty;
            }
        }

        /// <summary>
        ///Creates the Ceph service user and group.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CephUser(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/ceph-user",
                () =>
                {
                    var cephUser = cluster.Definition.HiveFS.Username;

                    // Ensure that the Ceph lib folder exists because this acts as
                    // the HOME directory for the user.

                    node.SudoCommand($"mkdir -p /var/lib/ceph");

                    // Create the [ceph] group.

                    node.SudoCommand($"groupadd --system --force {cephUser}");

                    // Create the [ceph] user.

                    node.SudoCommand($"useradd --system -g ceph --home-dir /var/lib/ceph --comment \"Ceph storage service\" {cephUser}");
                });
        }

        /// <summary>
        /// Bootstraps the Ceph monitor/manager nodes.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CephBootstrap(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/ceph-bootstrap",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    var cephUser = cluster.Definition.HiveFS.Username;
                    var tempPath = "/tmp/ceph";

                    // Create a temporary folder.

                    node.SudoCommand($"mkdir -p {tempPath}");

                    // Upload the Ceph config file here because we'll need it below.

                    UploadCephConf(node, configOnly: true);

                    // Create the monitor data directory and load the monitor map and keyring.

                    node.Status = "ceph-mon config";

                    var monFolder = $"/var/lib/ceph/mon/{hiveLogin.Ceph.Name}-{node.Name}";

                    node.SudoCommand($"mkdir -p {monFolder}");
                    node.SudoCommand($"chown {cephUser}:{cephUser} {monFolder}");
                    node.SudoCommand($"chmod 770 {monFolder}");

                    var monitorMapPath = LinuxPath.Combine(tempPath, "monmap");
                    var monitorKeyringPath = LinuxPath.Combine(tempPath, "ceph.mon.keyring");

                    node.UploadBytes(monitorMapPath, hiveLogin.Ceph.MonitorMap);
                    node.UploadText(monitorKeyringPath, hiveLogin.Ceph.MonitorKeyring);
                    node.SudoCommandAsUser(cephUser, $"ceph-mon --mkfs -i {node.Name} --monmap {monitorMapPath} --keyring {monitorKeyringPath}");

                    // Upload the client admin keyring.

                    var adminKeyringPath = "/etc/ceph/ceph.client.admin.keyring";

                    node.UploadText(adminKeyringPath, hiveLogin.Ceph.AdminKeyring);
                    node.SudoCommand($"chown {cephUser}:{cephUser} {adminKeyringPath}");
                    node.SudoCommand($"chmod 640 {adminKeyringPath}");

                    // Indicate that we're done configuring the monitor and start it.

                    node.Status = "ceph-mon start";
                    node.SudoCommand($"touch /var/lib/ceph/mon/ceph-{node.Name}/done");
                    node.SudoCommand($"systemctl enable ceph-mon@{node.Name}");
                    node.SudoCommand($"systemctl start ceph-mon@{node.Name}");

                    // Wait for the monitor to indicate that it has started.

                    try
                    {
                        NeonHelper.WaitFor(
                            () =>
                            {
                                return GetCephClusterStatus(node).IsHealthy;
                            },
                            timeout: TimeSpan.FromSeconds(120),
                            pollTime: TimeSpan.FromSeconds(2));
                    }
                    catch (TimeoutException)
                    {
                        node.Fault("Timeout waiting for Ceph Monitor.");
                    }

                    // Configure and start the manager service.

                    node.Status = "ceph-mgr config";

                    var mgrFolder = $"/var/lib/ceph/mgr/ceph-{node.Name}";

                    node.SudoCommandAsUser(cephUser, $"mkdir -p {mgrFolder}");

                    var mgrKeyringPath = $"{mgrFolder}/keyring";

                    node.UploadText(mgrKeyringPath, GetManagerKeyring(node));
                    node.SudoCommand($"chown {cephUser}:{cephUser} {mgrKeyringPath}");
                    node.SudoCommand($"chmod 640 {mgrKeyringPath}");

                    node.Status = "ceph-mgr start";
                    node.SudoCommand($"systemctl enable ceph-mgr@{node.Name}");
                    node.SudoCommand($"systemctl start ceph-mgr@{node.Name}");

                    // Give the manager some time to start.

                    Thread.Sleep(TimeSpan.FromSeconds(15));

                    if (cluster.Definition.Dashboard.HiveFS)
                    {
                        // Enable the Ceph dashboard.

                        node.Status = "ceph dashboard";

                        // Ceph versions after [luminous] require additional configuration
                        // to setup the TLS certificate and login credentials.

                        if (cluster.Definition.HiveFS.Release != "luminous")
                        {
                            node.SudoCommand($"ceph config-key set mgr/dashboard/crt -i /etc/neon/certs/hive.crt");
                            node.SudoCommand($"ceph config-key set mgr/dashboard/key -i /etc/neon/certs/hive.key");
                            node.SudoCommand($"ceph dashboard set-login-credentials {HiveConst.DefaultUsername} {HiveConst.DefaultPassword}");
                            node.SudoCommand($"ceph config set mgr mgr/dashboard/server_port {HiveHostPorts.CephDashboard}");

                            // Restart the MGR to pick up the changes.

                            node.SudoCommand($"systemctl restart ceph-mgr@{node.Name}");
                        }
                    }

                    // Remove the temp folder.

                    node.SudoCommand($"rm -r {tempPath}");
                });
        }

        /// <summary>
        /// Summarizes the Ceph cluster status.
        /// </summary>
        private class CephClusterStatus
        {
            /// <summary>
            /// Indicates that the neonHIVE includes a Ceph cluster.
            /// </summary>
            public bool IsEnabled { get; set; }

            /// <summary>
            /// Indicates that the hive is healthy.
            /// </summary>
            public bool IsHealthy { get; set; }

            /// <summary>
            /// The number of Monitor servers configured for the hive.
            /// </summary>
            public int MONCount { get; set; }

            /// <summary>
            /// The number of OSD servers configured for the hive.
            /// </summary>
            public int OSDCount { get; set; }

            /// <summary>
            /// The current number of active OSD servers.
            /// </summary>
            public int OSDActiveCount { get; set; }

            /// <summary>
            /// The number of MDS servers configured for the hive.
            /// </summary>
            public int MDSCount { get; set; }

            /// <summary>
            /// The number of active MDS servers configured for the hive.
            /// </summary>
            public int MDSActiveCount { get; set; }

            /// <summary>
            /// Indicates that the <b>hiveFS</b> file system is ready.
            /// </summary>
            public bool IsHiveFsReady { get; set; }
        }

        /// <summary>
        /// Queries a node for the current Ceph cluster status.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <returns>The <cref cref="CephClusterStatus"/>.</returns>
        private CephClusterStatus GetCephClusterStatus(SshProxy<NodeDefinition> node)
        {
            var status = new CephClusterStatus();

            if (!cluster.Definition.HiveFS.Enabled)
            {
                return status;
            }

            status.IsEnabled = true;

            // Get the Ceph cluster status as JSON to determine the number of
            // monitors as well as the number total and active OSD servers. 

            var result = node.SudoCommand("ceph status --format=json-pretty");

            if (result.ExitCode == 0)
            {
                var cephStatus = JObject.Parse(result.OutputText);

                var health = (JObject)cephStatus.GetValue("health");
                var monMap = (JObject)cephStatus.GetValue("monmap");
                var monArray = (JArray)monMap.GetValue("mons");
                var monCount = monArray.Count();
                var osdMap = (JObject)cephStatus.GetValue("osdmap");
                var osdMap2 = (JObject)osdMap.GetValue("osdmap");

                status.IsHealthy = (string)health.GetValue("status") == "HEALTH_OK";
                status.OSDCount = (int)osdMap2.GetValue("num_osds");
                status.OSDActiveCount = (int)osdMap2.GetValue("num_up_osds");
            }

            // Get the Ceph MDS status as JSON to determine the number of
            // running MDS servers. 

            result = node.SudoCommand("ceph mds stat --format=json-pretty");

            if (result.ExitCode == 0)
            {
                var mdsStatus = JObject.Parse(result.OutputText);

                // This counts the number of MDS servers that are running
                // in cold standby mode.

                var fsMap = (JObject)mdsStatus.GetValue("fsmap");
                var standbys = (JArray)fsMap.GetValue("standbys");

                foreach (JObject standby in standbys)
                {
                    var state = (string)standby.GetValue("state");

                    status.MDSCount++;

                    if (state.StartsWith("up:"))
                    {
                        status.MDSActiveCount++;
                    }
                }

                // This counts the number of MDS servers that are active
                // or are running in hot standby mode.

                var filesystems = (JArray)fsMap.GetValue("filesystems");
                var firstFileSystem = (JObject)filesystems.FirstOrDefault();

                if (firstFileSystem != null)
                {
                    var mdsMap = (JObject)firstFileSystem.GetValue("mdsmap");
                    var info = (JObject)mdsMap.GetValue("info");

                    foreach (var property in info.Properties())
                    {
                        var item = (JObject)property.Value;
                        var state = (string)item.GetValue("state");

                        status.MDSCount++;

                        if (state.StartsWith("up:"))
                        {
                            status.MDSActiveCount++;
                        }
                    }
                }
            }

            // $hack(jeff.lill):
            //
            // Detect if the [hiveFS] file system is ready.  There's probably a
            // way to detect this from the JSON status above but I need to
            // move on to other things.  This is fragile because it assumes
            // that there's only one file system deployed.

            result = node.SudoCommand("ceph mds stat");

            if (result.ExitCode == 0)
            {
                status.IsHiveFsReady = result.OutputText.StartsWith("hivefs-") &&
                                       result.OutputText.Contains("up:active");
            }

            return status;
        }

        /// <summary>
        /// Configures the Ceph cluster by configuring and starting the OSD and MDS 
        /// services and then creating and mounting a CephFS file system.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void CephCluster(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/ceph-osd",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "ceph-osd config";

                    var cephUser = cluster.Definition.HiveFS.Username;

                    // All nodes need the config file.

                    UploadCephConf(node);

                    // Configure OSD if enabled for this node.

                    if (node.Metadata.Labels.CephOSD)
                    {
                        node.Status = "ceph-osd start";
                        node.UploadText("/var/lib/ceph/bootstrap-osd/ceph.keyring", hiveLogin.Ceph.OSDKeyring);
                        node.SudoCommand($"ceph-volume lvm create --bluestore --data {node.Metadata.Labels.CephOSDDevice}");
                        node.Status = string.Empty;
                    }
                });

            // Wait for the hive OSD services to come up.  We're going to have
            // all nodes wait but only the first manager will report status to
            // the UX.

            var osdCount = cluster.Definition.Nodes.Count(n => n.Labels.CephOSD);

            if (node == cluster.FirstMaster)
            {
                node.Status = $"OSD servers: [0 of {osdCount}] ready";
            }
            else
            {
                node.Status = string.Empty;
            }

            try
            {
                NeonHelper.WaitFor(
                    () =>
                    {
                        var clusterStatus = GetCephClusterStatus(node);

                        if (node == cluster.FirstMaster)
                        {
                            node.Status = $"OSD servers: [{clusterStatus.OSDActiveCount} of {osdCount}] ready";
                        }

                        return clusterStatus.OSDActiveCount == osdCount;
                    },
                    timeout: TimeSpan.FromSeconds(120),
                    pollTime: TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                node.Fault("Timeout waiting for Ceph OSD servers.");
            }

            node.Status = string.Empty;

            // Configure MDS if enabled for this node.

            if (node.Metadata.Labels.CephMDS)
            {
                node.InvokeIdempotentAction("setup/ceph-mds",
                    () =>
                    {
                        node.Status = "ceph-msd";

                        var mdsFolder = $"/var/lib/ceph/mds/ceph-{node.Name}";

                        node.SudoCommand($"mkdir -p {mdsFolder}");

                        var mdsKeyringPath = $"{mdsFolder}/keyring";

                        node.UploadText(mdsKeyringPath, GetMDSKeyring(node));

                        node.Status = "ceph-mds start";
                        node.SudoCommand($"systemctl enable ceph-mds@{node.Name}");
                        node.SudoCommand($"systemctl start ceph-mds@{node.Name}");
                        node.Status = string.Empty;
                    });
            }

            // Wait for the hive MDS services to come up.  We're going to have
            // all nodes wait but only the first manager will report status to
            // the UX.

            var mdsCount = cluster.Definition.Nodes.Count(n => n.Labels.CephMDS);

            if (node == cluster.FirstMaster)
            {
                node.Status = $"MDS servers: [0 of {mdsCount}] ready";
            }
            else
            {
                node.Status = string.Empty;
            }

            try
            {
                NeonHelper.WaitFor(
                    () =>
                    {
                        var clusterStatus = GetCephClusterStatus(node);

                        if (node == cluster.FirstMaster)
                        {
                            node.Status = $"MDS servers: [{clusterStatus.MDSActiveCount} of {mdsCount}] ready";
                        }

                        return clusterStatus.MDSActiveCount == mdsCount;
                    },
                    timeout: TimeSpan.FromSeconds(120),
                    pollTime: TimeSpan.FromSeconds(2));
            }
            catch (TimeoutException)
            {
                node.Fault("Timeout waiting for Ceph MDS servers.");
            }

            node.Status = string.Empty;

            // We're going to have the first manager create the [hivefs_data] and [hivefs_metadata] storage 
            // pools and then the [hiveFS] filesystem using those pools.  Then we'll have the first manager 
            // wait for the filesystem to be created.

            if (node == cluster.FirstMaster)
            {
                node.InvokeIdempotentAction("setup/hive-fs",
                    () =>
                    {
                        node.Status = "create file system";
                        node.SudoCommand($"ceph osd pool create hivefs_data {cluster.Definition.HiveFS.OSDPlacementGroups}");
                        node.SudoCommand($"ceph osd pool create hivefs_metadata {cluster.Definition.HiveFS.OSDPlacementGroups}");
                        node.SudoCommand($"ceph fs new hivefs hivefs_metadata hivefs_data");
                    });
            }

            // Wait for the file system to start.

            try
            {
                node.Status = "ceph stablize (30s)";

                NeonHelper.WaitFor(
                    () =>
                    {
                        return GetCephClusterStatus(node).IsHiveFsReady;
                    },
                    timeout: TimeSpan.FromSeconds(120),
                    pollTime: TimeSpan.FromSeconds(2));

                // Wait longer to be really sure the filesystem initialization has completed.

                Thread.Sleep(TimeSpan.FromSeconds(30));
            }
            catch (TimeoutException)
            {
                node.Fault("Timeout waiting for Ceph file system.");
            }
            finally
            {
                node.Status = "ceph stablized";
            }

            // We're going to use the FUSE client to mount the file system at [/mnt/hivefs].

            node.InvokeIdempotentAction("setup/ceph-mount",
                () =>
                {
                    var monNode = cluster.Definition.SortedNodes.First(n => n.Labels.CephMON);

                    node.Status = "mount file system";
                    node.SudoCommand($"mkdir -p /mnt/hivefs");

                    // I've seen this fail with an transient error sometimes because no
                    // MDS server is ready.  We'll retry a few times to mitigate this.

                    var retry = new LinearRetryPolicy(typeof(TransientException), maxAttempts: 30, TimeSpan.FromSeconds(1));

                    retry.InvokeAsync(
                        async () =>
                        {
                            var response = node.SudoCommand($"ceph-fuse -m {monNode.PrivateAddress}:6789 /mnt/hivefs", RunOptions.Defaults & ~RunOptions.FaultOnError);

                            if (response.ExitCode != 0)
                            {
                                throw new TransientException(response.ErrorSummary);
                            }

                            if (response.ErrorText.Contains("ceph mount failed"))
                            {
                                // $hack)jeff.lill):
                                //
                                // I've seen situations where mounting failed but the exit code was
                                // still zero.  This appears to be a bug:
                                //
                                //      https://tracker.ceph.com/issues/23665
                                //
                                // I'm going to detect this case and throw a transient exception
                                // and retry.
                                //
                                // The strange thing is that the command also reports:
                                //
                                //      probably no MDS server is up?
                                //
                                // but I explicitly verify that the MDS servers are up and active
                                // above, so I'm not sure why this is failing.

                                throw new TransientException(response.ErrorSummary);
                            }

                            await Task.CompletedTask;

                        }).Wait();
                });

            // $hack(jeff.lill):
            //
            // I couldn't enable the built-in [ceph-fuse@/*.service] to have
            // [/mnt/hivefs] mount on reboot via:
            //
            //      systemctl enable ceph-fuse@/hivefs.service
            //
            // I was seeing an "Invalid argument" error.  I'm going to workaround
            // this by creating and enabling my own service.

            node.InvokeIdempotentAction("setup/ceph-fuse-service",
                () =>
                {
                    node.Status = "ceph fuse service";
                    node.UploadText("/etc/systemd/system/ceph-fuse-hivefs.service",
$@"[Unit]
Description=Ceph FUSE client (for /mnt/hivefs)
After=network-online.target local-fs.target time-sync.target
Wants=network-online.target local-fs.target time-sync.target
Conflicts=umount.target
PartOf=ceph-fuse.target

[Service]
EnvironmentFile=-/etc/default/ceph
Environment=CLUSTER=ceph
ExecStart=/usr/bin/ceph-fuse -f -o nonempty --cluster ${{CLUSTER}} /mnt/hivefs
TasksMax=infinity

{UnitRestartSettings}

[Install]
WantedBy=ceph-fuse.target
WantedBy=docker.service
");
                    node.SudoCommand("chmod 644 /etc/systemd/system/ceph-fuse-hivefs.service");
                    node.SudoCommand($"systemctl enable ceph-fuse-hivefs.service");
                    node.SudoCommand($"systemctl start ceph-fuse-hivefs.service");
                });

            if (node == cluster.FirstMaster)
            {
                node.InvokeIdempotentAction("setup/hive-fs-init",
                    () =>
                    {
                        // Initialize [/mnt/hivefs]:
                        //
                        //      /mnt/hivefs/READY   - Read-only file whose presence indicates that the file system is mounted
                        //      /mnt/hivefs/docker  - Holds mapped Docker volumes
                        //      /mnt/hivefs/neon    - Reserved for neonHIVE

                        node.Status = "populate hiveFS";
                        node.SudoCommand($"touch /mnt/hivefs/READY && chmod 444 /mnt/hivefs/READY");
                        node.SudoCommand($"mkdir -p /mnt/hivefs/docker && chown root:root /mnt/hivefs/docker && chmod 770 /mnt/hivefs/docker");
                        node.SudoCommand($"mkdir -p /mnt/hivefs/neon && chown root:root /mnt/hivefs/neon && chmod 770 /mnt/hivefs/neon");
                    });
            }

            node.InvokeIdempotentAction("setup/hive-fs-ready",
                () =>
                {
                    // Wait up to 120 seconds for the hive file system to indicate that it's ready
                    // by the presence of the [/mnt/hivefs/READY] file.

                    node.Status = "waiting for hiveFS";

                    NeonHelper.WaitFor(
                        () =>
                        {
                            try
                            {
                                return node.FileExists("/mnt/hivefs/READY");
                            }
                            catch (Exception e)
                            {
                                node.LogException("Waiting for hiveFS", e);
                                return false;
                            }
                        },
                        timeout: TimeSpan.FromSeconds(120),
                        pollTime: TimeSpan.FromSeconds(1));
                });

            // Download and install the [neon-volume-plugin].

            node.InvokeIdempotentAction("setup/neon-volume-plugin",
                () =>
                {
                    node.Status = "neon-volume-plugin: install";

                    var installCommand = new CommandBundle("./install.sh");

                    installCommand.AddFile("install.sh",
$@"# Download and install the plugin.

curl {Program.CurlOptions} {cluster.Definition.HiveFS.VolumePluginPackage} -o /tmp/neon-volume-plugin-deb 1>&2
dpkg --install /tmp/neon-volume-plugin-deb
rm /tmp/neon-volume-plugin-deb

# Enable and start the plugin service.

systemctl daemon-reload
systemctl enable neon-volume-plugin
systemctl start neon-volume-plugin
",
                        isExecutable: true);

                    node.SudoCommand(installCommand);
                });

            node.InvokeIdempotentAction("setup/neon-volume",
                () =>
                {
                    // $hack(jeff.lill):
                    //
                    // We need to create a volume using this plugin on every hive node
                    // so that the plugin will be reported to hive managers so they
                    // will be able to schedule tasks using the plugin on these nodes.
                    //
                    // This appears to be a legacy plugin issue.  I suspect that this
                    // won't be necessary for managed plugins.
                    //
                    //      https://github.com/jefflill/NeonForge/issues/226

                    node.Status = "neon-volume-plugin: register";
                    node.SudoCommand("docker volume create --driver=neon neon-do-not-remove");
                });
        }

        /// <summary> 
        /// Configures the Vault load balancer service: <b>neon-proxy-vault</b>.
        /// </summary>
        private void ConfigureVaultProxy()
        {
            // Create the comma separated list of Vault manager endpoints formatted as:
            //
            //      NODE:IP:PORT

            var sbEndpoints = new StringBuilder();

            foreach (var manager in cluster.Definition.SortedManagers)
            {
                sbEndpoints.AppendWithSeparator($"{manager.Name}:{manager.Name}.{cluster.Definition.Hostnames.Vault}:{NetworkPorts.Vault}", ",");
            }

            cluster.FirstMaster.InvokeIdempotentAction("setup/proxy-vault",
                () =>
                {
                    // Docker mesh routing seemed unstable on versions [17.03.0-ce]
                    // thru [17.12.1-ce] so we're going to provide an option to work
                    // around this by running the PUBLIC, PRIVATE and VAULT proxies 
                    // on all nodes and  publishing the ports to the host (not the mesh).
                    //
                    //      https://github.com/jefflill/NeonForge/issues/104
                    //
                    // Note that this mode feature is documented (somewhat poorly) here:
                    //
                    //      https://docs.docker.com/engine/swarm/services/#publish-ports

                    var options = new List<string>();

                    if (cluster.Definition.Docker.GetAvoidIngressNetwork(cluster.Definition))
                    {
                        options.Add("--publish");
                        options.Add($"mode=host,published={HiveHostPorts.ProxyVault},target={NetworkPorts.Vault}");
                    }
                    else
                    {
                        options.Add("--constraint");
                        options.Add($"node.role==manager");

                        options.Add("--publish");
                        options.Add($"{HiveHostPorts.ProxyVault}:{NetworkPorts.Vault}");
                    }

                    // Deploy [neon-proxy-vault].

                    ServiceHelper.StartService(cluster, "neon-proxy-vault", cluster.Definition.Image.ProxyVault,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-proxy-vault",
                            "--detach=false",
                            "--mode", "global",
                            "--endpoint-mode", "vip",
                            "--network", HiveConst.PrivateNetwork,
                            options,
                            "--mount", "type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--env", $"VAULT_ENDPOINTS={sbEndpoints}",
                            "--env", $"LOG_LEVEL=INFO",
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            ServiceHelper.ImagePlaceholderArg));
                });


            // We also need to deploy [neon-proxy-vault] to any pet nodes as Docker containers
            // to forward any Vault related traffic to the primary Vault instance running on onez
            // of the managers because pets aren't part of the Swarm.

            var vaultTasks = new List<Task>();

            foreach (var pet in cluster.Pets)
            {
                var task = Task.Run(
                    () =>
                    {
                        var steps = new ConfigStepList();

                        ServiceHelper.AddContainerStartSteps(cluster, steps, pet, "neon-proxy-vault", cluster.Definition.Image.ProxyVault,
                            new CommandBundle(
                                "docker run",
                                "--name", "neon-proxy-vault",
                                "--detach",
                                "--publish", $"{HiveHostPorts.ProxyVault}:{NetworkPorts.Vault}",
                                "--mount", "type=bind,source=/etc/neon/host-env,destination=/etc/neon/host-env,readonly=true",
                                "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                                "--env", $"VAULT_ENDPOINTS={sbEndpoints}",
                                "--env", $"LOG_LEVEL=INFO",
                                "--restart", "always",
                                ServiceHelper.ImagePlaceholderArg));

                        cluster.Configure(steps);
                    });

                vaultTasks.Add(task);
            }

            NeonHelper.WaitAllAsync(vaultTasks).Wait();
        }

        /// <summary>
        /// Ensures that Vault is unsealed on all manager nodes. 
        /// </summary>
        private void VaultUnseal()
        {
            // Wait for the Vault instance on each manager node to become ready 
            // and then unseal them.

            var firstMaster = cluster.FirstMaster;
            var timer = new Stopwatch();
            var timeout = TimeSpan.FromMinutes(5);

            // We're going to use a raw JsonClient here that is configured
            // to accept self-signed certificates.  We can't use [NeonClusterHelper.Vault]
            // here because the hive isn't fully initialized yet.

            var httpHandler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip,
            };

            using (httpHandler)
            {
                foreach (var manager in cluster.Managers)
                {
                    using (var vaultJsonClient = new JsonClient(httpHandler, disposeHandler: false))
                    {
                        vaultJsonClient.BaseAddress = new Uri($"https://{manager.Name}.{cluster.Definition.Hostnames.Vault}:{NetworkPorts.Vault}/");

                        // Wait for Vault to start and be able to respond to requests.

                        timer.Restart();

                        manager.Status = "vault: start";

                        while (true)
                        {
                            if (timer.Elapsed > timeout)
                            {
                                manager.Fault($"[Vault] is not ready after waiting [{timeout}].");
                                return;
                            }

                            try
                            {
                                var jsonResponse = vaultJsonClient.GetUnsafeAsync("/v1/sys/seal-status").Result;

                                if (jsonResponse.IsSuccess)
                                {
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                manager.LogException(e);
                                Thread.Sleep(TimeSpan.FromSeconds(1));
                            }
                        }

                        // Unseal the Vault instance.

                        manager.Status = "vault: unseal";

                        manager.SudoCommand($"vault-direct operator unseal -reset", RunOptions.None);    // This clears any previous uncompleted unseal attempts

                        for (int i = 0; i < hiveLogin.VaultCredentials.KeyThreshold; i++)
                        {
                            manager.SudoCommand($"vault-direct operator unseal", cluster.SecureRunOptions | RunOptions.FaultOnError, hiveLogin.VaultCredentials.UnsealKeys[i]);
                        }

                        // Wait for Vault to indicate that it's unsealed and is
                        // ready to accept commands.

                        timer.Reset();

                        manager.Status = "vault: wait for unseal";

                        while (true)
                        {
                            var response = manager.SudoCommand("vault-direct status", RunOptions.LogOutput);

                            firstMaster.Log($"*** VAULT STATUS: {response.ExitCode}/{response.AllText}");

                            if (response.ExitCode == 0)
                            {
                                break;
                            }

                            Thread.Sleep(TimeSpan.FromSeconds(5));
                        }

                        manager.Status = string.Empty;
                    }

                    // $todo(jeff.lill):
                    //
                    // Vault doesn't actually seem to be ready right away, even after
                    // verifing that all of the Vault instances are unsealed.  I believe
                    // this is because [neon-proxy-vault] has not yet realized that the
                    // Vault instances have transitioned to being healthy.
                    //
                    // We're going to ping Vault status via the proxy/REST API until
                    // it answers as ready.

                    using (var vaultJsonClient = new JsonClient(httpHandler, disposeHandler: false))
                    {
                        vaultJsonClient.BaseAddress = new Uri($"https://{manager.Name}.{cluster.Definition.Hostnames.Vault}:{NetworkPorts.Vault}/");

                        manager.Status = "vault: stablize";

                        timer.Reset();

                        while (true)
                        {
                            if (timer.Elapsed >= timeout)
                            {
                                firstMaster.Fault($"[Vault] cluster is not responding as ready after waiting [{timeout}].");
                                return;
                            }

                            try
                            {
                                var jsonResponse = vaultJsonClient.GetAsync("/v1/sys/seal-status").Result;

                                firstMaster.Log($"*** VAULT STATUS-CODE: {jsonResponse.HttpResponse.StatusCode}/{jsonResponse.HttpResponse.ReasonPhrase}");

                                if (jsonResponse.IsSuccess)
                                {
                                    break;
                                }
                            }
                            catch (Exception e)
                            {
                                manager.LogException(e);
                                Thread.Sleep(TimeSpan.FromSeconds(1));
                            }
                        }

                        manager.Status = string.Empty;
                    }
                }
            }

            // Be really sure that vault is ready on all managers.

            cluster.Vault.WaitUntilReady();
        }

        /// <summary>
        /// Configures the hive's HashiCorp Vault.
        /// </summary>
        private void ConfigureVault()
        {
            var firstMaster = cluster.FirstMaster;

            try
            {
                // Initialize the Vault cluster using the first manager if it
                // hasn't already been initialized.

                if (hiveLogin.VaultCredentials == null)
                {
                    firstMaster.Status = "vault: init";

                    var response = firstMaster.SudoCommand(
                        "vault-direct operator init",
                        cluster.SecureRunOptions | RunOptions.FaultOnError,
                        $"-key-shares={cluster.Definition.Vault.KeyCount}",
                        $"-key-threshold={cluster.Definition.Vault.KeyThreshold}");

                    if (response.ExitCode != 0)
                    {
                        firstMaster.Fault($"[vault operator init] exit code [{response.ExitCode}]");
                        return;
                    }

                    var rawVaultCredentials = response.OutputText;

                    hiveLogin.VaultCredentials = VaultCredentials.FromInit(rawVaultCredentials, cluster.Definition.Vault.KeyThreshold);

                    // Persist the Vault credentials.

                    hiveLogin.Save();
                }

                // Unseal Vault.

                VaultUnseal();

                // Configure the audit backend so that it sends events to syslog.

                firstMaster.InvokeIdempotentAction("setup/vault-audit",
                    () =>
                    {
                        // $todo(jeff.lill):
                        //
                        // This command fails with later Vault versions for some
                        // reason.  We're not doing anything with the audit stream
                        // at this point anyway so I'm going to comment this out.
                        // This issue is tracked here:
                        //
                        //      https://github.com/jefflill/NeonForge/issues/37

                        //firstMaster.Status = "vault: audit enable";
                        //hive.VaultCommand("vault audit enable syslog tag=\"vault\" facility=\"AUTH\"");
                    });

                // Mount a [generic] backend dedicated to neonHIVE related secrets.

                firstMaster.InvokeIdempotentAction("setup/vault-enable-neon-secret",
                    () =>
                    {
                        firstMaster.Status = "vault: enable neon-secret backend";
                        cluster.Vault.Command("vault secrets enable", "-path=neon-secret", "generic");
                    });

                // Mount the [transit] backend and create the hive key.

                firstMaster.InvokeIdempotentAction("setup/vault-enable-transit",
                    () =>
                    {
                        firstMaster.Status = "vault: transit backend";
                        cluster.Vault.Command("vault secrets enable transit");
                        cluster.Vault.Command($"vault write -f transit/keys/{HiveConst.VaultTransitKey}");
                    });

                // Mount the [approle] backend.

                firstMaster.InvokeIdempotentAction("setup/vault-enable-approle",
                    () =>
                    {
                        firstMaster.Status = "vault: approle backend";
                        cluster.Vault.Command("vault auth enable approle");
                    });

                // Initialize the standard policies.

                firstMaster.InvokeIdempotentAction("setup/vault-policies",
                    () =>
                    {
                        firstMaster.Status = "vault: policies";

                        var writeCapabilities = VaultCapabilies.Create | VaultCapabilies.Read | VaultCapabilies.Update | VaultCapabilies.Delete | VaultCapabilies.List;
                        var readCapabilities = VaultCapabilies.Read | VaultCapabilies.List;

                        cluster.Vault.SetPolicy(new VaultPolicy("neon-reader", "neon-secret/*", readCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-writer", "neon-secret/*", writeCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-cert-reader", "neon-secret/cert/*", readCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-cert-writer", "neon-secret/cert/*", writeCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-hosting-reader", "neon-secret/hosting/*", readCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-hosting-writer", "neon-secret/hosting/*", writeCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-service-reader", "neon-secret/service/*", readCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-service-writer", "neon-secret/service/*", writeCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-global-reader", "neon-secret/global/*", readCapabilities));
                        cluster.Vault.SetPolicy(new VaultPolicy("neon-global-writer", "neon-secret/global/*", writeCapabilities));
                    });

                // Initialize the [neon-proxy-*] related service roles.  Each of these services 
                // need read access to the TLS certificates and [neon-proxy-manager] also needs
                // read access to the hosting options.

                firstMaster.InvokeIdempotentAction("setup/vault-roles",
                    () =>
                    {
                        firstMaster.Status = "vault: roles";

                        cluster.Vault.SetAppRole("neon-proxy-manager", "neon-cert-reader", "neon-hosting-reader");
                        cluster.Vault.SetAppRole("neon-proxy-public", "neon-cert-reader");
                        cluster.Vault.SetAppRole("neon-proxy-private", "neon-cert-reader");
                    });

                // Store the hive hosting options in the Vault so services that need to perform
                // hosting level operations will have the credentials and other information to 
                // modify the environment.  For example in cloud environments, the [neon-proxy-manager]
                // service needs to be able to update the worker traffic manager rules so they match
                // the current PUBLIC routes.

                firstMaster.InvokeIdempotentAction("setup/vault-hostingoptions",
                    () =>
                    {
                        firstMaster.Status = "vault: hosting options";

                        using (var vault = HiveHelper.OpenVault(HiveCredentials.FromVaultToken(hiveLogin.VaultCredentials.RootToken)))
                        {
                            vault.WriteJsonAsync("neon-secret/hosting/options", cluster.Definition.Hosting).Wait();

                            // Store the zipped OpenVPN certificate authority files in the hive Vault.

                            var vpnCaCredentials = hiveLogin.VpnCredentials;

                            if (vpnCaCredentials != null)
                            {
                                var vpnCaFiles = VpnCaFiles.LoadZip(vpnCaCredentials.CaZip, vpnCaCredentials.CaZipKey);

                                vpnCaFiles.Clean();
                                vault.WriteBytesAsync("neon-secret/vpn/ca.zip.encrypted", vpnCaFiles.ToZipBytes(vpnCaCredentials.CaZipKey)).Wait();
                            }
                        }
                    });
            }
            finally
            {
                cluster.FirstMaster.Status = string.Empty;
            }
        }

        /// <summary>
        /// Configures Consul values.
        /// </summary>
        private void ConfigureConsul()
        {
            var firstMaster = cluster.FirstMaster;

            firstMaster.InvokeIdempotentAction("setup/consul-initialize",
                () =>
                {
                    firstMaster.Status = "consul initialize";

                    // Persist the hive definition (without important secrets) to
                    // Consul so it will be available services like [neon-proxy-manager]
                    // immediately (before [neon-hive-manager] spins up).

                    var loginClone = hiveLogin.Clone();

                    loginClone.ClearRootSecrets();
                    loginClone.Definition.Hosting.ClearSecrets();

                    HiveHelper.PutDefinitionAsync(loginClone.Definition, savePets: true).Wait();

                    firstMaster.Status = "saving hive globals";

                    cluster.Globals.Set(HiveGlobals.CreateDateUtc, DateTime.UtcNow.ToString(NeonHelper.DateFormatTZ, CultureInfo.InvariantCulture));
                    cluster.Globals.Set(HiveGlobals.NeonCli, Program.MinimumVersion);
                    cluster.Globals.Set(HiveGlobals.SetupPending, true);
                    cluster.Globals.Set(HiveGlobals.UserAllowUnitTesting, cluster.Definition.AllowUnitTesting);
                    cluster.Globals.Set(HiveGlobals.UserDisableAutoUnseal, false);
                    cluster.Globals.Set(HiveGlobals.UserLogRetentionDays, cluster.Definition.Log.RetentionDays);
                    cluster.Globals.Set(HiveGlobals.Uuid, Guid.NewGuid().ToString("D").ToLowerInvariant());
                    cluster.Globals.Set(HiveGlobals.Version, Program.Version);
                });


            // Write the Consul globals that hold the HiveMQ settings for the
            // [app], [neon], and [sysadmin] accounts. 

            firstMaster.InvokeIdempotentAction("setup/hivemq-settings",
                () =>
                {
                    firstMaster.Status = "consul: hivemq settings";

                    // $note(jeff.lill):
                    //
                    // We're going to assign up to 10 swarm nodes as the hosts for both
                    // the AMQP and Admin traffic, favoring non-manager nodes if possible.
                    // This works because the services are behind a traffic manager rule
                    // so all we're using these hosts for is to have the Docker ingress
                    // network forward traffic to the traffic manager which will forward
                    // it on to a healthy RabbitMQ node.
                    //
                    // This should work pretty well and this will tolerate some changes
                    // to the hive host nodes, as long as at least one of the listed hive
                    // hosts is still active.

                    var hosts = new List<string>();
                    var maxHosts = 10;

                    foreach (var node in cluster.Definition.SortedWorkers)
                    {
                        hosts.Add($"{node.Name}.{cluster.Definition.Hostnames.HiveMQ}");

                        if (hosts.Count >= maxHosts)
                        {
                            break;
                        }
                    }

                    if (hosts.Count < maxHosts)
                    {
                        foreach (var node in cluster.Definition.SortedManagers)
                        {
                            hosts.Add($"{node.Name}.{cluster.Definition.Hostnames.HiveMQ}");

                            if (hosts.Count >= maxHosts)
                            {
                                break;
                            }
                        }
                    }

                    var hiveMQSettings = new HiveMQSettings()
                    {
                        AmqpHosts = hosts,
                        AmqpPort = HiveHostPorts.ProxyPrivateHiveMQAMQP,
                        AdminHosts = hosts,
                        AdminPort = HiveHostPorts.ProxyPrivateHiveMQAdmin,
                        TlsEnabled = false,
                        Username = HiveConst.HiveMQSysadminUser,
                        Password = cluster.Definition.HiveMQ.SysadminPassword,
                        VirtualHost = "/"
                    };

                    firstMaster.InvokeIdempotentAction("setup/neon-hivemq-settings-sysadmin",
                        () => cluster.Globals.Set(HiveGlobals.HiveMQSettingSysadmin, hiveMQSettings));

                    hiveMQSettings.Username = HiveConst.HiveMQNeonUser;
                    hiveMQSettings.Password = cluster.Definition.HiveMQ.NeonPassword;
                    hiveMQSettings.VirtualHost = HiveConst.HiveMQNeonVHost;

                    firstMaster.InvokeIdempotentAction("setup/neon-hivemq-settings-neon",
                        () => cluster.Globals.Set(HiveGlobals.HiveMQSettingsNeon, hiveMQSettings));

                    hiveMQSettings.Username = HiveConst.HiveMQAppUser;
                    hiveMQSettings.Password = cluster.Definition.HiveMQ.AppPassword;
                    hiveMQSettings.VirtualHost = HiveConst.HiveMQAppVHost;

                    firstMaster.InvokeIdempotentAction("setup/neon-hivemq-settings-app",
                        () => cluster.Globals.Set(HiveGlobals.HiveMQSettingsApp, hiveMQSettings));

                    // Persist the HiveMQ bootstrap settings to Consul.  These settings have no credentials
                    // and reference the HiveMQ nodes directly (not via a traffic manager rule).

                    cluster.HiveMQ.SaveBootstrapSettings();
                });
        }

        /// <summary>
        /// Configures any hive Docker secrets.
        /// </summary>
        public void ConfigureSecrets()
        {
            var firstMaster = cluster.FirstMaster;

            // Create the [neon-ssh-credentials] Docker secret.

            firstMaster.InvokeIdempotentAction("setup/neon-ssh-credentials",
                () =>
                {
                    firstMaster.Status = "secret: SSH credentials";
                    cluster.Docker.Secret.Set("neon-ssh-credentials", $"{hiveLogin.SshUsername}/{hiveLogin.SshPassword}");
                });

            // Create the [neon-ceph-dashboard-credentials] Docker secret.

            firstMaster.InvokeIdempotentAction("setup/neon-ceph-dashboard-credentials",
                () =>
                {
                    firstMaster.Status = "secret: Ceph dashboard credentials";
                    hiveLogin.CephDashboardUsername = HiveConst.DefaultUsername;
                    // hiveLogin.CephDashboardPassword = NeonHelper.GetRandomPassword(20);  $todo(jeff.lill): UNCOMMENT THIS????
                    hiveLogin.CephDashboardPassword = HiveConst.DefaultPassword;
                    cluster.Docker.Secret.Set("neon-ceph-dashboard-credentials", $"{hiveLogin.CephDashboardUsername}/{hiveLogin.CephDashboardPassword}");
                });
        }

        /// <summary>
        /// Generates the SSH key to be used for authenticating SSH client connections.
        /// </summary>
        /// <param name="manager">A hive manager node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void GenerateClientSshKey(SshProxy<NodeDefinition> manager, TimeSpan stepDelay)
        {
            // Here's some information explaining what how I'm doing this:
            //
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

            if (!sshTlsAuth)
            {
                return;
            }

            if (hiveLogin.SshClientKey != null)
            {
                return; // Key has already been created.
            }

            Thread.Sleep(stepDelay);

            hiveLogin.SshClientKey = new SshClientKey();

            // $hack(jeff.lill): 
            //
            // We're going to generate a 2048 bit key pair on one of the
            // manager nodes and then download and then delete it.  This
            // means that the private key will be persisted to disk (tmpfs)
            // for a moment but I'm going to worry about that too much
            // since we'll be rebooting the manager later on during setup.
            //
            // Technically, I could have installed OpenSSL or something
            // on Windows or figured out the .NET Crypto libraries but
            // but OpenSSL didn't support generating the PUB format
            // SSH expects for the client public key.

            const string keyGenScript =
@"
# Generate a 2048-bit key without a passphrase (the -N """" option).

ssh-keygen -t rsa -b 2048 -N """" -C ""neonhive"" -f /run/ssh-key

# Relax permissions so we can download the key parts.

chmod 666 /run/ssh-key*
";
            var bundle = new CommandBundle("./keygen.sh");

            bundle.AddFile("keygen.sh", keyGenScript, isExecutable: true);

            manager.SudoCommand(bundle);

            using (var stream = new MemoryStream())
            {
                manager.Download("/run/ssh-key.pub", stream);

                hiveLogin.SshClientKey.PublicPUB = Encoding.UTF8.GetString(stream.ToArray());
            }

            using (var stream = new MemoryStream())
            {
                manager.Download("/run/ssh-key", stream);

                hiveLogin.SshClientKey.PrivatePEM = Encoding.UTF8.GetString(stream.ToArray());
            }

            manager.SudoCommand("rm /run/ssh-key*");

            // We're going to use WinSCP to convert the OpenSSH PEM formatted key
            // to the PPK format PuTTY/WinSCP require.  Note that this won't work
            // when the tool is running in a Docker Linux container.  We're going
            // to handle the conversion in the outer shim as a post run action.

            if (NeonHelper.IsWindows)
            {
                var pemKeyPath = Path.Combine(Program.HiveTempFolder, Guid.NewGuid().ToString("D"));
                var ppkKeyPath = Path.Combine(Program.HiveTempFolder, Guid.NewGuid().ToString("D"));

                try
                {
                    File.WriteAllText(pemKeyPath, hiveLogin.SshClientKey.PrivatePEM);

                    ExecuteResult result;

                    try
                    {
                        result = NeonHelper.ExecuteCapture("winscp.com", $@"/keygen ""{pemKeyPath}"" /comment=""{cluster.Definition.Name} Key"" /output=""{ppkKeyPath}""");
                    }
                    catch (Win32Exception)
                    {
                        return; // Tolerate when WinSCP isn't installed.
                    }

                    if (result.ExitCode != 0)
                    {
                        Console.WriteLine(result.OutputText);
                        Console.Error.WriteLine(result.ErrorText);
                        Program.Exit(result.ExitCode);
                    }

                    hiveLogin.SshClientKey.PrivatePPK = File.ReadAllText(ppkKeyPath);

                    // Persist the SSH client key.

                    hiveLogin.Save();
                }
                finally
                {
                    if (File.Exists(pemKeyPath))
                    {
                        File.Delete(pemKeyPath);
                    }

                    if (File.Exists(ppkKeyPath))
                    {
                        File.Delete(ppkKeyPath);
                    }
                }
            }
        }

        /// <summary>
        /// Changes the admin account's password on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void SetStrongPassword(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/strong-password",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "strong password";

                    var script =
$@"
echo '{hiveLogin.SshUsername}:{hiveLogin.SshPassword}' | chpasswd
";
                    var bundle = new CommandBundle("./set-strong-password.sh");

                    bundle.AddFile("set-strong-password.sh", script, isExecutable: true);

                    var response = node.SudoCommand(bundle);

                    if (response.ExitCode != 0)
                    {
                        Console.Error.WriteLine($"*** ERROR: Unable to set a strong password [exitcode={response.ExitCode}].");
                        Program.Exit(response.ExitCode);
                    }

                    node.UpdateCredentials(SshCredentials.FromUserPassword(hiveLogin.Username, hiveLogin.SshPassword));
                });
        }

        /// <summary>
        /// Generates the private key that will be used to secure SSH on the hive servers.
        /// </summary>
        private void ConfigureSshCerts()
        {
            cluster.FirstMaster.InvokeIdempotentAction("setup/ssh-server-key",
                () =>
                {
                    cluster.FirstMaster.Status = "generate server SSH key";

                    var configScript =
@"
# Generate the SSH server key and fingerprint.

mkdir -p /dev/shm/ssh

# For idempotentcy, ensure that the key file doesn't already exist to
# avoid having the [ssh-keygen] command prompt and wait for permission
# to overwrite it.

if [ -f /dev/shm/ssh/ssh_host_rsa_key ] ; then
    rm /dev/shm/ssh/ssh_host_rsa_key
fi

ssh-keygen -f /dev/shm/ssh/ssh_host_rsa_key -N '' -t rsa

# Extract the host's SSL RSA key fingerprint to a temporary file
# so [neon-cli] can download it.

ssh-keygen -l -E md5 -f /dev/shm/ssh/ssh_host_rsa_key > /dev/shm/ssh/ssh.fingerprint

# The files need to have user permissions so we can download them.

chmod 777 /dev/shm/ssh/
chmod 666 /dev/shm/ssh/ssh_host_rsa_key
chmod 666 /dev/shm/ssh/ssh_host_rsa_key.pub
chmod 666 /dev/shm/ssh/ssh.fingerprint
";
                    var bundle = new CommandBundle("./config.sh");

                    bundle.AddFile("config.sh", configScript, isExecutable: true);
                    cluster.FirstMaster.SudoCommand(bundle);

                    cluster.FirstMaster.Status = "download server SSH key";

                    hiveLogin.SshHiveHostPrivateKey = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh_host_rsa_key");
                    hiveLogin.SshHiveHostPublicKey = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh_host_rsa_key.pub");
                    hiveLogin.SshHiveHostKeyFingerprint = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh.fingerprint");

                    // Delete the SSH key files for security.

                    cluster.FirstMaster.SudoCommand("rm -r /dev/shm/ssh");

                    // Persist the server SSH key and fingerprint.

                    hiveLogin.Save();
                });
        }

        /// <summary>
        /// Configures SSH on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void ConfigureSsh(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            // Configure the SSH credentials on all hive nodes.

            node.InvokeIdempotentAction("setup/ssh",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    CommandBundle bundle;

                    // Here's some information explaining what how I'm doing this:
                    //
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

                    if (sshTlsAuth)
                    {
                        node.Status = "client SSH key";

                        // Enable the public key by appending it to [$HOME/.ssh/authorized_keys],
                        // creating the file if necessary.  Note that we're allowing only a single
                        // authorized key.

                        var addKeyScript =
$@"
chmod go-w ~/
mkdir -p $HOME/.ssh
chmod 700 $HOME/.ssh
touch $HOME/.ssh/authorized_keys
cat ssh-key.pub > $HOME/.ssh/authorized_keys
chmod 600 $HOME/.ssh/authorized_keys
";
                        bundle = new CommandBundle("./addkeys.sh");

                        bundle.AddFile("addkeys.sh", addKeyScript, isExecutable: true);
                        bundle.AddFile("ssh-key.pub", hiveLogin.SshClientKey.PublicPUB);

                        // NOTE: I'm explictly not running the bundle as [sudo] because the OpenSSH
                        //       server is very picky about the permissions on the user's [$HOME]
                        //       and [$HOME/.ssl] folder and contents.  This took me a couple 
                        //       hours to figure out.

                        node.RunCommand(bundle);
                    }

                    // These steps are required for both password and public key authentication.

                    // Upload the server key and edit the SSHD config to disable all host keys 
                    // except for RSA.

                    var configScript =
@"
# Copy the server key.

cp ssh_host_rsa_key /etc/ssh/ssh_host_rsa_key

# Disable all host keys except for RSA.

sed -i 's!^\HostKey /etc/ssh/ssh_host_dsa_key$!#HostKey /etc/ssh/ssh_host_dsa_key!g' /etc/ssh/sshd_config
sed -i 's!^\HostKey /etc/ssh/ssh_host_ecdsa_key$!#HostKey /etc/ssh/ssh_host_ecdsa_key!g' /etc/ssh/sshd_config
sed -i 's!^\HostKey /etc/ssh/ssh_host_ed25519_key$!#HostKey /etc/ssh/ssh_host_ed25519_key!g' /etc/ssh/sshd_config

# Restart SSHD to pick up the changes.

systemctl restart sshd
";
                    bundle = new CommandBundle("./config.sh");

                    bundle.AddFile("config.sh", configScript, isExecutable: true);
                    bundle.AddFile("ssh_host_rsa_key", hiveLogin.SshHiveHostPrivateKey);
                    node.SudoCommand(bundle);
                });
        }

        /// <summary>
        /// Configures the built-in hive dashboards.
        /// </summary>
        private void ConfigureDashboards()
        {
            var firstMaster = cluster.FirstMaster;

            firstMaster.InvokeIdempotentAction("setup/dashboards",
                () =>
                {
                    // Configure the [Ceph] dashboard.
                    //
                    // Note that the old [luminous] dashboard is HTTP and is hardcoded to listen
                    // on port 7000 whereas the [mimic] and later releases are HTTPS and listen
                    // on [HiveHostPorts.ProxyPrivateHttpCephDashboard].
                    //
                    // Note that only the Ceph dashboard running on the lead MGR can actually
                    // render properly so non-leader MGR dashboards will redirect incoming 
                    // requests to the leader.  We're going to mitigate this by setting up
                    // a traffic manager rule that only considers 2XX responses as valid so
                    // that we won't direct traffic to the non-lead MGRs that return 302
                    // redirects.
                    //
                    //      https://github.com/jefflill/NeonForge/issues/222

                    if (cluster.Definition.HiveFS.Enabled && cluster.Definition.Dashboard.HiveFS)
                    {
                        firstMaster.Status = "hivefs dashboard";

                        if (cluster.Definition.HiveFS.Release == "luminous")
                        {
                            var hiveFSDashboard = new HiveDashboard()
                            {
                                Name = "hivefs",
                                Title = "Hive File System",
                                Folder = HiveConst.DashboardSystemFolder,
                                Url = $"http://reachable-manager:{HiveHostPorts.ProxyPrivateHttpCephDashboard}",
                                Description = "Ceph distributed file system"
                            };

                            cluster.Dashboard.Set(hiveFSDashboard);

                            var rule = new TrafficHttpRule()
                            {
                                Name = "neon-hivefs-dashboard",
                                System = true,
                                Resolver = null
                            };

                            // The [luminous] dashboard is not secured by TLS, so we can perform
                            // normal HTTP health checks.  Note that only the active lead Ceph MGR
                            // node's dashboard actually works.  The non-leader nodes will return
                            // a 307 (temporary redirect) status code.
                            //
                            // We're going to consider only servers that return 2xx status codes
                            // as healthy so we'll always direct traffic to the lead MGR.

                            rule.CheckMode = TrafficCheckMode.Http;
                            rule.CheckTls = false;
                            rule.CheckExpect = @"rstatus ^2\d\d";

                            // Initialize the frontends and backends.

                            rule.Frontends.Add(
                                new TrafficHttpFrontend()
                                {
                                    ProxyPort = HiveHostPorts.ProxyPrivateHttpCephDashboard
                                });

                            foreach (var monNode in cluster.Nodes.Where(n => n.Metadata.Labels.CephMON))
                            {
                                rule.Backends.Add(
                                    new TrafficHttpBackend()
                                    {
                                        Server = monNode.Metadata.PrivateAddress.ToString(),
                                        Port = 7000,  // The [luminous] dashboard is hardcoded to port 7000
                                    });
                            }

                            cluster.PrivateTraffic.SetRule(rule);
                        }
                        else
                        {
                            // [mimic] and later releases is a bit more complicated because
                            // the dashboard is required to serve HTTPS.  So the dashboard 
                            // manages the certificate/key and which means that we need to
                            // deploy a pass-thru TCP rule.
                            //
                            // Note that only the active lead Ceph MGR node's dashboard actually
                            // works.  The non-leader nodes will return a 307 (temporary redirect) 
                            // status code.  The trick here is that we need to enable an HTTPS 
                            // based health check.
                            //
                            // We're going to consider only servers that return 2xx status codes
                            // as healthy so we'll always direct traffic to the lead MGR.

                            var hiveFSDashboard = new HiveDashboard()
                            {
                                Name = "hivefs",
                                Title = "Hive File System",
                                Folder = HiveConst.DashboardSystemFolder,
                                Url = $"https://reachable-manager:{HiveHostPorts.ProxyPrivateHttpCephDashboard}",
                                Description = "Ceph distributed file system"
                            };

                            cluster.Dashboard.Set(hiveFSDashboard);

                            var rule = new TrafficTcpRule()
                            {
                                Name = "neon-hivefs-dashboard",
                                System = true,
                                Resolver = null
                            };

                            // The [luminous] dashboard is not secured by TLS, so we can perform
                            // normal HTTP health checks.  Note that only the active lead Ceph MGR
                            // node's dashboard actually works.  The non-leader nodes will return
                            // a 307 (temporary redirect) status code.
                            //
                            // We're going to consider only servers that return 2xx status codes
                            // as healthy so we'll always direct traffic to the lead MGR.

                            rule.CheckMode = TrafficCheckMode.Http;
                            rule.CheckTls = true;
                            rule.CheckExpect = @"rstatus ^2\d\d";

                            // Initialize the frontends and backends.

                            rule.Frontends.Add(
                                new TrafficTcpFrontend()
                                {
                                    ProxyPort = HiveHostPorts.ProxyPrivateHttpCephDashboard
                                });

                            rule.Backends.Add(
                                new TrafficTcpBackend()
                                {
                                    Group = HiveHostGroups.CephMON,
                                    GroupLimit = 5,
                                    Port = HiveHostPorts.CephDashboard
                                });

                            cluster.PrivateTraffic.SetRule(rule);
                        }

                        firstMaster.Status = string.Empty;
                    }

                    // Configure the Consul dashboard.

                    if (cluster.Definition.Dashboard.Consul)
                    {
                        firstMaster.Status = "consul dashboard";

                        var consulDashboard = new HiveDashboard()
                        {
                            Name = "consul",
                            Title = "Consul",
                            Folder = HiveConst.DashboardSystemFolder,
                            Url = $"{cluster.Definition.Consul.Scheme}://reachable-manager:{NetworkPorts.Consul}/ui",
                            Description = "Hive Consul key/value store"
                        };

                        cluster.Dashboard.Set(consulDashboard);
                        firstMaster.Status = string.Empty;
                    }

                    // Configure the Kibana dashboard.

                    if (cluster.Definition.Log.Enabled && cluster.Definition.Dashboard.Kibana)
                    {
                        firstMaster.Status = "kibana dashboard";

                        var kibanaDashboard = new HiveDashboard()
                        {
                            Name = "kibana",
                            Title = "Kibana",
                            Folder = HiveConst.DashboardSystemFolder,
                            Url = $"http://reachable-manager:{HiveHostPorts.ProxyPrivateKibanaDashboard}",
                            Description = "Kibana hive monitoring dashboard"
                        };

                        cluster.Dashboard.Set(kibanaDashboard);
                        firstMaster.Status = string.Empty;
                    }

                    // Configure the Vault dashboard.

                    if (cluster.Definition.Dashboard.Vault)
                    {
                        firstMaster.Status = "vault dashboard";

                        var vaultDashboard = new HiveDashboard()
                        {
                            Name = "vault",
                            Title = "Vault",
                            Folder = HiveConst.DashboardSystemFolder,
                            Url = $"https://reachable-manager:{cluster.Definition.Vault.Port}/ui",
                            Description = "Hive Vault secure storage"
                        };

                        cluster.Dashboard.Set(vaultDashboard);
                        firstMaster.Status = string.Empty;
                    }

                    // Configure the HiveMQ dashboard.

                    if (cluster.Definition.Dashboard.HiveMQ)
                    {
                        firstMaster.Status = "hivemq dashboard";

                        var rabbitDashboard = new HiveDashboard()
                        {
                            Name = "hivemq",
                            Title = "Hive Messaging System",
                            Folder = HiveConst.DashboardSystemFolder,
                            Url = $"http://reachable-manager:{HiveHostPorts.ProxyPrivateHiveMQAdmin}",
                            //Url         = $"https://reachable-manager:{HiveHostPorts.ProxyPrivateHiveMQManagement}",
                            Description = "RabbitMQ based messaging system"
                        };

                        cluster.Dashboard.Set(rabbitDashboard);
                    }

                    firstMaster.Status = string.Empty;
                });
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None);
        }
    }
}