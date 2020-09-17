//-----------------------------------------------------------------------------
// FILE:	    ClusterSetupCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.SharpZipLib.Zip;
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

using k8s;
using k8s.Models;
using ICSharpCode.SharpZipLib.Tar;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster setup</b> command.
    /// </summary>
    public class ClusterSetupCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Holds information about a remote file we'll need to download.
        /// </summary>
        private class RemoteFile
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="path">The file path.</param>
            /// <param name="permissions">Optional file permissions.</param>
            /// <param name="owner">Optional file owner.</param>
            public RemoteFile(string path, string permissions = "600", string owner = "root:root")
            {
                this.Path = path;
                this.Permissions = permissions;
                this.Owner = owner;
            }

            /// <summary>
            /// Returns the file path.
            /// </summary>
            public string Path { get; private set; }

            /// <summary>
            /// Returns the file permissions.
            /// </summary>
            public string Permissions { get; private set; }

            /// <summary>
            /// Returns the file owner formatted as: USER:GROUP.
            /// </summary>
            public string Owner { get; private set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private const string usage = @"
Configures a neonKUBE as described in the cluster definition file.

USAGE: 

    neon cluster setup [OPTIONS] root@CLUSTER-NAME  

OPTIONS:

    --unredacted        - Runs Vault and other commands with potential
                          secrets without redacting logs.  This is useful 
                          for debugging cluster setup  issues.  Do not
                          use for production hives.

    --force             - Don't prompt before removing existing contexts
                          that reference the target cluster.
";
        private const string        logBeginMarker    = "# CLUSTER-BEGIN-SETUP ############################################################";
        private const string        logEndMarker      = "# CLUSTER-END-SETUP-SUCCESS ######################################################";
        private const string        logFailedMarker   = "# CLUSTER-END-SETUP-FAILED #######################################################";
        private const string        joinCommandMarker = "kubeadm join";
        private const int           maxJoinAttempts   = 5;
        private readonly TimeSpan   joinRetryDelay    = TimeSpan.FromSeconds(5);

        private KubeConfigContext       kubeContext;
        private KubeContextExtension    kubeContextExtension;
        private ClusterProxy            cluster;
        private HostingManager          hostingManager;
        private KubeSetupInfo           kubeSetupInfo;
        private HttpClient              httpClient;
        private Kubernetes              k8sClient;
        private string                  branch;

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "cluster", "setup" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--unredacted", "--force" }; }
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

            branch = commandLine.GetOption("--branch") ?? "master";

            var contextName = KubeContextName.Parse(commandLine.Arguments[0]);
            var kubeCluster = KubeHelper.Config.GetCluster(contextName.Cluster);

            kubeContextExtension = KubeHelper.GetContextExtension(contextName);

            if (kubeContextExtension == null)
            {
                Console.Error.WriteLine($"*** ERROR: Be sure to prepare the cluster first via [neon cluster prepare...].");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(kubeContextExtension.SshPassword))
            {
                Console.Error.WriteLine($"*** ERROR: No cluster node SSH password found.");
                Program.Exit(1);
            }

            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            using (httpClient = new HttpClient(handler, disposeHandler: true))
            {
                if (kubeCluster != null && !kubeContextExtension.SetupDetails.SetupPending)
                {
                    if (commandLine.GetOption("--force") == null && !Program.PromptYesNo($"One or more logins reference [{kubeCluster.Name}].  Do you wish to delete these?"))
                    {
                        Program.Exit(0);
                    }

                    // Remove the cluster from the kubeconfig and remove any 
                    // contexts that reference it.

                    KubeHelper.Config.Clusters.Remove(kubeCluster);

                    var delList = new List<KubeConfigContext>();

                    foreach (var context in KubeHelper.Config.Contexts)
                    {
                        if (context.Properties.Cluster == kubeCluster.Name)
                        {
                            delList.Add(context);
                        }
                    }

                    foreach (var context in delList)
                    {
                        KubeHelper.Config.Contexts.Remove(context);
                    }

                    if (KubeHelper.CurrentContext != null && KubeHelper.CurrentContext.Properties.Cluster == kubeCluster.Name)
                    {
                        KubeHelper.Config.CurrentContext = null;
                    }

                    KubeHelper.Config.Save();
                }

                kubeContext = new KubeConfigContext(contextName);

                if (kubeContextExtension.SetupDetails?.SetupInfo != null)
                {
                    kubeSetupInfo = kubeContextExtension.SetupDetails.SetupInfo;
                }
                else
                {
                    using (var client = new HeadendClient())
                    {
                        kubeSetupInfo = client.GetSetupInfoAsync(kubeContextExtension.ClusterDefinition).Result;
                    }
                }

                KubeHelper.InitContext(kubeContext);

                // Initialize the cluster proxy and the hbosting manager.

                cluster        = new ClusterProxy(kubeContext, Program.CreateNodeProxy<NodeDefinition>, appendToLog: true, defaultRunOptions: RunOptions.LogOutput | RunOptions.FaultOnError);
                hostingManager = new HostingManagerFactory(() => HostingLoader.Initialize()).GetManager(cluster, kubeSetupInfo, Program.LogPath);

                if (hostingManager == null)
                {
                    Console.Error.WriteLine($"*** ERROR: No hosting manager for the [{cluster.Definition.Hosting.Environment}] environment could be located.");
                    Program.Exit(1);
                }

                // Update the cluster node SSH credentials to use the secure password.

                var sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUsername, kubeContextExtension.SshPassword);

                foreach (var node in cluster.Nodes)
                {
                    node.UpdateCredentials(sshCredentials);
                }

                // Get on with cluster setup.

                var failed = false;

                try
                {
                    KubeHelper.Desktop.StartOperationAsync($"Setting up [{cluster.Name}]").Wait();

                    // Configure global options.

                    if (commandLine.HasOption("--unredacted"))
                    {
                        cluster.SecureRunOptions = RunOptions.None;
                    }

                    // Connect to existing cluster if it exists.

                    ConnectCluster();

                    // Perform the setup operations.

                    var controller =
                        new SetupController<NodeDefinition>(new string[] { "cluster", "setup", $"[{cluster.Name}]" }, cluster.Nodes)
                        {
                            ShowStatus = !Program.Quiet,
                            MaxParallel = Program.MaxParallel
                        };

                    controller.AddGlobalStep("download binaries", () => WorkstationBinaries());
                    controller.AddWaitUntilOnlineStep("connect");
                    controller.AddNodeStep("ssh certificate", GenerateClientSshCert, node => node == cluster.FirstMaster);
                    controller.AddNodeStep("verify OS", CommonSteps.VerifyOS);

                    // Write the operation begin marker to all cluster node logs.

                    cluster.LogLine(logBeginMarker);

                    // Perform common configuration for the bootstrap node first.
                    // We need to do this so the the package cache will be running
                    // when the remaining nodes are configured.

                    var configureFirstMasterStepLabel = cluster.Definition.Masters.Count() > 1 ? "setup first master" : "setup master";

                    controller.AddNodeStep(configureFirstMasterStepLabel,
                        (node, stepDelay) =>
                        {
                            SetupCommon(hostingManager, node, stepDelay);
                            node.InvokeIdempotentAction("setup/common-restart", () => RebootAndWait(node));
                            SetupNode(node);
                        },
                        node => node == cluster.FirstMaster,
                        stepStaggerSeconds: cluster.Definition.Setup.StepStaggerSeconds);

                    // Perform common configuration for the remaining nodes (if any).

                    if (cluster.Definition.Nodes.Count() > 1)
                    {
                        controller.AddNodeStep("setup other nodes",
                            (node, stepDelay) =>
                            {
                                SetupCommon(hostingManager, node, stepDelay);
                                node.InvokeIdempotentAction("setup/common-restart", () => RebootAndWait(node));
                                SetupNode(node);
                            },
                            node => node != cluster.FirstMaster,
                            stepStaggerSeconds: cluster.Definition.Setup.StepStaggerSeconds);
                    }

                    //-----------------------------------------------------------------
                    // Kubernetes configuration.

                    controller.AddGlobalStep("etc high-availability", SetupEtcHaProxy);
                    controller.AddNodeStep("setup kubernetes", SetupKubernetes);
                    controller.AddGlobalStep("setup cluster", SetupCluster);

                    controller.AddGlobalStep("taint nodes", TaintNodes);
                    if (cluster.Definition.Monitor.Enabled)
                    {
                        controller.AddGlobalStep("setup monitoring", SetupMonitoring);
                    }

                    //-----------------------------------------------------------------
                    // Verify the cluster.

                    controller.AddNodeStep("check masters",
                        (node, stepDelay) =>
                        {
                            ClusterDiagnostics.CheckMaster(node, cluster.Definition);
                        },
                        node => node.Metadata.IsMaster);

                    controller.AddNodeStep("check workers",
                        (node, stepDelay) =>
                        {
                            ClusterDiagnostics.CheckWorker(node, cluster.Definition);
                        },
                        node => node.Metadata.IsWorker);

                    // Generate and set the private SSH key for all cluster nodes.

                    controller.AddGlobalStep("set ssh certs", () => ConfigureSshCerts());

                    // This needs to be run last because it will likely disable
                    // SSH username/password authentication which may block
                    // connection attempts.
                    //
                    // It's also handy to do this last so it'll be possible to 
                    // manually login with the original credentials to diagnose
                    // setup issues.

                    controller.AddNodeStep("ssh secured", ConfigureSsh);

                    // Start setup.

                    if (!controller.Run())
                    {
                        // Write the operation end/failed to all cluster node logs.

                        cluster.LogLine(logFailedMarker);

                        Console.Error.WriteLine("*** ERROR: One or more configuration steps failed.");
                        Program.Exit(1);
                    }

                    // Indicate that setup is complete.

                    kubeContextExtension.SetupDetails.SetupPending = false;
                    kubeContextExtension.Save();

                    // Write the operation end marker to all cluster node logs.

                    cluster.LogLine(logEndMarker);

                }
                catch
                {
                    failed = true;
                    throw;
                }
                finally
                {
                    if (!failed)
                    {
                        KubeHelper.Desktop.EndOperationAsync($"Cluster [{cluster.Name}] is ready for use.").Wait();
                    }
                    else
                    {
                        KubeHelper.Desktop.EndOperationAsync($"Cluster [{cluster.Name}] setup failed.", failed: true).Wait();
                    }
                }

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Connects to a Kubernetes cluster if it already exists.
        /// </summary>
        public void ConnectCluster()
        {
            var configFile = Environment.GetEnvironmentVariable("KUBECONFIG").Split(';').Where(s => s.Contains("config")).FirstOrDefault();
            if (!string.IsNullOrEmpty(configFile) && File.Exists(configFile))
            {
                try
                {
                    k8sClient = new Kubernetes(KubernetesClientConfiguration.BuildConfigFromConfigFile(configFile, currentContext: cluster.KubeContext.Name));
                } 
                catch
                {
                    // k8s is not initialized, this is okay.
                }
            }
        }

        /// <summary>
        /// Downloads and installs any required binaries to the workstation cache if they're not already present.
        /// </summary>
        private async void WorkstationBinaries()
        {
            var firstMaster       = cluster.FirstMaster;
            var hostPlatform      = KubeHelper.HostPlatform;
            var cachedKubeCtlPath = KubeHelper.GetCachedComponentPath(hostPlatform, "kubectl", KubeVersions.KubernetesVersion);
            var cachedHelmPath    = KubeHelper.GetCachedComponentPath(hostPlatform, "helm", KubeVersions.HelmVersion);

            string kubeCtlUri;
            string helmUri;

            switch (hostPlatform)
            {
                case KubeHostPlatform.Linux:

                    kubeCtlUri = kubeSetupInfo.KubeCtlLinuxUri;
                    helmUri    = kubeSetupInfo.HelmLinuxUri;
                    break;

                case KubeHostPlatform.Osx:

                    kubeCtlUri = kubeSetupInfo.KubeCtlOsxUri;
                    helmUri    = kubeSetupInfo.HelmOsxUri;
                    break;

                case KubeHostPlatform.Windows:

                    kubeCtlUri = kubeSetupInfo.KubeCtlWindowsUri;
                    helmUri    = kubeSetupInfo.HelmWindowsUri;
                    break;

                default:

                    throw new NotSupportedException($"Unsupported workstation platform [{hostPlatform}]");
            }

            // Download the components if they're not already cached.

            if (!File.Exists(cachedKubeCtlPath))
            {
                firstMaster.Status = "download: kubectl";

                using (var response = await httpClient.GetStreamAsync(kubeCtlUri))
                {
                    using (var output = new FileStream(cachedKubeCtlPath, FileMode.Create, FileAccess.ReadWrite))
                    {
                        await response.CopyToAsync(output);
                    }
                }
            }

            if (!File.Exists(cachedHelmPath))
            {
                firstMaster.Status = "download: Helm";

                using (var response = await httpClient.GetStreamAsync(helmUri))
                {
                    // This is a [zip] file for Windows and a [tar.gz] file for Linux and OS/X.
                    // We're going to download to a temporary file so we can extract just the
                    // Helm binary.

                    var cachedTempHelmPath = cachedHelmPath + ".tmp";

                    try
                    {
                        using (var output = new FileStream(cachedTempHelmPath, FileMode.Create, FileAccess.ReadWrite))
                        {
                            await response.CopyToAsync(output);
                        }

                        switch (hostPlatform)
                        {
                            case KubeHostPlatform.Linux:
                            case KubeHostPlatform.Osx:

                                throw new NotImplementedException($"Unsupported workstation platform [{hostPlatform}]");

                            case KubeHostPlatform.Windows:

                                // The downloaded file is a ZIP archive for Windows.  We're going
                                // to extract the [windows-amd64/helm.exe] file.

                                using (var input = new FileStream(cachedTempHelmPath, FileMode.Open, FileAccess.ReadWrite))
                                {
                                    using (var zip = new ZipFile(input))
                                    {
                                        foreach (ZipEntry zipEntry in zip)
                                        {
                                            if (!zipEntry.IsFile)
                                            {
                                                continue;
                                            }

                                            if (zipEntry.Name == "windows-amd64/helm.exe")
                                            {
                                                using (var zipStream = zip.GetInputStream(zipEntry))
                                                {
                                                    using (var output = new FileStream(cachedHelmPath, FileMode.Create, FileAccess.ReadWrite))
                                                    {
                                                        zipStream.CopyTo(output);
                                                    }
                                                }
                                                break;
                                            }
                                        }
                                    }
                                }
                                break;

                            default:

                                throw new NotSupportedException($"Unsupported workstation platform [{hostPlatform}]");
                        }
                    }
                    finally
                    {
                        if (File.Exists(cachedTempHelmPath))
                        {
                            File.Delete(cachedTempHelmPath);
                        }
                    }
                }
            }

            // We're going to assume that the workstation tools are backwards 
            // compatible with older versions of Kubernetes and other infrastructure
            // components and simply compare the installed tool (if present) version
            // with the requested tool version and overwrite the installed tool if
            // the new one is more current.

            KubeHelper.InstallKubeCtl(kubeSetupInfo);
            KubeHelper.InstallHelm(kubeSetupInfo);

            firstMaster.Status = string.Empty;
        }

        /// <summary>
        /// Basic configuration that will happen every time if DEBUG setup
        /// mode is ENABLED or else will be invoked idempotently (if that's 
        /// a word).
        /// </summary>
        /// <param name="node">The target node.</param>
        private void ConfigureBasic(SshProxy<NodeDefinition> node)
        {
            // Configure the node's environment variables.

            CommonSteps.ConfigureEnvironmentVariables(node, cluster.Definition);

            // Upload the setup and configuration files.

            node.UploadConfigFiles(cluster.Definition, kubeSetupInfo);
            node.UploadResources(cluster.Definition, kubeSetupInfo);
        }

        /// <summary>
        /// Performs common node configuration.
        /// </summary>
        /// <param name="hostingManager">The hosting manager.</param>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void SetupCommon(HostingManager hostingManager, SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            Covenant.Requires<ArgumentNullException>(hostingManager != null, nameof(hostingManager));
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            //-----------------------------------------------------------------
            // NOTE: 
            //
            // We're going to perform the following steps outside of the
            // idempotent check to make it easier to debug and modify 
            // scripts and tools when cluster setup has been partially
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

                    CommonSteps.PrepareNode(node, cluster.Definition, kubeSetupInfo, hostingManager);

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

                    node.Status = "configure: package proxy";
                    node.SudoCommand("setup-package-proxy.sh");

                    // Perform basic node setup including changing the hostname.

                    UploadHostname(node);

                    node.Status = "configure: node basics";
                    node.SudoCommand("setup-node.sh");

                    // Create the container user and group.

                    // $todo(jefflill):
                    //
                    // This is a bit of a hack to enable local Persistent Volumes for
                    // pet-type pods.  We're going to precreate 100 folders and give
                    // the [container] user full ownership of them.  We need to do this
                    // because Kubernetes is unable to create these dynamically yet.

                    node.Status = "create: local persistent volume folders";

                    var sbVolumesScript = new StringBuilder();

                    for (int i = 0; i < 100; i++)
                    {
                        sbVolumesScript.AppendLineLinux($"mkdir -p {KubeConst.LocalVolumePath}/{i}");
                        sbVolumesScript.AppendLineLinux($"chown {KubeConst.ContainerUsername}:{KubeConst.ContainerGroup} {KubeConst.LocalVolumePath}/{i}");
                        sbVolumesScript.AppendLineLinux($"chmod 770 {KubeConst.LocalVolumePath}/{i}");
                    }

                    node.SudoCommand(CommandBundle.FromScript(sbVolumesScript));
                });
        }

        /// <summary>
        /// Performs basic node configuration.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void SetupNode(SshProxy<NodeDefinition> node)
        {
            node.InvokeIdempotentAction($"setup/{node.Metadata.Role}",
                () =>
                {
                    // Configure the APT package proxy on the masters
                    // and configure the proxy selector for all nodes.

                    node.Status = "configure: package proxy";
                    node.SudoCommand("setup-package-proxy.sh");

                    // Upgrade Linux packages if requested.  We're doing this after
                    // deploying the APT package proxy so it'll be faster.

                    switch (cluster.Definition.NodeOptions.Upgrade)
                    {
                        case OsUpgrade.Partial:

                            node.Status = "upgrade: partial";

                            node.SudoCommand("safe-apt-get upgrade -yq");
                            break;

                        case OsUpgrade.Full:

                            node.Status = "upgrade: full";

                            node.SudoCommand("safe-apt-get dist-upgrade -yq");
                            break;
                    }

                    // Check to see whether the upgrade requires a reboot and
                    // do that now if necessary.

                    if (node.FileExists("/var/run/reboot-required"))
                    {
                        node.Status = "restarting...";
                        node.Reboot();
                    }

                    // Setup NTP.

                    node.Status = "configure: NTP";
                    node.SudoCommand("setup-ntp.sh");

                    node.Status = "install: docker";

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
        /// <param name="node">The cluster node.</param>
        private void RebootAndWait(SshProxy<NodeDefinition> node)
        {
            node.Status = "restarting...";
            node.Reboot(wait: true);
        }

        /// <summary>
        /// Updates the node hostname and related configuration.
        /// </summary>
        /// <param name="node">The target node.</param>
        private void UploadHostname(SshProxy<NodeDefinition> node)
        {
            // Update the hostname.

            node.SudoCommand($"hostnamectl set-hostname {node.Name}");

            // We need to edit [/etc/cloud/cloud.cfg] to preserve the hostname change.

            var cloudCfg = node.DownloadText("/etc/cloud/cloud.cfg");

            cloudCfg = cloudCfg.Replace("preserve_hostname: false", "preserve_hostname: true");

            node.UploadText("/etc/cloud/cloud.cfg", cloudCfg);

            // Update the [/etc/hosts] file to resolve the new hostname.

            var sbHosts = new StringBuilder();

            var nodeAddress = node.Address.ToString();
            var separator   = new string(' ', Math.Max(16 - nodeAddress.Length, 1));

            sbHosts.Append(
$@"
127.0.0.1	    localhost
127.0.0.1       kubernetes-masters
{nodeAddress}{separator}{node.Name}
::1             localhost ip6-localhost ip6-loopback
ff02::1         ip6-allnodes
ff02::2         ip6-allrouters
");
            node.UploadText("/etc/hosts", sbHosts, 4, Encoding.UTF8);
        }

        /// <summary>
        /// Configures a local HAProxy container that makes the Kubernetes Etc
        /// cluster highly available.
        /// </summary>
        private void SetupEtcHaProxy()
        {
            var sbHaProxy = new StringBuilder();

            sbHaProxy.Append(
$@"global
    daemon
    log stdout  format raw  local0  info
    maxconn 32000

defaults
    balance                 roundrobin
    retries                 2
    http-reuse              safe
    timeout connect         5000
    timeout client          50000
    timeout server          50000
    timeout check           5000
    timeout http-keep-alive 500

frontend kubernetes_masters
    bind                    *:6442
    mode                    tcp
    log                     global
    option                  tcplog
    default_backend         kubernetes_masters_backend

backend kubernetes_masters_backend
    mode                    tcp
    balance                 roundrobin");

            foreach (var master in cluster.Masters)
            {
                sbHaProxy.Append(
$@"
    server {master.Name}         {master.Address}:6443");
            }

            foreach (var node in cluster.Nodes)
            {
                node.InvokeIdempotentAction("setup/setup-etc-ha",
                    () =>
                    {
                        node.Status = "setup: etc high-availability";

                        node.UploadText("/etc/neonkube/neon-etc-proxy.cfg", sbHaProxy);

                        node.SudoCommand("docker run",
                            "--name=neon-etc-proxy",
                            "--detach",
                            "--restart=always",
                            "-v=/etc/neonkube/neon-etc-proxy.cfg:/etc/haproxy/haproxy.cfg",
                            "--network=host",
                            "--log-driver=json-file",
                            $"{NeonHelper.NeonBranchRegistry}/haproxy"
                        );
                    });
            }
        }

        /// <summary>
        /// Installs the required Kubernetes related components on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void SetupKubernetes(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            node.InvokeIdempotentAction("setup/setup-install-kubernetes",
                () =>
                {
                    Thread.Sleep(stepDelay);

                    node.Status = "setup: kubernetes apt repository";

                    var bundle = CommandBundle.FromScript(
$@"#!/bin/bash
curl {Program.CurlOptions} https://packages.cloud.google.com/apt/doc/apt-key.gpg | apt-key add -
echo ""deb https://apt.kubernetes.io/ kubernetes-xenial main"" > /etc/apt/sources.list.d/kubernetes.list
safe-apt-get update
");
                    node.SudoCommand(bundle);

                    node.Status = "install: kubeadm";
                    node.SudoCommand($"safe-apt-get install -yq --allow-downgrades kubeadm={KubeVersions.KubeAdminPackageVersion}");

                    node.Status = "install: kubectl";
                    node.SudoCommand($"safe-apt-get install -yq --allow-downgrades kubectl={KubeVersions.KubeCtlPackageVersion}");

                    node.Status = "install: kubelet";
                    node.SudoCommand($"safe-apt-get install -yq --allow-downgrades kubelet={KubeVersions.KubeletPackageVersion}");

                    node.Status = "hold: kubernetes packages";
                    node.SudoCommand("apt-mark hold kubeadm kubectl kubelet");

                    node.Status = "configure: kubelet";
                    node.SudoCommand("mkdir -p /opt/cni/bin");
                    node.SudoCommand("mkdir -p /etc/cni/net.d");
                    node.SudoCommand(CommandBundle.FromScript(
@"#!/bin/bash

echo KUBELET_EXTRA_ARGS=--logging-format=json --volume-plugin-dir=/var/lib/kubelet/volume-plugins --network-plugin=cni --cni-bin-dir=/opt/cni/bin --cni-conf-dir=/etc/cni/net.d > /etc/default/kubelet
systemctl daemon-reload
service kubelet restart
"));

                    // Download and install the Helm client:

                    node.InvokeIdempotentAction("setup/cluster-helm",
                        () =>
                        {
                            node.Status = "install: helm";

                            var helmInstallScript =
$@"#!/bin/bash
cd /tmp
curl {Program.CurlOptions} {kubeSetupInfo.HelmLinuxUri} > helm.tar.gz
tar xvf helm.tar.gz
cp linux-amd64/helm /usr/local/bin
chmod 770 /usr/local/bin/helm
rm -f helm.tar.gz
rm -rf linux-amd64
";
                            node.SudoCommand(CommandBundle.FromScript(helmInstallScript));
                        });
                });
        }

        /// <summary>
        /// Initializes the cluster on the first manager, then joins the remaining
        /// masters and workers to the cluster.
        /// </summary>
        private void SetupCluster()
        {
            var firstMaster = cluster.FirstMaster;

            firstMaster.InvokeIdempotentAction("setup/cluster",
                () =>
                {
                    //---------------------------------------------------------
                    // Initialize the cluster on the first master:

                    firstMaster.Status = "create: cluster";

                    // Pull the Kubernetes images:

                    firstMaster.InvokeIdempotentAction("setup/cluster-images",
                        () =>
                        {
                            firstMaster.Status = "pull: kubernetes images...";
                            firstMaster.SudoCommand("kubeadm config images pull");
                        });

                    firstMaster.InvokeIdempotentAction("setup/cluster-init",
                        () =>
                        {
                            firstMaster.Status = "initialize: cluster";

                            // It's possible that a previous cluster initialization operation
                            // was interrupted.  This command resets the state.

                            firstMaster.SudoCommand("kubeadm reset --force");

                            // Configure the control plane's API server endpoint and initialize
                            // the certificate SAN names to include each master IP address as well
                            // as the HOSTNAME/ADDRESS of the API load balancer (if any).

                            var controlPlaneEndpoint = $"kubernetes-masters:6442";
                            var sbCertSANs           = new StringBuilder();

                            if (!string.IsNullOrEmpty(cluster.Definition.Kubernetes.ApiLoadBalancer))
                            {
                                controlPlaneEndpoint = cluster.Definition.Kubernetes.ApiLoadBalancer;

                                var fields = cluster.Definition.Kubernetes.ApiLoadBalancer.Split(':');

                                sbCertSANs.AppendLine($"  - \"{fields[0]}\"");
                            }

                            foreach (var node in cluster.Masters)
                            {
                                sbCertSANs.AppendLine($"  - \"{node.Address}\"");
                            }

                            var clusterConfig =
$@"
apiVersion: kubeadm.k8s.io/v1beta1
kind: ClusterConfiguration
clusterName: {cluster.Name}
kubernetesVersion: ""v{KubeVersions.KubernetesVersion}""
apiServer:
  logging-format: ""json""
  certSANs:
{sbCertSANs}
controlPlaneEndpoint: ""{controlPlaneEndpoint}""
networking:
  podSubnet: ""{cluster.Definition.Network.PodSubnet}""
  serviceSubnet: ""{cluster.Definition.Network.ServiceSubnet}""
controllerManager:
  logging-format: ""json""
scheduler:
  logging-format: ""json""
";
                            firstMaster.UploadText("/tmp/cluster.yaml", clusterConfig);

                            var response = firstMaster.SudoCommand($"kubeadm init --config /tmp/cluster.yaml");

                            firstMaster.SudoCommand("rm /tmp/cluster.yaml");

                            // Extract the cluster join command from the response.  We'll need this to join
                            // other nodes to the cluster.

                            var output = response.OutputText;
                            var pStart = output.IndexOf(joinCommandMarker, output.IndexOf(joinCommandMarker) + 1);

                            if (pStart == -1)
                            {
                                throw new KubeException("Cannot locate the [kubadm join ...] command in the [kubeadm init ...] response.");
                            }

                            var pEnd = output.Length;

                            if (pEnd == -1)
                            {
                                kubeContextExtension.SetupDetails.ClusterJoinCommand = Regex.Replace(output.Substring(pStart).Trim(), @"\t|\n|\r|\\", "");
                            }
                            else
                            {
                                kubeContextExtension.SetupDetails.ClusterJoinCommand = Regex.Replace(output.Substring(pStart, pEnd - pStart).Trim(), @"\t|\n|\r|\\", "");
                            }

                            kubeContextExtension.Save();
                        });

                    firstMaster.Status = "done";

                    // kubectl config:

                    firstMaster.InvokeIdempotentAction("setup/cluster-kubectl",
                        () =>
                        {
                            // Edit the Kubernetes configuration file to rename the context:
                            //
                            //       CLUSTERNAME-admin@kubernetes --> root@CLUSTERNAME
                            //
                            // rename the user:
                            //
                            //      CLUSTERNAME-admin --> CLUSTERNAME-root 

                            var adminConfig = firstMaster.DownloadText("/etc/kubernetes/admin.conf");

                            adminConfig = adminConfig.Replace($"kubernetes-admin@{cluster.Definition.Name}", $"root@{cluster.Definition.Name}");
                            adminConfig = adminConfig.Replace("kubernetes-admin", $"root@{cluster.Definition.Name}");

                            firstMaster.UploadText("/etc/kubernetes/admin.conf", adminConfig, permissions: "600", owner: "root:root");
                        });

                    // Download the boot master files that will need to be provisioned on
                    // the remaining masters and may also be needed for other purposes
                    // (if we haven't already downloaded these).

                    if (kubeContextExtension.SetupDetails.MasterFiles != null)
                    {
                        kubeContextExtension.SetupDetails.MasterFiles = new Dictionary<string, KubeFileDetails>();
                    }

                    if (kubeContextExtension.SetupDetails.MasterFiles.Count == 0)
                    {
                        // I'm hardcoding the permissions and owner here.  It would be nice to
                        // scrape this from the source files in the future but this was not
                        // worth the bother at this point.

                        var files = new RemoteFile[]
                        {
                            new RemoteFile("/etc/kubernetes/admin.conf", "600", "root:root"),
                            new RemoteFile("/etc/kubernetes/pki/ca.crt", "600", "root:root"),
                            new RemoteFile("/etc/kubernetes/pki/ca.key", "600", "root:root"),
                            new RemoteFile("/etc/kubernetes/pki/sa.pub", "600", "root:root"),
                            new RemoteFile("/etc/kubernetes/pki/sa.key", "644", "root:root"),
                            new RemoteFile("/etc/kubernetes/pki/front-proxy-ca.crt", "644", "root:root"),
                            new RemoteFile("/etc/kubernetes/pki/front-proxy-ca.key", "600", "root:root"),
                            new RemoteFile("/etc/kubernetes/pki/etcd/ca.crt", "644", "root:root"),
                            new RemoteFile("/etc/kubernetes/pki/etcd/ca.key", "600", "root:root"),
                        };

                        foreach (var file in files)
                        {
                            var text = firstMaster.DownloadText(file.Path);

                            kubeContextExtension.SetupDetails.MasterFiles[file.Path] = new KubeFileDetails(text, permissions: file.Permissions, owner: file.Owner);
                        }
                    }

                    // Persist the cluster join command and downloaded master files.

                    kubeContextExtension.Save();

                    firstMaster.Status = "joined";

                    //---------------------------------------------------------
                    // Join the remaining masters to the cluster:

                    foreach (var master in cluster.Masters.Where(m => m != firstMaster))
                    {
                        try
                        {
                            master.InvokeIdempotentAction("setup/cluster-kubectl",
                                () =>
                                {
                                    // It's possible that a previous cluster join operation
                                    // was interrupted.  This command resets the state.

                                    master.SudoCommand("kubeadm reset --force");

                                    // The other (non-boot) masters need files downloaded from the boot master.

                                    master.Status = "upload: master files";

                                    foreach (var file in kubeContextExtension.SetupDetails.MasterFiles)
                                    {
                                        master.UploadText(file.Key, file.Value.Text, permissions: file.Value.Permissions, owner: file.Value.Owner);
                                    }

                                    // Join the cluster:

                                    master.InvokeIdempotentAction("setup/cluster-join",
                                            () =>
                                            {
                                                var joined = false;

                                                master.Status = "join: as master";

                                                for (int attempt = 0; attempt < maxJoinAttempts; attempt++)
                                                {
                                                    var response = master.SudoCommand(kubeContextExtension.SetupDetails.ClusterJoinCommand + " --control-plane", RunOptions.Defaults & ~RunOptions.FaultOnError);

                                                    if (response.Success)
                                                    {
                                                        joined = true;
                                                        break;
                                                    }

                                                    Thread.Sleep(joinRetryDelay);
                                                }

                                                if (!joined)
                                                {
                                                    throw new Exception($"Unable to join node [{master.Name}] to the after [{maxJoinAttempts}] attempts.");
                                                }
                                            });

                                    // Pull the Kubernetes images:

                                    master.InvokeIdempotentAction("setup/cluster-images",
                                            () =>
                                            {
                                                master.Status = "pull: kubernetes images";
                                                master.SudoCommand("kubeadm config images pull");
                                            });
                                });
                        }
                        catch (Exception e)
                        {
                            master.Fault(NeonHelper.ExceptionError(e));
                            master.LogException(e);
                        }

                        master.Status = "joined";
                    }

                    // Configure [kube-apiserver] on all the masters

                    foreach (var master in cluster.Masters)
                    {
                        try
                        {
                            master.Status = "configure: kube-apiserver";

                            master.InvokeIdempotentAction("setup/cluster-kube-apiserver",
                                () =>
                                {
                                    master.Status = "configure: kube-apiserver";
                                    master.SudoCommand(CommandBundle.FromScript(
@"#!/bin/bash

sed -i 's/.*--enable-admission-plugins=.*/    - --enable-admission-plugins=NamespaceLifecycle,LimitRanger,ServiceAccount,DefaultStorageClass,DefaultTolerationSeconds,MutatingAdmissionWebhook,ValidatingAdmissionWebhook,Priority,ResourceQuota/' /etc/kubernetes/manifests/kube-apiserver.yaml
"));
                                }); 
                        }
                        catch (Exception e)
                        {
                            master.Fault(NeonHelper.ExceptionError(e));
                            master.LogException(e);
                        }

                        master.Status = string.Empty;
                    }

                    //---------------------------------------------------------
                    // Join the remaining workers to the cluster:

                    var parallelOptions = new ParallelOptions()
                    {
                        MaxDegreeOfParallelism = Program.MaxParallel
                    };

                    Parallel.ForEach(cluster.Workers, parallelOptions,
                        worker =>
                        {
                            try
                            {
                                worker.InvokeIdempotentAction("setup/cluster-join",
                                    () =>
                                    {
                                        var joined = false;

                                        worker.Status = "join: as worker";

                                        for (int attempt = 0; attempt < maxJoinAttempts; attempt++)
                                        {
                                            var response = worker.SudoCommand(kubeContextExtension.SetupDetails.ClusterJoinCommand, RunOptions.Defaults & ~RunOptions.FaultOnError);

                                            if (response.Success)
                                            {
                                                joined = true;
                                                break;
                                            }

                                            Thread.Sleep(joinRetryDelay);
                                        }

                                        if (!joined)
                                        {
                                            throw new Exception($"Unable to join node [{worker.Name}] to the cluster after [{maxJoinAttempts}] attempts.");
                                        }
                                    });
                            }
                            catch (Exception e)
                            {
                                worker.Fault(NeonHelper.ExceptionError(e));
                                worker.LogException(e);
                            }

                            worker.Status = "joined";
                        });
                });


            firstMaster.InvokeIdempotentAction("setup/workstation",
                () =>
                {
                    // Update the kubeconfig.

                    var kubeConfigPath = KubeHelper.KubeConfigPath;

                    // $todo(marcus.bowyer):
                    // This is hard coding the kubeconfig to point to the first master.
                    // Issue https://github.com/nforgeio/neonKUBE/issues/888 will fix this by adding a proxy to neon-desktop
                    // and load balancing requests across the k8s api servers.
                    var configText = kubeContextExtension.SetupDetails.MasterFiles["/etc/kubernetes/admin.conf"].Text;
                    configText = configText.Replace("kubernetes-masters", $"{cluster.Definition.Masters.FirstOrDefault().Address}");

                    if (!File.Exists(kubeConfigPath))
                    {
                        File.WriteAllText(kubeConfigPath, configText);
                    }
                    else
                    {
                        // The user already has an existing kubeconfig, so we need
                        // to merge in the new config.

                        var newConfig = NeonHelper.YamlDeserialize<KubeConfig>(configText);
                        var existingConfig = KubeHelper.Config;

                        // Remove any existing user, context, and cluster with the same names.
                        // Note that we're assuming that there's only one of each in the config
                        // we downloaded from the cluster.

                        var newCluster      = newConfig.Clusters.Single();
                        var newContext      = newConfig.Contexts.Single();
                        var newUser         = newConfig.Users.Single();
                        var existingCluster = existingConfig.GetCluster(newCluster.Name);
                        var existingContext = existingConfig.GetContext(newContext.Name);
                        var existingUser    = existingConfig.GetUser(newUser.Name);

                        if (existingConfig != null)
                        {
                            existingConfig.Clusters.Remove(existingCluster);
                        }

                        if (existingContext != null)
                        {
                            existingConfig.Contexts.Remove(existingContext);
                        }

                        if (existingUser != null)
                        {
                            existingConfig.Users.Remove(existingUser);
                        }

                        existingConfig.Clusters.Add(newCluster);
                        existingConfig.Contexts.Add(newContext);
                        existingConfig.Users.Add(newUser);

                        existingConfig.CurrentContext = newContext.Name;

                        KubeHelper.SetConfig(existingConfig);
                    }

                    ConnectCluster();

                });

            //-----------------------------------------------------------------
            // Configure the cluster.

            firstMaster.InvokeIdempotentAction("setup/cluster-configure",
                () =>
                {
                    foreach (var node in cluster.Nodes)
                    {
                        node.Status = string.Empty;
                    }

                    // Install the network CNI.

                    DeployCalicoCni(firstMaster);

                    // Allow pods to be scheduled on master nodes if enabled.

                    firstMaster.InvokeIdempotentAction("setup/cluster-master-pods",
                        () =>
                        {
                            // The [kubectl taint] command looks like it can return a non-zero exit code.
                            // We'll ignore this.

                            if (cluster.Definition.Kubernetes.AllowPodsOnMasters.GetValueOrDefault())
                            {
                                firstMaster.SudoCommand(@"until [ `kubectl get nodes | grep ""NotReady"" | wc -l ` == ""0"" ]; do     sleep 1; done", firstMaster.DefaultRunOptions & ~RunOptions.FaultOnError);
                                firstMaster.SudoCommand("kubectl taint nodes --all node-role.kubernetes.io/master-", firstMaster.DefaultRunOptions & ~RunOptions.FaultOnError);
                                firstMaster.SudoCommand(@"until [ `kubectl get nodes -o json | jq .items[].spec | grep ""NoSchedule"" | wc -l ` == ""0"" ]; do     sleep 1; done", firstMaster.DefaultRunOptions & ~RunOptions.FaultOnError);
                            }
                        });

                    // Label Nodes.

                    firstMaster.InvokeIdempotentAction("setup/cluster-label-nodes",
                        () =>
                        {
                            LabelNodes(firstMaster);
                        });

                    // Install Istio.

                    firstMaster.InvokeIdempotentAction("setup/cluster-deploy-istio",
                        () =>
                        {
                            InstallIstio(firstMaster);
                        });

                    // Install the Helm/Tiller service.  This will install the latest stable version.

//                    firstMaster.InvokeIdempotentAction("setup/cluster-deploy-helm",
//                        () =>
//                        {
//                            firstMaster.Status = "deploy: helm/tiller";

//                            firstMaster.KubectlApply(
//@"
//apiVersion: v1
//kind: ServiceAccount
//metadata:
//  name: tiller
//  namespace: kube-system
//---
//apiVersion: rbac.authorization.k8s.io/v1
//kind: ClusterRoleBinding
//metadata:
//  name: tiller
//roleRef:
//  apiGroup: rbac.authorization.k8s.io
//  kind: ClusterRole
//  name: cluster-admin
//subjects:
//- kind: ServiceAccount
//  name: tiller
//  namespace: kube-system
//");

//                            // $hack(marcus.bowyer)
//                            // helm init doesn't work on k8s 1.16.0 yet.
//                            var script =
//$@"#!/bin/bash

//helm init --service-account tiller --output yaml | sed 's@apiVersion: extensions/v1beta1@apiVersion: apps/v1@' | sed 's@  replicas: 1@  replicas: 1\n  selector: {{""matchLabels"": {{""app"": ""helm"", ""name"": ""tiller""}}}}@' | kubectl apply -f -
//";
//                            firstMaster.SudoCommand(CommandBundle.FromScript(script));
//                        });

                    // Create the cluster's [root-user]:

                    firstMaster.InvokeIdempotentAction("setup/cluster-root-user",
                        () =>
                        {
                            var userYaml =
$@"
apiVersion: v1
kind: ServiceAccount
metadata:
  name: {KubeConst.RootUser}-user
  namespace: kube-system
---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: {KubeConst.RootUser}-user
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: cluster-admin
subjects:
- kind: ServiceAccount
  name: {KubeConst.RootUser}-user
  namespace: kube-system
";
                            firstMaster.KubectlApply(userYaml);
                        });

                    // Install the Kubernetes dashboard:

                    firstMaster.InvokeIdempotentAction("setup/cluster-deploy-kubernetes-dashboard",
                        () =>
                        {
                            if (kubeContextExtension.KubernetesDashboardCertificate != null)
                            {
                                firstMaster.Status = "generate: dashboard certificate";

                                // We're going to tie the custom certificate to the IP addresses
                                // of the master nodes only.  This means that only these nodes
                                // can accept the traffic and also that we'd need to regenerate
                                // the certificate if we add/remove a master node.
                                //
                                // Here's the tracking task:
                                //
                                //      https://github.com/nforgeio/neonKUBE/issues/441

                                var masterAddresses = new List<string>();

                                foreach (var master in cluster.Masters)
                                {
                                    masterAddresses.Add(master.Address.ToString());
                                }

                                var utcNow     = DateTime.UtcNow;
                                var utc10Years = utcNow.AddYears(10);

                                var certificate = TlsCertificate.CreateSelfSigned(
                                    hostnames: masterAddresses,
                                    validDays: (int)(utc10Years - utcNow).TotalDays,
                                    issuedBy:  "kubernetes-dashboard");

                                kubeContextExtension.KubernetesDashboardCertificate = certificate.CombinedPem;
                                kubeContextExtension.Save();
                            }

                            // Deploy the dashboard.  Note that we need to insert the base-64
                            // encoded certificate and key PEM into the dashboard configuration
                            // YAML first.

                            firstMaster.Status = "deploy: kubernetes dashboard";

                            var dashboardYaml =
$@"# Copyright 2017 The Kubernetes Authors.
#
# Licensed under the Apache License, Version 2.0 (the """"License"""");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an """"AS IS"""" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.


apiVersion: v1
kind: Namespace
metadata:
  name: kubernetes-dashboard

---

apiVersion: v1
kind: ServiceAccount
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: kubernetes-dashboard

---

kind: Service
apiVersion: v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: kubernetes-dashboard
spec:
  type: NodePort
  ports:
  - port: 443
    targetPort: 8443
    nodePort: {KubeHostPorts.KubeDashboard}
  selector:
    k8s-app: kubernetes-dashboard

---

apiVersion: v1
kind: Secret
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard-certs
  namespace: kubernetes-dashboard
type: Opaque
data:
  cert.pem: $<CERTIFICATE>
  key.pem: $<PRIVATEKEY>

---

apiVersion: v1
kind: Secret
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard-csrf
  namespace: kubernetes-dashboard
type: Opaque
data:
  csrf: """"

---

apiVersion: v1
kind: Secret
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard-key-holder
  namespace: kubernetes-dashboard
type: Opaque

---

kind: ConfigMap
apiVersion: v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard-settings
  namespace: kubernetes-dashboard

---

kind: Role
apiVersion: rbac.authorization.k8s.io/v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: kubernetes-dashboard
rules:
  # Allow Dashboard to get, update and delete Dashboard exclusive secrets.
  - apiGroups: [""""]
    resources: [""secrets""]
    resourceNames: [""kubernetes-dashboard-key-holder"", ""kubernetes-dashboard-certs"", ""kubernetes-dashboard-csrf""]
    verbs: [""get"", ""update"", ""delete""]
    # Allow Dashboard to get and update 'kubernetes-dashboard-settings' config map.
  - apiGroups: [""""]
    resources: [""configmaps""]
    resourceNames: [""kubernetes-dashboard-settings""]
    verbs: [""get"", ""update""]
    # Allow Dashboard to get metrics.
  - apiGroups: [""""]
    resources: [""services""]
    resourceNames: [""heapster"", ""dashboard-metrics-scraper""]
    verbs: [""proxy""]
  - apiGroups: [""""]
    resources: [""services/proxy""]
    resourceNames: [""heapster"", ""http:heapster:"", ""https:heapster:"", ""dashboard-metrics-scraper"", ""http:dashboard-metrics-scraper""]
    verbs: [""get""]

---

kind: ClusterRole
apiVersion: rbac.authorization.k8s.io/v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
rules:
  # Allow Metrics Scraper to get metrics from the Metrics server
  - apiGroups: [""metrics.k8s.io""]
    resources: [""pods"", ""nodes""]
    verbs: [""get"", ""list"", ""watch""]

---

apiVersion: rbac.authorization.k8s.io/v1
kind: RoleBinding
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: kubernetes-dashboard
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: kubernetes-dashboard
subjects:
  - kind: ServiceAccount
    name: kubernetes-dashboard
    namespace: kubernetes-dashboard

---

apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: kubernetes-dashboard
  namespace: kubernetes-dashboard
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: kubernetes-dashboard
subjects:
  - kind: ServiceAccount
    name: kubernetes-dashboard
    namespace: kubernetes-dashboard

---

kind: Deployment
apiVersion: apps/v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: kubernetes-dashboard
spec:
  replicas: 1
  revisionHistoryLimit: 10
  selector:
    matchLabels:
      k8s-app: kubernetes-dashboard
  template:
    metadata:
      labels:
        k8s-app: kubernetes-dashboard
    spec:
      containers:
        - name: kubernetes-dashboard
          image: kubernetesui/dashboard:v2.0.0-rc2
          imagePullPolicy: Always
          ports:
            - containerPort: 8443
              protocol: TCP
          args:
            - --auto-generate-certificates=false
            - --tls-cert-file=cert.pem
            - --tls-key-file=key.pem
            - --namespace=kubernetes-dashboard
# Uncomment the following line to manually specify Kubernetes API server Host
# If not specified, Dashboard will attempt to auto discover the API server and connect
# to it. Uncomment only if the default does not work.
# - --apiserver-host=http://my-address:port
          volumeMounts:
            - name: kubernetes-dashboard-certs
              mountPath: /certs
              # Create on-disk volume to store exec logs
            - mountPath: /tmp
              name: tmp-volume
          livenessProbe:
            httpGet:
              scheme: HTTPS
              path: /
              port: 8443
            initialDelaySeconds: 30
            timeoutSeconds: 30
      volumes:
        - name: kubernetes-dashboard-certs
          secret:
            secretName: kubernetes-dashboard-certs
        - name: tmp-volume
          emptyDir: {{}}
      serviceAccountName: kubernetes-dashboard
# Comment the following tolerations if Dashboard must not be deployed on master
      tolerations:
        - key: node-role.kubernetes.io/master
          effect: NoSchedule

---

kind: Service
apiVersion: v1
metadata:
  labels:
    k8s-app: dashboard-metrics-scraper
  name: dashboard-metrics-scraper
  namespace: kubernetes-dashboard
spec:
  ports:
    - port: 8000
      targetPort: 8000
  selector:
    k8s-app: dashboard-metrics-scraper

---

kind: Deployment
apiVersion: apps/v1
metadata:
  labels:
    k8s-app: dashboard-metrics-scraper
  name: dashboard-metrics-scraper
  namespace: kubernetes-dashboard
spec:
  replicas: 1
  revisionHistoryLimit: 10
  selector:
    matchLabels:
      k8s-app: dashboard-metrics-scraper
  template:
    metadata:
      labels:
        k8s-app: dashboard-metrics-scraper
    spec:
      containers:
        - name: dashboard-metrics-scraper
          image: kubernetesui/metrics-scraper:v1.0.1
          ports:
            - containerPort: 8000
              protocol: TCP
          livenessProbe:
            httpGet:
              scheme: HTTP
              path: /
              port: 8000
            initialDelaySeconds: 30
            timeoutSeconds: 30
          volumeMounts:
          - mountPath: /tmp
            name: tmp-volume
      serviceAccountName: kubernetes-dashboard
# Comment the following tolerations if Dashboard must not be deployed on master
      tolerations:
        - key: node-role.kubernetes.io/master
          effect: NoSchedule
      volumes:
        - name: tmp-volume
          emptyDir: {{}}
";

                            var dashboardCert = TlsCertificate.Parse(kubeContextExtension.KubernetesDashboardCertificate);
                            var variables     = new Dictionary<string, string>();

                            variables.Add("CERTIFICATE", Convert.ToBase64String(Encoding.UTF8.GetBytes(dashboardCert.CertPemNormalized)));
                            variables.Add("PRIVATEKEY", Convert.ToBase64String(Encoding.UTF8.GetBytes(dashboardCert.KeyPemNormalized)));

                            using (var preprocessReader = 
                                new PreprocessReader(dashboardYaml, variables)
                                {
                                    StripComments     = false,
                                    ProcessStatements = false
                                }
                            )
                            {
                                dashboardYaml = preprocessReader.ReadToEnd();
                            }

                            firstMaster.KubectlApply(dashboardYaml);
                        });

                });
        }

        /// <summary>
        /// Installs the Calico CNI.
        /// </summary>
        /// <param name="master">The master node.</param>
        private void DeployCalicoCni(SshProxy<NodeDefinition> master)
        {
            master.InvokeIdempotentAction("setup/cluster-deploy-cni",
                () =>
                {
                    // Deploy Calico

                    var script =
$@"#!/bin/bash

# We need to edit the setup manifest to specify the 
# cluster subnet before applying it.

curl {Program.CurlOptions} {kubeSetupInfo.CalicoSetupYamlUri} > /tmp/calico.yaml
sed -i 's;192.168.0.0/16;{cluster.Definition.Network.PodSubnet};' /tmp/calico.yaml
kubectl apply -f /tmp/calico.yaml
rm /tmp/calico.yaml
";
                    master.SudoCommand(CommandBundle.FromScript(script));

                    // Wait for Calico and CoreDNS pods to report that they're running.

                    // $todo(jefflill):
                    //
                    // This is a horrible hack.  I'm going to examine the [kubectl get pods]
                    // response by skipping the column headers and then ensuring that each
                    // remaining line includes a " Running " string.  If one or more lines
                    // don't include this then we're not ready.
                    //
                    // [kubectl wait] as an experimental command that we should investigate
                    // in the future:
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/424
                    //
                    // We're going to wait a maximum of 120 seconds.

                    NeonHelper.WaitFor(
                        () =>
                        {
                            var response = master.SudoCommand("kubectl get pods --all-namespaces", RunOptions.LogOnErrorOnly);

                            using (var reader = new StringReader(response.OutputText))
                            {
                                foreach (var line in reader.Lines().Skip(1))
                                {
                                    if (!line.Contains(" Running "))
                                    {
                                        return false;
                                    }
                                }
                            }

                            return true;
                        },
                        timeout: TimeSpan.FromSeconds(120),
                        pollTime: TimeSpan.FromSeconds(1));
                });
        }

        /// <summary>
        /// Installs Istio.
        /// </summary>
        /// <param name="master">The master node.</param>
        private void InstallIstio(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: istio";

            var istioScript0 =
$@"#!/bin/bash

tmp=$(mktemp -d /tmp/istioctl.XXXXXX)
cd ""$tmp"" || exit

curl -fsLO {kubeSetupInfo.IstioLinuxUri}

tar -xzf ""istioctl-{KubeVersions.IstioVersion}-linux-amd64.tar.gz""

# setup istioctl
cd ""$HOME"" || exit
mkdir -p "".istioctl/bin""
mv ""${{tmp}}/istioctl"" "".istioctl/bin/istioctl""
chmod + x "".istioctl/bin/istioctl""
rm - r ""${{tmp}}""

export PATH=$PATH:$HOME/.istioctl/bin

istioctl operator init

kubectl create ns istio-system

cat <<EOF > istio-cni.yaml
apiVersion: install.istio.io/v1alpha1
kind: IstioOperator
metadata:
  namespace: istio-system
  name: istiocontrolplane
spec:
  hub: docker.io/istio
  tag: 1.6.4
  meshConfig:
    rootNamespace: istio-system
  components:
    ingressGateways:
    - name: istio-ingressgateway
      enabled: true
      k8s:
        overlays:
          - apiVersion: apps/v1
            kind: Deployment
            name: istio-ingressgateway
            patches:
              - path: kind
                value: DaemonSet
        resources:
          requests:
            cpu: 100m
            memory: 128Mi
          limits:
            cpu: 2000m
            memory: 1024Mi
        strategy:
          rollingUpdate:
            maxSurge: ""100%""
            maxUnavailable: ""25%""
    cni:
      enabled: true
      namespace: kube-system
  values:
    global:
      istiod:
        enabled: true
      logging:
        level: ""default:info""
      logAsJson: true
      defaultNodeSelector: 
        neonkube.io/istio: true
      tracer:
        zipkin:
          address: neon-logging-jaeger-collector.monitoring.svc.cluster.local:9411
    pilot:
      traceSampling: 100
    meshConfig:
      accessLogFile: ""/dev/stdout""
      accessLogFormat: '{{   ""authority"": ""%REQ(:AUTHORITY)%"",   ""mode"": ""%PROTOCOL%"",   ""upstream_service_time"": ""%RESP(X-ENVOY-UPSTREAM-SERVICE-TIME)%"",   ""upstream_local_address"": ""%UPSTREAM_LOCAL_ADDRESS%"",   ""duration"": ""%DURATION%"",   ""request_duration"": ""%REQUEST_DURATION%"",   ""response_duration"": ""%RESPONSE_DURATION%"",   ""response_tx_duration"": ""%RESPONSE_TX_DURATION%"",   ""downstream_local_address"": ""%DOWNSTREAM_LOCAL_ADDRESS%"",   ""upstream_transport_failure_reason"": ""%UPSTREAM_TRANSPORT_FAILURE_REASON%"",   ""route_name"": ""%ROUTE_NAME%"",   ""response_code"": ""%RESPONSE_CODE%"",   ""response_code_details"": ""%RESPONSE_CODE_DETAILS%"",   ""user_agent"": ""%REQ(USER-AGENT)%"",   ""response_flags"": ""%RESPONSE_FLAGS%"",   ""start_time"": ""%START_TIME(%s.%6f)%"",   ""method"": ""%REQ(:METHOD)%"",   ""host"": ""%REQ(:Host)%"",   ""referer"": ""%REQ(:Referer)%"",   ""request_id"": ""%REQ(X-REQUEST-ID)%"",   ""forwarded_host"": ""%REQ(X-FORWARDED-HOST)%"",   ""forwarded_proto"": ""%REQ(X-FORWARDED-PROTO)%"",   ""upstream_host"": ""%UPSTREAM_HOST%"",   ""downstream_local_uri_san"": ""%DOWNSTREAM_LOCAL_URI_SAN%"",   ""downstream_peer_uri_san"": ""%DOWNSTREAM_PEER_URI_SAN%"",   ""downstream_local_subject"": ""%DOWNSTREAM_LOCAL_SUBJECT%"",   ""downstream_peer_subject"": ""%DOWNSTREAM_PEER_SUBJECT%"",   ""downstream_peer_issuer"": ""%DOWNSTREAM_PEER_ISSUER%"",   ""downstream_tls_session_id"": ""%DOWNSTREAM_TLS_SESSION_ID%"",   ""downstream_tls_cipher"": ""%DOWNSTREAM_TLS_CIPHER%"",   ""downstream_tls_version"": ""%DOWNSTREAM_TLS_VERSION%"",   ""downstream_peer_serial"": ""%DOWNSTREAM_PEER_SERIAL%"",   ""downstream_peer_cert"": ""%DOWNSTREAM_PEER_CERT%"",   ""client_ip"": ""%REQ(X-FORWARDED-FOR)%"",   ""requested_server_name"": ""%REQUESTED_SERVER_NAME%"",   ""bytes_received"": ""%BYTES_RECEIVED%"",   ""bytes_sent"": ""%BYTES_SENT%"",   ""upstream_cluster"": ""%UPSTREAM_CLUSTER%"",   ""downstream_remote_address"": ""%DOWNSTREAM_REMOTE_ADDRESS%"",   ""path"": ""%REQ(X-ENVOY-ORIGINAL-PATH?:PATH)%"" }}'
      accessLogEncoding: ""JSON""
    gateways:
      istio-ingressgateway:
        type: NodePort
        externalTrafficPolicy: Local
        sds:
          enabled: true
        ports:
        - port: 80
          nodePort: 30080
          name: http2
        - port: 443
          nodePort: 30443
          name: https
        - port: 15443
          targetPort: 15443
          name: tls
    prometheus:
      enabled: false
    grafana:
      enabled: false
    istiocoredns:
      enabled: true
      coreDNSImage: coredns/coredns
      coreDNSTag: 1.6.2
      coreDNSPluginImage: istio/coredns-plugin:0.2-istio-1.1
    kiali:
      enabled: true
      replicaCount: 1
      hub: quay.io/kiali
      tag: v1.9
      contextPath: /kiali
      nodeSelector: {{}}
      podAntiAffinityLabelSelector: []
      podAntiAffinityTermLabelSelector: []
      dashboard:
        secretName: kiali
        usernameKey: username
        passphraseKey: passphrase
        viewOnlyMode: false
        grafanaURL: http://grafana.monitoring
      prometheusNamespace: monitoring
      createDemoSecret: false
    cni:
      excludeNamespaces:
       - istio-system
       - kube-system
       - kube-node-lease
       - kube-public
       - jobs
      logLevel: info
EOF

istioctl install -f istio-cni.yaml
";

            //            var istioScript1 =
            //$@"#!/bin/bash

            //# Enable sidecar injection.

            //kubectl label namespace default istio-injection=enabled

            //# Create istio-system namespace:

            //kubectl create namespace istio-system

            //# Download and extract the Istio binaries:

            //cd /tmp
            //curl {Program.CurlOptions} {kubeSetupInfo.IstioLinuxUri} > istio.tar.gz
            //tar xvf /tmp/istio.tar.gz
            //mv istio-{kubeSetupInfo.Versions.Istio} istio
            //cd istio

            //# Copy the tools:

            //chmod 330 bin/*
            //cp bin/* /usr/local/bin

            //# Install Istio's CRDs:

            //helm template install/kubernetes/helm/istio-init --name istio-init --set certmanager.enabled=true --namespace istio-system | kubectl apply -f -

            //# Verify that all 28 Istio CRDs were committed to the Kubernetes api-server

            //until [ `kubectl get crds | grep 'istio.io\|certmanager.k8s.io' | wc -l` == ""28"" ]; do
            //    sleep 1
            //done

            //# Install Istio:

            //helm template install/kubernetes/helm/istio \
            //    --name istio \
            //    --namespace istio-system \
            //    --set global.defaultNodeSelector.""neonkube\.io/istio""=true \
            //    --set istio_cni.enabled=true \
            //    --set global.proxy.accessLogFile=/dev/stdout \
            //    --set kiali.enabled=false \
            //    --set tracing.enabled=true \
            //    --set grafana.enabled=false \
            //    --set prometheus.enabled=false \
            //    --set certmanager.enabled=true \
            //    --set certmanager.email=mailbox@donotuseexample.com \
            //	--set gateways.istio-egressgateway.enabled=true \
            //";
            //            if (cluster.Definition.Network.Ingress.Count > 0)
            //            {
            //                istioScript1 +=
            //$@" \
            //    --set gateways.istio-ingressgateway.sds.enabled=true \
            //    --set gateways.istio-ingressgateway.type=NodePort \
            //";
            //                for (var i = 0; i < cluster.Definition.Network.Ingress.Count; i++)
            //                {
            //                    istioScript1 +=
            //$@" \
            //    --set gateways.istio-ingressgateway.ports[{i}].targetPort={cluster.Definition.Network.Ingress[i].TargetPort} \
            //    --set gateways.istio-ingressgateway.ports[{i}].port={cluster.Definition.Network.Ingress[i].Port} \
            //    --set gateways.istio-ingressgateway.ports[{i}].name={cluster.Definition.Network.Ingress[i].Name} \
            //    --set gateways.istio-ingressgateway.ports[{i}].nodePort={cluster.Definition.Network.Ingress[i].NodePort} \
            //";
            //                }
            //            }

            //            istioScript1 +=
            //$@" \
            //    | kubectl apply -f -

            //until [ `kubectl get pods --namespace istio-system --field-selector=status.phase!=Succeeded,status.phase!=Running | wc -l` == ""0"" ]; do
            //    sleep 1
            //done
            //";
            master.SudoCommand(CommandBundle.FromScript(istioScript0));
        }

        /// <summary>
        /// Initializes the EFK stack and other monitoring services.
        /// </summary>
        private void SetupMonitoring()
        {
            var firstMaster = cluster.FirstMaster;

            // Setup Kubernetes.

            firstMaster.InvokeIdempotentAction("setup/cluster-deploy-kubernetes-setup",
                () =>
                {
                    KubeSetup(firstMaster).Wait();
                });

            //// Setup Kubernetes.

            //firstMaster.InvokeIdempotentAction("setup/cluster-deploy-kubernetes-state-metrics",
            //    () =>
            //    {
            //        InstallKubeStateMetrics(firstMaster).Wait();
            //    });

            // Install metrics persistence if required.

            //if (cluster.Definition.Monitor.Prometheus.Persistence && cluster.Definition.Nodes.Any(n => n.Labels.Metrics == true))
            //{
            //    // Install an Etcd cluster to the monitoring namespace

            //    firstMaster.InvokeIdempotentAction("setup/cluster-deploy-etcd",
            //        () =>
            //        {
            //            InstallEtcd(firstMaster);
            //        });

            //    // Install an Metrics cluster to the monitoring namespace

            //    firstMaster.InvokeIdempotentAction("setup/cluster-deploy-\",
            //        () =>
            //        {
            //            InstallM3db(firstMaster);
            //        });

            //}

            //// Install an Prometheus cluster to the monitoring namespace

            firstMaster.InvokeIdempotentAction("setup/cluster-deploy-neon-metrics",
                () =>
                {
                    InstallNeonMetrics(firstMaster);
                });

            // Install Elasticsearch.

            if (cluster.Definition.Monitor.Logs.Enabled)
            {
                firstMaster.InvokeIdempotentAction("setup/cluster-deploy-elasticsearch",
                () =>
                {
                    InstallElasticSearch(firstMaster);
                });

                // Setup Fluentd.

                firstMaster.InvokeIdempotentAction("setup/cluster-deploy-fluentd",
                    () =>
                    {
                        InstallFluentd(firstMaster).Wait();
                    });

                // Setup Fluent-Bit.

                firstMaster.InvokeIdempotentAction("setup/cluster-deploy-fluent-bit",
                    () =>
                    {
                        InstallFluentBit(firstMaster).Wait();
                    });

                // Setup Kibana.

                firstMaster.InvokeIdempotentAction("setup/cluster-deploy-kibana",
                    () =>
                    {
                        InstallKibana(firstMaster).Wait();
                    });

                // Setup Jaeger.

                firstMaster.InvokeIdempotentAction("setup/cluster-deploy-jaeger",
                    () =>
                    {
                        InstallJaeger(firstMaster).Wait();
                    });
            }

            

            // Setup cluster-manager.

            firstMaster.InvokeIdempotentAction("setup/cluster-deploy-cluster-manager",
                () =>
                {
                    InstallClusterManager(firstMaster).Wait();
                });
        }

        /// <summary>
        /// Installs a Helm chart from the neonKUBE github repository.
        /// </summary>
        /// <param name="master">The master node that will install the Helm chart.</param>
        /// <param name="chartName">The name of the Helm chart.</param>
        /// <param name="namespace">The Kubernetes namespace where the Helm chart should be installed. Defaults to "default"</param>
        /// <param name="timeout">Optional timeout to in seconds. Defaults to 300 (5 mins)</param>
        /// <param name="wait">Whether to wait for all pods to be alive before exiting.</param>
        /// <param name="values">Optional values to override Helm chart values.</param>
        /// <returns></returns>
        private async Task InstallHelmChartAsync(
            SshProxy<NodeDefinition>            master,
            string                              chartName,
            string                              releaseName = null,
            string                              @namespace = "default",
            int                                 timeout    = 300,
            bool                                wait       = true,
            List<KeyValuePair<string, object>>  values     = null)
        {
            if (string.IsNullOrEmpty(releaseName))
            {
                releaseName = chartName;
            }

            using (var client = new HeadendClient())
            {
                var zip = await client.GetHelmChartZipAsync(chartName, branch);

                master.UploadBytes($"/tmp/charts/{chartName}.zip", zip);
            }

            var valueOverrides = "";

            if (values != null)
            {
                foreach (var value in values)
                {
                    switch (value.Value.GetType().Name)
                    {
                        case nameof(String):

                            valueOverrides += $"--set-string {value.Key}={value.Value} \\\n";
                            break;

                        case nameof(Int32):

                            valueOverrides += $"--set {value.Key}={value.Value} \\\n";
                            break;
                    }

                }
            }

            var helmChartScript =
$@"#!/bin/bash
cd /tmp/charts

until [ -f {chartName}.zip ]
do
  sleep 1
done

rm -rf {chartName}

unzip {chartName}.zip -d {chartName}
helm install {releaseName} {chartName} --namespace {@namespace} -f {chartName}/values.yaml {valueOverrides} --timeout {timeout}s {(wait ? "--wait" : "")}

START=`date +%s`
DEPLOY_END=$((START+15))

until [ `helm status {releaseName} --namespace {@namespace} | grep ""STATUS: deployed"" | wc -l` -eq 1  ];
do
  if [ $((`date +%s`)) -gt $DEPLOY_END ]; then
    helm delete {releaseName} || true
    exit 1
  fi
   sleep 1
done

rm -rf {chartName}*
";

            var tries = 0;
            var success = false;
            Exception exception = null;
            while (tries < 3 && !success)
            {
                try
                {
                    var response = master.SudoCommand(CommandBundle.FromScript(helmChartScript), RunOptions.None);
                    response.EnsureSuccess();
                    success = true;
                }
                catch (Exception e)
                {
                    tries++;
                    exception = e;
                }
            }

            if (!success && exception != null)
            {
                throw exception;
            }
        }

        /// <summary>
        /// Some initial kubernetes config.
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task KubeSetup(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: cluster-setup";

            await InstallHelmChartAsync(master, "cluster-setup");
        }

        /// <summary>
        /// Setup Kube state metrics.
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task InstallKubeStateMetrics(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: kube-state-metrics";

            await InstallHelmChartAsync(master, "kubernetes");
        }

        /// <summary>
        /// Deploy Kiali
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task InstallKiali(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: kiali";

            await InstallHelmChartAsync(master, "kiali", @namespace: "monitoring");
        }

        /// <summary>
        /// Installs an Etcd cluster to the monitoring namespace.
        /// </summary>
        /// <param name="master">The master node.</param>
        private void InstallEtcd(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: etcd";

            master.Status = "setup: neon-metrics-etcd-volumes";

            master.InvokeIdempotentAction("setup/neon-metrics-etcd-volumes",
                () =>
                {
                    var i = 0;
                    foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Metrics))
                    {
                        var volume = new V1PersistentVolume()
                        {
                            ApiVersion = "v1",
                            Kind = "PersistentVolume",
                            Metadata = new V1ObjectMeta()
                            {
                                Name = $"neon-metrics-etcd-data-{i}",
                                Labels = new Dictionary<string, string>()
                                {
                                    ["neonMetrics"] = "etcd"
                                }
                            },
                            Spec = new V1PersistentVolumeSpec()
                            {
                                Capacity = new Dictionary<string, ResourceQuantity>()
                                {
                                    { "storage", new ResourceQuantity("1Gi") }
                                },
                                AccessModes = new List<string>() { "ReadWriteOnce" },
                                PersistentVolumeReclaimPolicy = "Retain",
                                StorageClassName = "local-storage",
                                Local = new V1LocalVolumeSource()
                                {
                                    Path = $"{KubeConst.LocalVolumePath}/98"
                                },
                                NodeAffinity = new V1VolumeNodeAffinity()
                                {
                                    Required = new V1NodeSelector()
                                    {
                                        NodeSelectorTerms = new List<V1NodeSelectorTerm>()
                            {
                                new V1NodeSelectorTerm()
                                {
                                    MatchExpressions = new List<V1NodeSelectorRequirement>()
                                    {
                                        new V1NodeSelectorRequirement()
                                        {
                                            Key = "kubernetes.io/hostname",
                                            OperatorProperty = "In",
                                            Values = new List<string>() { $"{n.Name}" }
                                        }
                                    }
                                }
                            }
                                    }
                                }
                            },
                        };

                        k8sClient.CreatePersistentVolumeAsync(volume).Wait();
                        i++;
                    }
                });
            

            master.Status = "deploy: neon-metrics-etcd-cluster";

            master.InvokeIdempotentAction("deploy/neon-metrics-etcd-cluster",
                () =>
                {
                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>($"replicas", cluster.Definition.Nodes.Count(n => n.Labels.Metrics == true).ToString()));

                    values.Add(new KeyValuePair<string, object>($"volumeClaimTemplate.resources.requests.storage", "1Gi"));

                    var taints = new List<string>();
                    foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Metrics))
                    {
                        taints = taints.Union(n.Taints).ToList();
                    }

                    int i = 0;
                    foreach (var t in taints)
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                    }

                    InstallHelmChartAsync(master, "etcd-cluster", releaseName: "neon-metrics-etcd", @namespace: "monitoring", values: values).Wait();
                });

            
            // wait for cluster to be running before proceeding
            while ((k8sClient.ListNamespacedPodAsync("monitoring", labelSelector: "release=neon-metrics-etcd")).Result.Items.Where(p => p.Status.Phase == "Running").Count() != cluster.Definition.Nodes.Where(l => l.Labels.Metrics == true).Count())
            {
                Thread.Sleep(1000);
            }
        }

        /// <summary>
        /// Installs an Metrics cluster to the monitoring namespace.
        /// </summary>
        /// <param name="master">The master node.</param>
        private void InstallNeonMetrics(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: neon-metrics";

            var cortexValues = new List<KeyValuePair<string, object>>();

            master.InvokeIdempotentAction("deploy/neon-metrics-prometheus-operator",
                () =>
                {
                    var values = new List<KeyValuePair<string, object>>();

                    var taints = new List<string>();
                    foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Metrics))
                    {
                        taints = taints.Union(n.Taints).ToList();
                    }

                    int i = 0;
                    foreach (var t in taints)
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                    }

                    InstallHelmChartAsync(master, "prometheus-operator", releaseName: "neon-metrics-prometheus-operator", @namespace: "monitoring", values: values, timeout: 1200, wait: true).Wait();
                });

            if (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count() > 1)
            {
                InstallEtcd(master);
            }
            else
            {
                cortexValues.Add(new KeyValuePair<string, object>($"cortexConfig.ingester.lifecycler.ring.kvstore.store", "inmemory"));
            }


            master.InvokeIdempotentAction("deploy/neon-metrics-cortex",
                () =>
                {

                    switch (cluster.Definition.Monitor.Metrics.Storage)
                    {
                        case MetricsStorageOptions.Ephemeral:
                            break;
                        case MetricsStorageOptions.Filesystem:
                            // create folders
                            break;
                        case MetricsStorageOptions.Yugabyte:
                            InstallMetricsYugabyte(master);
                            break;
                        default:
                            break;
                    }

                    var taints = new List<string>();
                    foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Metrics))
                    {
                        taints = taints.Union(n.Taints).ToList();
                    }

                    int i = 0;
                    foreach (var t in taints)
                    {
                        cortexValues.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                        cortexValues.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                        cortexValues.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                    }

                    InstallHelmChartAsync(master, "cortex", releaseName: "neon-metrics-cortex", @namespace: "monitoring", values: cortexValues, timeout: 1200, wait: true).Wait();
                });

            master.InvokeIdempotentAction("deploy/istio-prometheus",
                () =>
                {
                    InstallHelmChartAsync(master, "istio-prometheus", @namespace: "monitoring").Wait();
                });

            master.InvokeIdempotentAction("deploy/grafana",
                () =>
                {
                    var values = new List<KeyValuePair<string, object>>();

                    var taints = new List<string>();
                    foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Metrics))
                    {
                        taints = taints.Union(n.Taints).ToList();
                    }

                    int i = 0;
                    foreach (var t in taints)
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                    }

                    InstallHelmChartAsync(master, "grafana", releaseName: "neon-metrics-grafana", @namespace: "monitoring", values: values).Wait();
                });

        }

        /// <summary>
        /// Installs a Yugabyte cluster for metrics storage.
        /// </summary>
        /// <param name="master"></param>
        private void InstallMetricsYugabyte(SshProxy<NodeDefinition> master)
        {
            master.InvokeIdempotentAction("deploy/metrics-persistence-yugabyte",
                () =>
                {
                    master.Status = "deploy: metrics storage (yugabyte)";

                    master.InvokeIdempotentAction("setup/metrics-yugabyte-volumes",
                        () =>
                        {
                            var i = 0;
                            foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Metrics))
                            {
                                var volume = new V1PersistentVolume()
                                {
                                    ApiVersion = "v1",
                                    Kind = "PersistentVolume",
                                    Metadata = new V1ObjectMeta()
                                    {
                                        Name = $"neon-metrics-db-master-data-{i}",
                                        Labels = new Dictionary<string, string>()
                                        {
                                            ["neonMetrics"] = "yb-master"
                                        }
                                    },
                                    Spec = new V1PersistentVolumeSpec()
                                    {
                                        Capacity = new Dictionary<string, ResourceQuantity>()
                                        {
                                            { "storage", cluster.Definition.Monitor.Metrics.DiskSize }
                                        },
                                        AccessModes = new List<string>() { "ReadWriteOnce" },
                                        PersistentVolumeReclaimPolicy = "Retain",
                                        StorageClassName = "local-storage",
                                        Local = new V1LocalVolumeSource()
                                        {
                                            Path = $"{KubeConst.LocalVolumePath}/96"
                                        },
                                        NodeAffinity = new V1VolumeNodeAffinity()
                                        {
                                            Required = new V1NodeSelector()
                                            {
                                                NodeSelectorTerms = new List<V1NodeSelectorTerm>()
                                                {
                                                    new V1NodeSelectorTerm()
                                                    {
                                                        MatchExpressions = new List<V1NodeSelectorRequirement>()
                                                        {
                                                            new V1NodeSelectorRequirement()
                                                            {
                                                                Key = "kubernetes.io/hostname",
                                                                OperatorProperty = "In",
                                                                Values = new List<string>() { $"{n.Name}" }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    },
                                };

                                k8sClient.CreatePersistentVolumeAsync(volume).Wait();

                                // tserver volume
                                volume.Metadata.Name = $"neon-metrics-db-tserver-data-{i}";
                                volume.Metadata.Labels["neonMetrics"] = $"yb-tserver";
                                volume.Spec.Local.Path = $"{KubeConst.LocalVolumePath}/95";

                                k8sClient.CreatePersistentVolumeAsync(volume).Wait();

                                i++;
                            }
                        });

                    master.InvokeIdempotentAction("setup/metrics-yugabyte",
                        () =>
                        {
                            var values = new List<KeyValuePair<string, object>>();
                            values.Add(new KeyValuePair<string, object>($"replicas.master", cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count()));
                            values.Add(new KeyValuePair<string, object>($"replicas.tserver", cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count()));

                            values.Add(new KeyValuePair<string, object>($"partition.master", cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count()));
                            values.Add(new KeyValuePair<string, object>($"partition.tserver", cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count()));

                            var taints = new List<string>();
                            foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Metrics))
                            {
                                taints = taints.Union(n.Taints).ToList();
                            }

                            int i = 0;
                            foreach (var t in taints)
                            {
                                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                            }

                            // We're not waiting because I was seeing this behaviour: https://github.com/helm/helm/issues/8674
                            InstallHelmChartAsync(master, "yugabyte", releaseName: "neon-metrics-db", @namespace: "monitoring", values: values, wait: false).Wait();
                        });
                });
        }

        /// <summary>
        /// Installs Elasticsearch
        /// </summary>
        /// <param name="master">The master node.</param>
        private void InstallElasticSearch(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: elasticsearch";

            master.InvokeIdempotentAction("setup/elasticsearch-volumes",
                () =>
                {
                    var i = 0;
                    foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Logs))
                    {
                        var volume = new V1PersistentVolume()
                        {
                            ApiVersion = "v1",
                            Kind = "PersistentVolume",
                            Metadata = new V1ObjectMeta()
                            {
                                Name = $"elasticsearch-data-{i}",
                                Labels = new Dictionary<string, string>()
                                {
                                    ["elasticsearch"] = "default"
                                }
                            },
                            Spec = new V1PersistentVolumeSpec()
                            {
                                Capacity = new Dictionary<string, ResourceQuantity>()
                                {
                                    { "storage", new ResourceQuantity(cluster.Definition.Monitor.Logs.DiskSize) }
                                },
                                AccessModes = new List<string>() { "ReadWriteOnce" },
                                PersistentVolumeReclaimPolicy = "Retain",
                                StorageClassName = "local-storage",
                                Local = new V1LocalVolumeSource()
                                {
                                    Path = $"{KubeConst.LocalVolumePath}/99"
                                },
                                NodeAffinity = new V1VolumeNodeAffinity()
                                {
                                    Required = new V1NodeSelector()
                                    {
                                        NodeSelectorTerms = new List<V1NodeSelectorTerm>()
                                {
                                    new V1NodeSelectorTerm()
                                    {
                                        MatchExpressions = new List<V1NodeSelectorRequirement>()
                                        {
                                            new V1NodeSelectorRequirement()
                                            {
                                                Key = "kubernetes.io/hostname",
                                                OperatorProperty = "In",
                                                Values = new List<string>() { $"{n.Name}" }
                                            }
                                        }
                                    }
                                }
                                    }
                                }
                            },
                        };

                        k8sClient.CreatePersistentVolumeAsync(volume).Wait();
                        i++;
                    }
                });

            master.InvokeIdempotentAction("deploy/elasticsearch",
                () =>
                {
                    var monitorOptions = cluster.Definition.Monitor;
                    var values         = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>("replicas", cluster.Nodes.Where(n => n.Metadata.Labels.Logs).Count()));
                    values.Add(new KeyValuePair<string, object>("volumeClaimTemplate.resources.requests.storage", monitorOptions.Logs.DiskSize));
                    values.Add(new KeyValuePair<string, object>("volumeClaimTemplate.storageClassName", KubeConst.LocalStorageClassName));
                    values.Add(new KeyValuePair<string, object>("volumeClaimTemplate.storageClassName", KubeConst.LocalStorageClassName));

                    if (cluster.Nodes.Where(n => n.Metadata.Labels.Logs).Count() == 1)
                    {
                        values.Add(new KeyValuePair<string, object>("minimumMasterNodes", 1));
                    }

                    if (monitorOptions.Logs.Resources != null)
                    {
                        if (monitorOptions.Logs.Resources.Limits != null)
                        {
                            foreach (var r in monitorOptions.Logs.Resources.Limits)
                            {
                                values.Add(new KeyValuePair<string, object>($"resources.limits.{r.Key}", r.Value.ToString()));
                            }
                        }

                        if (monitorOptions.Logs.Resources.Requests != null)
                        {
                            foreach (var r in monitorOptions.Logs.Resources.Requests)
                            {
                                values.Add(new KeyValuePair<string, object>($"resources.requests.{r.Key}", r.Value.ToString()));
                            }
                        }
                    }

                    var taints = new List<string>();
                    foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Logs))
                    {
                        taints = taints.Union(n.Taints).ToList();
                    }

                    int i = 0;
                    foreach (var t in taints)
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                    }

                    InstallHelmChartAsync(master, "elasticsearch", releaseName: "neon-logs-elasticsearch", @namespace: "monitoring", timeout: 1200, values: values, wait: false).Wait();

                    // wait for Elasticsearch cluster to be running before proceeding.
                    while ((k8sClient.ListNamespacedPodAsync("monitoring", labelSelector: "app=elasticsearch-master")).Result.Items.Where(p => p.Status.Phase == "Running").Count() != cluster.Definition.Nodes.Where(l => l.Labels.Metrics == true).Count())
                    {
                        Thread.Sleep(1000);
                    }
                });          
        }

        /// <summary>
        /// Installs FluentBit
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task InstallFluentBit(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: fluent-bit";            

            var values = new List<KeyValuePair<string, object>>();
            var i = 0;

            foreach (var taint in (await k8sClient.ListNodeAsync()).Items.Where(i => i.Spec.Taints != null).SelectMany(i => i.Spec.Taints))
            {
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", taint.Key));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", taint.Effect));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                i++;
            }

            await InstallHelmChartAsync(master, "fluent-bit", releaseName: "neon-log-host", @namespace: "monitoring", timeout: 300, values: values);
        }

        /// <summary>
        /// Installs fluentd
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task InstallFluentd(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: fluentd";

            var values = new List<KeyValuePair<string, object>>();
            values.Add(new KeyValuePair<string, object>($"autoscaling.minReplicas", (Math.Max(1, cluster.Definition.Workers.Count() % 6))));
            values.Add(new KeyValuePair<string, object>($"autoscaling.maxReplicas", (Math.Max(1, cluster.Definition.Workers.Count()))));

            var taints = new List<string>();
            foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Logs))
            {
                taints = taints.Union(n.Taints).ToList();
            }

            int i = 0;
            foreach (var t in taints)
            {
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
            }

            await InstallHelmChartAsync(master, "fluentd", releaseName: "neon-log-collector", @namespace: "monitoring", timeout: 300, values: values);
        }

        /// <summary>
        /// Installs Kibana
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task InstallKibana(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: kibana";

            var values = new List<KeyValuePair<string, object>>();

            var taints = new List<string>();
            foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Logs))
            {
                taints = taints.Union(n.Taints).ToList();
            }

            int i = 0;
            foreach (var t in taints)
            {
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
            }

            await InstallHelmChartAsync(master, "kibana", releaseName: "neon-logs-kibana", @namespace: "monitoring", timeout: 900, values: values);
        }

        /// <summary>
        /// Installs Kibana
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task InstallJaeger(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: jaeger";

            var values = new List<KeyValuePair<string, object>>();

            var taints = new List<string>();
            foreach (var n in cluster.Definition.Nodes.Where(n => n.Labels.Logs))
            {
                taints = taints.Union(n.Taints).ToList();
            }

            int i = 0;
            foreach (var t in taints)
            {
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Split("=")[0]}"));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", "NoSchedule"));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
            }

            await InstallHelmChartAsync(master, "jaeger", releaseName: "neon-logs-jaeger", @namespace: "monitoring", timeout: 900, values: values);
        }

        /// <summary>
        /// Installs metricbeat
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task InstallMetricbeat(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: metricbeat";

            var values = new List<KeyValuePair<string, object>>();
            var i = 0;

            foreach (var taint in (await k8sClient.ListNodeAsync()).Items.Where(i => i.Spec.Taints != null).SelectMany(i => i.Spec.Taints))
            {
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", taint.Key));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", taint.Effect));
                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                i++;
            }

            await InstallHelmChartAsync(master, "metricbeat", @namespace: "monitoring", timeout: 300, values: values);
        }

        /// <summary>
        /// Installs metricbeat
        /// </summary>
        /// <param name="master">The master node.</param>
        private async Task InstallClusterManager(SshProxy<NodeDefinition> master)
        {
            master.Status = "deploy: cluster-manager";

            await InstallHelmChartAsync(master, "cluster-manager", releaseName: "neon-cluster-manager", @namespace: "monitoring", timeout: 300);
        }

        /// <summary>
        /// Renders a Kubernetes label value in a format suitable for labeling a node.
        /// </summary>
        private string GetLabelValue(object value)
        {
            if (value is bool)
            {
                value = NeonHelper.ToBoolString((bool)value);
            }

            return $"\"{value}\"";
        }

        /// <summary>
        /// Adds the node labels.
        /// </summary>
        private void LabelNodes(SshProxy<NodeDefinition> master)
        {
            master.InvokeIdempotentAction("setup/cluster-label-nodes",
                () =>
                {
                    master.Status = "label: nodes";

                    if (cluster.Nodes.Count() == 1)
                    {
                    }

                    try
                    {
                        // Generate a Bash script we'll submit to the first master
                        // that initializes the labels for all nodes.

                        var sbScript = new StringBuilder();
                        var sbArgs = new StringBuilder();

                        sbScript.AppendLineLinux("#!/bin/bash");

                        foreach (var node in cluster.Nodes)
                        {
                            var labelDefinitions = new List<string>();

                            if (node.Metadata.IsWorker)
                            {
                                // Kubernetes doesn't set the role for worker nodes so we'll do that here.

                                labelDefinitions.Add("kubernetes.io/role=worker");
                            }

                            labelDefinitions.Add($"{NodeLabels.LabelDatacenter}={GetLabelValue(cluster.Definition.Datacenter.ToLowerInvariant())}");
                            labelDefinitions.Add($"{NodeLabels.LabelEnvironment}={GetLabelValue(cluster.Definition.Environment.ToString().ToLowerInvariant())}");

                            foreach (var label in node.Metadata.Labels.All)
                            {
                                labelDefinitions.Add($"{label.Key}={GetLabelValue(label.Value)}");
                            }

                            sbArgs.Clear();

                            foreach (var label in labelDefinitions)
                            {
                                sbArgs.AppendWithSeparator(label);
                            }

                            sbScript.AppendLine();
                            sbScript.AppendLineLinux($"kubectl label nodes --overwrite {node.Name} {sbArgs}");
                        }

                        master.SudoCommand(CommandBundle.FromScript(sbScript));
                    }
                    finally
                    {
                        master.Status = string.Empty;
                    }
                });
        }

        /// <summary>
        /// Adds the node taints.
        /// </summary>
        private void TaintNodes()
        {
            var master = cluster.FirstMaster;

            master.InvokeIdempotentAction("setup/cluster-taint-nodes",
                () =>
                {
                    master.Status = "taint: nodes";

                    try
                    {
                        // Generate a Bash script we'll submit to the first master
                        // that initializes the taints for all nodes.

                        var sbScript = new StringBuilder();
                        var sbArgs = new StringBuilder();

                        sbScript.AppendLineLinux("#!/bin/bash");

                        foreach (var node in cluster.Nodes)
                        {
                            var taintDefinitions = new List<string>();

                            if (node.Metadata.IsWorker)
                            {
                                // Kubernetes doesn't set the role for worker nodes so we'll do that here.

                                taintDefinitions.Add("kubernetes.io/role=worker");
                            }

                            taintDefinitions.Add($"{NodeLabels.LabelDatacenter}={GetLabelValue(cluster.Definition.Datacenter.ToLowerInvariant())}");
                            taintDefinitions.Add($"{NodeLabels.LabelEnvironment}={GetLabelValue(cluster.Definition.Environment.ToString().ToLowerInvariant())}");

                            if (node.Metadata.Taints != null)
                            {
                                foreach (var taint in node.Metadata.Taints)
                                {
                                    sbScript.AppendLine();
                                    sbScript.AppendLineLinux($"kubectl taint nodes {node.Name} {taint}");
                                }
                            }
                        }

                        master.SudoCommand(CommandBundle.FromScript(sbScript));
                    }
                    finally
                    {
                        master.Status = string.Empty;
                    }
                });
        }

        /// <summary>
        /// Generates the SSH key to be used for authenticating SSH client connections.
        /// </summary>
        /// <param name="master">A cluster manager node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        private void GenerateClientSshCert(SshProxy<NodeDefinition> master, TimeSpan stepDelay)
        {
            // Here's some information explaining what how I'm doing this:
            //
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
            //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

            if (kubeContextExtension.SshClientKey != null)
            {
                return; // Key has already been created.
            }

            Thread.Sleep(stepDelay);

            kubeContextExtension.SshClientKey = new SshClientKey();

            // $hack(jefflill): 
            //
            // We're going to generate a 2048 bit key pair on one of the
            // master nodes and then download and then delete it.  This
            // means that the private key will be persisted to disk (tmpfs)
            // for a moment but I'm going to worry about that too much
            // since we'll be rebooting the master later on during setup.
            //
            // Technically, I could have installed OpenSSL or something
            // on Windows or figured out the .NET Crypto libraries but
            // but OpenSSL didn't support generating the PUB format
            // SSH expects for the client public key.

            const string keyGenScript =
@"
# Generate a 2048-bit key without a passphrase (the -N option).

rm -f /run/ssh-key*
ssh-keygen -t rsa -b 2048 -N """" -C ""neonkube"" -f /run/ssh-key

# Relax permissions so we can download the key parts.

chmod 666 /run/ssh-key*
";
            master.SudoCommand(CommandBundle.FromScript(keyGenScript));

            using (var stream = new MemoryStream())
            {
                master.Download("/run/ssh-key.pub", stream);

                kubeContextExtension.SshClientKey.PublicPUB = NeonHelper.ToLinuxLineEndings(Encoding.UTF8.GetString(stream.ToArray()));
            }

            using (var stream = new MemoryStream())
            {
                master.Download("/run/ssh-key", stream);

                kubeContextExtension.SshClientKey.PrivatePEM = NeonHelper.ToLinuxLineEndings(Encoding.UTF8.GetString(stream.ToArray()));
            }

            master.SudoCommand("rm /run/ssh-key*");

            // We're going to use WinSCP to convert the OpenSSH PEM formatted key
            // to the PPK format PuTTY/WinSCP require.  Note that this won't work
            // when the tool is running in a Docker Linux container.  We're going
            // to handle the conversion in the outer shim as a post run action.

            if (NeonHelper.IsWindows)
            {
                var pemKeyPath = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("d"));
                var ppkKeyPath = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("d"));

                try
                {
                    File.WriteAllText(pemKeyPath, kubeContextExtension.SshClientKey.PrivatePEM);

                    ExecuteResponse result;

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

                    kubeContextExtension.SshClientKey.PrivatePPK = NeonHelper.ToLinuxLineEndings(File.ReadAllText(ppkKeyPath));

                    // Persist the SSH client key.

                    kubeContextExtension.Save();
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
        /// Generates the private key that will be used to secure SSH on the cluster nodes.
        /// </summary>
        private void ConfigureSshCerts()
        {
            cluster.FirstMaster.InvokeIdempotentAction("setup/ssh-server-key",
                () =>
                {
                    cluster.FirstMaster.Status = "generate: server SSH key";

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
                    cluster.FirstMaster.SudoCommand(CommandBundle.FromScript(configScript));

                    cluster.FirstMaster.Status = "download: server SSH key";

                    kubeContextExtension.SshNodePrivateKey  = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh_host_rsa_key");
                    kubeContextExtension.SshNodePublicKey   = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh_host_rsa_key.pub");
                    kubeContextExtension.SshNodeFingerprint = cluster.FirstMaster.DownloadText("/dev/shm/ssh/ssh.fingerprint");

                    // Delete the SSH key files for security.

                    cluster.FirstMaster.SudoCommand("rm -r /dev/shm/ssh");

                    // Persist the server SSH key and fingerprint.

                    kubeContextExtension.Save();
                });
        }

        /// <summary>
        /// Configures SSH on a node.
        /// </summary>
        /// <param name="node">The target node.</param>
        /// <param name="stepDelay">Ignored.</param>
        private void ConfigureSsh(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            // Configure the SSH credentials on all cluster nodes.

            node.InvokeIdempotentAction("setup/ssh",
                () =>
                {
                    CommandBundle bundle;

                    // Here's some information explaining what how I'm doing this:
                    //
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Configuring
                    //      https://help.ubuntu.com/community/SSH/OpenSSH/Keys

                    node.Status = "setup: client SSH key";

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
                    bundle.AddFile("ssh-key.pub", kubeContextExtension.SshClientKey.PublicPUB);

                    // NOTE: I'm explictly not running the bundle as [sudo] because the OpenSSH
                    //       server is very picky about the permissions on the user's [$HOME]
                    //       and [$HOME/.ssl] folder and contents.  This took me a couple 
                    //       hours to figure out.

                    node.RunCommand(bundle);

                    // These steps are required for both password and public key authentication.

                    // Upload the server key and edit the [sshd] config to disable all host keys 
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
                    bundle.AddFile("ssh_host_rsa_key", kubeContextExtension.SshNodePrivateKey);
                    node.SudoCommand(bundle);
                });
        }
    }
}