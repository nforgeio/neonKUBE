//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.cs
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
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube
{
    /// <summary>
    /// Implements cluster setup operations.
    /// </summary>
    public static partial class KubeSetup
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
                this.Path        = path;
                this.Permissions = permissions;
                this.Owner       = owner;
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
            /// Returns the file owner formatted as: <b>USER:GROUP</b>
            /// </summary>
            public string Owner { get; private set; }
        }

        //---------------------------------------------------------------------
        // Private constants

        private const string                    joinCommandMarker       = "kubeadm join";
        private const int                       defaultMaxParallelNodes = 10;
        private const int                       maxJoinAttempts         = 5;
        private static readonly TimeSpan        joinRetryDelay          = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan        clusterOpTimeout        = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan        clusterOpPollInterval   = TimeSpan.FromSeconds(1);
        private static readonly IRetryPolicy    podExecRetry            = new ExponentialRetryPolicy(e => e is ExecuteException, maxAttempts: 10, maxRetryInterval: TimeSpan.FromSeconds(5));
        private static IStaticDirectory         cachedResources;
        private static ClusterManifest          cachedClusterManifest;

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

                return cachedResources = Assembly.GetExecutingAssembly().GetResourceFileSystem("Neon.Kube.Resources");
            }
        }

        /// <summary>
        /// Returns the <see cref="ClusterManifest"/> for the current neonKUBE build.  This is generated
        /// by the internal <b>neon-image prepare node ...</b> tool command which prepares node images.
        /// This manifest describes the container images that will be provisioned into clusters.
        /// </summary>
        public static ClusterManifest ClusterManifest
        {
            get
            {
                if (cachedClusterManifest != null)
                {
                    return cachedClusterManifest;
                }

                return cachedClusterManifest = NeonHelper.JsonDeserialize<ClusterManifest>(Resources.GetFile("/cluster-manifest.json").ReadAllText());
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

            foreach (var node in (await GetK8sClient(controller).ListNodeAsync()).Items.Where(node => node.Metadata.Labels.Any(label => label.Key == labelKey && label.Value == labelValue)))
            {
                if (node.Spec.Taints?.Count() > 0)
                {
                    foreach (var taint in node.Spec.Taints)
                    {
                        if (!taints.Any(t => t.Key == taint.Key && t.Effect == taint.Effect && t.Value == taint.Value))
                        {
                            taints.Add(taint);
                        }
                    }
                }
            }

            return taints;
        }

        /// <summary>
        /// Returns the path of the current kubeconfig file based on the KUBECONFIG environment variable.
        /// </summary>
        /// <returns>
        /// The kubeconfig file path or <c>~\.kube\config</c> (the standard default location) when environment
        /// variable doesn't exist or is empty.
        /// </returns>
        /// <remarks>
        /// <note>
        /// Only the first file specified in KUBECONFIG is returned when present.  Multiple config
        /// files are not supported.
        /// </note>
        /// </remarks>
        public static string GetCurrentKubeConfigPath()
        {
            var kubeConfigVar = Environment.GetEnvironmentVariable("KUBECONFIG");

            if (string.IsNullOrEmpty(kubeConfigVar))
            {
                return Path.Combine(NeonHelper.UserHomeFolder, ".kube", "config");
            }

            return kubeConfigVar.Split(';').Where(variable => variable.Contains("config")).FirstOrDefault();
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

            if (controller.ContainsKey(KubeSetupProperty.K8sClient))
            {
                return;     // Already connected
            }

            var cluster    = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var configPath = GetCurrentKubeConfigPath();

            Covenant.Assert(!string.IsNullOrEmpty(configPath) && File.Exists(configPath), $"Cannot locate Kubernetes config at [{configPath}].");

            // We're using a generated wrapper class to handle transient retries rather than 
            // modifying the built-in base retry policy.  We're really just trying to handle
            // the transients that happen during setup when the API server is unavailable for
            // some reaon (like it's being restarted).

            var k8s = new KubernetesWithRetry(KubernetesClientConfiguration.BuildConfigFromConfigFile(configPath, currentContext: cluster.KubeContext.Name));

            k8s.RetryPolicy =
                new ExponentialRetryPolicy(
                    transientDetector: 
                        exception =>
                        {
                            var exceptionType = exception.GetType();

                            // Exceptions like this happen when a API server connection can't be established
                            // because the server isn't running or ready.

                            if (exceptionType == typeof(HttpRequestException) && exception.InnerException != null && exception.InnerException.GetType() == typeof(SocketException))
                            {
                                return true;
                            }

                            var httpOperationException = exception as HttpOperationException;

                            if (httpOperationException != null)
                            {
                                var statusCode = httpOperationException.Response.StatusCode;

                                switch (statusCode)
                                {
                                    case HttpStatusCode.GatewayTimeout:
                                    case HttpStatusCode.InternalServerError:
                                    case HttpStatusCode.RequestTimeout:
                                    case HttpStatusCode.ServiceUnavailable:
                                    case (HttpStatusCode)423:   // Locked
                                    case (HttpStatusCode)429:   // Too many requests

                                        return true;
                                }
                            }

                            // This might be another variant of the check just above.  This looks like an SSL negotiation problem.

                            if (exceptionType == typeof(HttpRequestException) && exception.InnerException != null && exception.InnerException.GetType() == typeof(IOException))
                            {
                                return true;
                            }

                            return false;
                        },
                    maxAttempts:          int.MaxValue,
                    initialRetryInterval: TimeSpan.FromSeconds(1),
                    maxRetryInterval:     TimeSpan.FromSeconds(5),
                    timeout:              TimeSpan.FromMinutes(5));

            controller.Add(KubeSetupProperty.K8sClient, k8s);
        }

        /// <summary>
        /// <para>
        /// Collects the cluster prepare/setup logs into a ZIP archive which is that uploaded to
        /// the headend for potential analysis.
        /// </para>
        /// <note>
        /// This method does nothing when the <see cref="KubeEnv.DisableTelemetryVariable"/> is set to <b>true</b>.
        /// </note>
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="e">Passed as the exception thrown for the problem.</param>
        /// <returns>The tracing <see cref="Task"/>.</returns>
        private static async Task UploadDeploymentLogs(ISetupController controller, Exception e)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(e != null, nameof(e));

            if (KubeEnv.IsTelemetryDisabled)
            {
                // Don't upload cluster logs when the telemetry is disabled.

                return;
            }

            var logFolder = KubeHelper.LogFolder;

            if (!Directory.Exists(logFolder) || Directory.GetFiles(logFolder, "*.log", SearchOption.TopDirectoryOnly).Length == 0)
            {
                // There don't appear to be any log files so skip this step.

                return;
            }

            // Tell the user what's going on.

            var preparing = controller.Get<bool>(KubeSetupProperty.Preparing);

            controller.SetGlobalStepStatus($"Cluster {(preparing ? "prepare" : "setup")} failure: uploading redacted logs for analysis");

            var headendClient     = controller.Get<HeadendClient>(KubeSetupProperty.NeonCloudHeadendClient);
            var clusterProxy      = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterDefinition = clusterProxy?.Definition;

            if (clusterDefinition != null)
            {
                clusterDefinition = NeonHelper.JsonClone(clusterDefinition);
                clusterDefinition.ClearSetupState();

                // $note(jefflill): ClearSetupState() isn't clearing these right now so we're going to force this.

                clusterDefinition.Hosting?.ClearSecrets(clusterDefinition);
            }

            // We're going to create a ZIP archive including all of the log files plus
            // additional [metadata.yaml] and [cluster-definition.yaml] files with additional
            // information about the operation as well as the redacted cluster definition
            // (we remove things like cloud crendentials).

            var timestampUtc = DateTime.UtcNow;
            var uploadId     = Guid.NewGuid();
            var clientId     = KubeHelper.ClientId;
            var userId       = Guid.Empty;      // $todo(jefflill): Setting this to ZEROs until we implement neonCLOUD users

            using (var tempFile = new TempFile(".zip"))
            {
                using (var tempStream = File.Create(tempFile.Path))
                {
                    using (var zipStream = tempStream)
                    {
                        using (var zip = ZipFile.Create(zipStream))
                        {
                            // Generate and add: metadata.yaml

                            var metadata = new ClusterSetupFailureMetadata()
                            {
                                TimestampUtc    = timestampUtc,
                                NeonKubeVersion = KubeVersions.NeonKube,
                                CliendId        = clientId,
                                UserId          = userId,
                                Exception       = e.ToString()
                            };

                            // Add: metadata.yaml

                            zip.Add(new ICSharpCode.SharpZipLib.Zip.StaticStringDataSource(NeonHelper.YamlSerialize(metadata)), "metadata.yaml");

                            // Add: cluster-definition.yaml

                            zip.Add(new ICSharpCode.SharpZipLib.Zip.StaticStringDataSource(NeonHelper.YamlSerialize(clusterDefinition)), "cluster-definition.yaml");

                            // We're going to upload all the files in the the log folder.

                            zip.AddDirectory(logFolder);
                        }
                    }

                    // The temp file now holds the ZIP archive data.  We're going to upload that
                    // to the headend.

                    tempStream.Position = 0;

                    try
                    {
                        await headendClient.ClusterSetup.UploadClusterSetupLogAsync(tempStream, uploadId.ToString("d"), timestampUtc, KubeVersions.NeonKube, clientId.ToString("d"), userId.ToString("d"), preparing);

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
