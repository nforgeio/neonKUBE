//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.PrepareCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
        /// <param name="nodeImageUri">
        /// <para>
        /// Optionally specifies the node image URI.
        /// </para>
        /// <note>
        /// One of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be specified for 
        /// on-premise hypervisor based environments.  This is ignored for cloud hosting.
        /// </note>
        /// </param>
        /// <param name="nodeImagePath">
        /// <para>
        /// Optionally specifies the node image path.
        /// </para>
        /// <note>
        /// One of <paramref name="nodeImageUri"/> or <paramref name="nodeImagePath"/> must be specified for 
        /// on-premise hypervisor based environments.  This is ignored for cloud hosting.
        /// </note>
        /// </param>
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
        /// Package cache servers are deployed to the control-plane nodes by default.
        /// </note>
        /// </param>
        /// <param name="unredacted">
        /// Optionally indicates that sensitive information <b>won't be redacted</b> from the setup logs 
        /// (typically used when debugging).
        /// </param>
        /// <param name="debugMode">Optionally indicates that the cluster will be prepared in debug mode.</param>
        /// <param name="baseImageName">Optionally specifies the base image name to use for debug mode.</param>
        /// <param name="clusterspace">Optionally specifies the clusterspace for the operation.</param>
        /// <param name="neonCloudHeadendUri">Optionally overrides the headend service URI.  This defaults to <see cref="KubeConst.NeonCloudHeadendUri"/>.</param>
        /// <param name="removeExisting">Optionally remove any existing cluster with the same name in the target environment.</param>
        /// <param name="disableConsoleOutput">
        /// Optionally disables status output to the console.  This is typically
        /// enabled for non-console applications.
        /// </param>
        /// <returns>The <see cref="ISetupController"/>.</returns>
        /// <exception cref="NeonKubeException">Thrown when there's a problem.</exception>
        public static ISetupController CreateClusterPrepareController(
            ClusterDefinition           clusterDefinition,
            string                      nodeImageUri          = null,
            string                      nodeImagePath         = null,
            int                         maxParallel           = 500,
            IEnumerable<IPEndPoint>     packageCacheEndpoints = null,
            bool                        unredacted            = false, 
            bool                        debugMode             = false, 
            string                      baseImageName         = null,
            string                      clusterspace          = null,
            string                      neonCloudHeadendUri   = null,
            bool                        removeExisting        = false,
            bool                        disableConsoleOutput  = false)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (KubeHelper.IsOnPremiseHypervisorEnvironment(clusterDefinition.Hosting.Environment))
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeImageUri) || !string.IsNullOrEmpty(nodeImagePath), $"{nameof(nodeImageUri)}/{nameof(nodeImagePath)}");
            }

            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));
            Covenant.Requires<ArgumentNullException>(!debugMode || !string.IsNullOrEmpty(baseImageName), nameof(baseImageName));

            neonCloudHeadendUri ??= KubeConst.NeonCloudHeadendUri;

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
                operation:              ClusterProxy.Operation.Prepare,
                nodeImageUri:           nodeImageUri,
                nodeImagePath:          nodeImagePath,
                nodeProxyCreator:       (nodeName, nodeAddress) =>
                {
                    var logStream      = new FileStream(Path.Combine(logFolder, $"{nodeName}.log"), FileMode.Create, FileAccess.ReadWrite, FileShare.ReadWrite);
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

            var controller = new SetupController<NodeDefinition>($"Preparing [{cluster.Definition.Name}] cluster infrastructure", cluster.Nodes, KubeHelper.LogFolder, disableConsoleOutput: disableConsoleOutput)
            {
                MaxParallel     = maxParallel,
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
            controller.Add(KubeSetupProperty.ClusterspaceFolder, clusterspace);
            controller.Add(KubeSetupProperty.NeonCloudHeadendUri, neonCloudHeadendUri);
            controller.Add(KubeSetupProperty.DisableImageDownload, !string.IsNullOrEmpty(nodeImagePath));
            controller.Add(KubeSetupProperty.Redact, !unredacted);

            // Configure the cluster preparation steps.

            controller.AddGlobalStep("configure hosting manager",
                controller =>
                {
                    controller.SetGlobalStepStatus("configure: hosting manager");

                    hostingManager.MaxParallel = maxParallel;
                    hostingManager.WaitSeconds = 60;
                });

            // Delete any existing cluster in the environment when requested.

            if (removeExisting)
            {
                controller.AddGlobalStep("remove existing cluster",
                    async controller =>
                    {
                        await hostingManager.RemoveClusterAsync(removeOrphans: true);
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
                    // We're going to use the cloud API to configure this secure password
                    // when creating the VMs.  For on-premise hypervisor environments such
                    // as Hyper-V and XenServer, we're going use the [neon-init]
                    // service to mount a virtual DVD that will change the password before
                    // configuring the network on first boot.

                    var hostingManager = controller.Get<IHostingManager>(KubeSetupProperty.HostingManager);
                    var clusterLogin   = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

                    controller.SetGlobalStepStatus("generate: SSH password");

                    // Generate a secure SSH password and append a string that guarantees that
                    // the generated password meets minimum cloud requirements.

                    clusterLogin.SshPassword  = NeonHelper.GetCryptoRandomPassword(clusterDefinition.Security.PasswordLength);
                    clusterLogin.SshPassword += ".Aa0";

                    // We're also going to generate the server's SSH key here and pass that to the hosting
                    // manager's provisioner.  We need to do this up front because some hosting environments
                    // like Azure don't allow SSH password authentication by default, so we'll need the SSH key
                    // to initialize the nodes after they've been provisioned for those environments.

                    if (clusterLogin.SshKey == null)
                    {
                        // Generate a 2048 bit SSH key pair.

                        controller.SetGlobalStepStatus("generate: SSH client key pair");

                        clusterLogin.SshKey = KubeHelper.GenerateSshKey(cluster.Name, KubeConst.SysAdminUser);
                    }

                    // We also need to generate the root SSO password when necessary and add this
                    // to the cluster login.

                    controller.SetGlobalStepStatus("generate: SSO password");
                    
                    clusterLogin.SsoUsername = "root";
                    clusterLogin.SsoPassword = cluster.Definition.RootPassword ?? NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength);

                    clusterLogin.Save();

                    // Update node proxies with the generated SSH credentials.

                    foreach (var node in cluster.Nodes)
                    {
                        node.UpdateCredentials(clusterLogin.SshCredentials);
                    }
                });

            // Have the hosting manager add any custom proviosioning steps.

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
                    // We're going to read this file if it exists and delete the script.

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

            controller.AddNodeStep("check image version",
                (controller, node) =>
                {
                    // Ensure that the node image version matches the current neonKUBE version.

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
                });

            controller.AddNodeStep("prepare nodes",
                (controller, node) =>
                {
                    node.PrepareNode(controller);
                });

            controller.AddGlobalStep("neoncluster.io domain",
                async controller =>
                {
                    controller.SetGlobalStepStatus("create: *.neoncluster.io domain (for TLS)");

                    var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
                    var clusterAddresses   = cluster.HostingManager.GetClusterAddresses();

                    using (var jsonClient = new JsonClient())
                    {
                        jsonClient.BaseAddress = new Uri(controller.Get<string>(KubeSetupProperty.NeonCloudHeadendUri));

                        var args = new ArgDictionary();
                        args.Add("addresses", string.Join(',', clusterAddresses));
                        args.Add("api-version", KubeConst.NeonCloudHeadendVersion);

                        var result = await jsonClient.PostAsync<Dictionary<string, string>>($"/cluster-setup/domain", args: args);

                        clusterLogin.ClusterDefinition.Id     = result["Id"];
                        clusterLogin.ClusterDefinition.Domain = result["Domain"];
                    }
                   
                    clusterLogin.Save();
                });

            // Some hosting managers may have to some additional work after
            // the cluster has been otherwise prepared.

            hostingManager.AddPostProvisioningSteps(controller);

            // Indicate that cluster prepare succeeded by creating [prepare-ok] file to
            // the log folder.  Cluster setup will verify that this file exists before
            // proceeding.

            controller.AddGlobalStep("finish",
                controller =>
                {
                    File.Create(Path.Combine(logFolder, "prepare-ok"));
                },
                quiet: true);

            // We need to dispose this after the setup controller runs.

            controller.AddDisposable(cluster);

            return controller;
        }
    }
}
