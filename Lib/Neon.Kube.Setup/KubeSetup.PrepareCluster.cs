 //-----------------------------------------------------------------------------
// FILE:        KubeSetup.PrepareCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.ComponentModel.Design;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

using k8s.Models;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Hosting;
using Neon.Kube.K8s;
using Neon.Kube.Proxy;
using Neon.Kube.Setup;
using Neon.Kube.SSH;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Setup
{
    public static partial class KubeSetup
    {
        /// <summary>
        /// Constructs the <see cref="ISetupController"/> to be used for preparing a cluster.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="cloudMarketplace">
        /// <para>
        /// For cloud environments, this specifies whether the cluster should be provisioned
        /// using a VM image from the public cloud marketplace when <c>true</c> or from the
        /// private NEONFORGE image gallery for testing when <c>false</c>.  This is ignored
        /// for on-premise environments.
        /// </para>
        /// <note>
        /// Only NEONFORGE maintainers will have permission to use the private image.
        /// </note>
        /// </param>
        /// <param name="options">Specifies the cluster prepare options.</param>
        /// <returns>The <see cref="ISetupController"/>.</returns>
        /// <exception cref="NeonKubeException">Thrown when there's a problem.</exception>
        public static async Task<ISetupController> CreateClusterPrepareControllerAsync(
            ClusterDefinition           clusterDefinition,
            bool                        cloudMarketplace,
            PrepareClusterOptions       options)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(options != null, nameof(options));

            if (!options.DebugMode && KubeHelper.IsOnPremiseHypervisorEnvironment(clusterDefinition.Hosting.Environment))
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(options.NodeImageUri) || !string.IsNullOrEmpty(options.NodeImagePath), $"{nameof(options.NodeImageUri)}/{nameof(options.NodeImagePath)}");
            }

            Covenant.Requires<ArgumentException>(options.MaxParallel >= 0, nameof(options.MaxParallel));

            if (options.DesktopReadyToGo)
            {
                Covenant.Assert(clusterDefinition.IsDesktop, $"Expected [{nameof(clusterDefinition.IsDesktop)}] to be TRUE.");
                Covenant.Assert(clusterDefinition.Name == KubeConst.NeonDesktopClusterName, () => $"Expected cluster name [{KubeConst.NeonDesktopClusterName}] not [{clusterDefinition.Name}].");

                options.DebugMode = false;
            }

            clusterDefinition.Validate();

            if (!string.IsNullOrEmpty(options.NodeImagePath))
            {
                if (!File.Exists(options.NodeImagePath))
                {
                    throw new NeonKubeException($"No node image file exists at: {options.NodeImagePath}");
                }
            }

            // Determine where the log files should go and remove any log files
            // that might left over from previous operations.

            var logFolder = KubeHelper.LogFolder;

            NeonHelper.DeleteFolderContents(logFolder);

            // Initialize the cluster proxy.

            var cluster = ClusterProxy.Create(
                hostingManagerFactory: new HostingManagerFactory(() => HostingLoader.Initialize()),
                cloudMarketplace:      cloudMarketplace,
                operation:             ClusterProxy.Operation.Prepare,
                setupState:            new KubeSetupState() { ClusterDefinition = clusterDefinition },
                nodeImageUri:          options.NodeImageUri,
                nodeImagePath:         options.NodeImagePath,
                nodeProxyCreator:      (nodeName, nodeAddress) =>
                {
                    var logStream      = new FileStream(Path.Combine(logFolder, $"{nodeName}.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    var logWriter      = new StreamWriter(logStream);
                    var sshCredentials = options.DesktopReadyToGo ? SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, KubeHelper.GetBuiltinDesktopSshKey().PrivatePEM)
                                                                  : SshCredentials.FromUserPassword(KubeConst.SysAdminUser, KubeConst.SysAdminPassword);

                    return new NodeSshProxy<NodeDefinition>(nodeName, nodeAddress, sshCredentials, logWriter: logWriter);
                },
                debugMode: options.DebugMode);
            
            if (options.Unredacted)
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            var hostingManager = cluster.HostingManager;

            // Ensure that the nodes have valid IP addresses.

            clusterDefinition.ValidatePrivateNodeAddresses();

            // Perform final cluster definition validation.  This gives hosting managers
            // the chance verify that cloud VM sizes are valid.

            await hostingManager.CheckDeploymentReadinessAsync(clusterDefinition);

            // Override the cluster definition package caches when requested.

            if (options.PackageCacheEndpoints != null && options.PackageCacheEndpoints.Count() > 0)
            {
                var sb = new StringBuilder();

                foreach (var endpoint in options.PackageCacheEndpoints)
                {
                    sb.AppendWithSeparator($"{endpoint.Address}:{endpoint.Port}");
                }

                clusterDefinition.PackageProxy = sb.ToString();
            }

            // Configure the setup controller.

            var controller = new SetupController<NodeDefinition>($"Preparing [{clusterDefinition.Name}] cluster infrastructure", cluster.Nodes, KubeHelper.LogFolder, disableConsoleOutput: options.DisableConsoleOutput)
            {
                MaxParallel     = options.MaxParallel > 0 ? options.MaxParallel: hostingManager.MaxParallel,
                LogBeginMarker  = "# CLUSTER-BEGIN-PREPARE #######################################################",
                LogEndMarker    = "# CLUSTER-END-PREPARE-SUCCESS #################################################",
                LogFailedMarker = "# CLUSTER-END-PREPARE-FAILED ##################################################"
            };

            // Initialize the setup state.  This is backed by a file and is used to
            // persist setup related state across the cluster prepare and setup phases.

            var contextName = $"{KubeConst.RootUser}@{clusterDefinition.Name}";
            var setupState  = (KubeSetupState)null;

            if (KubeSetupState.Exists(contextName))
            {
                setupState = KubeSetupState.Load(contextName);

                switch (setupState.DeploymentStatus)
                {
                    case ClusterDeploymentStatus.Prepared:
                    case ClusterDeploymentStatus.Ready:

                        // We must have details left over from a previous run so 
                        // we'll start out fresh.

                        setupState = KubeSetupState.Create(contextName);
                        break;
                }
            }
            else
            {
                setupState = KubeSetupState.Create(contextName);
            }

            setupState.ClusterDefinition = clusterDefinition;
            setupState.SshUsername       = KubeConst.SysAdminUser;

            cluster.SetupState = setupState;

            setupState.Save();

            // Create a [DesktopService] proxy so setup can perform some privileged operations 
            // from a non-privileged process.

            var desktopServiceProxy = new DesktopServiceProxy();

            // Configure the setup controller state.

            controller.Add(KubeSetupProperty.Preparing, true);
            controller.Add(KubeSetupProperty.ReleaseMode, KubeHelper.IsRelease);
            controller.Add(KubeSetupProperty.DebugMode, options.DebugMode);
            controller.Add(KubeSetupProperty.MaintainerMode, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")));
            controller.Add(KubeSetupProperty.ClusterProxy, cluster);
            controller.Add(KubeSetupProperty.HostingManager, cluster.HostingManager);
            controller.Add(KubeSetupProperty.HostingEnvironment, cluster.HostingManager.HostingEnvironment);
            controller.Add(KubeSetupProperty.NeonCloudHeadendClient, HeadendClient.Create());
            controller.Add(KubeSetupProperty.DisableImageDownload, !string.IsNullOrEmpty(options.NodeImagePath));
            controller.Add(KubeSetupProperty.Redact, !options.Unredacted);
            controller.Add(KubeSetupProperty.DesktopReadyToGo, options.DesktopReadyToGo);
            controller.Add(KubeSetupProperty.BuildDesktopImage, options.BuildDesktopImage);
            controller.Add(KubeSetupProperty.DesktopServiceProxy, desktopServiceProxy);
            controller.Add(KubeSetupProperty.Insecure, options.Insecure);

            // Configure the cluster preparation steps.

            controller.AddGlobalStep("configure hosting manager",
                controller =>
                {
                    controller.SetGlobalStepStatus("configure: hosting manager");

                    if (!string.IsNullOrEmpty(options.NodeImageUri))
                    {
                        controller.LogGlobal();
                        controller.LogGlobal($"node image URI: {options.NodeImageUri}");
                    }
                    else if (string.IsNullOrEmpty(options.NodeImagePath))
                    {
                        controller.LogGlobal();
                        controller.LogGlobal($"node image PATH: {options.NodeImagePath}");
                    }

                    hostingManager.MaxParallel = controller.MaxParallel;
                    hostingManager.WaitSeconds = 60;
                });

            // Check for IP address conflicts.

            controller.AddGlobalStep("check IP conflicts",
                async controller =>
                {
                    var conflicts = await cluster.HostingManager.CheckForConflictsAsync(clusterDefinition);

                    if (conflicts != null)
                    {
                        throw new NeonKubeException(conflicts);
                    }
                });

            // Delete any existing cluster in the environment when requested.

            if (options.RemoveExisting)
            {
                controller.AddGlobalStep("remove existing cluster",
                    async controller =>
                    {
                        await hostingManager.DeleteClusterAsync();
                    });
            }

            controller.AddGlobalStep("generate credentials",
                controller =>
                {
                    var desktopReadyToGo = controller.Get<bool>(KubeSetupProperty.DesktopReadyToGo);

                    if (options.Insecure)
                    {
                        // This mode is used by maintainers so they can easily SSH into cluster nodes
                        // for debugging purposes.
                        //
                        // WARNING: This should never be used for production clusters!

                        setupState.SshPassword = KubeConst.SysAdminPassword;
                    }
                    else
                    {
                        // We're going to generate a secure random password and we're going to append
                        // an extra 4-character string to ensure that the password meets Azure (and probably
                        // other cloud) minimum requirements:
                        //
                        // The supplied password must be between 6-72 characters long and must 
                        // satisfy at least 3 of password complexity requirements from the following: 
                        //
                        //      1. Contains an uppercase character
                        //      2. Contains a lowercase character
                        //      3. Contains a numeric digit
                        //      4. Contains a special character
                        //      5. Control characters are not allowed
                        //
                        // For on-premise hypervisor environments such as Hyper-V and XenServer, we're
                        // going use the [neon-init] service to mount a virtual DVD that will change
                        // the password before configuring the network on first boot.
                        //
                        // For cloud environments, we're going to use the cloud APIs to interact with
                        // [cloud-init] to provision the [sysadmin] account's SSH key.

                        controller.SetGlobalStepStatus("generate: SSH password");

                        if (desktopReadyToGo)
                        {
                            // We're going to configure a fixed password for NEONDESKTOP clusters.

                            setupState.SshPassword = KubeConst.SysAdminPassword;
                        }
                        else
                        {
                            // Generate a secure SSH password and append a string that guarantees that
                            // the generated password meets minimum cloud requirements.

                            setupState.SshPassword  = NeonHelper.GetCryptoRandomPassword(clusterDefinition.Security.PasswordLength);
                            setupState.SshPassword += ".Aa0";
                        }
                    }

                    // We're also going to generate the server's SSH key here and pass that to the hosting
                    // manager's provisioner.  We need to do this up front because some hosting environments
                    // like AWS don't allow SSH password authentication by default, so we'll need the SSH key
                    // to initialize the nodes after they've been provisioned for those environments.
                    //
                    // NOTE: All build-in NEONDESKTOP clusters share the same SSH keys.  This isn't really
                    //       a security issue because these clusters are not reachable from outside the host
                    //       machine and these are also not intended for production workloads.

                    if (desktopReadyToGo || clusterDefinition.IsDesktop)
                    {
                        setupState.SshKey = KubeHelper.GetBuiltinDesktopSshKey();
                    }
                    else
                    {
                        if (setupState.SshKey == null)
                        {
                            // Generate a 2048 bit SSH key pair.

                            controller.SetGlobalStepStatus("generate: SSH client key pair");

                            setupState.SshKey = KubeHelper.GenerateSshKey(cluster.Name, KubeConst.SysAdminUser);
                        }
                    }

                    // We also need to generate the cluster's root SSO password, unless this was specified
                    // in the cluster definition (typically for NEONDESKTOP clusters).

                    controller.SetGlobalStepStatus("generate: SSO password");

                    setupState.SsoUsername = KubeConst.RootUser;
                    setupState.SsoPassword = clusterDefinition.RootPassword ?? NeonHelper.GetCryptoRandomPassword(clusterDefinition.Security.PasswordLength);

                    setupState.Save();
                });

            // Give the hosting manager a chance to add any additional provisioning steps.

            hostingManager.AddProvisioningSteps(controller);

            // Add the provisioning steps.

            controller.AddWaitUntilOnlineStep(timeout: TimeSpan.FromMinutes(15));
            controller.AddNodeStep("check node operating system", (controller, node) => node.VerifyNodeOS());

            controller.AddNodeStep("delete boot script",
                (controller, node) =>
                {
                    // Hosting managers may use [cloud-init] to execute custom scripts
                    // when node virtual machine first boots to configure networking and
                    // also to set a secure SSH password.
                    //
                    // We need to delete this script file since it includes the SSH password.
                    // If present, the script writes the path to itself to:
                    //
                    //      /etc/neonkube/cloud-init/boot-script-path
                    //
                    // We're going to delete this file if it exists.

                    var scriptPath = "/etc/neonkube/cloud-init/boot-script-path";

                    if (node.FileExists(scriptPath))
                    {
                        scriptPath = node.DownloadText(scriptPath);

                        if (!string.IsNullOrEmpty(scriptPath))
                        {
                            node.SudoCommand("rm", "-f", scriptPath.Trim());
                        }
                    }
                });

            controller.AddNodeStep("node check",
                (controller, node) =>
                {
                    // Ensure that the node image type and version matches the current NEONKUBE version.

                    var imageType = node.ImageType;

                    if (options.DebugMode)
                    {
                        if (imageType != KubeImageType.Base)
                        {
                            throw new Exception($"VM image is not the NEONKUBE base image.");
                        }
                    }
                    else
                    {
                        if (options.DesktopReadyToGo)
                        {
                            if (node.ImageType != KubeImageType.Desktop)
                            {
                                throw new Exception($"VM image is not a pre-built desktop cluster.");
                            }
                        }
                        else
                        {
                            if (node.ImageType != KubeImageType.Node)
                            {
                                throw new Exception($"VM image type is [{node.ImageType}], expected: [{KubeImageType.Node}]");
                            }
                        }

                        var imageVersion = node.ImageVersion;

                        if (imageVersion == null)
                        {
                            throw new Exception($"VM image is not stamped with the image version file: {KubeConst.ImageVersionPath}");
                        }

                        if (!imageVersion.ToString().StartsWith(KubeVersions.NeonKube))
                        {
                            throw new Exception($"VM image version [{imageVersion}] does not match the NEONKUBE version [{KubeVersions.NeonKube}] implemented by the current build.");
                        }
                    }
                });

            controller.AddNodeStep("node credentials",
                (controller, node) =>
                {
                    node.ConfigureSshKey(controller);
                    node.AllowSshPasswordLogin(options.Insecure);

                    // Update node proxies with the generated SSH credentials.

                    node.UpdateCredentials(setupState.SshCredentials);
                });

            if (!options.DesktopReadyToGo)
            {
                controller.AddNodeStep("prepare nodes",
                    (controller, node) =>
                    {
                        node.PrepareNode(controller);
                    });
            }

            // Register the cluster domain with the headend, except for NEONDESKTOP clusters.
            //
            // Note that we're also going to add this entry to the local [$/etc/hosts] file so clusters
            // will be recable from this machine even when not connected to the Internet.  We'll do
            // this for NEONDESKTOP clusters as well.

            controller.AddGlobalStep("neoncluster.io domain",
                async controller =>
                {
                    string    hostName;
                    IPAddress hostAddress;

                    controller.SetGlobalStepStatus("create: cluster neoncluster.io domain");

                    if (options.DesktopReadyToGo)
                    {
                        setupState.ClusterId     = KubeHelper.GenerateClusterId();
                        setupState.ClusterName   = clusterDefinition.Name;
                        setupState.ClusterDomain = KubeConst.DesktopClusterDomain;

                        hostName    = KubeConst.DesktopClusterDomain;
                        hostAddress = IPAddress.Parse(clusterDefinition.NodeDefinitions.Values.Single().Address);
                    }
                    else
                    {
                        var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
                        var headendClient      = controller.Get<HeadendClient>(KubeSetupProperty.NeonCloudHeadendClient);
                        var clusterAddresses   = string.Join(',', cluster.HostingManager.GetClusterAddresses());

                        var result = await headendClient.ClusterSetup.CreateClusterAsync();

                        setupState.ClusterId      = result["Id"];
                        setupState.NeonCloudToken = result["Token"];

                        headendClient.HttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", setupState.NeonCloudToken);

                        if (options.BuildDesktopImage)
                        {
                            setupState.ClusterDomain = KubeConst.DesktopClusterDomain;
                        }
                        else
                        {
                            setupState.ClusterDomain = await headendClient.Cluster.UpdateClusterDomainAsync(
                                clusterId: setupState.ClusterId,
                                addresses: clusterAddresses);
                        }

                        hostName    = setupState.ClusterId;
                        hostAddress = IPAddress.Parse(cluster.HostingManager.GetClusterAddresses().First());
                    }

                    // Log the cluster ID for debugging purposes.

                    controller.LogGlobal($"CLUSTER-ID: {setupState.ClusterId}");

                    // For the NEONDESKTOP cluster, add these records to both the
                    // node's local [/etc/hosts] file.  Note that the node is named
                    // "neon-desktop".
                    //
                    //      ADDRESS     desktop.neoncluster.io
                    //      ADDRESS     *.desktop.neoncluster.io

                    if (options.DesktopReadyToGo)
                    {
                        controller.SetGlobalStepStatus($"configure: node local DNS");

                        var node    = cluster.Nodes.Single();
                        var sbHosts = new StringBuilder(node.DownloadText("/etc/hosts"));

                        sbHosts.AppendLineLinux($"{hostAddress} {hostName}");
                        sbHosts.AppendLineLinux($"{hostAddress} *.{hostName}");

                        node.UploadText("/etc/hosts", sbHosts, permissions: "644");
                    }

                    setupState.SshPassword = null;    // We're no longer allowing SSH password authentication so we can clear this.
                    setupState.Save();
                });

            // Some hosting managers may have to do some additional work after
            // the cluster has been otherwise prepared.
            //
            // NOTE: This isn't required for NEONDESKTOP clusters.

            if (!options.DesktopReadyToGo)
            {
                hostingManager.AddPostProvisioningSteps(controller);
            }

            // NEONDESKTOP clusters need to configure the workstation login, etc.

            if (options.DesktopReadyToGo)
            {
                controller.AddNodeStep("configure: workstation", KubeSetup.ConfigureWorkstation, (controller, node) => node == cluster.DeploymentControlNode); ;
            }

            // We need to wait for pods to start and stabilize for NEONDESKTOP clusters.
            // We're going to waita maximum of 10 minutes before giving up.

            if (options.DesktopReadyToGo)
            {
                controller.AddGlobalStep("stabilizing: cluster...",
                    async controller =>
                    {
                        controller.SetGlobalStepStatus("Waiting for cluster to stabilize...");
                        setupState.Save();

                        // Wait a bit to give the cluster a chance to restart the pods.

                        await Task.Delay(TimeSpan.FromSeconds(60));

                        // Wait for the pods to stabilize.

                        using (var k8s = KubeHelper.CreateKubernetesClient())
                        {
                            var startedUtc      = DateTime.UtcNow;
                            var maxWaitInterval = TimeSpan.FromMinutes(15);
                            var pauseInterval   = TimeSpan.FromSeconds(5);
                            var badPods         = new List<V1Pod>();

                            while (true)
                            {
                                var pods = (await k8s.CoreV1.ListAllPodsAsync()).Items;

                                badPods.Clear();

                                foreach (var pod in pods)
                                {
                                    if (pod.Status.Phase != "Running" || pod.Status.ContainerStatuses.Any(status => !status.Ready))
                                    {
                                        badPods.Add(pod);
                                    }
                                }

                                if (badPods.Count == 0)
                                {
                                    return;
                                }

                                // Fail if we've exceeded the maximum wait time, logging info
                                // about the failed pods.

                                if (DateTime.UtcNow - startedUtc >= maxWaitInterval)
                                {
                                    var sb = new StringBuilder();

                                    controller.LogGlobalError($"[{badPods.Count}] unhealthy pods are preventing cluster stabilization.");

                                    foreach (var badPod in badPods
                                        .OrderBy(pod => pod.Namespace())
                                        .ThenBy(pod => pod.Name()))
                                    {
                                        sb.Clear();
                                        sb.AppendLine();
                                        sb.AppendLine($"POD: {badPod.Name()}@{badPod.Namespace()}");
                                        sb.AppendLine("-----------------------------");
                                        sb.AppendLine(NeonHelper.YamlSerialize(badPod));
                                    }

                                    throw new TimeoutException($"The cluster hasn't stabilized ");
                                }

                                // Otherwise wait a bit and retry.

                                await Task.Delay(pauseInterval);
                            }
                        }
                    });
            }

            // Indicate that cluster prepare succeeded in the cluster setup state.  Cluster setup
            // will use this to verify that the cluster was prepared successfully before proceeding.

            controller.AddGlobalStep("finish",
                controller =>
                {
                    if (options.DesktopReadyToGo)
                    {
                        setupState.DeploymentStatus = ClusterDeploymentStatus.Ready;
                    }
                    else
                    {
                        setupState.DeploymentStatus = ClusterDeploymentStatus.Prepared;
                    }

                    setupState.Save();
                },
                quiet: true);

            // We need to dispose these after the setup controller runs.

            controller.AddDisposable(cluster);
            controller.AddDisposable(desktopServiceProxy);

            return await Task.FromResult(controller);
        }
    }
}
