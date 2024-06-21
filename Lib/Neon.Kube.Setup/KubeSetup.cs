//-----------------------------------------------------------------------------
// FILE:        KubeSetup.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using ICSharpCode.SharpZipLib.Zip;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Kube.Proxy;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Implements cluster setup operations.
    /// </summary>
    public static partial class KubeSetup
    {
        //---------------------------------------------------------------------
        // Private constants

        private const string                    joinCommandMarker       = "kubeadm join";
        private const int                       defaultMaxParallelNodes = 10;
        private const int                       maxJoinAttempts         = 5;
        private static readonly TimeSpan        joinRetryDelay          = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan        clusterOpTimeout        = TimeSpan.FromMinutes(10);
        private static readonly int             clusterOpTimeoutSeconds = (int)Math.Ceiling(clusterOpTimeout.TotalSeconds);
        private static readonly TimeSpan        clusterOpPollInterval   = TimeSpan.FromSeconds(1);
        private static readonly IRetryPolicy    operationRetry          = new LinearRetryPolicy(transientDetector: null, retryInterval: clusterOpPollInterval, timeout: clusterOpTimeout);
        private static readonly IRetryPolicy    podExecRetry            = new ExponentialRetryPolicy(e => e is ExecuteException, maxAttempts: 10, maxRetryInterval: TimeSpan.FromSeconds(5));
        private static IStaticDirectory         cachedResources;
        private static ClusterManifest          cachedClusterManifest;
        private static ClusterAdvisor           clusterAdvisor;

        //---------------------------------------------------------------------
        // Implementation


        /// <summary>
        /// Returns the <see cref="IStaticDirectory"/> for the assembly's resources.
        /// </summary>
        public static IStaticDirectory Resources
        {
            get
            {
                if (cachedResources != null)
                {
                    return cachedResources;
                }

                return cachedResources = Assembly.GetExecutingAssembly().GetResourceFileSystem("Neon.Kube.Setup.Resources");
            }
        }

        /// <summary>
        /// <para>
        /// Returns the <see cref="ClusterManifest"/> for the current NeonKUBE build.  This is generated
        /// by the internal <b>neon-image prepare node ...</b> tool command which prepares node images.
        /// This manifest describes the container images that will be provisioned into clusters.
        /// </para>
        /// <para>
        /// The cluster manifest is uploaded to our S3 bucket at <see cref="KubeDownloads.NeonClusterManifestUri"/>
        /// and is available from there and for installed <b>NeonDESKTOP</b> and <b>NeonCLIENT</b> applications,
        /// the cluster manifest will also be persisted as <b>cluster-manifest.json</b> in the app installation
        /// folder.
        /// </para>
        /// <para>
        /// This property first attempts to loads (and caches) the manifest from the local <b>cluster-manifest.json</b> 
        /// file and then falls back to downloading it from S3.
        /// </para>
        /// </summary>
        /// <param name="debugMode">
        /// <para>
        /// Optionally indicates that setup is running in <b>debug mode</b> which currently means that
        /// the cluster manifest has not been generated yet.  When this is <c>true</c>, this method
        /// will return an empty manifest and any subsequent calls will also return an empty manifest,
        /// even when <paramref name="debugMode"/> is passed as <c>false</c>.
        /// </para>
        /// <note>
        /// Clusters deployed with <b>debug mode</b> will not pin the NeonKUBE related images on cluster
        /// nodes, resulting in the possibility that Kubelet may evict these images due to disk pressure.
        /// </note>
        /// </param>
        /// <returns>The cluster manifest.</returns>
        public static ClusterManifest ClusterManifest(bool debugMode = false)
        {
            if (cachedClusterManifest != null)
            {
                return cachedClusterManifest;
            }

            var localClusterManifestPath = Path.Combine(NeonHelper.GetApplicationFolder(), "cluster-manifest.json");

            if (File.Exists(localClusterManifestPath))
            {
                return cachedClusterManifest = NeonHelper.JsonDeserialize<ClusterManifest>(File.ReadAllText(localClusterManifestPath));
            }
            else
            {
                if (debugMode)
                {
                    return cachedClusterManifest = new ClusterManifest();
                }

                using (var httpClient = new HttpClient())
                {
                    var response = httpClient.GetSafeAsync(KubeDownloads.NeonClusterManifestUri).Result;

                    return cachedClusterManifest = NeonHelper.JsonDeserialize<ClusterManifest>(response.Content.ReadAsStringAsync().Result);
                }
            }
        }

        /// <summary>
        /// Returns the cluster definition required to prepare a NeonDESKTOP cluster for 
        /// a specific hosting environment.
        /// </summary>
        /// <param name="hostEnvironment">Specifies the target environment.</param>
        /// <param name="deploymentPrefix">
        /// <para>
        /// Optionally specifies a deployment prefix string to be set as <see cref="DeploymentOptions.Prefix"/>
        /// in the cluster definition returned.  This can be used by <b>ClusterFixture</b> and custom tools
        /// to help isolated temporary cluster assets from production clusters.
        /// </para>
        /// </param>
        /// <returns>The cluster definition.</returns>
        public static ClusterDefinition GetDesktopClusterDefinition(HostingEnvironment hostEnvironment, string deploymentPrefix = null)
        {
            var resourceName = "Neon.Kube.Setup.ClusterDefinitions.";

            switch (hostEnvironment)
            {
                case HostingEnvironment.HyperV:

                    resourceName += "neon-desktop.hyperv.cluster.yaml";
                    break;

                case HostingEnvironment.Aws:
                case HostingEnvironment.Azure:
                case HostingEnvironment.BareMetal:
                case HostingEnvironment.Google:
                case HostingEnvironment.XenServer:

                default:

                    throw new NotSupportedException($"[{nameof(hostEnvironment)}={hostEnvironment}].");
            }

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
            {
                using (var reader = new StreamReader(stream, encoding: Encoding.UTF8))
                {
                    var clusterDefinition = ClusterDefinition.FromYaml(reader.ReadToEnd());

                    clusterDefinition.Validate();
                    Covenant.Assert(clusterDefinition.NodeDefinitions.Count == 1, "NeonDESKTOP cluster definitions must include exactly one node.");

                    if (!string.IsNullOrEmpty(deploymentPrefix))
                    {
                        clusterDefinition.Deployment.Prefix = deploymentPrefix;
                    }

                    // We allow the NeonDESKTOP cluster to be deployed on machines with only
                    // 4 processors.  When we see this, we're going to reduce the number
                    // of processors assigned to the buuilt-in VM to just 3.

                    var processorCount = Environment.ProcessorCount;

                    if (processorCount < 4)
                    {
                        throw new NotSupportedException($"NeonDESKTOP clusters require the host to have at least [4] processors.  Only [{processorCount}] processors are present.");
                    }
                    else if (processorCount == 4)
                    {
                        clusterDefinition.Hosting.Hypervisor.VCpus = 3;
                    }

                    // Use the insecure SSO password for NeonDESKTOP clusters.

                    clusterDefinition.SsoPassword = KubeConst.SysAdminInsecurePassword;

                    return clusterDefinition;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="IKubernetes"/> client persisted in the controller passed.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The <see cref="Kubernetes"/> client.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there is no persisted Kubernetes client, indicating that <see cref="ConnectCluster(ISetupController)"/>
        /// has not been called yet.
        /// </exception>
        public static IKubernetes GetK8sClient(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            try
            {
                return controller.Get<IKubernetes>(KubeSetupProperty.K8sClient);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Cannot retrieve the Kubernetes client because the cluster hasn't been connected via [{nameof(ConnectCluster)}()].", e);
            }
        }

        /// <summary>
        /// Renders a Kubernetes label value in a format suitable for labeling a node.
        /// </summary>
        private static string GetLabelValue(object value)
        {
            if (value is bool)
            {
                value = NeonHelper.ToBoolString((bool)value);
            }

            return $"\"{value}\"";
        }

        /// <summary>
        /// Gets a list of taints that are currently applied to all nodes matching the given node label/value pair.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="labelKey">The target nodes label key.</param>
        /// <param name="labelValue">The target nodes label value.</param>
        /// <returns>The taint list.</returns>
        public static async Task<List<V1Taint>> GetTaintsAsync(ISetupController controller, string labelKey, string labelValue)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var taints = new List<V1Taint>();

            foreach (var node in (await GetK8sClient(controller).CoreV1.ListNodeAsync()).Items.Where(node => node.Metadata.Labels.Any(label => label.Key == labelKey && label.Value == labelValue)))
            {
                if (node.Spec.Taints?.Count() > 0)
                {
                    foreach (var taint in node.Spec.Taints)
                    {
                        if (!taints.Any(taint => taint.Key == taint.Key && taint.Effect == taint.Effect && taint.Value == taint.Value))
                        {
                            taints.Add(taint);
                        }
                    }
                }
            }

            return taints;
        }

        /// <summary>
        /// <para>
        /// Connects to a Kubernetes cluster if it already exists.  This sets the <see cref="KubeSetupProperty.K8sClient"/>
        /// property in the setup controller state when Kubernetes is running and a connection has not already 
        /// been established.
        /// </para>
        /// <note>
        /// The <see cref="KubeSetupProperty.K8sClient"/> will not be set when Kubernetes has not been started, so 
        /// <see cref="ObjectDictionary.Get{TValue}(string)"/> calls for this property will fail when the
        /// cluster has not been connected yet, which will be useful for debugging setup steps that require
        /// a connection but this hasn't happened yet.
        /// </note>
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public static void ConnectCluster(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            k8s.KubeConfigModels.K8SConfiguration v;    // $debug(jefflill): DELETE THIS!

            if (controller.ContainsKey(KubeSetupProperty.K8sClient))
            {
                return;     // Already connected
            }

            var cluster    = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var configPath = KubeHelper.KubeConfigPath;

            Covenant.Assert(!string.IsNullOrEmpty(configPath) && File.Exists(configPath), $"Cannot locate Kubernetes config at [{configPath}].");

            controller.Add(KubeSetupProperty.K8sClient, KubeHelper.CreateKubernetesClient());
        }

        /// <summary>
        /// <para>
        /// Collects the cluster prepare/setup logs into a ZIP archive which is then uploaded to
        /// the headend for potential failure analysis.
        /// </para>
        /// <note>
        /// This method does nothing when <see cref="KubeEnv.DisableTelemetryVariable"/> returns <b>true</b>,
        /// when the cluster was deployed with unredacted logs or when there's no error.
        /// </note>
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="e">Optionally passed as the exception thrown for the problem.</param>
        /// <returns>The tracing <see cref="Task"/>.</returns>
        private static void UploadFailedDeploymentLogs(ISetupController controller, Exception e = null)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            if (!controller.IsFaulted && e == null)
            {
                return;
            }

            var logFolder = KubeHelper.LogFolder;

            if (!controller.IsFaulted)
            {
                return;
            }

            // Don't upload cluster logs when telemetry is disabled.

            if (KubeEnv.IsTelemetryDisabled)
            {
                return;
            }

            // Don't upload anything when logs are not redacted.

            var redact = controller.Get<bool>(KubeSetupProperty.Redact);

            if (!redact)
            {
                return;
            }

            // Don't do anything when there are no log files.

            if (!Directory.Exists(logFolder) || Directory.GetFiles(logFolder, "*.log", SearchOption.AllDirectories).Length == 0)
            {
                return;
            }

            // Tell the user what's going on.

            var preparing = controller.Get<bool>(KubeSetupProperty.Preparing);

            controller.SetGlobalStepStatus($"Cluster {(preparing ? "prepare" : "setup")} failure: uploading redacted logs for analysis");

            var headendClient     = controller.Get<HeadendClient>(KubeSetupProperty.NeonCloudHeadendClient);
            var cluster           = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterDefinition = cluster?.SetupState.ClusterDefinition;

            if (clusterDefinition != null)
            {
                clusterDefinition = NeonHelper.JsonClone(clusterDefinition);
                clusterDefinition.Hosting?.ClearSecrets(clusterDefinition);
            }

            // We're going to create a ZIP archive including all of the log files plus
            // additional [metadata.yaml] and [cluster-definition.yaml] files with additional
            // information about the operation as well as the redacted cluster definition
            // (we remove things like cloud crendentials).

            var timestampUtc = DateTime.UtcNow;
            var uploadId     = Guid.NewGuid();
            var clientId     = KubeHelper.ClientId;
            var userId       = Guid.Empty;      // $todo(jefflill): Setting this to ZEROs until we implement NeonCLOUD users

            using (var tempZipFile = new TempFile(".zip"))
            {
                using (var tempZipStream = File.Create(tempZipFile.Path))
                {
                    using (var zipStream = tempZipStream)
                    {
                        using (var zip = ZipFile.Create(zipStream))
                        {
                            using (var tempFolder = new TempFolder())
                            {
                                zip.BeginUpdate();

                                // Generate and add: metadata.yaml

                                var metadata = new ClusterSetupFailureMetadata()
                                {
                                    TimestampUtc    = timestampUtc,
                                    NeonKubeVersion = KubeVersion.NeonKube,
                                    CliendId        = clientId,
                                    UserId          = userId,
                                    Exception       = e?.ToString()
                                };

                                zip.Add(new ICSharpCode.SharpZipLib.Zip.StaticStringDataSource(NeonHelper.YamlSerialize(metadata)), "metadata.yaml");

                                // Add: cluster-definition.yaml

                                zip.Add(new ICSharpCode.SharpZipLib.Zip.StaticStringDataSource(NeonHelper.YamlSerialize(clusterDefinition.Redact())), "cluster-definition.yaml");

                                // We're going to upload all the files from the log folder.

                                // $note(jefflill):
                                //
                                // The cluster log files may still be open at this point for writing, but these
                                // still allow READ access.  The problem is that [ZipFile] appears to require
                                // exclusive access.
                                //
                                // Rather than trying to close these files so I can add them the ZIP archive, I'm
                                // going to copy their contents to temp files in a temporary folder and then add those.

                                var fileId = 0;

                                foreach (var path in Directory.EnumerateFiles(KubeHelper.LogFolder, "*.*", SearchOption.AllDirectories))
                                {
                                    var tempFile = Path.Combine(tempFolder.Path, $"{fileId++}.dat");

                                    File.Copy(path, tempFile);
                                    zip.Add(tempFile, path.Substring(KubeHelper.LogFolder.Length));
                                }

                                // We're done updating the ZIP archive.

                                zip.CommitUpdate();
                            }
                        }
                    }
                }

                // The temp file now holds the ZIP archive data.  We're going to upload that
                // to the headend.

                using (var tempZipStream = File.OpenRead(tempZipFile.Path))
                {
                    try
                    {
                        headendClient.ClusterSetup.PostDeploymentLogAsync(tempZipStream, uploadId.ToString("d"), timestampUtc, KubeVersion.NeonKube, clientId.ToString("d"), userId.ToString("d"), preparing).Wait();

                        // Tell the user that we're done.

                        controller.SetGlobalStepStatus($"Cluster log upload complete");
                    }
                    catch (Exception eUpload)
                    {
                        controller.SetGlobalStepStatus($"Cluster log upload failed: {NeonHelper.ExceptionError(eUpload)}");
                    }
                }
            }
        }
    }
}
