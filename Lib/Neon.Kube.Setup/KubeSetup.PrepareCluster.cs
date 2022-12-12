//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.PrepareCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube
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
        /// <param name="nodeImageUri">
        /// <para>
        /// Optionally specifies the node image URI.
        /// </para>
        /// </param>
        /// <param name="nodeImagePath">
        /// <para>
        /// Optionally specifies the node image path.
        /// </para>
        /// <note>
        /// One of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be specified for 
        /// on-premise hypervisor based environments.  These are ignored for cloud hosting.
        /// </note>
        /// </param>
        /// <param name="maxParallel">
        /// Optionally specifies the maximum number of node operations to be performed in parallel.
        /// This <b>defaults to 0</b> which means that we'll use <see cref="IHostingManager.MaxParallel"/>.
        /// </param>
        /// <param name="packageCacheEndpoints">
        /// <para>
        /// Optionally specifies the IP endpoints for the APT package caches to be used by
        /// the cluster, overriding the cluster definition settings.  This is useful when
        /// package caches are already deployed in an environment.
        /// </para>
        /// <note>
        /// Package cache servers are deployed to the control-plane nodes by default.
        /// </note>
        /// </param>
        /// <param name="unredacted">
        /// Optionally indicates that sensitive information <b>won't be redacted</b> from the setup logs 
        /// (typically used when debugging).
        /// </param>
        /// <param name="debugMode">Optionally indicates that the cluster will be prepared in debug mode.</param>
        /// <param name="baseImageName">Optionally specifies the base image name to use for debug mode.</param>
        /// <param name="removeExisting">Optionally remove any existing cluster with the same name in the target environment.</param>
        /// <param name="disableConsoleOutput">
        /// Optionally disables status output to the console.  This is typically
        /// enabled for non-console applications.
        /// </param>
        /// <param name="desktopReadyToGo">
        /// Optionally indicates that we're setting up a neon-desktop built-in cluster
        /// from a completely prebuilt desktop image.  In this case, the controller
        /// returned will fully deploy the cluster (so no setup controller needs to
        /// be created and run afterwards).
        /// </param>
        /// <returns>The <see cref="ISetupController"/>.</returns>
        /// <exception cref="NeonKubeException">Thrown when there's a problem.</exception>
        public static ISetupController CreateClusterPrepareController(
            ClusterDefinition           clusterDefinition,
            bool                        cloudMarketplace,
            string                      nodeImageUri          = null,
            string                      nodeImagePath         = null,
            int                         maxParallel           = 0,
            IEnumerable<IPEndPoint>     packageCacheEndpoints = null,
            bool                        unredacted            = false, 
            bool                        debugMode             = false, 
            string                      baseImageName         = null,
            bool                        removeExisting        = false,
            bool                        disableConsoleOutput  = false,
            bool                        desktopReadyToGo      = false)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (KubeHelper.IsOnPremiseHypervisorEnvironment(clusterDefinition.Hosting.Environment))
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeImageUri) || !string.IsNullOrEmpty(nodeImagePath), $"{nameof(nodeImageUri)}/{nameof(nodeImagePath)}");
            }

            Covenant.Requires<ArgumentException>(maxParallel >= 0, nameof(maxParallel));
            Covenant.Requires<ArgumentNullException>(!debugMode || !string.IsNullOrEmpty(baseImageName), nameof(baseImageName));

            if (desktopReadyToGo)
            {
                Covenant.Assert(clusterDefinition.IsDesktop, $"Expected [{nameof(clusterDefinition.IsDesktop)}] to be TRUE.");
                Covenant.Assert(clusterDefinition.Name == KubeConst.NeonDesktopClusterName, $"Expected cluster name [{KubeConst.NeonDesktopClusterName}] not [{clusterDefinition.Name}].");

                debugMode = false;
            }

            clusterDefinition.Validate();

            if (!string.IsNullOrEmpty(nodeImagePath))
            {
                if (!File.Exists(nodeImagePath))
                {
                    throw new NeonKubeException($"No node image file exists at: {nodeImagePath}");
                }
            }

            // Determine where the log files should go.

            var logFolder = KubeHelper.LogFolder;

            // Remove any log files left over from a previous prepare/setup operation.

            foreach (var file in Directory.GetFiles(logFolder, "*.*", SearchOption.AllDirectories))
            {
                File.Delete(file);
            }

            // Initialize the cluster proxy.

            var cluster = new ClusterProxy(
                clusterDefinition:      clusterDefinition,
                hostingManagerFactory:  new HostingManagerFactory(() => HostingLoader.Initialize()),
                cloudMarketplace:       cloudMarketplace,
                operation:              ClusterProxy.Operation.Prepare,
                nodeImageUri:           nodeImageUri,
                nodeImagePath:          nodeImagePath,
                nodeProxyCreator:       (nodeName, nodeAddress) =>
                {
                    var logStream      = new FileStream(Path.Combine(logFolder, $"{nodeName}.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
                    var logWriter      = new StreamWriter(logStream);
                    var sshCredentials = desktopReadyToGo ? SshCredentials.FromPrivateKey(KubeConst.SysAdminUser, KubeHelper.GetBuiltinDesktopSshKey().PrivatePEM)
                                                          : SshCredentials.FromUserPassword(KubeConst.SysAdminUser, KubeConst.SysAdminPassword);

                    return new NodeSshProxy<NodeDefinition>(nodeName, nodeAddress, sshCredentials, logWriter: logWriter);
                });
            
            if (unredacted)
            {
                cluster.SecureRunOptions = RunOptions.None;
            }

            var hostingManager = cluster.HostingManager;

            // Ensure that the nodes have valid IP addresses.

            cluster.Definition.ValidatePrivateNodeAddresses();

            // Override the cluster definition package caches when requested.

            if (packageCacheEndpoints != null && packageCacheEndpoints.Count() > 0)
            {
                var sb = new StringBuilder();

                foreach (var endpoint in packageCacheEndpoints)
                {
                    sb.AppendWithSeparator($"{endpoint.Address}:{endpoint.Port}");
                }

                clusterDefinition.PackageProxy = sb.ToString();
            }

            // Configure the setup controller.

            var controller = new SetupController<NodeDefinition>($"Preparing [{cluster.Definition.Name}] cluster infrastructure", cluster.Nodes, KubeHelper.LogFolder, disableConsoleOutput: disableConsoleOutput)
            {
                MaxParallel     = maxParallel > 0 ? maxParallel: hostingManager.MaxParallel,
                LogBeginMarker  = "# CLUSTER-BEGIN-PREPARE #######################################################",
                LogEndMarker    = "# CLUSTER-END-PREPARE-SUCCESS #################################################",
                LogFailedMarker = "# CLUSTER-END-PREPARE-FAILED ##################################################"
            };

            // Load the cluster login information if it exists and when it indicates that
            // setup is still pending, we'll use that information (especially the generated
            // secure SSH password).
            //
            // Otherwise, we'll fail the cluster prepare to avoid the possiblity of overwriting
            // the login for an active cluster.

            var contextName      = $"{KubeConst.RootUser}@{clusterDefinition.Name}";
            var clusterLoginPath = KubeHelper.GetClusterLoginPath((KubeContextName)contextName);
            var clusterLogin     = ClusterLogin.Load(clusterLoginPath);

            if (clusterLogin == null || !clusterLogin.SetupDetails.SetupPending)
            {
                clusterLogin = new ClusterLogin(clusterLoginPath)
                {
                    ClusterDefinition = clusterDefinition,
                    SshUsername       = KubeConst.SysAdminUser,
                    SetupDetails      = new KubeSetupDetails() { SetupPending = true }
                };

                clusterLogin.Save();
            }
            else
            {
                throw new InvalidOperationException($"Cannot overwrite existing cluster login [{KubeConst.RootUser}@{clusterDefinition.Name}].  Remove the login first when you're VERY SURE IT'S NOT IMPORTANT!");
            }

            // Create a [DesktopService] proxy so setup can perform some privileged operations 
            // from a non-privileged process.

            var desktopServiceProxy = new DesktopServiceProxy();

            // Configure the setup controller state.

            controller.Add(KubeSetupProperty.Preparing, true);
            controller.Add(KubeSetupProperty.ReleaseMode, KubeHelper.IsRelease);
            controller.Add(KubeSetupProperty.DebugMode, debugMode);
            controller.Add(KubeSetupProperty.BaseImageName, baseImageName);
            controller.Add(KubeSetupProperty.MaintainerMode, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")));
            controller.Add(KubeSetupProperty.ClusterProxy, cluster);
            controller.Add(KubeSetupProperty.ClusterLogin, clusterLogin);
            controller.Add(KubeSetupProperty.HostingManager, cluster.HostingManager);
            controller.Add(KubeSetupProperty.HostingEnvironment, cluster.HostingManager.HostingEnvironment);
            controller.Add(KubeSetupProperty.NeonCloudHeadendClient, HeadendClient.Create());
            controller.Add(KubeSetupProperty.DisableImageDownload, !string.IsNullOrEmpty(nodeImagePath));
            controller.Add(KubeSetupProperty.Redact, !unredacted);
            controller.Add(KubeSetupProperty.DesktopReadyToGo, desktopReadyToGo);
            controller.Add(KubeSetupProperty.DesktopServiceProxy, desktopServiceProxy);

            // Configure the cluster preparation steps.

            controller.AddGlobalStep("configure hosting manager",
                controller =>
                {
                    controller.SetGlobalStepStatus("configure: hosting manager");

                    hostingManager.MaxParallel = controller.MaxParallel;
                    hostingManager.WaitSeconds = 60;
                });

            // Delete any existing cluster in the environment when requested.

            if (removeExisting)
            {
                controller.AddGlobalStep("remove existing cluster",
                    async controller =>
                    {
                        await hostingManager.DeleteClusterAsync(removeOrphans: true);
                    });
            }

            controller.AddGlobalStep("generate credentials",
                controller =>
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

                    var hostingManager   = controller.Get<IHostingManager>(KubeSetupProperty.HostingManager);
                    var clusterLogin     = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
                    var desktopReadyToGo = controller.Get<bool>(KubeSetupProperty.DesktopReadyToGo);

                    controller.SetGlobalStepStatus("generate: SSH password");

                    if (desktopReadyToGo)
                    {
                        // We're going to configure a fixed password for built-in desktop clusters.

                        clusterLogin.SshPassword = KubeConst.SysAdminPassword;
                    }
                    else
                    {
                        // Generate a secure SSH password and append a string that guarantees that
                        // the generated password meets minimum cloud requirements.

                        clusterLogin.SshPassword  = NeonHelper.GetCryptoRandomPassword(clusterDefinition.Security.PasswordLength);
                        clusterLogin.SshPassword += ".Aa0";
                    }

                    //#######################################
                    // $debug(jefflill): DELETE THIS CODE!!!!
                    //clusterLogin.SshPassword = KubeConst.SysAdminPassword;
                    //#######################################

                    // We're also going to generate the server's SSH key here and pass that to the hosting
                    // manager's provisioner.  We need to do this up front because some hosting environment
                    // like AWS don't allow SSH password authentication by default, so we'll need the SSH key
                    // to initialize the nodes after they've been provisioned for those environments.
                    //
                    // NOTE: All build-in neon-desktop clusters share the same SSH keys.  This isn't really
                    //       a security issue because these clusters are not reachable from outside the host
                    //       machine and are also not intended for production workloads.

                    if (desktopReadyToGo || clusterDefinition.IsDesktop)
                    {
                        clusterLogin.SshKey = KubeHelper.GetBuiltinDesktopSshKey();
                    }
                    else
                    {
                        if (clusterLogin.SshKey == null)
                        {
                            // Generate a 2048 bit SSH key pair.

                            controller.SetGlobalStepStatus("generate: SSH client key pair");

                            clusterLogin.SshKey = KubeHelper.GenerateSshKey(cluster.Name, KubeConst.SysAdminUser);
                        }
                    }

                    // We also need to generate the cluster's root SSO password, unless this was specified
                    // in the cluster definition (typically for built-in neon-desktop clusters).

                    controller.SetGlobalStepStatus("generate: SSO password");
                    
                    clusterLogin.SsoUsername = KubeConst.RootUser;
                    clusterLogin.SsoPassword = cluster.Definition.RootPassword ?? NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength);

                    clusterLogin.Save();
                });

            // Give the hosting manager a chance to add any additional provisioning steps.

            hostingManager.AddProvisioningSteps(controller);

            // Add the provisioning steps.

            controller.AddWaitUntilOnlineStep(timeout: TimeSpan.FromMinutes(15));
            controller.AddNodeStep("check node OS", (controller, node) => node.VerifyNodeOS());

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
                    // Ensure that the node image type and version matches the current neonKUBE version.

                    var imageType = node.ImageType;

                    if (desktopReadyToGo)
                    {
                        if (node.ImageType != KubeImageType.Desktop)
                        {
                            throw new Exception($"Node is not a pre-built desktop cluster.");
                        }
                        else if (node.ImageType != KubeImageType.Node)
                        {
                            throw new Exception($"Node image type is [{node.ImageType}], expected: [{KubeImageType.Node}]");
                        }
                    }

                    var imageVersion = node.ImageVersion;

                    if (imageVersion == null)
                    {
                        throw new Exception($"Node image is not stamped with the image version file: {KubeConst.ImageVersionPath}");
                    }

                    if (imageVersion != SemanticVersion.Parse(KubeVersions.NeonKube))
                    {
                        throw new Exception($"Node image version [{imageVersion}] does not match the neonKUBE version [{KubeVersions.NeonKube}] implemented by the current build.");
                    }
                });

            controller.AddNodeStep("node credentials",
                (controller, node) =>
                {
                    node.ConfigureSshKey(controller);
                    node.SetSshPasswordLogin(false);

                    // Update node proxies with the generated SSH credentials.

                    node.UpdateCredentials(clusterLogin.SshCredentials);

                    // Remove the [sysadmin] user password; we support only SSH key authentication.

                    if (!desktopReadyToGo)
                    {
                        node.SudoCommand("passwd", "--delete", KubeConst.SysAdminUser).EnsureSuccess();
                    }
                });

            if (!desktopReadyToGo)
            {
                controller.AddNodeStep("prepare nodes",
                    (controller, node) =>
                    {
                        node.PrepareNode(controller);
                    });
            }

            // Register the cluster domain with the headend, except for built-in desktop clusters.
            //
            // Note that we're also going to add this entry to the local [$/etc/hosts] file so clusters
            // will be recable from this machine even when not connected to the Internet.  We'll do
            // this for built-in clusters as well.

            controller.AddGlobalStep("neoncluster.io domain",
                async controller =>
                {
                    string      hostName;
                    IPAddress   hostAddress;

                    controller.SetGlobalStepStatus("create: cluster neoncluster.io domain");

                    if (desktopReadyToGo)
                    {
                        clusterLogin.ClusterDefinition.Id     = KubeHelper.GenerateClusterId();
                        clusterLogin.ClusterDefinition.Domain = KubeConst.DesktopHostname;

                        hostName    = KubeConst.DesktopHostname;
                        hostAddress = IPAddress.Parse(cluster.Definition.NodeDefinitions.Values.Single().Address);
                    }
                    else
                    {
                        var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
                        var headendClient      = controller.Get<HeadendClient>(KubeSetupProperty.NeonCloudHeadendClient);
                        var clusterAddresses   = string.Join(',', cluster.HostingManager.GetClusterAddresses());

                        var result = await headendClient.ClusterSetup.CreateClusterAsync(addresses: clusterAddresses);

                        clusterLogin.ClusterDefinition.Id     = result["Id"];
                        clusterLogin.ClusterDefinition.Domain = result["Domain"];

                        hostName    = clusterLogin.ClusterDefinition.Id;
                        hostAddress = IPAddress.Parse(cluster.HostingManager.GetClusterAddresses().First());
                    }

                    // For the built-in desktop cluster, add these records to both the
                    // node's local [/etc/hosts] file as well as the host file for the
                    // local workstation:
                    //
                    //      ADDRESS     desktop.neoncluster.io
                    //      ADDRESS     *.desktop.neoncluster.io

                    if (desktopReadyToGo)
                    {
                        controller.SetGlobalStepStatus($"configure: node local DNS");

                        var node                = cluster.Nodes.Single();
                        var sbHosts             = new StringBuilder(node.DownloadText("/etc/hosts"));
                        var desktopServiceProxy = controller.Get<DesktopServiceProxy>(KubeSetupProperty.DesktopServiceProxy);

                        sbHosts.AppendLineLinux($"{hostAddress} {hostName}");
                        sbHosts.AppendLineLinux($"{hostAddress} *.{hostName}");

                        node.UploadText("/etc/hosts", sbHosts, permissions: "644");

                        controller.SetGlobalStepStatus($"configure: workstation local DNS");

                        var sections    = desktopServiceProxy.ListLocalHostsSections();
                        var neonSection = sections.FirstOrDefault(section => section.Name.Equals(KubeConst.EtcHostsSectionName, StringComparison.InvariantCultureIgnoreCase));
                        var hostEntries = neonSection != null ? neonSection.HostEntries : new Dictionary<string, IPAddress>();

                        hostEntries[hostName]        = hostAddress;
                        hostEntries[$"*.{hostName}"] = hostAddress;

                        desktopServiceProxy.ModifyLocalHosts(KubeConst.EtcHostsSectionName, hostEntries);

                        // Wait for the new local cluster DNS record to become active.

                        await NeonHelper.WaitForAsync(
                            async () =>
                            {
                                return (await Dns.GetHostAddressesAsync(hostName)).Count() > 0;
                            },
                            timeout: TimeSpan.FromSeconds(120));
                    }

                    clusterLogin.SshPassword = null;    // We're no longer allowing SSH password authentication so we can clear this.
                    clusterLogin.Save();
                });

            // Some hosting managers may have to do some additional work after
            // the cluster has been otherwise prepared.
            //
            // NOTE: This isn't required for pre-built clusters.

            if (!desktopReadyToGo)
            {
                hostingManager.AddPostProvisioningSteps(controller);
            }

            // Built-in neon-desktop clusters need to configure the workstation login, etc.

            if (desktopReadyToGo)
            {
                controller.AddGlobalStep("configure: workstation", KubeSetup.ConfigureWorkstation);
            }

            // Indicate that cluster prepare succeeded by creating [prepare-ok] file to
            // the log folder.  Cluster setup will verify that this file exists before
            // proceeding.

            controller.AddGlobalStep("finish",
                controller =>
                {
                    File.Create(Path.Combine(logFolder, "prepare-ok"));
                },
                quiet: true);

            // We need to dispose these after the setup controller runs.

            controller.AddDisposable(cluster);
            controller.AddDisposable(desktopServiceProxy);

            return controller;
        }
    }
}
