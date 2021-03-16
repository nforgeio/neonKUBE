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

namespace Neon.Kube
{
    /// <summary>
    /// Implements cluster setup operations.
    /// </summary>
    public static class KubeSetup
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
        // These string constants are used to persist state in [SetupControllers].

        /// <summary>
        /// <para>
        /// Property name for accessing a <c>bool</c> that indicates that we're running cluster prepare/setup in <b>debug mode</b>.
        /// In debug mode, setup works like it did in the past, where we deployed the base node image first and then 
        /// configured the node from that, rather than starting with the node image with assets already prepositioned.
        /// </para>
        /// <para>
        /// This mode is useful when debugging cluster setup or adding new features.
        /// </para>
        /// </summary>
        public const string DebugModeProperty = "debug-setup";

        /// <summary>
        /// Property name for a <c>bool</c> that identifies the base image name to be used for preparing
        /// a cluster in <b>debug mode</b>.  This is the name of the base image file as persisted to our
        /// public S3 bucket.  This will not be set for cluster setup.
        /// </summary>
        public const string BaseImageNameProperty = "base-image-name";

        /// <summary>
        /// Property name for determining the current hosting environment: <see cref="HostingEnvironment"/>,
        /// </summary>
        public const string HostingEnvironmentProperty = "hosting-environment";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="ClusterProxy"/> property.
        /// </summary>
        public const string ClusterProxyProperty = "cluster-proxy";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="ClusterLogin"/> property.
        /// </summary>
        public const string ClusterLoginProperty = "cluster-login";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="IHostingManager"/> property.
        /// </summary>
        public const string HostingManagerProperty = "hosting-manager";

        /// <summary>
        /// Property name for accessing the <see cref="SetupController{NodeMetadata}"/>'s <see cref="Kubernetes"/> client property.
        /// </summary>
        public const string K8sClientProperty = "k8sclient";

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Returns the <see cref="Kubernetes"/> client persisted in the dictionary passed.
        /// </summary>
        /// <param name="setupState">The setup state.</param>
        /// <returns>The <see cref="Kubernetes"/> client.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when there is no persisted client, indicating that <see cref="ConnectCluster(ObjectDictionary)"/>
        /// has not been called yet.
        /// </exception>
        public static IKubernetes GetK8sClient(ObjectDictionary setupState)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));

            try
            {
                return setupState.Get<IKubernetes>(K8sClientProperty);
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
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="labelKey">The target nodes label key.</param>
        /// <param name="labelValue">The target nodes label value.</param>
        /// <returns>The taint list.</returns>
        public static async Task<List<V1Taint>> GetTaintsAsync(ObjectDictionary setupState, string labelKey, string labelValue)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));

            var taints = new List<V1Taint>();

            foreach (var n in (await GetK8sClient(setupState).ListNodeAsync()).Items.Where(n => n.Metadata.Labels.Any(l => l.Key == labelKey && l.Value == labelValue)))
            {
                if (n.Spec.Taints?.Count() > 0)
                {
                    foreach (var t in n.Spec.Taints)
                    {
                        if (!taints.Any(x => x.Key == t.Key && x.Effect == t.Effect && x.Value == t.Value))
                        {
                            taints.Add(t);
                        }
                    }
                }
            }

            return taints;
        }

        /// <summary>
        /// Downloads and installs any required binaries to the workstation cache if they're not already present.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task InstallWorkstationBinariesAsync(ObjectDictionary setupState, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));

            var cluster           = setupState.Get<ClusterProxy>(KubeSetup.ClusterProxyProperty);
            var firstMaster       = cluster.FirstMaster;
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
                    KubeHelper.WriteStatus(statusWriter, "Download", "kubectl");
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
                    KubeHelper.WriteStatus(statusWriter, "Download", "helm");
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

            firstMaster.Status = string.Empty;
        }

        /// <summary>
        /// <para>
        /// Connects to a Kubernetes cluster if it already exists.  This sets the <see cref="K8sClientProperty"/>
        /// property in the setup controller state when Kubernetes is running and a connection has not already 
        /// been established.
        /// </para>
        /// <note>
        /// The <see cref="K8sClientProperty"/> will not be set when Kubernetes has not been started, so 
        /// <see cref="ObjectDictionary.Get{TValue}(string)"/> calls for this property will fail when the
        /// cluster has not been connected yet, which will be useful for debugging setup steps that require
        /// a connection but this hasn't happened yet.
        /// </note>
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        public static void ConnectCluster(ObjectDictionary setupState)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));

            if (setupState.ContainsKey(K8sClientProperty))
            {
                return;     // Already connected
            }

            var cluster    = setupState.Get<ClusterProxy>(ClusterProxyProperty);
            var configFile = Environment.GetEnvironmentVariable("KUBECONFIG").Split(';').Where(s => s.Contains("config")).FirstOrDefault();

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

                                // Exceptions like this happen when a API server can't be established
                                // because the server isn't running or ready.

                                if (exceptionType == typeof(HttpRequestException) && exception.InnerException != null && exception.InnerException.GetType() == typeof(SocketException))
                                {
                                    return true;
                                }

                                // We also see this sometimes when the API server isn't ready.

                                if (exceptionType == typeof(HttpOperationException) && ((HttpOperationException)exception).Response.StatusCode == HttpStatusCode.Forbidden)
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

                setupState.Add(K8sClientProperty, k8sClient);
            }
        }

        /// <summary>
        /// Configures a local HAProxy container that makes the Kubernetes Etc
        /// cluster highly available.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="node">The node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static void SetupEtcdHaProxy(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> node, Action<string> statusWriter = null)
        {
            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            KubeHelper.WriteStatus(statusWriter, "Setup", "etc HA");
            node.Status = "setup: etcd HA";

            var sbHaProxyConfig = new StringBuilder();

            sbHaProxyConfig.Append(
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

frontend harbor_http
    bind                    *:80
    mode                    http
    log                     global
    option                  httplog
    default_backend         harbor_backend_http

frontend harbor
    bind                    *:443
    mode                    tcp
    log                     global
    option                  tcplog
    default_backend         harbor_backend

backend kubernetes_masters_backend
    mode                    tcp
    balance                 roundrobin");

            foreach (var master in cluster.Masters)
            {
                sbHaProxyConfig.Append(
$@"
    server {master.Name}         {master.Address}:{KubeNodePorts.KubeApiServer}");
            }

            sbHaProxyConfig.Append(
$@"
backend harbor_backend_http
    mode                    http
    balance                 roundrobin");

            foreach (var master in cluster.Masters)
            {
                sbHaProxyConfig.Append(
$@"
    server {master.Name}         {master.Address}:30080");
            }

            sbHaProxyConfig.Append(
$@"
backend harbor_backend
    mode                    tcp
    balance                 roundrobin");

            foreach (var master in cluster.Masters)
            {
                sbHaProxyConfig.Append(
$@"
    server {master.Name}         {master.Address}:30443");
            }

            node.UploadText(" /etc/neonkube/neon-etcd-proxy.cfg", sbHaProxyConfig);

            var sbHaProxyPod = new StringBuilder();

            sbHaProxyPod.Append(
$@"
apiVersion: v1
kind: Pod
metadata:
  name: neon-etcd-proxy
  namespace: kube-system
  labels:
    app: neon-etcd-proxy
    role: neon-etcd-proxy
    release: neon-etcd-proxy
spec:
  volumes:
   - name: neon-etcd-proxy-config
     hostPath:
       path: /etc/neonkube/neon-etcd-proxy.cfg
       type: File
  hostNetwork: true
  containers:
    - name: web
      image: {KubeConst.NeonContainerRegistery(setupState)}/haproxy:{KubeVersions.HaproxyVersion}
      volumeMounts:
        - name: neon-etcd-proxy-config
          mountPath: /etc/haproxy/haproxy.cfg
      ports:
        - name: k8s-masters
          containerPort: 6442
          protocol: TCP
");
            node.UploadText("/etc/kubernetes/manifests/neon-etcd-proxy.yaml", sbHaProxyPod, permissions: "600", owner: "root:root");
        }

        /// <summary>
        /// Adds the Kubernetes node labels.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The first master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task LabelNodesAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            await master.InvokeIdempotentAsync("setup/label-nodes",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Label", "Nodes");
                    master.Status = "label: nodes";

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
                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Initializes the cluster on the first manager, then joins the remaining
        /// masters and workers to the cluster.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="maxParallel">
        /// The maximum number of operations on separate nodes to be performed in parallel.
        /// This defaults to <see cref="defaultMaxParallelNodes"/>.
        /// </param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task SetupClusterAsync(ObjectDictionary setupState, int maxParallel = defaultMaxParallelNodes, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            var cluster      = setupState.Get<ClusterProxy>(ClusterProxyProperty);
            var clusterLogin = setupState.Get<ClusterLogin>(ClusterLoginProperty);
            var firstMaster  = cluster.FirstMaster;
            var debugMode    = setupState.Get<bool>(KubeSetup.DebugModeProperty);

            cluster.ClearStatus();

            ConfigureKubernetes(setupState, cluster.FirstMaster, statusWriter);
            ConfigureWorkstation(setupState, firstMaster, statusWriter);
            ConnectCluster(setupState);

            // We need to taint before deploying pods.

            await ConfigureMasterTaintsAsync(setupState, firstMaster, statusWriter);

            // Run configuration tasks in parallel when not [--debug] mode.

            if (debugMode)
            {
                await TaintNodesAsync(setupState, statusWriter);
                await LabelNodesAsync(setupState, firstMaster, statusWriter);
                await NeonHelper.WaitAllAsync(await CreateNamespacesAsync(setupState, firstMaster, statusWriter));
                await CreateRootUserAsync(setupState, firstMaster, statusWriter);
                await InstallCalicoCniAsync(setupState, firstMaster, statusWriter);
                await InstallIstioAsync(setupState, firstMaster, statusWriter);
                await InstallMetricsServerAsync(setupState, firstMaster, statusWriter);
            }
            else
            {
                var tasks = new List<Task>();

                tasks.Add(TaintNodesAsync(setupState, statusWriter));
                tasks.Add(LabelNodesAsync(setupState, firstMaster, statusWriter));
                tasks.AddRange(await CreateNamespacesAsync(setupState, firstMaster, statusWriter));
                tasks.Add(CreateRootUserAsync(setupState, firstMaster, statusWriter));
                tasks.Add(InstallCalicoCniAsync(setupState, firstMaster, statusWriter));
                tasks.Add(InstallIstioAsync(setupState, firstMaster, statusWriter));
                tasks.Add(InstallMetricsServerAsync(setupState, firstMaster, statusWriter));

                await NeonHelper.WaitAllAsync(tasks);
            }

            if (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count() >= 3)
            {
                await InstallEtcdAsync(setupState, firstMaster, statusWriter);
            }

            // Additional configuration.

            if (debugMode)
            {
                await InstallKialiAsync(setupState, firstMaster, statusWriter);
                await InstallKubeDashboardAsync(setupState, firstMaster, statusWriter);
                await InstallOpenEBSAsync(setupState, firstMaster, statusWriter);
                await InstallPrometheusAsync(setupState, firstMaster, statusWriter);
                await InstallSystemDbAsync(setupState, firstMaster, statusWriter);
                await InstallMinioAsync(setupState, firstMaster, statusWriter);
                await InstallClusterManagerAsync(setupState, firstMaster, statusWriter);
                await InstallContainerRegistryAsync(setupState, firstMaster, statusWriter);
                await NeonHelper.WaitAllAsync(await SetupMonitoringAsync(setupState, statusWriter));
            }
            else
            {
                var tasks = new List<Task>();

                tasks.Add(InstallKialiAsync(setupState, firstMaster, statusWriter));
                tasks.Add(InstallKubeDashboardAsync(setupState, firstMaster, statusWriter));
                await InstallOpenEBSAsync(setupState, firstMaster, statusWriter);
                await InstallPrometheusAsync(setupState, firstMaster, statusWriter);
                await InstallSystemDbAsync(setupState, firstMaster, statusWriter);
                await InstallMinioAsync(setupState, firstMaster, statusWriter);
                tasks.Add(InstallClusterManagerAsync(setupState, firstMaster, statusWriter));
                tasks.Add(InstallContainerRegistryAsync(setupState, firstMaster, statusWriter));
                tasks.AddRange(await SetupMonitoringAsync(setupState, statusWriter));

                await NeonHelper.WaitAllAsync(tasks);
            }
        }

        /// <summary>
        /// Basic Kubernetes cluster initialization.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="firstMaster">The first master node.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static void ConfigureKubernetes(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> firstMaster, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(firstMaster != null, nameof(firstMaster));

            var hostingEnvironment = setupState.Get<HostingEnvironment>(KubeSetup.HostingEnvironmentProperty);
            var cluster            = setupState.Get<ClusterProxy>(ClusterProxyProperty);
            var clusterLogin       = setupState.Get<ClusterLogin>(ClusterLoginProperty);

            firstMaster.InvokeIdempotent("setup/cluster-init",
                () =>
                {
                    //---------------------------------------------------------
                    // Initialize the cluster on the first master:

                    KubeHelper.WriteStatus(statusWriter, "Create", "Cluster");
                    firstMaster.Status = "create: cluster";

                    // Initialize Kubernetes:

                    firstMaster.InvokeIdempotent("setup/kubernetes-init",
                        () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Initialize", "Kubernetes");
                            firstMaster.Status = "initialize: kubernetes";

                            // It's possible that a previous cluster initialization operation
                            // was interrupted.  This command resets the state.

                            firstMaster.SudoCommand("kubeadm reset --force");

                            SetupEtcdHaProxy(setupState, firstMaster, statusWriter);

                            KubeHelper.WriteStatus(statusWriter, "Initialize", "Cluster");
                            firstMaster.Status = "initialize: cluster";

                            // Configure the control plane's API server endpoint and initialize
                            // the certificate SAN names to include each master IP address as well
                            // as the HOSTNAME/ADDRESS of the API load balancer (if any).

                            var controlPlaneEndpoint = $"kubernetes-masters:6442";
                            var sbCertSANs           = new StringBuilder();

                            if (hostingEnvironment == HostingEnvironment.Wsl2)
                            {
                                // Tweak the API server endpoint for WSL2.

                                controlPlaneEndpoint = $"localhost:{KubeNodePorts.KubeApiServer}";
                            }

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

                            var kubeletFailSwapOnLine           = string.Empty;
                            var kubeInitgnoreSwapOnPreflightArg = string.Empty;

                            if (hostingEnvironment == HostingEnvironment.Wsl2)
                            {
                                // SWAP will be enabled by the default Microsoft WSL2 kernel which
                                // will cause Kubernetes to complain because this isn't a supported
                                // configuration.  We need to disable these error checks.

                                kubeletFailSwapOnLine = "failSwapOn: false";
                                kubeInitgnoreSwapOnPreflightArg = "--ignore-preflight-errors=Swap";
                            }

                            var clusterConfig = new StringBuilder();

                            clusterConfig.AppendLine(
$@"
apiVersion: kubeadm.k8s.io/v1beta2
kind: ClusterConfiguration
clusterName: {cluster.Name}
kubernetesVersion: ""v{KubeVersions.KubernetesVersion}""
imageRepository: ""{KubeConst.NeonContainerRegistery(setupState)}""
apiServer:
  extraArgs:
    bind-address: 0.0.0.0
    logging-format: json
    default-not-ready-toleration-seconds: ""30"" # default 300
    default-unreachable-toleration-seconds: ""30"" #default  300
    allow-privileged: ""true""
  certSANs:
{sbCertSANs}
controlPlaneEndpoint: ""{controlPlaneEndpoint}""
networking:
  podSubnet: ""{cluster.Definition.Network.PodSubnet}""
  serviceSubnet: ""{cluster.Definition.Network.ServiceSubnet}""
controllerManager:
  extraArgs:
    logging-format: json
    node-monitor-grace-period: 15s #default 40s
    node-monitor-period: 5s #default 5s
    pod-eviction-timeout: 30s #default 5m0s
scheduler:
  extraArgs:
    logging-format: json");

                            if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2)
                            {
                                clusterConfig.AppendLine($@"
etcd:
  local:
    extraArgs:
        listen-peer-urls: https://127.0.0.1:2380
        listen-client-urls: https://127.0.0.1:2379
        advertise-client-urls: https://127.0.0.1:2379
        initial-advertise-peer-urls: https://127.0.0.1:2380
        initial-cluster=master-0: https://127.0.0.1:2380");
                            }

                            clusterConfig.AppendLine($@"
---
apiVersion: kubelet.config.k8s.io/v1beta1
kind: KubeletConfiguration
logging:
  format: json
nodeStatusReportFrequency: 4s
volumePluginDir: /var/lib/kubelet/volume-plugins
{kubeletFailSwapOnLine}
");

                            var kubeInitScript =
$@"
systemctl enable kubelet.service
kubeadm init --config cluster.yaml --ignore-preflight-errors=DirAvailable--etc-kubernetes-manifests
";
                            var response = firstMaster.SudoCommand(CommandBundle.FromScript(kubeInitScript).AddFile("cluster.yaml", clusterConfig.ToString()));

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
                                clusterLogin.SetupDetails.ClusterJoinCommand = Regex.Replace(output.Substring(pStart).Trim(), @"\t|\n|\r|\\", "");
                            }
                            else
                            {
                                clusterLogin.SetupDetails.ClusterJoinCommand = Regex.Replace(output.Substring(pStart, pEnd - pStart).Trim(), @"\t|\n|\r|\\", "");
                            }

                            clusterLogin.Save();

                            KubeHelper.WriteStatus(statusWriter, "Cluster", "Created");
                            firstMaster.Status = "cluster created";
                        });

                    firstMaster.InvokeIdempotent("setup/kubectl",
                        () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Setup", "Kubectl");
                            firstMaster.Status = "setup: kubectl";

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

                    if (clusterLogin.SetupDetails.MasterFiles != null)
                    {
                        clusterLogin.SetupDetails.MasterFiles = new Dictionary<string, KubeFileDetails>();
                    }

                    if (clusterLogin.SetupDetails.MasterFiles.Count == 0)
                    {
                        // I'm hardcoding the permissions and owner here.  It would be nice to
                        // scrape this from the source files in the future but it's not worth
                        // the bother at this point.

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

                            clusterLogin.SetupDetails.MasterFiles[file.Path] = new KubeFileDetails(text, permissions: file.Permissions, owner: file.Owner);
                        }
                    }

                    // Persist the cluster join command and downloaded master files.

                    clusterLogin.Save();

                    //---------------------------------------------------------
                    // Join the remaining masters to the cluster:

                    foreach (var master in cluster.Masters.Where(m => m != firstMaster))
                    {
                        try
                        {
                            master.InvokeIdempotent("setup/kubectl",
                                () =>
                                {
                                    KubeHelper.WriteStatus(statusWriter, "Setup", "Kubectl");
                                    firstMaster.Status = "setup: kubectl";

                                    // It's possible that a previous cluster join operation
                                    // was interrupted.  This command resets the state.

                                    master.SudoCommand("kubeadm reset --force");

                                    // The other (non-boot) masters need files downloaded from the boot master.

                                    KubeHelper.WriteStatus(statusWriter, "Upload", "master files");
                                    master.Status = "upload: master files";

                                    foreach (var file in clusterLogin.SetupDetails.MasterFiles)
                                    {
                                        master.UploadText(file.Key, file.Value.Text, permissions: file.Value.Permissions, owner: file.Value.Owner);
                                    }

                                    // Join the cluster:

                                    master.InvokeIdempotent("setup/master-join",
                                        () =>
                                        {
                                            KubeHelper.WriteStatus(statusWriter, "Join", "Master to cluster");
                                            firstMaster.Status = "join: master to cluster";

                                            SetupEtcdHaProxy(setupState, master, statusWriter);

                                            var joined = false;

                                            KubeHelper.WriteStatus(statusWriter, "Join", "As MASTER");
                                            master.Status = "join: as master";

                                            master.SudoCommand("podman run",
                                                   "--name=neon-etcd-proxy",
                                                   "--detach",
                                                   "--restart=always",
                                                   "-v=/etc/neonkube/neon-etcd-proxy.cfg:/etc/haproxy/haproxy.cfg",
                                                   "--network=host",
                                                   "--log-driver=k8s-file",
                                                   $"{KubeConst.NeonContainerRegistery(setupState)}/haproxy:neonkube-{KubeConst.NeonKubeVersion}"
                                               );

                                            for (int attempt = 0; attempt < maxJoinAttempts; attempt++)
                                            {
                                                var response = master.SudoCommand(clusterLogin.SetupDetails.ClusterJoinCommand + " --control-plane --ignore-preflight-errors=DirAvailable--etc-kubernetes-manifests", RunOptions.Defaults & ~RunOptions.FaultOnError);

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

                                            master.SudoCommand("docker kill neon-etcd-proxy");
                                            master.SudoCommand("docker rm neon-etcd-proxy");
                                        });
                                });
                        }
                        catch (Exception e)
                        {
                            master.Fault(NeonHelper.ExceptionError(e));
                            master.LogException(e);
                        }

                        KubeHelper.WriteStatus(statusWriter, "Joined");
                        master.Status = "joined";
                    }

                    // Configure [kube-apiserver] on all the masters

                    foreach (var master in cluster.Masters)
                    {
                        try
                        {
                            master.InvokeIdempotent("setup/kubernetes-apiserver",
                                () =>
                                {
                                    KubeHelper.WriteStatus(statusWriter, "Configure", "Kubernetes API Server");
                                    master.Status = "configure: kubernetes apiserver";

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
                        MaxDegreeOfParallelism = defaultMaxParallelNodes
                    };

                    Parallel.ForEach(cluster.Workers, parallelOptions,
                        worker =>
                        {
                            try
                            {
                                worker.InvokeIdempotent("setup/worker-join",
                                    () =>
                                    {
                                        KubeHelper.WriteStatus(statusWriter, "Join", "Worker to Cluster");
                                        firstMaster.Status = "join: worker to cluster";

                                        SetupEtcdHaProxy(setupState, worker, statusWriter);

                                        var joined = false;

                                        KubeHelper.WriteStatus(statusWriter, "Join", "As WORKER");
                                        worker.Status = "join: as worker";

                                        worker.SudoCommand("podman run",
                                            "--name=neon-etcd-proxy",
                                            "--detach",
                                            "--restart=always",
                                            "-v=/etc/neonkube/neon-etcd-proxy.cfg:/etc/haproxy/haproxy.cfg",
                                            "--network=host",
                                            "--log-driver=k8s-file",
                                            $"{KubeConst.NeonContainerRegistery(setupState)}/haproxy:neonkube-{KubeConst.NeonKubeVersion}"
                                        );

                                        for (int attempt = 0; attempt < maxJoinAttempts; attempt++)
                                        {
                                            var response = worker.SudoCommand(clusterLogin.SetupDetails.ClusterJoinCommand + " --ignore-preflight-errors=DirAvailable--etc-kubernetes-manifests", RunOptions.Defaults & ~RunOptions.FaultOnError);

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

                                        worker.SudoCommand("docker kill neon-etcd-proxy");
                                        worker.SudoCommand("docker rm neon-etcd-proxy");
                                    });
                            }
                            catch (Exception e)
                            {
                                worker.Fault(NeonHelper.ExceptionError(e));
                                worker.LogException(e);
                            }

                            KubeHelper.WriteStatus(statusWriter, "Joined");
                            worker.Status = "joined";
                        });
                });
        }

        /// <summary>
        /// Configures the local workstation.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static void ConfigureWorkstation(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            master.InvokeIdempotent("setup/workstation",
                () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Workstation");
                    master.Status = "setup : workstation";

                    var cluster        = setupState.Get<ClusterProxy>(ClusterProxyProperty);
                    var clusterLogin   = setupState.Get<ClusterLogin>(ClusterLoginProperty);
                    var kubeConfigPath = KubeHelper.KubeConfigPath;

                    // Update kubeconfig.

                    // $todo(marcusbooyah):
                    //
                    // This is hardcoding the kubeconfig to point to the first master.  Issue 
                    // https://github.com/nforgeio/neonKUBE/issues/888 will fix this by adding a proxy
                    // to neonDESKTOP and load balancing requests across the k8s api servers.

                    var configText  = clusterLogin.SetupDetails.MasterFiles["/etc/kubernetes/admin.conf"].Text;
                    var firstMaster = cluster.Definition.SortedMasterNodes.First();

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
                });
        }

        /// <summary>
        /// Installs the Calico CNI.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task InstallCalicoCniAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;

            await master.InvokeIdempotentAsync("setup/cni",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Calico");
                    master.Status = "setup: calico";

                    // Deploy Calico
                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>("images.organization", KubeConst.NeonContainerRegistery(setupState)));

                    if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2)
                    {
                        values.Add(new KeyValuePair<string, object>($"neonDesktop", $"true"));
                        values.Add(new KeyValuePair<string, object>($"kubernetes.service.host", $"localhost"));
                        values.Add(new KeyValuePair<string, object>($"kubernetes.service.port", KubeNodePorts.KubeApiServer));

                    }
                    await master.InstallHelmChartAsync("calico", releaseName: "calico", @namespace: "kube-system", values: values, statusWriter: statusWriter);

                    // Wait for Calico and CoreDNS pods to report that they're running.
                    // We're going to wait a maximum of 300 seconds.

                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            var pods = await GetK8sClient(setupState).ListPodForAllNamespacesAsync();

                            foreach (var pod in pods.Items)
                            {
                                if (pod.Status.Phase != "Running")
                                {
                                    if (pod.Metadata.Name.Contains("coredns") && pod.Status.Phase == "Pending")
                                    {
                                        master.SudoCommand("kubectl rollout restart --namespace kube-system deployment/coredns", RunOptions.LogOnErrorOnly);
                                    }

                                    await Task.Delay(5000);

                                    return false;
                                }
                            }

                            return true;
                        },
                        timeout: clusterOpTimeout,
                        pollInterval: clusterOpRetryInterval);

                    await master.InvokeIdempotentAsync("setup/cni-ready",
                        async () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Wait", "for Calico");
                            master.Status = "wait: for calico";

                            var pods = await GetK8sClient(setupState).CreateNamespacedPodAsync(
                                new V1Pod()
                                {
                                    Metadata = new V1ObjectMeta()
                                    {
                                        Name              = "dnsutils",
                                        NamespaceProperty = "default"
                                    },
                                    Spec = new V1PodSpec()
                                    {
                                        Containers = new List<V1Container>()
                                        {
                                            new V1Container()
                                            {
                                                Name            = "dnsutils",
                                                Image           = $"{KubeConst.NeonContainerRegistery(setupState)}/kubernetes-e2e-test-images-dnsutils:1.3",
                                                Command         = new List<string>() {"sleep", "3600" },
                                                ImagePullPolicy = "IfNotPresent"
                                            }
                                        },
                                        RestartPolicy = "Always"
                                    }
                                }, "default");
                        });


                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            var result = master.SudoCommand("kubectl exec -i -t dnsutils -- nslookup kubernetes.default", RunOptions.LogOutput);

                            if (result.Success)
                            {
                                await GetK8sClient(setupState).DeleteNamespacedPodAsync("dnsutils", "default");
                                return await Task.FromResult(true);
                            }
                            else
                            {
                                master.SudoCommand("kubectl rollout restart --namespace kube-system deployment/coredns", RunOptions.LogOnErrorOnly);
                                await Task.Delay(5000);
                                return await Task.FromResult(false);
                            }
                        },
                        timeout: clusterOpTimeout,
                        pollInterval: clusterOpRetryInterval);
                });
        }

        /// <summary>
        /// Configures pods to be schedule on masters when enabled.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task ConfigureMasterTaintsAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;

            await master.InvokeIdempotentAsync("setup/kubernetes-master-taints",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Configure", "Master Taints");
                    master.Status = "configure: master taints";

                    // The [kubectl taint] command looks like it can return a non-zero exit code.
                    // We'll ignore this.

                    if (cluster.Definition.Kubernetes.AllowPodsOnMasters.GetValueOrDefault())
                    {
                        master.SudoCommand(@"until [ `kubectl get nodes | grep ""NotReady"" | wc -l ` == ""0"" ]; do sleep 1; done", master.DefaultRunOptions & ~RunOptions.FaultOnError);
                        master.SudoCommand("kubectl taint nodes --all node-role.kubernetes.io/master-", master.DefaultRunOptions & ~RunOptions.FaultOnError);
                        master.SudoCommand(@"until [ `kubectl get nodes -o json | jq .items[].spec | grep ""NoSchedule"" | wc -l ` == ""0"" ]; do sleep 1; done", master.DefaultRunOptions & ~RunOptions.FaultOnError);
                    }
                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Installs the Kubernetes Metrics Server service.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task InstallMetricsServerAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;

            await master.InvokeIdempotentAsync("setup/kubernetes-metrics-server",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Kubernetes Metrics Server");
                    master.Status = "setup: kubernetes-metrics-server";

                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                        i++;
                    }

                    await master.InstallHelmChartAsync("metrics-server", releaseName: "metrics-server", @namespace: "kube-system", values: values, statusWriter: statusWriter);
                });
        }

        /// <summary>
        /// Installs Istio.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task InstallIstioAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/istio",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Istio");
                    master.Status = "setup: istio";

                    var istioScript0 =
$@"
tmp=$(mktemp -d /tmp/istioctl.XXXXXX)
cd ""$tmp"" || exit

curl -fsLO {KubeDownloads.IstioLinuxUri}

tar -xzf ""istioctl-{KubeVersions.IstioVersion}-linux-amd64.tar.gz""

# setup istioctl
cd ""$HOME"" || exit
mkdir -p "".istioctl/bin""
mv ""${{tmp}}/istioctl"" "".istioctl/bin/istioctl""
chmod +x "".istioctl/bin/istioctl""
rm -r ""${{tmp}}""

export PATH=$PATH:$HOME/.istioctl/bin

istioctl operator init --hub={KubeConst.NeonContainerRegistery(setupState)} --tag={KubeVersions.IstioVersion}-distroless

kubectl create ns istio-system

cat <<EOF > istio-cni.yaml
apiVersion: install.istio.io/v1alpha1
kind: IstioOperator
metadata:
  namespace: istio-system
  name: istiocontrolplane
spec:
  hub: {KubeConst.NeonContainerRegistery(setupState)}
  tag: {KubeVersions.IstioVersion}-distroless
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
        service:
          ports:
          - name: http2
            protocol: TCP
            port: 80
            targetPort: 8080
            nodePort: 30080
          - name: https
            protocol: TCP
            port: 443
            targetPort: 8443
            nodePort: 30443
          - name: tls
            protocol: TCP
            port: 15443
            targetPort: 15443
            nodePort: 31922
        resources:
          requests:
            cpu: 10m
            memory: 64Mi
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
      logging:
        level: ""default:info""
      logAsJson: true
      imagePullPolicy: IfNotPresent
      proxy:
        resources:
          limits:
            cpu: 2000m
            memory: 1024Mi
          requests:
            cpu: 10m
            memory: 64Mi
      defaultNodeSelector: 
        neonkube.io/istio: true
      tracer:
        zipkin:
          address: neon-logging-jaeger-collector.monitoring.svc.cluster.local:9411
    pilot:
      traceSampling: 100
    meshConfig:
      accessLogFile: """"
      accessLogFormat: '{{   ""authority"": ""%REQ(:AUTHORITY)%"",   ""mode"": ""%PROTOCOL%"",   ""upstream_service_time"": ""%RESP(X-ENVOY-UPSTREAM-SERVICE-TIME)%"",   ""upstream_local_address"": ""%UPSTREAM_LOCAL_ADDRESS%"",   ""duration"": ""%DURATION%"",   ""request_duration"": ""%REQUEST_DURATION%"",   ""response_duration"": ""%RESPONSE_DURATION%"",   ""response_tx_duration"": ""%RESPONSE_TX_DURATION%"",   ""downstream_local_address"": ""%DOWNSTREAM_LOCAL_ADDRESS%"",   ""upstream_transport_failure_reason"": ""%UPSTREAM_TRANSPORT_FAILURE_REASON%"",   ""route_name"": ""%ROUTE_NAME%"",   ""response_code"": ""%RESPONSE_CODE%"",   ""response_code_details"": ""%RESPONSE_CODE_DETAILS%"",   ""user_agent"": ""%REQ(USER-AGENT)%"",   ""response_flags"": ""%RESPONSE_FLAGS%"",   ""start_time"": ""%START_TIME(%s.%6f)%"",   ""method"": ""%REQ(:METHOD)%"",   ""host"": ""%REQ(:Host)%"",   ""referer"": ""%REQ(:Referer)%"",   ""request_id"": ""%REQ(X-REQUEST-ID)%"",   ""forwarded_host"": ""%REQ(X-FORWARDED-HOST)%"",   ""forwarded_proto"": ""%REQ(X-FORWARDED-PROTO)%"",   ""upstream_host"": ""%UPSTREAM_HOST%"",   ""downstream_local_uri_san"": ""%DOWNSTREAM_LOCAL_URI_SAN%"",   ""downstream_peer_uri_san"": ""%DOWNSTREAM_PEER_URI_SAN%"",   ""downstream_local_subject"": ""%DOWNSTREAM_LOCAL_SUBJECT%"",   ""downstream_peer_subject"": ""%DOWNSTREAM_PEER_SUBJECT%"",   ""downstream_peer_issuer"": ""%DOWNSTREAM_PEER_ISSUER%"",   ""downstream_tls_session_id"": ""%DOWNSTREAM_TLS_SESSION_ID%"",   ""downstream_tls_cipher"": ""%DOWNSTREAM_TLS_CIPHER%"",   ""downstream_tls_version"": ""%DOWNSTREAM_TLS_VERSION%"",   ""downstream_peer_serial"": ""%DOWNSTREAM_PEER_SERIAL%"",   ""downstream_peer_cert"": ""%DOWNSTREAM_PEER_CERT%"",   ""client_ip"": ""%REQ(X-FORWARDED-FOR)%"",   ""requested_server_name"": ""%REQUESTED_SERVER_NAME%"",   ""bytes_received"": ""%BYTES_RECEIVED%"",   ""bytes_sent"": ""%BYTES_SENT%"",   ""upstream_cluster"": ""%UPSTREAM_CLUSTER%"",   ""downstream_remote_address"": ""%DOWNSTREAM_REMOTE_ADDRESS%"",   ""path"": ""%REQ(X-ENVOY-ORIGINAL-PATH?:PATH)%"" }}'
      accessLogEncoding: ""JSON""
    gateways:
      istio-ingressgateway:
        type: NodePort
        externalTrafficPolicy: Local
        sds:
          enabled: true
    prometheus:
      enabled: false
    grafana:
      enabled: false
    istiocoredns:
      enabled: true
      coreDNSImage: {KubeConst.NeonContainerRegistery(setupState)}/coredns-coredns
      coreDNSTag: {KubeVersions.CoreDNSVersion}
      coreDNSPluginImage: {KubeConst.NeonContainerRegistery(setupState)}/coredns-plugin:{KubeVersions.CoreDNSPluginVersion}
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
                    master.SudoCommand(CommandBundle.FromScript(istioScript0));
                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Configures the root Kubernetes user.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task CreateRootUserAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/root-user",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Create", "Kubernetes Root User");
                    master.Status = "create: kubernetes root user";

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
                    master.KubectlApply(userYaml);

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Configures the root Kubernetes user.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task InstallKubeDashboardAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster      = setupState.Get<ClusterProxy>(ClusterProxyProperty);
            var clusterLogin = setupState.Get<ClusterLogin>(ClusterLoginProperty);

            master.InvokeIdempotent("setup/kube-dashboard",
                () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Kubernetes Dashboard");
                    master.Status = "setup: kubernetes dashboard";

                    if (clusterLogin.DashboardCertificate != null)
                    {
                        KubeHelper.WriteStatus(statusWriter, "Generate", "Dashboard Certificate");
                        master.Status = "generate: dashboard certificate";

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

                        var utcNow = DateTime.UtcNow;
                        var utc10Years = utcNow.AddYears(10);

                        var certificate = TlsCertificate.CreateSelfSigned(
                            hostnames: masterAddresses,
                            validDays: (int)(utc10Years - utcNow).TotalDays,
                            issuedBy:  "kubernetes-dashboard");

                        clusterLogin.DashboardCertificate = certificate.CombinedPem;
                        clusterLogin.Save();
                    }

                    // Deploy the dashboard.  Note that we need to insert the base-64
                    // encoded certificate and key PEM into the dashboard configuration
                    // YAML first.

                    KubeHelper.WriteStatus(statusWriter, "Deploy", "Kubernetes Dashboard");
                    master.Status = "deploy: kubernetes dashboard";

                    var dashboardYaml =
$@"# Copyright 2017 The Kubernetes Authors.
#
# Licensed under the Apache License, Version 2.0 (the """"License"""");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
# http://www.apache.org/licenses/LICENSE-2.0
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
    nodePort: {KubeNodePorts.KubeDashboard}
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
          image: {KubeConst.NeonContainerRegistery(setupState)}/kubernetesui-dashboard:v{KubeVersions.KubernetesDashboardVersion}
          imagePullPolicy: IfNotPresent
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
          image: {KubeConst.NeonContainerRegistery(setupState)}/kubernetesui-metrics-scraper:{KubeVersions.KubernetesDashboardMetricsVersion}
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

                    var dashboardCert = TlsCertificate.Parse(clusterLogin.DashboardCertificate);
                    var variables     = new Dictionary<string, string>();

                    variables.Add("CERTIFICATE", Convert.ToBase64String(Encoding.UTF8.GetBytes(dashboardCert.CertPemNormalized)));
                    variables.Add("PRIVATEKEY", Convert.ToBase64String(Encoding.UTF8.GetBytes(dashboardCert.KeyPemNormalized)));

                    using (var preprocessReader =
                        new PreprocessReader(dashboardYaml, variables)
                        {
                            StripComments = false,
                            ProcessStatements = false
                        }
                    )
                    {
                        dashboardYaml = preprocessReader.ReadToEnd();
                    }

                    master.KubectlApply(dashboardYaml);
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Adds the node taints.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        public static async Task TaintNodesAsync(ObjectDictionary setupState, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));

            var cluster     = setupState.Get<ClusterProxy>(ClusterProxyProperty);
            var firstMaster = cluster.FirstMaster;

            await firstMaster.InvokeIdempotentAsync("setup/taint-nodes",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Taint", "Nodes");
                    firstMaster.Status = "taint: nodes";

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

                        firstMaster.SudoCommand(CommandBundle.FromScript(sbScript));
                    }
                    finally
                    {
                        firstMaster.Status = string.Empty;
                    }

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Deploy Kiali.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        private static async Task InstallKialiAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/kiali",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Kaili");
                    master.Status = "setup: kiali";

                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("cr.spec.deployment.image_name", $"{KubeConst.NeonContainerRegistery(setupState)}/kiali-kiali"));

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelIstio, "true"))
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                        i++;
                    }

                    await master.InstallHelmChartAsync("kiali", releaseName: "kiali-operator", @namespace: "istio-system", values: values, statusWriter: statusWriter);
                });

            await master.InvokeIdempotentAsync("setup/kiali-ready",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Wait", "for Kaili");
                    master.Status = "wait: for kaili";

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(setupState, "istio-system", "kiali-operator"),
                            WaitForDeploymentAsync(setupState, "istio-system", "kiali")
                        });
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Some initial kubernetes configuration.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task KubeSetupAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/initial-kubernetes", async
                () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Kubernetes");
                    master.Status = "setup: kubernetes";

                    await master.InstallHelmChartAsync("cluster_setup", statusWriter: statusWriter);
                });
        }

        /// <summary>
        /// Installs OpenEBS.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallOpenEBSAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            await master.InvokeIdempotentAsync("setup/openebs-all",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Deploy", "OpenEBS");
                    master.Status = "deploy: openebs";

                    master.InvokeIdempotent("setup/openebs-namespace",
                        () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Deploy", "OpenEBS Namespace");
                            master.Status = "deploy: openebs-namespace";

                            GetK8sClient(setupState).CreateNamespace(new V1Namespace()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name = "openebs",
                                    Labels = new Dictionary<string, string>()
                                    {
                                        { "istio-injection", "disabled" }
                                    }
                                }
                            });
                        });

                    await master.InvokeIdempotentAsync("setup/openebs",
                        async () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Setup", "OpenEBS");
                            master.Status = "setup: openebs";

                            var values = new List<KeyValuePair<string, object>>();

                            values.Add(new KeyValuePair<string, object>("apiserver.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("helper.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("localprovisioner.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("policies.monitoring.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("snapshotOperator.controller.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("snapshotOperator.provisioner.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("provisioner.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("ndm.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("ndmOperator.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("webhook.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("jiva.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                            if (cluster.Definition.Workers.Count() >= 3)
                            {
                                var replicas = Math.Max(1, cluster.Definition.Workers.Count() / 3);

                                values.Add(new KeyValuePair<string, object>($"apiserver.replicas", replicas));
                                values.Add(new KeyValuePair<string, object>($"provisioner.replicas", replicas));
                                values.Add(new KeyValuePair<string, object>($"localprovisioner.replicas", replicas));
                                values.Add(new KeyValuePair<string, object>($"snapshotOperator.replicas", replicas));
                                values.Add(new KeyValuePair<string, object>($"ndmOperator.replicas", 1));
                                values.Add(new KeyValuePair<string, object>($"webhook.replicas", replicas));
                            }

                            await master.InstallHelmChartAsync("openebs", releaseName: "neon-storage-openebs", values: values, @namespace: "openebs", statusWriter: statusWriter);
                        });

                    if (cluster.HostingManager.HostingEnvironment != HostingEnvironment.Wsl2)
                    {
                        await master.InvokeIdempotentAsync("setup/openebs-cstor",
                            async () =>
                            {
                                KubeHelper.WriteStatus(statusWriter, "Setup", "OpenEBS cStor");
                                master.Status = "setup: openebs cstor";

                                var values = new List<KeyValuePair<string, object>>();

                                values.Add(new KeyValuePair<string, object>("cspcOperator.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("cspcOperator.poolManager.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("cspcOperator.cstorPool.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("cspcOperator.cstorPoolExporter.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                                values.Add(new KeyValuePair<string, object>("cvcOperator.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("cvcOperator.target.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("cvcOperator.volumeMgmt.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("cvcOperator.volumeExporter.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                                values.Add(new KeyValuePair<string, object>("csiController.resizer.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("csiController.snapshotter.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("csiController.snapshotController.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("csiController.attacher.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("csiController.provisioner.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                                values.Add(new KeyValuePair<string, object>("csiController.driverRegistrar.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                                values.Add(new KeyValuePair<string, object>("cstorCSIPlugin.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                                values.Add(new KeyValuePair<string, object>("csiNode.driverRegistrar.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                                values.Add(new KeyValuePair<string, object>("admissionServer.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                                await master.InstallHelmChartAsync("openebs_cstor_operator", releaseName: "neon-storage-openebs-cstor", values: values, @namespace: "openebs", statusWriter: statusWriter);
                            });
                    }

                    await master.InvokeIdempotentAsync("setup/openebs-ready",
                        async () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Wait", "for OpenEBS");
                            master.Status = "wait: for openebs";

                            await NeonHelper.WaitAllAsync(
                                new List<Task>()
                                {
                                    WaitForDaemonsetAsync(setupState, "openebs", "neon-storage-openebs-ndm"),
                                    WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-admission-server"),
                                    WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-apiserver"),
                                    WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-localpv-provisioner"),
                                    WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-ndm-operator"),
                                    WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-provisioner"),
                                    WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-snapshot-operator")
                                });
                        });

                    if (cluster.HostingManager.HostingEnvironment != HostingEnvironment.Wsl2)
                    {
                        KubeHelper.WriteStatus(statusWriter, "Setup", "OpenEBS Pool");
                        master.Status = "setup: openebs pool";

                        await master.InvokeIdempotentAsync("setup/openebs-pool",
                        async () =>
                        {
                            var cStorPoolCluster = new V1CStorPoolCluster()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name              = "cspc-stripe",
                                    NamespaceProperty = "openebs"
                                },
                                Spec = new V1CStorPoolClusterSpec()
                                {
                                    Pools = new List<V1CStorPoolSpec>()
                                }
                            };

                            var blockDevices = ((JObject)await GetK8sClient(setupState).ListNamespacedCustomObjectAsync("openebs.io", "v1alpha1", "openebs", "blockdevices")).ToObject<V1CStorBlockDeviceList>();

                            foreach (var n in cluster.Definition.Nodes)
                            {
                                if (blockDevices.Items.Any(bd => bd.Spec.NodeAttributes.GetValueOrDefault("nodeName") == n.Name))
                                {
                                    var pool = new V1CStorPoolSpec()
                                    {
                                        NodeSelector = new Dictionary<string, string>()
                                            {
                                               { "kubernetes.io/hostname", n.Name }
                                            },
                                        DataRaidGroups = new List<V1CStorDataRaidGroup>()
                                            {
                                                new V1CStorDataRaidGroup()
                                                {
                                                    BlockDevices = new List<V1CStorBlockDeviceRef>()
                                                }
                                            },
                                        PoolConfig = new V1CStorPoolConfig()
                                        {
                                            DataRaidGroupType = DataRaidGroupType.Stripe,
                                            Tolerations       = new List<V1Toleration>()
                                                {
                                                    { new V1Toleration() { Effect = "NoSchedule", OperatorProperty = "Exists" } },
                                                    { new V1Toleration() { Effect = "NoExecute", OperatorProperty = "Exists" } }
                                                }
                                        }
                                    };

                                    foreach (var bd in blockDevices.Items.Where(bd => bd.Spec.NodeAttributes.GetValueOrDefault("nodeName") == n.Name))
                                    {
                                        pool.DataRaidGroups.FirstOrDefault().BlockDevices.Add(
                                            new V1CStorBlockDeviceRef()
                                            {
                                                BlockDeviceName = bd.Metadata.Name
                                            });
                                    }

                                    cStorPoolCluster.Spec.Pools.Add(pool);
                                }
                            }

                            GetK8sClient(setupState).CreateNamespacedCustomObject(cStorPoolCluster, "cstor.openebs.io", "v1", "openebs", "cstorpoolclusters");
                        });

                        await master.InvokeIdempotentAsync("setup/openebs-cstor-ready",
                            async () =>
                            {
                                KubeHelper.WriteStatus(statusWriter, "Wait", "for OpenEBS cStor");
                                master.Status = "wait: for openebs cStore";

                                await NeonHelper.WaitAllAsync(
                                    new List<Task>()
                                    {
                                        WaitForDaemonsetAsync(setupState, "openebs", "neon-storage-openebs-cstor-csi-node"),
                                        WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-cstor-admission-server"),
                                        WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-cstor-cvc-operator"),
                                        WaitForDeploymentAsync(setupState, "openebs", "neon-storage-openebs-cstor-cspc-operator")
                                    });
                            });

                        var replicas = 3;

                        if (cluster.Definition.Nodes.Where(n => n.OpenEBS).Count() < 3)
                        {
                            replicas = 1;
                        }

                        await CreateCstorStorageClass(setupState, master, "openebs-cstor", replicaCount: replicas);
                        await CreateCstorStorageClass(setupState, master, "openebs-cstor-unreplicated", replicaCount: 1);
                    }
                    else
                    {
                        await CreateHostPathStorageClass(setupState, master, "openebs-cstor");
                        await CreateHostPathStorageClass(setupState, master, "openebs-cstor-unreplicated");
                    }
                });
        }

        /// <summary>
        /// Creates a Kubernetes namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new Namespace name.</param>
        /// <param name="istioInjectionEnabled">Whether Istio sidecar injection should be enabled.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateNamespaceAsync(
            ObjectDictionary                setupState,
            NodeSshProxy<NodeDefinition>    master,
            string                          name,
            bool                            istioInjectionEnabled = true,
            Action<string>                  statusWriter          = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync($"setup/{name}-namespace",
                async () =>
                {
                    await GetK8sClient(setupState).CreateNamespaceAsync(new V1Namespace()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = name,
                            Labels = new Dictionary<string, string>()
                            {
                                { "istio-injection", istioInjectionEnabled ? "enabled" : "disabled" }
                            }
                        }
                    });
                });
        }

        /// <summary>
        /// Creates a Kubernetes Storage Class.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateHostPathStorageClass(
            ObjectDictionary                setupState,
            NodeSshProxy<NodeDefinition>    master,
            string                          name,
            Action<string>                  statusWriter = null)
        {
            await master.InvokeIdempotentAsync($"setup/storage-class-hostpath-{name}",
                async () =>
                {
                    var storageClass = new V1StorageClass()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name        = name,
                            Annotations = new Dictionary<string, string>()
                    {
                        {  "cas.openebs.io/config", 
$@"- name: StorageType
  value: ""hostpath""
- name: BasePath
  value: /var/openebs/local
" },
                        {"openebs.io/cas-type", "local" }
                    },
                        },
                        Provisioner       = "openebs.io/local",
                        ReclaimPolicy     = "Delete",
                        VolumeBindingMode = "WaitForFirstConsumer"
                    };

                    await GetK8sClient(setupState).CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Creates an OpenEBS cStor Kubernetes Storage Class.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="cstorPoolCluster"></param>
        /// <param name="replicaCount"></param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateCstorStorageClass(
            ObjectDictionary                setupState,
            NodeSshProxy<NodeDefinition>    master,
            string                          name,
            string                          cstorPoolCluster = "cspc-stripe",
            int                             replicaCount     = 3,
            Action<string>                  statusWriter     = null)
        {
            await master.InvokeIdempotentAsync($"setup/storage-class-cstor-{name}",
                async () =>
                {
                    if (master.Cluster.Definition.Nodes.Where(n => n.OpenEBS).Count() < replicaCount)
                    {
                        replicaCount = master.Cluster.Definition.Nodes.Where(n => n.OpenEBS).Count();
                    }

                    var storageClass = new V1StorageClass()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = name
                        },
                        Parameters = new Dictionary<string, string>
                        {
                            {  "cas-type", "cstor" },
                            {  "cstorPoolCluster", cstorPoolCluster },
                            {  "replicaCount", $"{replicaCount}" },

                        },
                        AllowVolumeExpansion = true,
                        Provisioner          = "cstor.csi.openebs.io",
                        ReclaimPolicy        = "Delete",
                        VolumeBindingMode    = "Immediate"
                    };

                    await GetK8sClient(setupState).CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Installs an Etcd cluster to the monitoring namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallEtcdAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            await master.InvokeIdempotentAsync("setup/monitoring-etc",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Etc");
                    master.Status = "setup: etc";

                    await CreateCstorStorageClass(setupState, master, "neon-internal-etcd");

                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>($"replicas", cluster.Definition.Nodes.Count(n => n.Labels.Metrics == true).ToString()));

                    values.Add(new KeyValuePair<string, object>($"volumeClaimTemplate.resources.requests.storage", "1Gi"));

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                        i++;
                    }

                    await master.InstallHelmChartAsync("etcd_cluster", releaseName: "neon-etcd", @namespace: "neon-system", values: values, statusWriter: statusWriter);
                });

            await master.InvokeIdempotentAsync("setup/setup/monitoring-etc-ready",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Wait", "for Etc (monitoring)");
                    master.Status = "wait: for etc (monitoring)";

                    await WaitForStatefulsetAsync(setupState, "monitoring", "neon-system-etcd");
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Installs a Prometheus Operator to the monitoring namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallPrometheusAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            await master.InvokeIdempotentAsync("setup/monitoring-prometheus",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Prometheus");
                    master.Status = "setup: prometheus";

                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>($"alertmanager.alertmanagerSpec.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>($"prometheusOperator.tlsProxy.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>($"prometheusOperator.admissionWebhooks.patch.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>($"prometheusOperator.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>($"prometheusOperator.configmapReloadImage.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>($"prometheusOperator.prometheusConfigReloaderImage.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>($"global.kubeStateMetrics.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>($"global.nodeExporter.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add(new KeyValuePair<string, object>($"alertmanager.alertmanagerSpec.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"alertmanager.alertmanagerSpec.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"alertmanager.alertmanagerSpec.tolerations[{i}].operator", "Exists"));

                        values.Add(new KeyValuePair<string, object>($"prometheusOperator.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"prometheusOperator.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"prometheusOperator.tolerations[{i}].operator", "Exists"));

                        values.Add(new KeyValuePair<string, object>($"prometheusOperator.admissionWebhooks.patch.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"prometheusOperator.admissionWebhooks.patch.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"prometheusOperator.admissionWebhooks.patch.tolerations[{i}].operator", "Exists"));

                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.tolerations[{i}].operator", "Exists"));

                        i++;
                    }

                    if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2
                        || cluster.Definition.Nodes.Count() == 1)
                    {
                        await CreateHostPathStorageClass(setupState, master, "neon-internal-prometheus");

                        values.Add(new KeyValuePair<string, object>($"alertmanager.alertmanagerSpec.storage.volumeClaimTemplate.spec.storageClassName", $"neon-internal-prometheus"));
                        values.Add(new KeyValuePair<string, object>($"alertmanager.alertmanagerSpec.storage.volumeClaimTemplate.spec.accessModes[0]", "ReadWriteOnce"));
                        values.Add(new KeyValuePair<string, object>($"alertmanager.alertmanagerSpec.storage.volumeClaimTemplate.spec.resources.requests.storage", $"5Gi"));
                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.storage.volumeClaimTemplate.spec.storageClassName", $"neon-internal-prometheus"));
                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.storage.volumeClaimTemplate.spec.accessModes[0]", "ReadWriteOnce"));
                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.storage.volumeClaimTemplate.spec.resources.requests.storage", $"5Gi"));
                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.remoteRead", null));
                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.remoteWrite", null));
                        values.Add(new KeyValuePair<string, object>($"prometheus.prometheusSpec.scrapeInterval", "2m"));
                    }

                    await master.InstallHelmChartAsync("prometheus_operator", releaseName: "neon-metrics-prometheus", @namespace: "monitoring", values: values, statusWriter: statusWriter);
                });
        }

        /// <summary>
        /// Waits for Prometheus to be fully ready.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task WaitForPrometheusAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            await master.InvokeIdempotentAsync("setup/monitoring-prometheus-ready",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Wait", "for Prometheus");
                    master.Status = "wait: for prometheus";

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(setupState, "monitoring", "neon-metrics-prometheus-ku-operator"),
                            WaitForDeploymentAsync(setupState, "monitoring", "neon-metrics-prometheus-kube-state-metrics"),
                            WaitForDaemonsetAsync(setupState, "monitoring", "neon-metrics-prometheus-prometheus-node-exporter"),
                            WaitForStatefulsetAsync(setupState, "monitoring", "alertmanager-neon-metrics-prometheus-ku-alertmanager"),
                            WaitForStatefulsetAsync(setupState, "monitoring", "prometheus-neon-metrics-prometheus-ku-prometheus")
                        });
                });
        }

        /// <summary>
        /// Installs Cortex to the monitoring namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallCortexAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/monitoring-cortex-all",
                async () =>
                {
                    var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);
                    var values  = new List<KeyValuePair<string, object>>();

                    if (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count() >= 3)
                    {
                        values.Add(new KeyValuePair<string, object>($"replicas", Math.Min(3, (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count()))));
                        values.Add(new KeyValuePair<string, object>($"cortexConfig.ingester.lifecycler.ring.kvstore.store", "etcd"));
                        values.Add(new KeyValuePair<string, object>($"cortexConfig.ingester.lifecycler.ring.kvstore.replication_factor", 3));
                    }

                    await master.InvokeIdempotentAsync("setup/monitoring-cortex",
                        async () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Setup", "Cortex");
                            master.Status = "setup: Cortex";

                            if (cluster.Definition.Nodes.Any(n => n.Vm.GetMemory(cluster.Definition) < 4294965097L))
                            {
                                values.Add(new KeyValuePair<string, object>($"cortexConfig.ingester.retain_period", $"120s"));
                                values.Add(new KeyValuePair<string, object>($"cortexConfig.ingester.metadata_retain_period", $"5m"));
                                values.Add(new KeyValuePair<string, object>($"cortexConfig.querier.batch_iterators", true));
                                values.Add(new KeyValuePair<string, object>($"cortexConfig.querier.max_samples", 10000000));
                                values.Add(new KeyValuePair<string, object>($"cortexConfig.table_manager.retention_period", "12h"));
                            }

                            int i = 0;
                            foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelMetrics, "true"))
                            {
                                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", t.Effect));
                                values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                                i++;
                            }

                            values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));

                            await master.InstallHelmChartAsync("cortex", releaseName: "neon-metrics-cortex", @namespace: "monitoring", values: values, statusWriter: statusWriter);
                        });

                    await master.InvokeIdempotentAsync("setup/monitoring-cortex-ready",
                        async () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Wait", "for Cortex");
                            master.Status = "wait: for cortex";

                            await WaitForDeploymentAsync(setupState, "monitoring", "neon-metrics-cortex");
                        });
                });
        }

        /// <summary>
        /// Installs Loki to the monitoring namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallLokiAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            await master.InvokeIdempotentAsync("setup/monitoring-loki",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Loki");
                    master.Status = "setup: loki";

                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));

                    if (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count() >= 3)
                    {
                        values.Add(new KeyValuePair<string, object>($"replicas", Math.Min(3, (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count()))));
                        values.Add(new KeyValuePair<string, object>($"config.ingester.lifecycler.ring.kvstore.store", "etcd"));
                        values.Add(new KeyValuePair<string, object>($"config.ingester.lifecycler.ring.kvstore.replication_factor", 3));
                    }

                    if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2
                        || cluster.Definition.Nodes.Count() == 1)
                    {
                        values.Add(new KeyValuePair<string, object>($"config.limits_config.reject_old_samples_max_age", "15m"));
                        values.Add(new KeyValuePair<string, object>($"resources.requests.memory", "64Mi"));
                        values.Add(new KeyValuePair<string, object>($"resources.limits.memory", "128Mi"));
                    }

                    await master.InstallHelmChartAsync("loki", releaseName: "neon-logs-loki", @namespace: "monitoring", values: values, statusWriter: statusWriter);
                });
        }

        /// <summary>
        /// Installs Promtail to the monitoring namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallPromtailAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            await master.InvokeIdempotentAsync("setup/monitoring-promtail",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Promtail");
                    master.Status = "setup: promtail";

                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));

                    if (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count() >= 3)
                    {
                        values.Add(new KeyValuePair<string, object>($"replicas", Math.Min(3, (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count()))));
                        values.Add(new KeyValuePair<string, object>($"config.ingester.lifecycler.ring.kvstore.store", "etcd"));
                        values.Add(new KeyValuePair<string, object>($"config.ingester.lifecycler.ring.kvstore.replication_factor", 3));
                    }

                    if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2
                        || cluster.Definition.Nodes.Count() == 1)
                    {
                        values.Add(new KeyValuePair<string, object>($"resources.requests.memory", "64Mi"));
                        values.Add(new KeyValuePair<string, object>($"resources.limits.memory", "128Mi"));
                    }

                    await master.InstallHelmChartAsync("promtail", releaseName: "neon-logs-promtail", @namespace: "monitoring", values: values, statusWriter: statusWriter);
                });
        }

        /// <summary>
        /// Installs Grafana to the monitoring namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallGrafanaAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/monitoring-grafana",
                    async () =>
                    {
                        KubeHelper.WriteStatus(statusWriter, "Setup", "Grafana");
                        master.Status = "setup: Grafana";

                        var values = new List<KeyValuePair<string, object>>();

                        values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));
                        values.Add(new KeyValuePair<string, object>("downloadDashboardsImage.organization", KubeConst.NeonContainerRegistery(setupState)));
                        values.Add(new KeyValuePair<string, object>("sidecar.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                        int i = 0;
                        foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelMetrics, "true"))
                        {
                            values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                            values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", t.Effect));
                            values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                            i++;
                        }

                        if (master.Cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2
                            || master.Cluster.Definition.Nodes.Count() == 1)
                        {
                            values.Add(new KeyValuePair<string, object>($"prometheusEndpoint", "http://prometheus-operated:9090"));
                            values.Add(new KeyValuePair<string, object>($"resources.requests.memory", "64Mi"));
                            values.Add(new KeyValuePair<string, object>($"resources.limits.memory", "128Mi"));
                        }

                        await master.InstallHelmChartAsync("grafana", releaseName: "neon-metrics-grafana", @namespace: "monitoring", values: values, statusWriter: statusWriter);
                    });

            await master.InvokeIdempotentAsync("setup/monitoring-grafana-ready",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Wait", "for Grafana");
                    master.Status = "wait: for grafana";

                    await WaitForDeploymentAsync(setupState, "monitoring", "neon-metrics-grafana");
                });
        }

        /// <summary>
        /// Installs a Minio cluster to the monitoring namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMinioAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/minio-all",
                async () =>
                {
                    var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

                    if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2)
                    {
                        await CreateHostPathStorageClass(setupState, master, "neon-internal-minio");
                    }
                    else
                    {
                        await CreateCstorStorageClass(setupState, master, "neon-internal-minio");
                    }

                    await master.InvokeIdempotentAsync("setup/minio",
                        async () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Setup", "Minio");
                            master.Status = "setup: minio";

                            var values = new List<KeyValuePair<string, object>>();

                            values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("mcImage.organization", KubeConst.NeonContainerRegistery(setupState)));
                            values.Add(new KeyValuePair<string, object>("helmKubectlJqImage.organization", KubeConst.NeonContainerRegistery(setupState)));

                            if (cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count() >= 3)
                            {
                                var replicas = Math.Min(4, Math.Max(4, cluster.Definition.Nodes.Where(n => n.Labels.Metrics).Count() / 4));
                                values.Add(new KeyValuePair<string, object>($"replicas", replicas));
                                values.Add(new KeyValuePair<string, object>($"mode", "distributed"));
                            }

                            if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2
                                || cluster.Definition.Nodes.Count() == 1)
                            {
                                values.Add(new KeyValuePair<string, object>($"resources.requests.memory", "64Mi"));
                                values.Add(new KeyValuePair<string, object>($"resources.limits.memory", "128Mi"));
                            }

                            await master.InstallHelmChartAsync("minio", releaseName: "neon-system-minio", @namespace: "neon-system", values: values, statusWriter: statusWriter);
                        });

                    await master.InvokeIdempotentAsync("configure/minio-secret",
                        async () =>
                        {
                            KubeHelper.WriteStatus(statusWriter, "Configure", "Minio Secret");
                            master.Status = "configure: minio secret";

                            var secret = await GetK8sClient(setupState).ReadNamespacedSecretAsync("neon-system-minio", "neon-system");

                            secret.Metadata.NamespaceProperty = "monitoring";

                            var monitoringSecret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name = secret.Name()
                                },
                                Data = secret.Data,
                            };
                            await GetK8sClient(setupState).CreateNamespacedSecretAsync(monitoringSecret, "monitoring");
                        });
                });
        }

        /// <summary>
        /// Installs an Neon Monitoring to the monitoring namespace.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task<List<Task>> SetupMonitoringAsync(ObjectDictionary setupState, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));

            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);
            var master  = cluster.FirstMaster;
            var tasks   = new List<Task>();

            KubeHelper.WriteStatus(statusWriter, "Setup", "Metrics");
            master.Status = "setup: metrics";

            tasks.Add(WaitForPrometheusAsync(setupState, master, statusWriter));

            if (cluster.HostingManager.HostingEnvironment != HostingEnvironment.Wsl2)
            {
                tasks.Add(InstallCortexAsync(setupState, master, statusWriter));
            }

            tasks.Add(InstallLokiAsync(setupState, master, statusWriter));
            tasks.Add(InstallPromtailAsync(setupState, master, statusWriter));
            tasks.Add(master.InstallHelmChartAsync("istio_prometheus", @namespace: "monitoring", statusWriter: statusWriter));
            tasks.Add(InstallGrafanaAsync(setupState, master, statusWriter));

            return tasks;
        }

        /// <summary>
        /// Installs Jaeger
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <remarks>The tracking <see cref="Task"/>.</remarks>
        public static async Task InstallJaegerAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/monitoring-jaeger",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Jaeger");
                    master.Status = "setup: jaeger";

                    var values = new List<KeyValuePair<string, object>>();

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelLogs, "true"))
                    {
                        values.Add(new KeyValuePair<string, object>($"ingester.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"ingester.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"ingester.tolerations[{i}].operator", "Exists"));

                        values.Add(new KeyValuePair<string, object>($"agent.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"agent.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"agent.tolerations[{i}].operator", "Exists"));

                        values.Add(new KeyValuePair<string, object>($"collector.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"collector.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"collector.tolerations[{i}].operator", "Exists"));

                        values.Add(new KeyValuePair<string, object>($"query.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"query.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"query.tolerations[{i}].operator", "Exists"));

                        values.Add(new KeyValuePair<string, object>($"esIndexCleaner.tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"esIndexCleaner.tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"esIndexCleaner.tolerations[{i}].operator", "Exists"));
                        i++;
                    }

                    await master.InstallHelmChartAsync("jaeger", releaseName: "neon-logs-jaeger", @namespace: "monitoring", values: values, statusWriter: statusWriter);
                });

            await master.InvokeIdempotentAsync("setup/monitoring-jaeger-ready",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Wait", "for Jaeger");
                    master.Status = "wait: for jaeger";

                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            var deployments = await GetK8sClient(setupState).ListNamespacedDeploymentAsync("monitoring", labelSelector: "release=neon-logs-jaeger");
                            if (deployments == null || deployments.Items.Count < 2)
                            {
                                return false;
                            }

                            return deployments.Items.All(p => p.Status.AvailableReplicas == p.Spec.Replicas);
                        },
                        timeout:      clusterOpTimeout,
                        pollInterval: clusterOpRetryInterval);
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Installs a harbor container registry and required components.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallContainerRegistryAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            var adminPassword = NeonHelper.GetCryptoRandomPassword(20);

            await master.InvokeIdempotentAsync("setup/harbor-certificate",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Harbor Certificate");
                    master.Status = "create: harbor certificate";

                    await SyncContext.ClearAsync;

                    var cert = TlsCertificate.CreateSelfSigned(KubeConst.NeonContainerRegistery(setupState), 4096);

                    var harborCert = new V1Secret()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Name = "neon-registry-harbor-internal"
                            },
                            Type       = "Opaque",
                            StringData = new Dictionary<string, string>()
                            {
                                { "tls.crt", cert.CertPemNormalized },
                                { "tls.key", cert.KeyPemNormalized }
                            }
                        };

                        await GetK8sClient(setupState).CreateNamespacedSecretAsync(harborCert, "neon-system");
                });

            await master.InvokeIdempotentAsync("setup/harbor-redis",
                async () =>
                {
                    await SyncContext.ClearAsync;
                    
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Harbor Redis");
                    master.Status = "setup: harbor redis";

                    var values   = new List<KeyValuePair<string, object>>();
                    
                    values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));

                    var replicas = Math.Min(3, cluster.Definition.Masters.Count());

                    values.Add(new KeyValuePair<string, object>($"replicas", $"{replicas}"));
                    
                    if (replicas < 2)
                    {
                        values.Add(new KeyValuePair<string, object>($"hardAntiAffinity", false));
                        values.Add(new KeyValuePair<string, object>($"sentinel.quorum", 1));
                    }

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelNeonSystemRegistry, "true"))
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                        i++;
                    }

                    await master.InstallHelmChartAsync("redis_ha", releaseName: "neon-system-registry-redis", @namespace: "neon-system", values: values, statusWriter: statusWriter);
                });

            await master.InvokeIdempotentAsync("setup/harbor-redis-ready",
                async () =>
                {
                    await SyncContext.ClearAsync;

                    KubeHelper.WriteStatus(statusWriter, "Wait", "for Harbor Redis");
                    master.Status = "wait: for harbor redis";

                    await WaitForStatefulsetAsync(setupState, "neon-system", "neon-system-registry-redis-server");
                });

            await master.InvokeIdempotentAsync("setup/harbor",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "Harbor");
                    master.Status = "setup: harbor";

                    var values = new List<KeyValuePair<string, object>>();

                    values.Add(new KeyValuePair<string, object>("nginx.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("portal.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("core.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("jobservice.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("registry.registry.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("registry.controller.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("chartmuseum.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("clair.clair.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("clair.adapter.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("trivy.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("notary.server.image.organization", KubeConst.NeonContainerRegistery(setupState)));
                    values.Add(new KeyValuePair<string, object>("notary.signer.image.organization", KubeConst.NeonContainerRegistery(setupState)));

                    values.Add(new KeyValuePair<string, object>($"harborAdminPassword", adminPassword));

                    if (cluster.Definition.Masters.Count() > 1)
                    {
                        var redisConnStr = string.Empty;
                        for (int i = 0; i < Math.Min(3, cluster.Definition.Masters.Count()); i++)
                        {
                            if (i > 0)
                            {
                                redisConnStr += "\\,";
                            }

                            redisConnStr += $"neon-system-registry-redis-announce-{i}:26379";
                        }

                        values.Add(new KeyValuePair<string, object>($"redis.external.addr", redisConnStr));
                        values.Add(new KeyValuePair<string, object>($"redis.external.sentinelMasterSet", "master"));
                    }

                    int j = 0;
                    foreach (var taint in await GetTaintsAsync(setupState, NodeLabels.LabelNeonSystemRegistry, "true"))
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{j}].key", $"{taint.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{j}].effect", taint.Effect));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{j}].operator", "Exists"));
                        j++;
                    }

                    await master.InstallHelmChartAsync("harbor", releaseName: "neon-system-registry-harbor", @namespace: "neon-system", values: values, statusWriter: statusWriter);
                });

            await master.InvokeIdempotentAsync("setup/harbor-ready",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Wait", "for Harbor");
                    master.Status = "wait: for harbor";

                    var startUtc = DateTime.UtcNow;

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-registry-harbor-chartmuseum"),
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-registry-harbor-clair"),
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-registry-harbor-core"),
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-registry-harbor-jobservice"),
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-registry-harbor-notary-server"),
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-registry-harbor-notary-signer"),
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-registry-harbor-portal"),
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-registry-harbor-registry")
                        });
                });
        }

        /// <summary>
        /// Installs the Neon Cluster Manager.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallClusterManagerAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/cluster-manager",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "[neon-cluster-manager]");
                    master.Status = "setup: [neon-cluster-manager]";

                    var values = new List<KeyValuePair<string, object>>();
                    
                    values.Add(new KeyValuePair<string, object>("image.organization", KubeConst.NeonContainerRegistery(setupState)));

                    await master.InstallHelmChartAsync("neon_cluster_manager", releaseName: "neon-cluster-manager", @namespace: "neon-system", values: values, statusWriter: statusWriter);
                });

            await master.InvokeIdempotentAsync("setup/cluster-manager-ready",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "wait", "for [neon-cluster-manager]");
                    master.Status = "wait: for [neon-cluster-manager]";

                    await WaitForDeploymentAsync(setupState, "neon-system", "neon-cluster-manager");
                });
        }

        /// <summary>
        /// Installs the Neon Cluster Manager.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task<List<Task>> CreateNamespacesAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await SyncContext.ClearAsync;

            var tasks = new List<Task>();

            tasks.Add(CreateNamespaceAsync(setupState, master, "neon-system", true));
            tasks.Add(CreateNamespaceAsync(setupState, master, "jobs", false));
            tasks.Add(CreateNamespaceAsync(setupState, master, "monitoring", true));

            return await Task.FromResult(tasks);
        }

        /// <summary>
        /// Installs a Citus-postgres database used by neon-system services.
        /// </summary>
        /// <param name="setupState">The setup controller state.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="statusWriter">Optional status writer used when the method is not being executed within a setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallSystemDbAsync(ObjectDictionary setupState, NodeSshProxy<NodeDefinition> master, Action<string> statusWriter = null)
        {
            Covenant.Requires<ArgumentNullException>(setupState != null, nameof(setupState));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = setupState.Get<ClusterProxy>(ClusterProxyProperty);

            var values = new List<KeyValuePair<string, object>>();

            values.Add(new KeyValuePair<string, object>($"image.organization", KubeConst.NeonContainerRegistery(setupState)));
            values.Add(new KeyValuePair<string, object>($"busybox.image.organization", KubeConst.NeonContainerRegistery(setupState)));
            values.Add(new KeyValuePair<string, object>($"prometheus.image.organization", KubeConst.NeonContainerRegistery(setupState)));
            values.Add(new KeyValuePair<string, object>($"manager.image.organization", KubeConst.NeonContainerRegistery(setupState)));

            if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2
                || cluster.Definition.Nodes.Count() == 1)
            {
                await CreateHostPathStorageClass(setupState, master, "neon-internal-citus");
                values.Add(new KeyValuePair<string, object>($"worker.resources.requests.memory", "64Mi"));
                values.Add(new KeyValuePair<string, object>($"worker.resources.limits.memory", "128Mi"));
                values.Add(new KeyValuePair<string, object>($"master.resources.requests.memory", "64Mi"));
                values.Add(new KeyValuePair<string, object>($"master.resources.limits.memory", "128Mi"));
                values.Add(new KeyValuePair<string, object>($"manager.resources.requests.memory", "64Mi"));
                values.Add(new KeyValuePair<string, object>($"manager.resources.limits.memory", "128Mi"));
            }
            else
            {
                await CreateCstorStorageClass(setupState, master, "neon-internal-citus");
            }

            await master.InvokeIdempotentAsync("setup/system-db",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Setup", "System Database");
                    master.Status = "setup: system database";

                    var replicas = Math.Max(1, cluster.Definition.Masters.Count() / 5);

                    values.Add(new KeyValuePair<string, object>($"master.replicas", replicas));
                    values.Add(new KeyValuePair<string, object>($"manager.replicas", replicas));
                    values.Add(new KeyValuePair<string, object>($"worker.replicas", replicas));

                    if (replicas < 3)
                    {
                        values.Add(new KeyValuePair<string, object>($"manager.minimumWorkers", "1"));
                    }

                    if (cluster.Definition.Nodes.Where(n => n.Labels.OpenEBS).Count() < 3)
                    {
                        values.Add(new KeyValuePair<string, object>($"persistence.replicaCount", "1"));
                    }

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(setupState, NodeLabels.LabelNeonSystemDb, "true"))
                    {
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}"));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].effect", t.Effect));
                        values.Add(new KeyValuePair<string, object>($"tolerations[{i}].operator", "Exists"));
                        i++;
                    }

                    await master.InstallHelmChartAsync("citus_postgresql", releaseName: "neon-system-db", @namespace: "neon-system", values: values, statusWriter: statusWriter);
                });

            await master.InvokeIdempotentAsync("setup/system-db-ready",
                async () =>
                {
                    KubeHelper.WriteStatus(statusWriter, "Wait", "for System Database");
                    master.Status = "wait: for system database";

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(setupState, "neon-system", "neon-system-db-citus-postgresql-manager"),
                            WaitForStatefulsetAsync(setupState, "neon-system", "neon-system-db-citus-postgresql-master"),
                            WaitForStatefulsetAsync(setupState, "neon-system", "neon-system-db-citus-postgresql-worker")
                        });
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="setupState"></param>
        /// <param name="namespace"></param>
        /// <param name="name"></param>
        /// <param name="labelSelector"></param>
        /// <param name="fieldSelector"></param>
        /// <returns></returns>
        public static async Task WaitForDeploymentAsync(
            ObjectDictionary setupState, 
            string @namespace, 
            string name = null, 
            string labelSelector = null,
            string fieldSelector = null)
        {
            Covenant.Requires<ArgumentException>(name != null || labelSelector != null || fieldSelector != null, "One of name, labelSelector or fieldSelector must be set,");

            if (!string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(fieldSelector)){
                    fieldSelector += $",metadata.name={name}";
                }
                else
                {
                    fieldSelector = $"metadata.name={name}";
                }
            }

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var deployments = await GetK8sClient(setupState).ListNamespacedDeploymentAsync(@namespace, fieldSelector: fieldSelector, labelSelector: labelSelector);
                        if (deployments == null || deployments.Items.Count == 0)
                        {
                            return false;
                        }

                        return deployments.Items.All(d => d.Status.AvailableReplicas == d.Spec.Replicas);
                    }
                    catch
                    {
                        return false;
                    }
                            
                },
                timeout: clusterOpTimeout,
                pollInterval: clusterOpRetryInterval);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="setupState"></param>
        /// <param name="namespace"></param>
        /// <param name="name"></param>
        /// <param name="labelSelector"></param>
        /// <param name="fieldSelector"></param>
        /// <returns></returns>
        public static async Task WaitForStatefulsetAsync(
            ObjectDictionary setupState,
            string @namespace,
            string name = null,
            string labelSelector = null,
            string fieldSelector = null)
        {
            Covenant.Requires<ArgumentException>(name != null || labelSelector != null || fieldSelector != null, "One of name, labelSelector or fieldSelector must be set,");

            if (!string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(fieldSelector))
                {
                    fieldSelector += $",metadata.name={name}";
                }
                else
                {
                    fieldSelector = $"metadata.name={name}";
                }
            }

            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var statefulsets = await GetK8sClient(setupState).ListNamespacedStatefulSetAsync(@namespace, fieldSelector: fieldSelector, labelSelector: labelSelector);
                        if (statefulsets == null || statefulsets.Items.Count == 0)
                        {
                            return false;
                        }

                        return statefulsets.Items.All(s => s.Status.ReadyReplicas == s.Spec.Replicas);
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout: clusterOpTimeout,
                pollInterval: clusterOpRetryInterval);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="setupState"></param>
        /// <param name="namespace"></param>
        /// <param name="name"></param>
        /// <param name="labelSelector"></param>
        /// <param name="fieldSelector"></param>
        /// <returns></returns>
        public static async Task WaitForDaemonsetAsync(
            ObjectDictionary setupState,
            string @namespace,
            string name = null,
            string labelSelector = null,
            string fieldSelector = null)
        {
            Covenant.Requires<ArgumentException>(name != null || labelSelector != null || fieldSelector != null, "One of name, labelSelector or fieldSelector must be set,");

            if (!string.IsNullOrEmpty(name))
            {
                if (!string.IsNullOrEmpty(fieldSelector))
                {
                    fieldSelector += $",metadata.name={name}";
                }
                else
                {
                    fieldSelector = $"metadata.name={name}";
                }
            }
            await NeonHelper.WaitForAsync(
                async () =>
                {
                    try
                    {
                        var daemonsets = await GetK8sClient(setupState).ListNamespacedDaemonSetAsync(@namespace, fieldSelector: fieldSelector, labelSelector: labelSelector);
                        if (daemonsets == null || daemonsets.Items.Count == 0)
                        {
                            return false;
                        }

                        return daemonsets.Items.All(d => d.Status.NumberAvailable == d.Status.DesiredNumberScheduled);
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout: clusterOpTimeout,
                pollInterval: clusterOpRetryInterval);
        }

        /// <summary>
        /// Returns the built-in cluster definition (as text) for a cluster provisioned on WSL2.
        /// </summary>
        /// <returns>The cluster definition text.</returns>
        public static string GetWsl2ClusterDefintion()
        {
            var definition =
@"
name: wsl2
datacenter: wsl2
environment: development
timeSources:
- pool.ntp.org
allowUnitTesting: true
kubernetes:
  allowPodsOnMasters: true
hosting:
  environment: wsl2
nodes:
  master-0:
    role: master
";
            return definition;
        }
    }
}
