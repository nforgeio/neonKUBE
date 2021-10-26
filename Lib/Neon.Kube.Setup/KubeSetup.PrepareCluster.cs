//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.PrepareCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using ICSharpCode.SharpZipLib.Zip;
using k8s;
using k8s.Models;
using Microsoft.Rest;
using Newtonsoft.Json.Linq;

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
        /// <param name="nodeImageUri">Optionally specifies the node image URI (one of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be specified).</param>
        /// <param name="nodeImagePath">Optionally specifies the node image path (one of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be specified).</param>
        /// <param name="maxParallel">
        /// Optionally specifies the maximum number of node operations to be performed in parallel.
        /// This <b>defaults to 500</b> which is effectively infinite.
        /// </param>
        /// <param name="packageCacheEndpoints">
        /// <para>
        /// Optionally specifies the IP endpoints for the APT package caches to be used by
        /// the cluster, overriding the cluster definition settings.  This is useful when
        /// package caches are already deployed in an environment.
        /// </para>
        /// <note>
        /// Package cache servers are deployed to the masters by default.
        /// </note>
        /// </param>
        /// <param name="unredacted">
        /// Optionally indicates that sensitive information <b>won't be redacted</b> from the setup logs 
        /// (typically used when debugging).
        /// </param>
        /// <param name="debugMode">Optionally indicates that the cluster will be prepared in debug mode.</param>
        /// <param name="baseImageName">Optionally specifies the base image name to use for debug mode.</param>
        /// <param name="automationFolder">
        /// Optionally specifies that the operation is to be performed in <b>automation mode</b> by specifying
        /// the non-default directory where cluster state such as logs, logins, etc. will be written, overriding
        /// the default <b>$(USERPROFILE)\.neonkube</b> directory.
        /// </param>
        /// <param name="headendUri">Optionally override the headend service URI</param>
        /// <param name="disableImageDownload">
        /// Optionally indicate that the node image is already present locally and does not need to be downloaded.
        /// </param>
        /// <param name="removeExisting">Optionally remove any existing cluster with the same name in the target environment.</param>
        /// <param name="readyToGoMode">
        /// Optionally creates a setup controller that prepares and partially sets up a ready-to-go image or completes
        /// the cluster setup for a provisioned ready-to-go cluster.  This defaults to <see cref="ReadyToGoMode.Normal"/>.
        /// </param>
        /// <returns>The <see cref="ISetupController"/>.</returns>
        /// <exception cref="KubeException">Thrown when there's a problem.</exception>
        public static ISetupController CreateClusterPrepareController(
            ClusterDefinition           clusterDefinition,
            string                      nodeImageUri          = null,
            string                      nodeImagePath         = null,
            int                         maxParallel           = 500,
            IEnumerable<IPEndPoint>     packageCacheEndpoints = null,
            bool                        unredacted            = false, 
            bool                        debugMode             = false, 
            string                      baseImageName         = null,
            string                      automationFolder      = null,
            string                      headendUri            = "https://headend.neoncloud.io",
            bool                        disableImageDownload  = false,
            bool                        removeExisting        = false,
            ReadyToGoMode               readyToGoMode         = ReadyToGoMode.Normal)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeImageUri) || !string.IsNullOrEmpty(nodeImagePath), $"{nameof(nodeImageUri)}/{nameof(nodeImagePath)}");
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));
            Covenant.Requires<ArgumentNullException>(!debugMode || !string.IsNullOrEmpty(baseImageName), nameof(baseImageName));

            // Create the automation subfolder for the operation if required and determine
            // where the log files should go.

            var logFolder = KubeHelper.LogFolder;

            if (!string.IsNullOrEmpty(automationFolder))
            {
                logFolder = Path.Combine(automationFolder, logFolder);
            }

            // Initialize the cluster proxy.

            var cluster = new ClusterProxy(
                clusterDefinition:      clusterDefinition,
                hostingManagerFactory:  new HostingManagerFactory(() => HostingLoader.Initialize()),
                operation:              ClusterProxy.Operation.Prepare,
                nodeImageUri:           nodeImageUri,
                nodeImagePath:          nodeImagePath,
                nodeProxyCreator:       (nodeName, nodeAddress, appendToLog) =>
                {
                    var logStream = new FileStream(Path.Combine(logFolder, $"{nodeName}.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);

                    if (appendToLog)
                    {
                        logStream.Seek(0, SeekOrigin.End);
                    }

                    var logWriter      = new StreamWriter(logStream);
                    var sshCredentials = SshCredentials.FromUserPassword(KubeConst.SysAdminUser, KubeConst.SysAdminPassword);

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

            var controller = new SetupController<NodeDefinition>($"Preparing [{cluster.Definition.Name}] cluster infrastructure", cluster.Nodes, KubeHelper.LogFolder)
            {
                MaxParallel     = maxParallel,
                LogBeginMarker  = "# CLUSTER-BEGIN-PREPARE ##########################################################",
                LogEndMarker    = "# CLUSTER-END-PREPARE-SUCCESS ####################################################",
                LogFailedMarker = "# CLUSTER-END-PREPARE-FAILED #####################################################"
            };

            // Load the cluster login information if it exists and when it indicates that
            // setup is still pending, we'll use that information (especially the generated
            // secure SSH password).
            //
            // Otherwise, we'll write (or overwrite) the context file with a fresh context.

            var clusterLoginPath = KubeHelper.GetClusterLoginPath((KubeContextName)$"{KubeConst.RootUser}@{clusterDefinition.Name}");
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

            // Configure the setup controller state.

            controller.Add(KubeSetupProperty.ReleaseMode, KubeHelper.IsRelease);
            controller.Add(KubeSetupProperty.DebugMode, debugMode);
            controller.Add(KubeSetupProperty.BaseImageName, baseImageName);
            controller.Add(KubeSetupProperty.MaintainerMode, !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NC_ROOT")));
            controller.Add(KubeSetupProperty.ClusterProxy, cluster);
            controller.Add(KubeSetupProperty.ClusterLogin, clusterLogin);
            controller.Add(KubeSetupProperty.HostingManager, cluster.HostingManager);
            controller.Add(KubeSetupProperty.HostingEnvironment, cluster.HostingManager.HostingEnvironment);
            controller.Add(KubeSetupProperty.AutomationFolder, automationFolder);
            controller.Add(KubeSetupProperty.HeadendUri, headendUri);
            controller.Add(KubeSetupProperty.DisableImageDownload, disableImageDownload);
            controller.Add(KubeSetupProperty.ClusterIp, clusterDefinition.Kubernetes.ApiLoadBalancer ?? clusterDefinition.SortedMasterNodes.First().Address);
            controller.Add(KubeSetupProperty.ReadyToGoMode, readyToGoMode);

            // Configure the cluster preparation steps.

            controller.AddGlobalStep("configure hosting manager",
                controller =>
                {
                    controller.SetGlobalStepStatus("configure: hosting manager");

                    if (hostingManager.RequiresAdminPrivileges)
                    {
                        try
                        {
                            KubeHelper.VerifyAdminPrivileges();
                        }
                        catch (Exception e)
                        {
                            controller.LogProgressError(NeonHelper.ExceptionError(e));
                            return;
                        }
                    }

                    hostingManager.MaxParallel = maxParallel;
                    hostingManager.WaitSeconds = 60;
                });

            // Delete any existing cluster in the environment when requested.

            if (removeExisting)
            {
                controller.AddGlobalStep("remove existing cluster",
                    async controller =>
                    {
                        await hostingManager.RemoveClusterAsync(removeOrphansByPrefix: true);
                    });
            }

            // We don't want to set a secure SSH password when preparing a ready-to-go
            // node image because the partial setup operation that follows as well as
            // actual end user cluster setup assumes the default password.

            if (readyToGoMode == ReadyToGoMode.Prepare)
            {
                clusterLogin.SshPassword = KubeConst.SysAdminPassword;
                clusterLogin.Save();
            }
            else
            {
                controller.AddGlobalStep("generate ssh credentials",
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
                        // We're going to use the cloud API to configure this secure password
                        // when creating the VMs.  For on-premise hypervisor environments such
                        // as Hyper-V and XenServer, we're going use the [neon-init]
                        // service to mount a virtual DVD that will change the password before
                        // configuring the network on first boot.
                        //
                        // For bare metal, we're going to leave the password along and just use
                        // whatever the user specified when the nodes were built out.
                        //
                        // WSL2 NOTE:
                        //
                        // We're going to leave the default password in place for WSL2 distribution
                        // so that they'll be easy for the user to manage.  This isn't a security
                        // gap because WSL2 distros are configured such that OpenSSH server can
                        // only be reached from the host workstation via the internal [172.x.x.x]
                        // address and not from the external network.

                        var hostingManager = controller.Get<IHostingManager>(KubeSetupProperty.HostingManager);
                        var clusterLogin   = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

                        if (!hostingManager.GenerateSecurePassword)
                        {
                            clusterLogin.SshPassword = KubeConst.SysAdminPassword;
                            clusterLogin.Save();
                        }
                        else if (hostingManager.GenerateSecurePassword && string.IsNullOrEmpty(clusterLogin.SshPassword))
                        {
                            controller.SetGlobalStepStatus("generate: SSH password");

                            clusterLogin.SshPassword = NeonHelper.GetCryptoRandomPassword(clusterDefinition.Security.PasswordLength);

                            // Append a string that guarantees that the generated password meets
                            // minimum cloud requirements.

                            clusterLogin.SshPassword += ".Aa0";
                            clusterLogin.Save();
                        }

                        // We're also going to generate the server's SSH key here and pass that to the hosting
                        // manager's provisioner.  We need to do this up front because some hosting environments
                        // like AWS don't allow SSH password authentication by default, so we'll need the SSH key
                        // to initialize the nodes after they've been provisioned for those environments.

                        if (clusterLogin.SshKey == null)
                        {
                            // Generate a 2048 bit SSH key pair.

                            controller.SetGlobalStepStatus("generate: SSH key pair");

                            clusterLogin.SshKey = KubeHelper.GenerateSshKey(cluster.Name, KubeConst.SysAdminUser);

                            // We're going to use WinSCP (if it's installed) to convert the OpenSSH PEM formatted key
                            // to the PPK format PuTTY/WinSCP requires.

                            if (NeonHelper.IsWindows)
                            {
                                var pemKeyPath = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("d"));
                                var ppkKeyPath = Path.Combine(KubeHelper.TempFolder, Guid.NewGuid().ToString("d"));

                                try
                                {
                                    File.WriteAllText(pemKeyPath, clusterLogin.SshKey.PrivateOpenSSH);

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
                                    controller.LogProgressError(result.AllText);
                                    return;
                                }

                                clusterLogin.SshKey.PrivatePPK = NeonHelper.ToLinuxLineEndings(File.ReadAllText(ppkKeyPath));

                                // Persist the SSH key.

                                clusterLogin.Save();
                            }
                            finally
                            {
                                NeonHelper.DeleteFile(pemKeyPath);
                                NeonHelper.DeleteFile(ppkKeyPath);
                            }
                        }
                    }
                });
            }

            hostingManager.AddProvisioningSteps(controller);

            controller.AddWaitUntilOnlineStep(timeout: TimeSpan.FromMinutes(15));
            controller.AddNodeStep("check node OS", (state, node) => node.VerifyNodeOS());

            controller.AddNodeStep("vm image type",
                (state, node) =>
                {
                    // Ensure that the source node image type is supported by the
                    // current operation and when preparing a ready-to-go image that
                    // we set the node image types to ready-to-go.

                    switch (readyToGoMode)
                    {
                        case ReadyToGoMode.Normal:

                            switch (node.ImageType)
                            {
                                case KubeImageType.Node:

                                    // This is expected.

                                    break;

                                case KubeImageType.ReadyToGo:
                                case KubeImageType.Base:
                                default:

                                    throw new Exception($"Unexpected source VM image type [{node.ImageType}].");
                            }
                            break;

                        case ReadyToGoMode.Prepare:

                            switch (node.ImageType)
                            {
                                case KubeImageType.Node:

                                    // This is expected.

                                    node.ImageType = KubeImageType.ReadyToGo;
                                    break;

                                case KubeImageType.ReadyToGo:
                                case KubeImageType.Base:
                                default:

                                    throw new Exception($"Unexpected source VM image type [{node.ImageType}].");
                            }
                            break;

                        case ReadyToGoMode.Setup:

                            switch (node.ImageType)
                            {
                                case KubeImageType.ReadyToGo:

                                    // This is expected.

                                    break;

                                case KubeImageType.Node:
                                case KubeImageType.Base:
                                default:

                                    throw new Exception($"Unexpected source VM image type [{node.ImageType}].");
                            }
                            break;

                        default:

                            throw new NotImplementedException();
                    }

                    var imageVersion = node.ImageVersion;

                    if (imageVersion == null)
                    {
                        throw new Exception("Node image is not stamped with the image version.  You'll need to regenerate the node image.");
                    }

                    if (imageVersion != SemanticVersion.Parse(KubeConst.NeonKubeVersion))
                    {
                        throw new Exception($"Node image version [{imageVersion}] does not match the neonKUBE version [{KubeConst.NeonKubeVersion}] implemented by the current build.");
                    }
                });

            controller.AddNodeStep("check image version",
                (state, node) =>
                {
                    // Ensure that the node image version matches the current neonKUBE version.

                    var imageVersion = node.ImageVersion;

                    if (imageVersion == null)
                    {
                        throw new Exception("Node image is not stamped with the image version.  You'll need to regenerate the node image.");
                    }

                    if (imageVersion != SemanticVersion.Parse(KubeConst.NeonKubeVersion))
                    {
                        throw new Exception($"Node image version [{imageVersion}] does not match the neonKUBE version [{KubeConst.NeonKubeVersion}] implemented by the current build.");
                    }
                });

            controller.AddNodeStep("node credentials",
                (state, node) =>
                {
                    node.ConfigureSshKey(controller);
                });

            controller.AddNodeStep("prepare nodes",
                (state, node) =>
                {
                    // Mark the node as ready-to-go if we're creating a ready-to-go image.

                    if (readyToGoMode == ReadyToGoMode.Prepare)
                    {
                        node.ImageType = KubeImageType.ReadyToGo;
                    }

                    // Prepare the node.

                    node.PrepareNode(controller);

                    // When preparing a ready-to-go image, we need to re-enable the [neon-init]
                    // service so we'll be able to mount an ISO with our special script to configure
                    // the SSH password and network when preparing user clusters using this prepared
                    // image.
                    //
                    // Note that we're passing [keepNetworkSettings=true] so that the VM will boot
                    // using the current static IP.  This really only makes sense for ready-to-go
                    // mode.

                    if (readyToGoMode == ReadyToGoMode.Prepare)
                    {
                        node.SetNeonInitStatus(false, keepNetworkSettings: false);
                    }
                });

            controller.AddGlobalStep("create neoncluster.io domain",
                async (controller) =>
                {
                    controller.SetGlobalStepStatus("create: neoncluster.io subdomain for TLS");

                    var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
                    var clusterIp          = controller.Get<string>(KubeSetupProperty.ClusterIp);

                    if (IPAddress.TryParse(clusterIp, out var ip))
                    {
                        using (var jsonClient = new JsonClient())
                        {
                            jsonClient.BaseAddress                = new Uri(controller.Get<string>(KubeSetupProperty.HeadendUri));
                            clusterLogin.ClusterDefinition.Domain = await jsonClient.GetAsync<string>($"/cluster/domain?ipAddress={clusterIp}");
                            clusterLogin.Save();
                        }
                    }
                });

            // Some hosting managers may have to some additional work after
            // the cluster has been otherwise prepared.

            hostingManager.AddPostProvisioningSteps(controller);

            // We need to dispose this after the setup controller runs.

            controller.AddDisposable(cluster);

            return controller;
        }
    }
}
