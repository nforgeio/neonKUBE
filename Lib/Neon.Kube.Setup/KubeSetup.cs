//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.cs
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

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;
using Newtonsoft.Json.Linq;

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
            /// Returns the file owner formatted as: USER:GROUP.
            /// </summary>
            public string Owner { get; private set; }
        }

        //---------------------------------------------------------------------
        // Private constants

        private const string                joinCommandMarker       = "kubeadm join";
        private const int                   defaultMaxParallelNodes = 10;
        private const int                   maxJoinAttempts         = 5;
        private static readonly TimeSpan    joinRetryDelay          = TimeSpan.FromSeconds(5);
        private static readonly TimeSpan    clusterOpTimeout        = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan    clusterOpRetryInterval  = TimeSpan.FromSeconds(10);

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Returns the <see cref="Kubernetes"/> client persisted in the dictionary passed.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The <see cref="Kubernetes"/> client.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there is no persisted client, indicating that <see cref="ConnectCluster(ISetupController)"/>
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var taints = new List<V1Taint>();

            foreach (var node in (await GetK8sClient(controller).ListNodeAsync()).Items.Where(node => node.Metadata.Labels.Any(label => label.Key == labelKey && label.Value == labelValue)))
            {
                if (node.Spec.Taints?.Count() > 0)
                {
                    foreach (var taintList in node.Spec.Taints)
                    {
                        if (!taints.Any(taintList => taintList.Key == taintList.Key && taintList.Effect == taintList.Effect && taintList.Value == taintList.Value))
                        {
                            taints.Add(taintList);
                        }
                    }
                }
            }

            return taints;
        }

        /// <summary>
        /// Downloads and installs any required binaries to the workstation cache if they're not already present.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public static async Task InstallWorkstationBinariesAsync(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster           = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var master            = cluster.FirstMaster;
            var hostPlatform      = KubeHelper.HostPlatform;
            var cachedKubeCtlPath = KubeHelper.GetCachedComponentPath(hostPlatform, "kubectl", KubeVersions.KubernetesVersion);
            var cachedHelmPath    = KubeHelper.GetCachedComponentPath(hostPlatform, "helm", KubeVersions.HelmVersion);

            string kubeCtlUri;
            string helmUri;

            switch (hostPlatform)
            {
                case KubeClientPlatform.Linux:

                    kubeCtlUri = KubeDownloads.KubeCtlLinuxUri;
                    helmUri    = KubeDownloads.HelmLinuxUri;
                    break;

                case KubeClientPlatform.Osx:

                    kubeCtlUri = KubeDownloads.KubeCtlOsxUri;
                    helmUri    = KubeDownloads.HelmOsxUri;
                    break;

                case KubeClientPlatform.Windows:

                    kubeCtlUri = KubeDownloads.KubeCtlWindowsUri;
                    helmUri    = KubeDownloads.HelmWindowsUri;
                    break;

                default:

                    throw new NotSupportedException($"Unsupported workstation platform [{hostPlatform}]");
            }

            // Download the components if they're not already cached.

            var handler = new HttpClientHandler()
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };

            using (var httpClient = new HttpClient(handler, disposeHandler: true))
            {
                if (!File.Exists(cachedKubeCtlPath))
                {
                    controller.LogProgress(master, verb: "download", message: "kubectl");

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
                    controller.LogProgress(master, verb: "download", message: "Helm");

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
                                case KubeClientPlatform.Linux:
                                case KubeClientPlatform.Osx:

                                    throw new NotImplementedException($"Unsupported workstation platform [{hostPlatform}]");

                                case KubeClientPlatform.Windows:

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
                            NeonHelper.DeleteFile(cachedTempHelmPath);
                        }
                    }
                }
            }

            // We're going to assume that the workstation tools are backwards 
            // compatible with older versions of Kubernetes and other infrastructure
            // components and simply compare the installed tool (if present) version
            // with the requested tool version and overwrite the installed tool if
            // the new one is more current.

            KubeHelper.InstallKubeCtl();
            KubeHelper.InstallWorkstationHelm();

            master.Status = string.Empty;
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
            var configFile = Environment.GetEnvironmentVariable("KUBECONFIG").Split(';').Where(variable => variable.Contains("config")).FirstOrDefault();

            if (!string.IsNullOrEmpty(configFile) && File.Exists(configFile))
            {
                // We're using a generated wrapper class to handle transient retries rather than 
                // modifying the built-in base retry policy.  We're really just trying to handle
                // the transients that happen during setup when the API server is unavailable for
                // some reaon (like it's being restarted).

                var k8sClient = new KubernetesWithRetry(KubernetesClientConfiguration.BuildConfigFromConfigFile(configFile, currentContext: cluster.KubeContext.Name));

                k8sClient.RetryPolicy =
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
                        timeout:              TimeSpan.FromSeconds(120));

                controller.Add(KubeSetupProperty.K8sClient, k8sClient);
            }
        }
    }
}
