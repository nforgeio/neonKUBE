//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.Operations.cs
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
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Helm.Helm;
using k8s;
using k8s.Models;
using Microsoft.Rest;
using Minio;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube.Resources;
using Neon.Postgres;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;
using Neon.Net;

using Tomlyn;
using Tomlyn.Model;
using Tomlyn.Syntax;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;
using YamlDotNet.Serialization.NamingConventions;
using System.Text.Json;

namespace Neon.Kube
{
    public static partial class KubeSetup
    {
        /// <summary>
        /// Configures a local HAProxy container that makes the Kubernetes Etc
        /// cluster highly available.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="node">The node where the operation will be performed.</param>
        public static void SetupEtcdHaProxy(ISetupController controller, NodeSshProxy<NodeDefinition> node)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            controller.LogProgress(node, verb: "configure", message: "etcd ha");

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

            foreach (var n in cluster.Nodes.Where(n => n.Metadata.Labels.Istio))
            {
                sbHaProxyConfig.Append(
$@"
    server {n.Name}         {n.Address}:{KubeNodePorts.IstioIngressHttp}");
            }

            sbHaProxyConfig.Append(
$@"
backend harbor_backend
    mode                    tcp
    balance                 roundrobin");

            foreach (var n in cluster.Nodes.Where(n => n.Metadata.Labels.Istio))
            {
                sbHaProxyConfig.Append(
$@"
    server {n.Name}         {n.Address}:{KubeNodePorts.IstioIngressHttps}");
            }

            node.UploadText("/etc/neonkube/neon-etcd-proxy.cfg", sbHaProxyConfig);

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
  priorityClassName: { PriorityClass.SystemNodeCritical.Name }
  containers:
    - name: web
      image: {KubeConst.LocalClusterRegistry}/haproxy:{KubeVersions.Haproxy}
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
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The first master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task LabelNodesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/label-nodes",
                async () =>
                {
                    controller.LogProgress(master, verb: "label", message: "nodes");

                    try
                    {
                        var k8sNodes = (await k8s.ListNodeAsync()).Items;

                        foreach (var node in cluster.Nodes)
                        {
                            var k8sNode = k8sNodes.Where(n => n.Metadata.Name == node.Name).FirstOrDefault();

                            var patch = new V1Node()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Labels = k8sNode.Labels()
                                }
                            };

                            if (node.Metadata.IsWorker)
                            {
                                // Kubernetes doesn't set the role for worker nodes so we'll do that here.

                                patch.Metadata.Labels.Add("kubernetes.io/role", "worker");
                            }

                            patch.Metadata.Labels.Add(NodeLabels.LabelDatacenter, cluster.Definition.Datacenter.ToLowerInvariant());
                            patch.Metadata.Labels.Add(NodeLabels.LabelEnvironment, cluster.Definition.Environment.ToString().ToLowerInvariant());

                            foreach (var label in node.Metadata.Labels.All)
                            {
                                if (label.Value != null)
                                {
                                    patch.Metadata.Labels.Add(label.Key, label.Value.ToString());
                                }
                            }

                            await k8s.PatchNodeAsync(new V1Patch(patch, V1Patch.PatchType.StrategicMergePatch), k8sNode.Metadata.Name);
                        }
                    }
                    finally
                    {
                        master.Status = string.Empty;
                    }

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Initializes the cluster on the first manager, joins the remaining
        /// masters and workers to the cluster and then performs the rest of
        /// cluster setup.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="maxParallel">
        /// The maximum number of operations on separate nodes to be performed in parallel.
        /// This defaults to <see cref="defaultMaxParallelNodes"/>.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task SetupClusterAsync(ISetupController controller, int maxParallel = defaultMaxParallelNodes)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin  = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var master        = cluster.FirstMaster;
            var debugMode     = controller.Get<bool>(KubeSetupProperty.DebugMode);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);

            cluster.ClearStatus();

            KubeHelper.K8sClientConverterInitialize();

            ConfigureKubernetes(controller, master);
            ConfigureFeatureGates(controller, cluster.Masters);
            ConfigureWorkstation(controller, master);

            ConnectCluster(controller);

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await ConfigureKubeletAsync(controller, master);
                await RestartPodsAsync(controller, master);
            }

            await ConfigureMasterTaintsAsync(controller, master);
            await TaintNodesAsync(controller);
            await LabelNodesAsync(controller, master);
            await CreateNamespacesAsync(controller, master);
            await CreateRootUserAsync(controller, master);
            await ConfigurePriorityClassesAsync(controller, master);
            await InstallCalicoCniAsync(controller, master);
            await InstallMetricsServerAsync(controller, master);
            await InstallIstioAsync(controller, master);

            if (cluster.Definition.Nodes.Where(node => node.Labels.Metrics).Count() >= 3)
            {
                await InstallEtcdAsync(controller, master);
            }

            await InstallPrometheusAsync(controller, master);
            await InstallCertManagerAsync(controller, master);
            await InstallKubeDashboardAsync(controller, master);
            await InstallNodeProblemDetectorAsync(controller, master);
            await InstallOpenEbsAsync(controller, master);
            await InstallReloaderAsync(controller, master);
            await InstallSystemDbAsync(controller, master);
            await InstallRedisAsync(controller, master);
            await InstallSsoAsync(controller, master);
            await InstallKialiAsync(controller, master);

            await InstallMinioAsync(controller, master);
            await SetupGrafanaAsync(controller, master);
            await InstallHarborAsync(controller, master);
            await InstallMonitoringAsync(controller);

            // Install the cluster operators and any required custom resources.
            //
            // NOTE: The neonKUBE CRDs are installed with [neon-cluster-operator]
            //       so we need to install that first.

            await InstallClusterOperatorAsync(controller, master);
            await InstallNeonDashboardAsync(controller, master);
            await InstallNodeAgentAsync(controller, master);
            await InstallContainerRegistryResources(controller, master);
        }

        /// <summary>
        /// Method to generate Kubernetes cluster configuration.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static string GenerateKubernetesClusterConfig(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var hostingEnvironment   = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
            var cluster              = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin         = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var readyToGoMode        = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var controlPlaneEndpoint = $"kubernetes-masters:6442";
            var sbCertSANs           = new StringBuilder();

            if (hostingEnvironment == HostingEnvironment.Wsl2)
            {
                // Tweak the API server endpoint for WSL2.

                controlPlaneEndpoint = $"{KubeConst.NeonDesktopWsl2BuiltInDistroName}:{KubeNodePorts.KubeApiServer}";
            }

            if (!string.IsNullOrEmpty(cluster.Definition.Kubernetes.ApiLoadBalancer))
            {
                controlPlaneEndpoint = cluster.Definition.Kubernetes.ApiLoadBalancer;

                var fields = cluster.Definition.Kubernetes.ApiLoadBalancer.Split(':');

                sbCertSANs.AppendLine($"  - \"{fields[0]}\"");
                sbCertSANs.AppendLine($"  - \"kubernetes-masters\"");
            }

            foreach (var node in cluster.Masters)
            {
                sbCertSANs.AppendLine($"  - \"{node.Address}\"");
                sbCertSANs.AppendLine($"  - \"{node.Name}\"");
            }

            if (cluster.Definition.IsDesktopCluster)
            {
                sbCertSANs.AppendLine($"  - \"{Dns.GetHostName()}\"");
                sbCertSANs.AppendLine($"  - \"{cluster.Definition.Name}\"");
            }

            var kubeletFailSwapOnLine = string.Empty;

            if (hostingEnvironment == HostingEnvironment.Wsl2)
            {
                // SWAP will be enabled by the default Microsoft WSL2 kernel which
                // will cause Kubernetes to complain because this isn't a supported
                // configuration.  We need to disable these error checks.

                kubeletFailSwapOnLine = "failSwapOn: false";
            }

            var clusterConfig = new StringBuilder();

            clusterConfig.AppendLine(
$@"
apiVersion: kubeadm.k8s.io/v1beta2
kind: ClusterConfiguration
clusterName: {cluster.Name}
kubernetesVersion: ""v{KubeVersions.Kubernetes}""
imageRepository: ""{KubeConst.LocalClusterRegistry}""
apiServer:
  extraArgs:
    bind-address: 0.0.0.0
    advertise-address: 0.0.0.0
    logging-format: json
    default-not-ready-toleration-seconds: ""30"" # default 300
    default-unreachable-toleration-seconds: ""30"" #default  300
    allow-privileged: ""true""
    api-audiences: api
    service-account-issuer: kubernetes.default.svc
    service-account-key-file: /etc/kubernetes/pki/sa.key
    service-account-signing-key-file: /etc/kubernetes/pki/sa.key
    oidc-issuer-url: https://sso.{cluster.Definition.Domain}
    oidc-client-id: kubernetes
    oidc-username-claim: email
    oidc-groups-claim: groups
    oidc-username-prefix: ""-""
    oidc-groups-prefix: """"
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

            clusterConfig.AppendLine($@"
---
apiVersion: kubelet.config.k8s.io/v1beta1
kind: KubeletConfiguration
logging:
  format: json
nodeStatusReportFrequency: 4s
volumePluginDir: /var/lib/kubelet/volume-plugins
cgroupDriver: systemd
runtimeRequestTimeout: 5m
{kubeletFailSwapOnLine}
");

            var kubeProxyMode = "ipvs";

            clusterConfig.AppendLine($@"
---
apiVersion: kubeproxy.config.k8s.io/v1alpha1
kind: KubeProxyConfiguration
mode: {kubeProxyMode}");

            return clusterConfig.ToString();
        }

        /// <summary>
        /// Restart all pods in a cluster. This is used when updating CA certs.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ConfigureKubeletAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
            var cluster            = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterIp          = controller.Get<string>(KubeSetupProperty.ClusterIp);
            var clusterLogin       = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var k8s                = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("ready-to-go/kube-apiserver-running-config",
                async () =>
                {
                    controller.LogProgress(master, verb: "ready-to-go", message: "configure kube-apiserver");

                    var k8s           = GetK8sClient(controller);
                    var configMap     = await k8s.ReadNamespacedConfigMapAsync("kubeadm-config", KubeNamespaces.KubeSystem);
                    var clusterConfig = configMap.Data["ClusterConfiguration"];

                    clusterConfig = Regex.Replace(clusterConfig, @"oidc-issuer-url.*", $"oidc-issuer-url: https://{ClusterDomain.Sso}.{cluster.Definition.Domain}");
                    configMap.Data["ClusterConfiguration"] = clusterConfig;

                    await k8s.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Namespace());
                });

            await master.InvokeIdempotentAsync("ready-to-go/kube-apiserver-static-config",
                async () =>
                {
                    controller.LogProgress(master, verb: "ready-to-go", message: "configure kube-apiserver static pod");

                    var kubeletConfig = master.DownloadText("/etc/kubernetes/manifests/kube-apiserver.yaml");

                    kubeletConfig = Regex.Replace(kubeletConfig, @"oidc-issuer-url.*", $"oidc-issuer-url=https://{ClusterDomain.Sso}.{cluster.Definition.Domain}");

                    master.UploadText("/etc/kubernetes/manifests/kube-apiserver.yaml", kubeletConfig, permissions: "600", owner: "root:root");

                    master.SudoCommand("systemctl", "restart", "kubelet");

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Restart all pods in a cluster. This is used when updating CA certs.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task RestartPodsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
            var cluster            = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin       = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var readyToGoMode      = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var k8s                = GetK8sClient(controller);
            var numPods            = 0;

            await master.InvokeIdempotentAsync("ready-to-go/restart-pods",
                async () =>
                {
                    controller.LogProgress(master, verb: "ready-to-go", message: "restart pods");

                    var pods = await k8s.ListPodForAllNamespacesAsync();

                    numPods = pods.Items.Count();

                    foreach (var p in pods.Items)
                    {
                        // We don't want to restart the apiserver since it have already been restarted.
                        // Not restaarting pods using the default serviceaccount is an optimization.
                        if (p.Name() == "kube-apiserver-neon-desktop"
                                || p.Spec.ServiceAccount == "default"
                                || p.Spec.ServiceAccountName == "default")
                        {
                            continue;
                        }

                        await k8s.DeleteNamespacedPodAsync(p.Name(), p.Namespace(), gracePeriodSeconds: 0);
                    }
                });

            await master.InvokeIdempotentAsync("ready-to-go/wait-for-pods",
                async () =>
                {
                    controller.LogProgress(master, verb: "ready-to-go", message: "restart pods - wait");

                    await NeonHelper.WaitForAsync(
                            async () =>
                            {
                                try
                                {
                                    var pods = await k8s.ListPodForAllNamespacesAsync();

                                    return pods.Items.All(p => p.Status.Phase != "Pending") && pods.Items.Where(p => p.Namespace() == KubeNamespaces.NeonSystem).Count() > 1;
                                }
                                catch
                                {
                                    return false;
                                }
                            },
                            timeout: TimeSpan.FromMinutes(10),
                            pollInterval: TimeSpan.FromMilliseconds(500));
                });
        }
        
        /// <summary>
        /// Basic Kubernetes cluster initialization.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        public static void ConfigureKubernetes(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
            var cluster            = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin       = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var readyToGoMode      = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            
            master.InvokeIdempotent("setup/cluster-init",
                () =>
                {
                    //---------------------------------------------------------
                    // Initialize the cluster on the first master:

                    controller.LogProgress(master, verb: "create", message: "cluster");

                    // Initialize Kubernetes:

                    master.InvokeIdempotent("setup/kubernetes-init",
                        () =>
                        {
                            controller.LogProgress(master, verb: "initialize", message: "kubernetes");

                            // It's possible that a previous cluster initialization operation
                            // was interrupted.  This command resets the state.

                            master.SudoCommand("kubeadm reset --force");

                            SetupEtcdHaProxy(controller, master);

                            // CRI-O needs to be running and listening on its unix domain socket so that
                            // Kubelet can start and the cluster can be initialized via [kubeadm].  CRI-O
                            // takes perhaps 20-30 seconds to start and we've run into occassional trouble
                            // with cluster setup failures because CRI-O hadn't started listening on its
                            // socket in time.
                            //
                            // We're going to wait for the presence of the CRI-O socket here.

                            const string crioSocket = "/var/run/crio/crio.sock";

                            NeonHelper.WaitFor(
                                () =>
                                {
                                    var socketResponse = master.SudoCommand("cat", new object[] { "/proc/net/unix" });

                                    return socketResponse.Success && socketResponse.OutputText.Contains(crioSocket);

                                },
                                pollInterval: TimeSpan.FromSeconds(0.5),
                                timeout: TimeSpan.FromSeconds(60));

                            // Configure the control plane's API server endpoint and initialize
                            // the certificate SAN names to include each master IP address as well
                            // as the HOSTNAME/ADDRESS of the API load balancer (if any).

                            controller.LogProgress(master, verb: "initialize", message: "cluster");

                            var clusterConfig = GenerateKubernetesClusterConfig(controller, master);

                            var kubeInitScript =
$@"
set -euo pipefail

systemctl enable kubelet.service
kubeadm init --config cluster.yaml --ignore-preflight-errors=DirAvailable--etc-kubernetes-manifests --cri-socket={crioSocket}
";
                            var response = master.SudoCommand(CommandBundle.FromScript(kubeInitScript).AddFile("cluster.yaml", clusterConfig.ToString()));

                            // Extract the cluster join command from the response.  We'll need this to join
                            // other nodes to the cluster.

                            var output = response.OutputText;
                            var pStart = output.IndexOf(joinCommandMarker, output.IndexOf(joinCommandMarker) + 1);

                            if (pStart == -1)
                            {
                                master.LogLine("START: [kubeadm init ...] response ============================================");

                                using (var reader = new StringReader(response.AllText))
                                {
                                    foreach (var line in reader.Lines())
                                    {
                                        master.LogLine(line);
                                    }
                                }

                                master.LogLine("END: [kubeadm init ...] response ==============================================");

                                throw new KubeException("Cannot locate the [kubeadm join ...] command in the [kubeadm init ...] response.");
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

                            controller.LogProgress(master, verb: "created", message: "cluster");
                        });

                    master.InvokeIdempotent("setup/kubectl",
                        () =>
                        {
                            controller.LogProgress(master, verb: "configure", message: "kubectl");

                            // Edit the Kubernetes configuration file to rename the context:
                            //
                            //       CLUSTERNAME-admin@kubernetes --> root@CLUSTERNAME
                            //
                            // rename the user:
                            //
                            //      CLUSTERNAME-admin --> CLUSTERNAME-root 

                            var adminConfig = master.DownloadText("/etc/kubernetes/admin.conf");

                            adminConfig = adminConfig.Replace($"kubernetes-admin@{cluster.Definition.Name}", $"root@{cluster.Definition.Name}");
                            adminConfig = adminConfig.Replace("kubernetes-admin", $"root@{cluster.Definition.Name}");

                            master.UploadText("/etc/kubernetes/admin.conf", adminConfig, permissions: "600", owner: "root:root");
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
                            var text = master.DownloadText(file.Path);

                            clusterLogin.SetupDetails.MasterFiles[file.Path] = new KubeFileDetails(text, permissions: file.Permissions, owner: file.Owner);
                        }
                    }

                    // Persist the cluster join command and downloaded master files.

                    clusterLogin.Save();

                    //---------------------------------------------------------
                    // Join the remaining masters to the cluster:

                    foreach (var master in cluster.Masters.Where(node => node != master))
                    {
                        try
                        {
                            master.InvokeIdempotent("setup/kubectl",
                                () =>
                                {
                                    controller.LogProgress(master, verb: "setup", message: "kubectl");

                                    // It's possible that a previous cluster join operation
                                    // was interrupted.  This command resets the state.

                                    master.SudoCommand("kubeadm reset --force");

                                    // The other (non-boot) masters need files downloaded from the boot master.

                                    controller.LogProgress(master, verb: "upload", message: "master files");

                                    foreach (var file in clusterLogin.SetupDetails.MasterFiles)
                                    {
                                        master.UploadText(file.Key, file.Value.Text, permissions: file.Value.Permissions, owner: file.Value.Owner);
                                    }

                                    // Join the cluster:

                                    master.InvokeIdempotent("setup/master-join",
                                        () =>
                                        {
                                            controller.LogProgress(master, verb: "join", message: "master to cluster");

                                            SetupEtcdHaProxy(controller, master);

                                            var joined = false;

                                            controller.LogProgress(master, verb: "join", message: "as master");

                                            master.SudoCommand("podman run",
                                                   "--name=neon-etcd-proxy",
                                                   "--detach",
                                                   "--restart=always",
                                                   "-v=/etc/neonkube/neon-etcd-proxy.cfg:/etc/haproxy/haproxy.cfg",
                                                   "--network=host",
                                                   "--log-driver=k8s-file",
                                                   $"{KubeConst.LocalClusterRegistry}/haproxy:{KubeVersions.Haproxy}"
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

                        controller.LogProgress(master, verb: "joined", message: "to cluster");
                    }

                    // Configure [kube-apiserver] on all the masters

                    foreach (var master in cluster.Masters)
                    {
                        try
                        {
                            master.InvokeIdempotent("setup/kubernetes-apiserver",
                                () =>
                                {
                                    controller.LogProgress(master, verb: "configure", message: "api server");

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
                                        controller.LogProgress(worker, verb: "join", message: "worker to cluster");

                                        SetupEtcdHaProxy(controller, worker);

                                        var joined = false;

                                        controller.LogProgress(worker, verb: "join", message: "as worker");

                                        worker.SudoCommand("podman run",
                                            "--name=neon-etcd-proxy",
                                            "--detach",
                                            "--restart=always",
                                            "-v=/etc/neonkube/neon-etcd-proxy.cfg:/etc/haproxy/haproxy.cfg",
                                            "--network=host",
                                            "--log-driver=k8s-file",
                                            $"{KubeConst.LocalClusterRegistry}/haproxy:{KubeVersions.Haproxy}",
                                            RunOptions.FaultOnError);

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

                            controller.LogProgress(worker, verb: "joined", message: "to cluster");
                        });
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                master.InvokeIdempotent("ready-to-go/renew-certs",
                () =>
                {
                    controller.LogProgress(master, verb: "ready-to-go", message: "renew kubectl certs");

                    var clusterConfig = GenerateKubernetesClusterConfig(controller, master);

                    var kubeInitScript =
$@"
set -euo pipefail

rm -rf /etc/kubernetes/pki/*
rm -f /etc/kubernetes/admin.conf
rm -f /etc/kubernetes/kubelet.conf
rm -f /etc/kubernetes/controller-manager.conf
rm -f /etc/kubernetes/scheduler.conf
kubeadm init --config cluster.yaml phase certs all
kubeadm init --config cluster.yaml phase kubeconfig all

set +e

until kubectl get pods
do
  sleep 0.5
done

for namespace in $(kubectl get ns --no-headers | awk '{{print $1}}'); do
    for token in $(kubectl get secrets --namespace ""$namespace"" --field-selector type=kubernetes.io/service-account-token -o name); do
        kubectl delete $token --namespace ""$namespace""
    done
done
";
                    var response = master.SudoCommand(CommandBundle.FromScript(kubeInitScript).AddFile("cluster.yaml", clusterConfig.ToString()));
                    var pods     = NeonHelper.JsonDeserialize<dynamic>(master.SudoCommand("crictl", "pods", "--namespace", "kube-system", "-o", "json").AllText);

                    foreach (dynamic p in pods.items)
                    {
                        master.SudoCommand("crictl", "stopp", p.id);
                        master.SudoCommand("crictl", "rmp", p.id);
                    }

                    master.SudoCommand("rm", "-f", "/var/lib/kubelet/pki/kubelet-client*");
                    master.SudoCommand("systemctl", "restart", "kubelet");

                    // Edit the Kubernetes configuration file to rename the context:
                    //
                    //       CLUSTERNAME-admin@kubernetes --> root@CLUSTERNAME
                    //
                    // rename the user:
                    //
                    //      CLUSTERNAME-admin --> CLUSTERNAME-root 

                    var adminConfig = master.DownloadText("/etc/kubernetes/admin.conf");

                    adminConfig = adminConfig.Replace($"kubernetes-admin@{cluster.Definition.Name}", $"root@{cluster.Definition.Name}");
                    adminConfig = adminConfig.Replace("kubernetes-admin", $"root@{cluster.Definition.Name}");

                    master.UploadText("/etc/kubernetes/admin.conf", adminConfig, permissions: "600", owner: "root:root");
                });

                master.InvokeIdempotent("ready-to-go/download-certs",
                () =>
                {
                    controller.LogProgress(master, verb: "readytogo", message: "renew kubectl certs");

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
                        var text = master.DownloadText(file.Path);

                        clusterLogin.SetupDetails.MasterFiles[file.Path] = new KubeFileDetails(text, permissions: file.Permissions, owner: file.Owner);
                    }

                    clusterLogin.Save();
                    }
                });
            }
        }

        /// <summary>
        /// Configures the Kubernetes feature gates specified by the <see cref="ClusterDefinition.FeatureGates"/> dictionary.
        /// It does this by editing the API server's static pod manifest located at <b>/etc/kubernetes/manifests/kube-apiserver.yaml</b>
        /// on the master nodes as required.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="masterNodes">The target master nodes.</param>
        /// <remarks>
        /// This operation doesn't do anything when we're preparing a ready-to-go node image.
        /// </remarks>
        public static void ConfigureFeatureGates(ISetupController controller, IEnumerable<NodeSshProxy<NodeDefinition>> masterNodes)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(masterNodes != null, nameof(masterNodes));
            Covenant.Requires<ArgumentException>(masterNodes.Count() > 0, nameof(masterNodes));

            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode, ReadyToGoMode.Normal);

            if (readyToGoMode == ReadyToGoMode.Prepare)
            {
                return;
            }

            var cluster           = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterDefinition = cluster.Definition;

            if (clusterDefinition.FeatureGates.Count() == 0)
            {
                return;
            }

            // We need to generate a "--feature-gates=..." command line option and add it to the end
            // of the command arguments in the API server static pod manifest at: 
            //
            // Here's what the static pod manifest looks like:
            //
            //  apiVersion: v1
            //  kind: Pod
            //  metadata:
            //  annotations:
            //      kubeadm.kubernetes.io/kube-apiserver.advertise-address.endpoint: 100.64.0.2:6443
            //    creationTimestamp: null
            //    labels:
            //      component: kube-apiserver
            //      tier: control-plane
            //    name: kube-apiserver
            //    namespace: kube-system
            //  spec:
            //    containers:
            //    - command:
            //      - kube-apiserver
            //      - --advertise-address=0.0.0.0
            //      - --allow-privileged=true
            //      - --api-audiences=api
            //      - --authorization-mode=Node,RBAC
            //      - --bind-address=0.0.0.0
            //      - --client-ca-file=/etc/kubernetes/pki/ca.crt
            //      - --default-not-ready-toleration-seconds=30
            //      - --default-unreachable-toleration-seconds=30
            //      - --enable-admission-plugins=NamespaceLifecycle,LimitRanger,ServiceAccount,DefaultStorageClass,DefaultTolerationSeconds,MutatingAdmissionWebhook,ValidatingAdmissionWebhook,Priority,ResourceQuota
            //      - --enable-bootstrap-token-auth=true
            //      - --etcd-cafile=/etc/kubernetes/pki/etcd/ca.crt
            //      - --etcd-certfile=/etc/kubernetes/pki/apiserver-etcd-client.crt
            //      - --etcd-keyfile=/etc/kubernetes/pki/apiserver-etcd-client.key
            //      - --etcd-servers=https://127.0.0.1:2379
            //      - --insecure-port=0
            //      - --kubelet-client-certificate=/etc/kubernetes/pki/apiserver-kubelet-client.crt
            //      - --kubelet-client-key=/etc/kubernetes/pki/apiserver-kubelet-client.key
            //      - --kubelet-preferred-address-types=InternalIP,ExternalIP,Hostname
            //      - --logging-format=json
            //      - --oidc-client-id=kubernetes
            //      - --oidc-groups-claim=groups
            //      - --oidc-groups-prefix=
            //      - --oidc-issuer-url=https://sso.f4ef74204ee34bbb888e823b3f0c8e3b.neoncluster.io
            //      - --oidc-username-claim=email
            //      - --oidc-username-prefix=-
            //      - --proxy-client-cert-file=/etc/kubernetes/pki/front-proxy-client.crt
            //      - --proxy-client-key-file=/etc/kubernetes/pki/front-proxy-client.key
            //      - --requestheader-allowed-names=front-proxy-client
            //      - --requestheader-client-ca-file=/etc/kubernetes/pki/front-proxy-ca.crt
            //      - --requestheader-extra-headers-prefix=X-Remote-Extra-
            //      - --requestheader-group-headers=X-Remote-Group
            //      - --requestheader-username-headers=X-Remote-User
            //      - --secure-port=6443
            //      - --service-account-issuer=kubernetes.default.svc
            //      - --service-account-key-file=/etc/kubernetes/pki/sa.key
            //      - --service-account-signing-key-file=/etc/kubernetes/pki/sa.key
            //      - --service-cluster-ip-range=10.253.0.0/16
            //      - --tls-cert-file=/etc/kubernetes/pki/apiserver.crt
            //      - --tls-private-key-file=/etc/kubernetes/pki/apiserver.key
            //      - --feature-gates=EphemeralContainers=true,...                        <<--- WE'RE INSERTING SOMETHING LIKE THIS!
            //      image: neon-registry.node.local/kube-apiserver:v1.21.4
            //      imagePullPolicy: IfNotPresent
            //      livenessProbe:
            //        failureThreshold: 8
            //        httpGet:
            //          host: 100.64.0.2
            //          path: /livez
            //          port: 6443
            //          scheme: HTTPS
            //        initialDelaySeconds: 10
            //        periodSeconds: 10
            //        timeoutSeconds: 15
            //      name: kube-apiserver
            //      ...
            //
            // Note that Kublet will automatically restart the API server's static pod when it
            // notices that that static pod manifest has been modified.

            const string manifestPath = "/etc/kubernetes/manifests/kube-apiserver.yaml";

            foreach (var master in masterNodes)
            {
                master.InvokeIdempotent("setup/feature-gates",
                    () =>
                    {
                        controller.LogProgress(master, verb: "configure", message: "feature-gates");

                        var manifestText = master.DownloadText(manifestPath);
                        var manifest     = NeonHelper.YamlDeserialize<dynamic>(manifestText);
                        var spec         = manifest["spec"];
                        var containers   = spec["containers"];
                        var container    = containers[0];
                        var command      = (List<object>)container["command"];
                        var sbFeatures   = new StringBuilder();

                        foreach (var featureGate in clusterDefinition.FeatureGates)
                        {
                            sbFeatures.AppendWithSeparator($"{featureGate.Key}={NeonHelper.ToBoolString(featureGate.Value)}", ",");
                        }

                        // Search for a [--feature-gates] command line argument.  If one is present,
                        // we'll replace it, otherwise we'll append a new one.  We may see an
                        // existing option when setting up a ready-to-go cluster.

                        var featureGateOption = $"--feature-gates={sbFeatures}";
                        var existingArgIndex  = -1;

                        for (int i = 0; i < command.Count; i++)
                        {
                            var arg = (string)command[i];

                            if (arg.StartsWith("--feature-gates="))
                            {
                                existingArgIndex = i;
                                break;
                            }
                        }

                        if (existingArgIndex >= 0)
                        {
                            command[existingArgIndex] = featureGateOption;
                        }
                        else
                        {
                            command.Add(featureGateOption);
                        }

                        manifestText = NeonHelper.YamlSerialize(manifest);

                        master.UploadText(manifestPath, manifestText, permissions: "600", owner: "root");
                    });
            }
        }

        /// <summary>
        /// Configures the local workstation.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="firstMaster">The master node where the operation will be performed.</param>
        public static void ConfigureWorkstation(ISetupController controller, NodeSshProxy<NodeDefinition> firstMaster)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(firstMaster != null, nameof(firstMaster));

            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);

            firstMaster.InvokeIdempotent($"{(readyToGoMode == ReadyToGoMode.Setup ? "ready-to-go" : "setup")}/workstation",
                (Action)(() =>
                {
                    controller.LogProgress(firstMaster, verb: "configure", message: "workstation");

                    var cluster        = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var clusterLogin   = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
                    var kubeConfigPath = KubeHelper.KubeConfigPath;

                    // Update kubeconfig.

                    // $todo(marcusbooyah):
                    //
                    // This is hardcoding the kubeconfig to point to the first master.  Issue 
                    // https://github.com/nforgeio/neonKUBE/issues/888 will fix this by adding a proxy
                    // to neonDESKTOP and load balancing requests across the k8s api servers.

                    var configText = clusterLogin.SetupDetails.MasterFiles["/etc/kubernetes/admin.conf"].Text;

                    configText = configText.Replace("kubernetes-masters", $"{cluster.Definition.Masters.FirstOrDefault().Address}");

                    if (!File.Exists(kubeConfigPath))
                    {
                        File.WriteAllText(kubeConfigPath, configText);
                    }
                    else
                    {
                        // The user already has an existing kubeconfig, so we need
                        // to merge in the new config.

                        var newConfig      = NeonHelper.YamlDeserialize<KubeConfig>(configText);
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

                    if (readyToGoMode == ReadyToGoMode.Setup)
                    {
                        var configFile = Environment.GetEnvironmentVariable("KUBECONFIG").Split(';').Where(variable => variable.Contains("config")).FirstOrDefault();

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
                                    timeout:              TimeSpan.FromMinutes(5));

                        controller[KubeSetupProperty.K8sClient] = k8sClient;
                    }
                }));
        }

        /// <summary>
        /// Adds the neonKUBE standard priority classes to the cluster.
        /// </summary>
        /// <param name="controller"></param>
        /// <param name="master"></param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ConfigurePriorityClassesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;
            var k8s     = GetK8sClient(controller);

            master.InvokeIdempotent("setup/priorityclass",
                () =>
                {
                    controller.LogProgress(master, verb: "configure", message: "priority classes");

                    // I couldn't figure out how to specify the priority class name when create them
                    // via the Kubernetes client, so I'll just use [kubectl] to apply them all at
                    // once on the master.

                    var sbPriorityClasses = new StringBuilder();

                    foreach (var priorityClassDef in PriorityClass.Values.Where(priorityClass => !priorityClass.IsSystem))
                    {
                        if (sbPriorityClasses.Length > 0)
                        {
                            sbPriorityClasses.AppendLine("---");
                        }

                        var definition =
$@"apiVersion: scheduling.k8s.io/v1
kind: PriorityClass
metadata:
  name: {priorityClassDef.Name}
value: {priorityClassDef.Value}
description: ""{priorityClassDef.Description}""
globalDefault: {NeonHelper.ToBoolString(priorityClassDef.IsDefault)}
";
                        sbPriorityClasses.Append(definition);
                    }

                    var script =
@"
set -euo pipefail

kubectl apply -f priorityclasses.yaml
";
                    var bundle = CommandBundle.FromScript(script);

                    bundle.AddFile("priorityclasses.yaml", sbPriorityClasses.ToString());

                    master.SudoCommand(bundle, RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs the Calico CNI.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallCalicoCniAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;
            var k8s     = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/cni",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "calico");

                    var values = new Dictionary<string, object>();

                    values.Add("images.organization", KubeConst.LocalClusterRegistry);

                    if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2)
                    {
                        values.Add($"neonDesktop", $"true");
                        values.Add($"kubernetes.service.host", $"neon-desktop");
                        values.Add($"kubernetes.service.port", KubeNodePorts.KubeApiServer);
                    }

                    await master.InstallHelmChartAsync(controller, "calico", releaseName: "calico", @namespace: KubeNamespaces.KubeSystem, values: values);

                    // Wait for Calico and CoreDNS pods to report that they're running.
                    // We're going to wait a maximum of 300 seconds.

                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            var pods = await k8s.ListPodForAllNamespacesAsync();

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
                        timeout:      clusterOpTimeout,
                        pollInterval: clusterOpPollInterval);
                    
                    await master.InvokeIdempotentAsync("setup/dnsutils",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "setup", message: "dnsutils");

                            var pods = await k8s.CreateNamespacedPodAsync(
                                new V1Pod()
                                {
                                    Metadata = new V1ObjectMeta()
                                    {
                                        Name              = "dnsutils",
                                        NamespaceProperty = KubeNamespaces.NeonSystem
                                    },
                                    Spec = new V1PodSpec()
                                    {
                                        Containers = new List<V1Container>()
                                        {
                                            new V1Container()
                                            {
                                                Name            = "dnsutils",
                                                Image           = $"{KubeConst.LocalClusterRegistry}/kubernetes-e2e-test-images-dnsutils:{KubeVersions.DnsUtils}",
                                                Command         = new List<string>() {"sleep", "3600" },
                                                ImagePullPolicy = "IfNotPresent"
                                            }
                                        },
                                        RestartPolicy = "Always",
                                        Tolerations = new List<V1Toleration>()
                                        {
                                            { new V1Toleration() { Effect = "NoSchedule", OperatorProperty = "Exists" } },
                                            { new V1Toleration() { Effect = "NoExecute", OperatorProperty = "Exists" } }
                                        }
                                    }
                                }, 
                                KubeNamespaces.NeonSystem);
                        });

                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            var result = master.SudoCommand($"kubectl exec -n {KubeNamespaces.NeonSystem} -t dnsutils -- nslookup kubernetes.default", RunOptions.LogOutput);

                            if (result.Success)
                            {
                                return await Task.FromResult(true);
                            }
                            else
                            {
                                master.SudoCommand("kubectl rollout restart --namespace kube-system deployment/coredns", RunOptions.LogOnErrorOnly);
                                await Task.Delay(5000);
                                return await Task.FromResult(false);
                            }
                        },
                        timeout:      clusterOpTimeout,
                        pollInterval: clusterOpPollInterval);

                    await k8s.DeleteNamespacedPodAsync("dnsutils", KubeNamespaces.NeonSystem);
                });
        }

        /// <summary>
        /// Uploads cluster related metadata to cluster nodes to <b>/etc/neonkube/metadata</b>
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="node">The target cluster node.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ConfigureMetadataAsync(ISetupController controller, NodeSshProxy<NodeDefinition> node)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            node.InvokeIdempotent("cluster-metadata",
                () =>
                {
                    node.UploadText(LinuxPath.Combine(KubeNodeFolders.Config, "metadata", "cluster-manifest.json"), NeonHelper.JsonSerialize(ClusterManifest, Formatting.Indented));
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Configures pods to be schedule on masters when enabled.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ConfigureMasterTaintsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;
            var k8s     = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/kubernetes-master-taints",
                async () =>
                {
                    controller.LogProgress(master, verb: "configure", message: "master taints");

                    // The [kubectl taint] command looks like it can return a non-zero exit code.
                    // We'll ignore this.

                    if (cluster.Definition.Kubernetes.AllowPodsOnMasters.GetValueOrDefault())
                    {
                        var nodes = new V1NodeList();

                        await NeonHelper.WaitForAsync(
                           async () =>
                           {
                               nodes = await k8s.ListNodeAsync(labelSelector: "node-role.kubernetes.io/master=");
                               return nodes.Items.All(n => n.Status.Conditions.Any(c => c.Type == "Ready" && c.Status == "True"));
                           },
                           timeout:      TimeSpan.FromMinutes(5),
                           pollInterval: TimeSpan.FromSeconds(5));

                        foreach (var master in nodes.Items)
                        {
                            if (master.Spec.Taints == null)
                            {
                                continue;
                            }

                            var patch = new V1Node()
                            {
                                Spec = new V1NodeSpec()
                                {
                                    Taints = master.Spec.Taints.Where(t => t.Key != "node-role.kubernetes.io/master").ToList()
                                }
                            };

                            await k8s.PatchNodeAsync(new V1Patch(patch, V1Patch.PatchType.StrategicMergePatch), master.Metadata.Name);
                        }
                    }
                });
        }

        /// <summary>
        /// Installs the Kubernetes Metrics Server service.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMetricsServerAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;
            var k8s     = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/kubernetes-metrics-server",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "metrics-server");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "metrics-server", releaseName: "metrics-server", @namespace: KubeNamespaces.KubeSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/kubernetes-metrics-server-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "metrics-server");

                    await k8s.WaitForDeploymentAsync("kube-system", "metrics-server", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs Istio.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallIstioAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin  = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var ingressAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioIngressGateway);
            var proxyAdvice   = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioProxy);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);

            await master.InvokeIdempotentAsync("setup/ingress-namespace",
                async () =>
                {
                    await k8s.CreateNamespaceAsync(new V1Namespace()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = KubeNamespaces.NeonIngress
                        }
                    });
                });

            await master.InvokeIdempotentAsync("setup/ingress",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "ingress");

                    var values = new Dictionary<string, object>();

                    values.Add("cluster.name", clusterLogin.ClusterDefinition.Name);
                    values.Add("cluster.domain", clusterLogin.ClusterDefinition.Domain);

                    var i = 0;
                    foreach (var rule in master.Cluster.Definition.Network.IngressRules)
                    {
                        values.Add($"nodePorts[{i}].name", $"{rule.Name}");
                        values.Add($"nodePorts[{i}].protocol", $"{rule.Protocol.ToString().ToUpper()}");
                        values.Add($"nodePorts[{i}].port", rule.ExternalPort);
                        values.Add($"nodePorts[{i}].targetPort", rule.TargetPort);
                        values.Add($"nodePorts[{i}].nodePort", rule.NodePort);
                        i++;
                    }

                    values.Add($"resources.ingress.limits.cpu", $"{ToSiString(ingressAdvice.PodCpuLimit)}");
                    values.Add($"resources.ingress.limits.memory", $"{ToSiString(ingressAdvice.PodMemoryLimit)}");
                    values.Add($"resources.ingress.requests.cpu", $"{ToSiString(ingressAdvice.PodCpuRequest)}");
                    values.Add($"resources.ingress.requests.memory", $"{ToSiString(ingressAdvice.PodMemoryRequest)}");

                    values.Add($"resources.proxy.limits.cpu", $"{ToSiString(proxyAdvice.PodCpuLimit)}");
                    values.Add($"resources.proxy.limits.memory", $"{ToSiString(proxyAdvice.PodMemoryLimit)}");
                    values.Add($"resources.proxy.requests.cpu", $"{ToSiString(proxyAdvice.PodCpuRequest)}");
                    values.Add($"resources.proxy.requests.memory", $"{ToSiString(proxyAdvice.PodMemoryRequest)}");

                    await master.InstallHelmChartAsync(controller, "istio",
                        releaseName:  "neon-ingress",
                        @namespace:   KubeNamespaces.NeonIngress, 
                        prioritySpec: PriorityClass.SystemClusterCritical.Name,
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/ingress-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "istio");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonIngress, "istio-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonIngress, "istiod", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDaemonsetAsync(KubeNamespaces.NeonIngress, "istio-ingressgateway", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDaemonsetAsync(KubeNamespaces.KubeSystem, "istio-cni-node", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                        });
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/neoncluster-gateway",
                async () =>
                {
                    var gateway = await k8s.GetNamespacedCustomObjectAsync<Gateway>(KubeNamespaces.NeonIngress, "neoncluster-gateway");
                    var regexPattern = "[a-z0-9]+.neoncluster.io";
                    var servers      = new List<Server>();

                    foreach (var server in gateway.Spec.Servers)
                    {
                        var hosts = new List<string>();

                        foreach (var host in server.Hosts)
                        {
                            hosts.Add(Regex.Replace(host, regexPattern, cluster.Definition.Domain));
                        }
                        server.Hosts = hosts;
                    }

                    await k8s.ReplaceNamespacedCustomObjectAsync<Gateway>(gateway, KubeNamespaces.NeonIngress, gateway.Name());
                });
            }
        }

        /// <summary>
        /// Installs Cert Manager.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallCertManagerAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster            = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin       = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var k8s                = GetK8sClient(controller);
            var readyToGoMode      = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice      = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice      = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.CertManager);
            var ingressAdvice      = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioIngressGateway);
            var proxyAdvice        = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioProxy);
            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            await master.InvokeIdempotentAsync("setup/cert-manager",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "cert-manager");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    values.Add($"prometheus.servicemonitor.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"prometheus.servicemonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelIngress, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "cert-manager", 
                        releaseName:  "cert-manager",
                        @namespace:   KubeNamespaces.NeonIngress,
                        prioritySpec: $"global.priorityClassName={PriorityClass.NeonNetwork.Name}", 
                        values:       values);
                });


            await master.InvokeIdempotentAsync("setup/cert-manager-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "cert-manager");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonIngress, "cert-manager", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonIngress, "cert-manager-cainjector", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonIngress, "cert-manager-webhook", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                        });
                });

            await master.InvokeIdempotentAsync("setup/neon-acme",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "neon-acme");
                    
                    var clusterLogin  = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
                    var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
                    var values        = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("image.tag", KubeVersions.NeonKubeContainerImageTag);
                    values.Add("cluster.name", clusterLogin.ClusterDefinition.Name);
                    values.Add("cluster.domain", clusterLogin.ClusterDefinition.Domain);

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelIngress, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "neon-acme", 
                        releaseName:  "neon-acme", 
                        @namespace:   KubeNamespaces.NeonIngress, 
                        prioritySpec: PriorityClass.NeonNetwork.Name,
                        values:       values);
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/cluster-cert",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "renew cluster cert");

                        await k8s.DeleteNamespacedCustomObjectAsync<Certificate>(KubeNamespaces.NeonIngress, "neon-cluster-certificate");
                        await k8s.DeleteNamespacedCustomObjectAsync<CertificateRequest>(KubeNamespaces.NeonIngress, "neon-cluster-certificate");
                        
                        try
                        {
                            await k8s.DeleteNamespacedSecretAsync("neon-cluster-certificate", KubeNamespaces.NeonIngress);
                        }
                        catch (HttpOperationException e)
                        {
                            if (e.Response.StatusCode != HttpStatusCode.NotFound)
                            {
                                throw;
                            }
                        }

                        var cert = new Certificate()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Name = "neon-cluster-certificate",
                                NamespaceProperty = KubeNamespaces.NeonIngress
                            },
                            Spec = new CertificateSpec()
                        };

                        cert.Spec.CommonName  = clusterLogin.ClusterDefinition.Domain;
                        cert.Spec.RenewBefore = "360h0m0s";
                        cert.Spec.SecretName  = "neon-cluster-certificate";
                        cert.Spec.Usages      = new X509Usages[] { X509Usages.ServerAuth, X509Usages.ClientAuth };
                        cert.Spec.Duration    = "2160h0m0s";
                        cert.Spec.Duration    = "2160h0m0s";
                        cert.Spec.DnsNames    = new List<string>()
                        {
                            $"{clusterLogin.ClusterDefinition.Domain}",
                            $"*.{clusterLogin.ClusterDefinition.Domain}"
                        };
                        cert.Spec.IssuerRef = new IssuerRef()
                        {
                            Group = "cert-manager.io",
                            Kind  = "ClusterIssuer",
                            Name  = "neon-acme"
                        };
                        cert.Spec.PrivateKey = new PrivateKey()
                        {
                            Algorithm      = KeyAlgorithm.RSA,
                            Encoding       = KeyEncoding.PKCS1,
                            RotationPolicy = RotationPolicy.Always
                        };

                        await k8s.UpsertNamespacedCustomObjectAsync<Certificate>(cert, KubeNamespaces.NeonIngress, cert.Name());
                    });
            }
        }

        /// <summary>
        /// Configures the root Kubernetes user.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateRootUserAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/root-user",
                async () =>
                {
                    controller.LogProgress(master, verb: "create", message: "root user");

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
- kind: Group
  apiGroup: rbac.authorization.k8s.io
  name: superadmin
";
                    master.KubectlApply(userYaml, RunOptions.FaultOnError);

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Generates a dashboard certificate.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The generated certificate.</returns>
        public static TlsCertificate GenerateDashboardCert(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin  = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);

            // We're going to tie the custom certificate to the IP addresses
            // of the master nodes only.  This means that only these nodes
            // can accept the traffic and also that we'd need to regenerate
            // the certificate if we add/remove a master node.
            //
            // Here's the tracking task:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/441

            var masterAddresses = new List<string>();

            foreach (var masterNode in cluster.Masters)
            {
                masterAddresses.Add(masterNode.Address.ToString());
            }

            var utcNow     = DateTime.UtcNow;
            var utc10Years = utcNow.AddYears(10);

            var certificate = TlsCertificate.CreateSelfSigned(
                hostnames: masterAddresses,
                validDays: (int)(utc10Years - utcNow).TotalDays,
                issuedBy:  "kubernetes-dashboard");

            return certificate;
        }

        /// <summary>
        /// Configures the root Kubernetes user.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallKubeDashboardAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin  = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.KubernetesDashboard);

            await master.InvokeIdempotentAsync("setup/kube-dashboard",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "kubernetes dashboard");

                    var values = new Dictionary<string, object>();

                    values.Add("replicas", serviceAdvice.ReplicaCount);
                    values.Add("cluster.name", cluster.Definition.Name);
                    values.Add("settings.clusterName", cluster.Definition.Name);
                    values.Add("cluster.domain", cluster.Definition.Domain);
                    values.Add("ingress.subdomain", ClusterDomain.KubernetesDashboard);
                    values.Add($"metricsScraper.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"metricsScraper.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                    await master.InstallHelmChartAsync(controller, "kubernetes-dashboard",
                        releaseName:     "kubernetes-dashboard", 
                        @namespace:      KubeNamespaces.NeonSystem,
                        prioritySpec:    PriorityClass.NeonApp.Name,
                        values:          values, 
                        progressMessage: "kubernetes-dashboard");

                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/k8s-ingress",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update k8s ingress");

                        var virtualService = await k8s.GetNamespacedCustomObjectAsync<VirtualService>(KubeNamespaces.NeonIngress, "k8s-dashboard-virtual-service");

                        virtualService.Spec.Hosts =
                            new List<string>()
                            {
                                $"{ClusterDomain.KubernetesDashboard}.{cluster.Definition.Domain}"
                            };

                        await k8s.ReplaceNamespacedCustomObjectAsync<VirtualService>(virtualService, KubeNamespaces.NeonIngress, virtualService.Name());
                    });
            }
        }

        /// <summary>
        /// Adds the node taints.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task TaintNodesAsync(ISetupController controller)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var master  = cluster.FirstMaster;

            await master.InvokeIdempotentAsync("setup/taint-nodes",
                async () =>
                {
                    controller.LogProgress(master, verb: "taint", message: "nodes");

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

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Deploy Kiali.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task InstallKialiAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);

            var k8s = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/kiali",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "kiali");

                    var values = new Dictionary<string, object>();

                    var secret = await k8s.ReadNamespacedSecretAsync(KubeConst.DexSecret, KubeNamespaces.NeonSystem);

                    values.Add("oidc.secret", Encoding.UTF8.GetString(secret.Data["KUBERNETES_CLIENT_SECRET"]));
                    values.Add("image.operator.organization", KubeConst.LocalClusterRegistry);
                    values.Add("image.operator.repository", "kiali-kiali-operator");
                    values.Add("image.kiali.organization", KubeConst.LocalClusterRegistry);
                    values.Add("image.kiali.repository", "kiali-kiali");
                    values.Add("cluster.name", cluster.Definition.Name);
                    values.Add("cluster.domain", cluster.Definition.Domain);
                    values.Add("ingress.subdomain", ClusterDomain.Kiali);
                    values.Add("grafanaPassword", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
                    
                    int i = 0;
                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelIstio, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "kiali", 
                        releaseName:  "kiali-operator", 
                        @namespace:   KubeNamespaces.NeonSystem,
                        prioritySpec: PriorityClass.NeonApp.Name, 
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/kiali-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "kiali");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "kiali-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "kiali", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval)
                        });
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/kiali-ingress",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update kiali ingress");
                        
                        var virtualService = await k8s.GetNamespacedCustomObjectAsync<VirtualService>(KubeNamespaces.NeonIngress, "kiali-dashboard-virtual-service");

                        virtualService.Spec.Hosts =
                            new List<string>()
                            {
                                $"{ClusterDomain.Kiali}.{cluster.Definition.Domain}"
                            };

                        await k8s.ReplaceNamespacedCustomObjectAsync<VirtualService>(virtualService, KubeNamespaces.NeonIngress, virtualService.Name());
                    });

                await master.InvokeIdempotentAsync("ready-to-go/kiali-crd-config",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update kiali crd config");

                        var kiali = await k8s.GetNamespacedCustomObjectAsync<Kiali>(KubeNamespaces.NeonSystem, "kiali");

                        kiali.Spec["auth"]["openid"]["issuer_uri"] = $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}";
                        kiali.Spec["external_services"]["grafana"]["url"] = $"https://{ClusterDomain.Grafana}.{cluster.Definition.Domain}";

                        await k8s.ReplaceNamespacedCustomObjectAsync(kiali, KubeNamespaces.NeonSystem, kiali.Name());
                    });
            }
        }

        /// <summary>
        /// Some initial kubernetes configuration.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task KubeSetupAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/initial-kubernetes", async
                () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "kubernetes");

                    await master.InstallHelmChartAsync(controller, "cluster-setup");
                });
        }

        /// <summary>
        /// Installs the Node Problem Detector.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallNodeProblemDetectorAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.NodeProblemDetector);

            var values = new Dictionary<string, object>();

            values.Add("cluster.name", cluster.Definition.Name);
            values.Add("cluster.domain", cluster.Definition.Domain);
            values.Add($"metrics.serviceMonitor.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
            values.Add($"metrics.serviceMonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

            await master.InvokeIdempotentAsync("setup/node-problem-detector",
                async () =>
                {
                    await master.InstallHelmChartAsync(controller, "node-problem-detector", 
                        releaseName:  "node-problem-detector",
                        prioritySpec: PriorityClass.NeonOperator.Name,
                        @namespace:   KubeNamespaces.NeonSystem);
                });

            await master.InvokeIdempotentAsync("setup/node-problem-detector-ready",
                async () =>
                {
                    await k8s.WaitForDaemonsetAsync(KubeNamespaces.NeonSystem, "node-problem-detector", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs OpenEBS.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallOpenEbsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster                = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterAdvice          = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var apiServerAdvice        = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.OpenEbsApiServer);
            var provisionerAdvice      = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.OpenEbsProvisioner);
            var localPvAdvice          = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.OpenEbsLocalPvProvisioner);
            var snapshotOperatorAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.OpenEbsSnapshotOperator);
            var ndmOperatorAdvice      = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.OpenEbsNdmOperator);
            var webhookAdvice          = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.OpenEbsWebhook);

            await master.InvokeIdempotentAsync("setup/openebs-all",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "openebs");

                    await master.InvokeIdempotentAsync("setup/openebs",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "setup", message: "openebs-base");

                            var values = new Dictionary<string, object>();

                            values.Add("apiserver.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("helper.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("localprovisioner.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("policies.monitoring.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("snapshotOperator.controller.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("snapshotOperator.provisioner.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("provisioner.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("ndm.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("ndmOperator.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("webhook.image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("jiva.image.organization", KubeConst.LocalClusterRegistry);

                            values.Add($"apiserver.replicas", apiServerAdvice.ReplicaCount);
                            values.Add($"provisioner.replicas", provisionerAdvice.ReplicaCount);
                            values.Add($"localprovisioner.replicas", localPvAdvice.ReplicaCount);
                            values.Add($"snapshotOperator.replicas", snapshotOperatorAdvice.ReplicaCount);
                            values.Add($"ndmOperator.replicas", ndmOperatorAdvice.ReplicaCount);
                            values.Add($"webhook.replicas", webhookAdvice.ReplicaCount);
                            values.Add($"serviceMonitor.interval", clusterAdvice.MetricsInterval);

                            await master.InstallHelmChartAsync(controller, "openebs", 
                                releaseName:  "openebs",
                                @namespace:   KubeNamespaces.NeonStorage,
                                prioritySpec: PriorityClass.NeonStorage.Name,
                                values:       values);
                        });

                    switch (cluster.Definition.OpenEbs.Engine)
                    {
                        case OpenEbsEngine.cStor:

                            await DeployOpenEbsWithcStor(controller, master);
                            break;

                        case OpenEbsEngine.HostPath:
                        case OpenEbsEngine.Jiva:
                            
                            await WaitForOpenEbsReady(controller, master);
                            break;

                        default:
                        case OpenEbsEngine.Default:
                        case OpenEbsEngine.Mayastor:

                            throw new NotImplementedException($"[{cluster.Definition.OpenEbs.Engine}]");
                    }
                });
        }

        /// <summary>
        /// Deploys OpenEBS using the cStor engine.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task DeployOpenEbsWithcStor(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/openebs-cstor",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "openebs-cstor");

                    var values = new Dictionary<string, object>();

                    values.Add("cspcOperator.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("cspcOperator.poolManager.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("cspcOperator.cstorPool.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("cspcOperator.cstorPoolExporter.image.organization", KubeConst.LocalClusterRegistry);

                    values.Add("cvcOperator.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("cvcOperator.target.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("cvcOperator.volumeMgmt.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("cvcOperator.volumeExporter.image.organization", KubeConst.LocalClusterRegistry);

                    values.Add("csiController.resizer.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.snapshotter.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.snapshotController.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.attacher.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.provisioner.image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.driverRegistrar.image.organization", KubeConst.LocalClusterRegistry);

                    values.Add("cstorCSIPlugin.image.organization", KubeConst.LocalClusterRegistry);

                    values.Add("csiNode.driverRegistrar.image.organization", KubeConst.LocalClusterRegistry);

                    values.Add("admissionServer.image.organization", KubeConst.LocalClusterRegistry);

                    await master.InstallHelmChartAsync(controller, "openebs-cstor-operator", releaseName: "openebs-cstor", values: values, @namespace: KubeNamespaces.NeonStorage);
                });

            await WaitForOpenEbsReady(controller, master);

            controller.LogProgress(master, verb: "setup", message: "openebs-pool");

            await master.InvokeIdempotentAsync("setup/openebs-pool",
                async () =>
                {
                    var cStorPoolCluster = new V1CStorPoolCluster()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = "cspc-stripe",
                            NamespaceProperty = KubeNamespaces.NeonStorage
                        },
                        Spec = new V1CStorPoolClusterSpec()
                        {
                            Pools = new List<V1CStorPoolSpec>()
                        }
                    };

                    var blockDevices = await k8s.ListNamespacedCustomObjectAsync<V1CStorBlockDeviceList>(KubeNamespaces.NeonStorage);
                    
                    foreach (var node in cluster.Definition.Nodes)
                    {
                        if (blockDevices.Items.Any(device => device.Spec.NodeAttributes.GetValueOrDefault("nodeName") == node.Name))
                        {
                            var pool = new V1CStorPoolSpec()
                            {
                                NodeSelector = new Dictionary<string, string>()
                                {
                                    { "kubernetes.io/hostname", node.Name }
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
                                    Tolerations = new List<V1Toleration>()
                                    {
                                        { new V1Toleration() { Effect = "NoSchedule", OperatorProperty = "Exists" } },
                                        { new V1Toleration() { Effect = "NoExecute", OperatorProperty = "Exists" } }
                                    }
                                }
                            };

                            foreach (var device in blockDevices.Items.Where(device => device.Spec.NodeAttributes.GetValueOrDefault("nodeName") == node.Name))
                            {
                                pool.DataRaidGroups.FirstOrDefault().BlockDevices.Add(
                                    new V1CStorBlockDeviceRef()
                                    {
                                        BlockDeviceName = device.Metadata.Name
                                    });
                            }

                            cStorPoolCluster.Spec.Pools.Add(pool);
                        }
                    }

                    await k8s.CreateNamespacedCustomObjectAsync<V1CStorPoolCluster>(cStorPoolCluster, KubeNamespaces.NeonStorage);
                });

            await master.InvokeIdempotentAsync("setup/openebs-cstor-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "openebs cstor");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.WaitForDaemonsetAsync(KubeNamespaces.NeonStorage, "openebs-cstor-csi-node", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-cstor-admission-server", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-cstor-cvc-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-cstor-cspc-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval)
                        });
                });

            var replicas = 3;

            if (cluster.Definition.Nodes.Where(node => node.OpenEbsStorage).Count() < replicas)
            {
                replicas = cluster.Definition.Nodes.Where(node => node.OpenEbsStorage).Count();
            }

            await CreateCstorStorageClass(controller, master, "openebs-cstor", replicaCount: replicas);
        }

        /// <summary>
        /// Waits for OpenEBS to become ready.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task WaitForOpenEbsReady(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            var k8s = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/openebs-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "openebs");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.WaitForDaemonsetAsync(KubeNamespaces.NeonStorage, "openebs-ndm", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDaemonsetAsync(KubeNamespaces.NeonStorage, "openebs-ndm-node-exporter", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-admission-server", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-apiserver", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-localpv-provisioner", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-ndm-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-ndm-cluster-exporter", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-provisioner", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonStorage, "openebs-snapshot-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval)
                        });
                });
        }

        /// <summary>
        /// Creates a Kubernetes namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new Namespace name.</param>
        /// <param name="istioInjectionEnabled">Whether Istio sidecar injection should be enabled.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateNamespaceAsync(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    master,
            string                          name,
            bool                            istioInjectionEnabled = true)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var k8s = GetK8sClient(controller);

            await master.InvokeIdempotentAsync($"setup/namespace-{name}",
                async () =>
                {
                    await k8s.CreateNamespaceAsync(new V1Namespace()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name   = name,
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
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="replicaCount">Specifies the data replication factor.</param>
        /// <param name="storagePool">Specifies the OpenEBS storage pool.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateJivaStorageClass(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    master,
            string                          name,
            int                             replicaCount = 3,
            string                          storagePool  = "default")
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(replicaCount > 0, nameof(replicaCount));

            var k8s = GetK8sClient(controller);

            await master.InvokeIdempotentAsync($"setup/storage-class-jiva-{name}",
                async () =>
                {

                    if (master.Cluster.Definition.Nodes.Count() < replicaCount)
                    {
                        replicaCount = master.Cluster.Definition.Nodes.Count();
                    }

                    var storageClass = new V1StorageClass()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = name,
                            Annotations = new Dictionary<string, string>()
                            {
                                {  "cas.openebs.io/config",
$@"- name: ReplicaCount
  value: ""{replicaCount}""
- name: StoragePool
  value: {storagePool}
" },
                                {"openebs.io/cas-type", "jiva" }
                            },
                        },
                        Provisioner       = "openebs.io/provisioner-iscsi",
                        ReclaimPolicy     = "Delete",
                        VolumeBindingMode = "WaitForFirstConsumer"
                    };

                    await k8s.CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Creates a Kubernetes Storage Class.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateHostPathStorageClass(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    master,
            string                          name)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var k8s = GetK8sClient(controller);

            await master.InvokeIdempotentAsync($"setup/storage-class-hostpath-{name}",
                async () =>
                {
                    var storageClass = new V1StorageClass()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = name,
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

                    await k8s.CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Creates an OpenEBS cStor Kubernetes Storage Class.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="cstorPoolCluster">Specifies the cStor pool name.</param>
        /// <param name="replicaCount">Specifies the data replication factor.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateCstorStorageClass(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    master,
            string                          name,
            string                          cstorPoolCluster = "cspc-stripe",
            int                             replicaCount     = 3)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(replicaCount > 0, nameof(replicaCount));

            var k8s = GetK8sClient(controller);

            await master.InvokeIdempotentAsync($"setup/storage-class-cstor-{name}",
                async () =>
                {
                    if (master.Cluster.Definition.Nodes.Where(node => node.OpenEbsStorage).Count() < replicaCount)
                    {
                        replicaCount = master.Cluster.Definition.Nodes.Where(node => node.OpenEbsStorage).Count();
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

                    await k8s.CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Creates the approperiate OpenEBS Kubernetes Storage Class for the cluster.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="replicaCount">Specifies the data replication factor.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateStorageClass(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    master,
            string                          name,
            int                             replicaCount = 3)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(replicaCount > 0, nameof(replicaCount));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            switch (cluster.Definition.OpenEbs.Engine)
            {
                case OpenEbsEngine.Default:

                    throw new InvalidOperationException($"[{nameof(OpenEbsEngine.Default)}] is not valid here.  This must be set to one of the other storage engines in [{nameof(OpenEbsOptions)}.Validate()].");

                case OpenEbsEngine.HostPath:

                    await CreateHostPathStorageClass(controller, master, name);
                    break;

                case OpenEbsEngine.cStor:

                    await CreateCstorStorageClass(controller, master, name);
                    break;

                case OpenEbsEngine.Jiva:

                    await CreateJivaStorageClass(controller, master, name);
                    break;

                case OpenEbsEngine.Mayastor:
                default:

                    throw new NotImplementedException($"Support for the [{cluster.Definition.OpenEbs.Engine}] OpenEBS storage engine is not implemented.");
            };
        }

        /// <summary>
        /// Installs an Etcd cluster to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallEtcdAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);
            var advice  = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.EtcdCluster);

            await master.InvokeIdempotentAsync("setup/monitoring-etcd",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "etcd");

                    await CreateStorageClass(controller, master, "neon-internal-etcd");

                    var values = new Dictionary<string, object>();

                    values.Add($"replicas", advice.ReplicaCount);

                    values.Add($"volumeClaimTemplate.resources.requests.storage", "1Gi");

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "etcd-cluster", releaseName: "neon-etcd", @namespace: KubeNamespaces.NeonSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/setup/monitoring-etcd-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "etc (monitoring)");

                    await k8s.WaitForStatefulSetAsync(KubeNamespaces.NeonMonitor, "neon-system-etcd", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Installs The Grafana Agent to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallPrometheusAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster         = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterAdvice   = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var agentAdvice     = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.GrafanaAgent);
            var agentNodeAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.GrafanaAgentNode);
            var istioAdvice     = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioProxy);

            await master.InvokeIdempotentAsync("setup/monitoring-prometheus",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "prometheus");

                    var values = new Dictionary<string, object>();
                    var i      = 0;

                    values.Add($"cluster.name", cluster.Definition.Name);
                    values.Add($"cluster.domain", cluster.Definition.Domain);

                    values.Add($"metrics.global.scrapeInterval", clusterAdvice.MetricsInterval);
                    values.Add($"metrics.crio.scrapeInterval", clusterAdvice.MetricsInterval);
                    values.Add($"metrics.istio.scrapeInterval", istioAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);
                    values.Add($"metrics.kubelet.scrapeInterval", clusterAdvice.MetricsInterval);
                    values.Add($"metrics.cadvisor.scrapeInterval", clusterAdvice.MetricsInterval);

                    if (agentAdvice.PodMemoryRequest != null && agentAdvice.PodMemoryLimit != null)
                    {
                        values.Add($"resources.agent.requests.memory", ToSiString(agentAdvice.PodMemoryRequest.Value));
                        values.Add($"resources.agent.limits.memory", ToSiString(agentAdvice.PodMemoryLimit.Value));
                    }

                    if (agentNodeAdvice.PodMemoryRequest != null && agentNodeAdvice.PodMemoryLimit != null)
                    {
                        values.Add($"resources.agentNode.requests.memory", ToSiString(agentNodeAdvice.PodMemoryRequest.Value));
                        values.Add($"resources.agentNode.limits.memory", ToSiString(agentNodeAdvice.PodMemoryLimit.Value));
                    }

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "grafana-agent",
                        releaseName:  "grafana-agent", 
                        @namespace:   KubeNamespaces.NeonMonitor, 
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });
        }

        /// <summary>
        /// Waits for Prometheus to be fully ready.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task WaitForPrometheusAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/monitoring-grafana-agent-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "grafana agent");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonMonitor, "grafana-agent-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDaemonsetAsync(KubeNamespaces.NeonMonitor, "grafana-agent-node", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForStatefulSetAsync(KubeNamespaces.NeonMonitor, "grafana-agent", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                        });
                });
        }

        /// <summary>
        /// Installs Cortex to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallCortexAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/monitoring-cortex-all",
                async () =>
                {
                    var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var k8s           = GetK8sClient(controller);
                    var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
                    var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Cortex);
                    var values        = new Dictionary<string, object>();

                    values.Add($"ingress.alertmanager.subdomain", ClusterDomain.AlertManager);
                    values.Add($"ingress.ruler.subdomain", ClusterDomain.CortexRuler);
                    values.Add($"serviceMonitor.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"serviceMonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                    if (cluster.Definition.Nodes.Where(node => node.Labels.Metrics).Count() >= 3)
                    {
                        values.Add($"cortexConfig.distributor.ha_tracker.enable_ha_tracker", true);
                        values.Add($"cortexConfig.distributor.ha_tracker.kvstore.store", "etcd");
                        values.Add($"cortexConfig.distributor.ring.kvstore.store", "etcd");

                        values.Add($"cortexConfig.ingester.lifecycler.ring.kvstore.store", "etcd");
                        values.Add($"cortexConfig.ingester.lifecycler.ring.replication_factor", 3);

                        values.Add($"cortexConfig.ruler.ring.kvstore.store", "etcd");

                        values.Add($"cortexConfig.alertmanager.sharding_enabled", true);
                        values.Add($"cortexConfig.alertmanager.sharding_ring.kvstore.store", "etcd");
                        values.Add($"cortexConfig.alertmanager.sharding_ring.replication_factor", 3);

                        values.Add($"cortexConfig.compactor.sharding_enabled", true);
                        values.Add($"cortexConfig.compactor.sharding_ring.kvstore.store", "etcd");
                        values.Add($"cortexConfig.compactor.sharding_ring.kvstore.replication_factor", 3);
                    }

                    if (serviceAdvice.PodMemoryRequest != null && serviceAdvice.PodMemoryLimit != null)
                    {
                        values.Add($"resources.requests.memory", ToSiString(serviceAdvice.PodMemoryRequest.Value));
                        values.Add($"resources.limits.memory", ToSiString(serviceAdvice.PodMemoryLimit.Value));
                    }

                    await master.InvokeIdempotentAsync("setup/monitoring-cortex-secret",
                        async () =>
                        {

                            var dbSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);

                            var citusSecret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name              = KubeConst.CitusSecretKey,
                                    NamespaceProperty = KubeNamespaces.NeonMonitor
                                },
                                Data       = new Dictionary<string, byte[]>(),
                                StringData = new Dictionary<string, string>()
                            };

                            citusSecret.Data["username"] = dbSecret.Data["username"];
                            citusSecret.Data["password"] = dbSecret.Data["password"];

                            await k8s.UpsertSecretAsync(citusSecret, KubeNamespaces.NeonMonitor);
                        }
                        );

                    await master.InvokeIdempotentAsync("setup/monitoring-cortex",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "setup", message: "cortex");

                            if (cluster.Definition.IsDesktopCluster ||
                                cluster.Definition.Nodes.Any(node => node.Vm.GetMemory(cluster.Definition) < ByteUnits.Parse("4 GiB")))
                            {
                                values.Add($"cortexConfig.ingester.retain_period", $"120s");
                                values.Add($"cortexConfig.ingester.metadata_retain_period", $"5m");
                                values.Add($"cortexConfig.querier.batch_iterators", true);
                                values.Add($"cortexConfig.querier.max_samples", 10000000);
                                values.Add($"cortexConfig.table_manager.retention_period", "12h");
                            }

                            int i = 0;

                            foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                            {
                                values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                                values.Add($"tolerations[{i}].effect", taint.Effect);
                                values.Add($"tolerations[{i}].operator", "Exists");
                                i++;
                            }

                            values.Add("image.organization", KubeConst.LocalClusterRegistry);

                            await master.InstallHelmChartAsync(controller, "cortex", 
                                releaseName:  "cortex", 
                                @namespace:   KubeNamespaces.NeonMonitor, 
                                prioritySpec: PriorityClass.NeonMonitor.Name, 
                                values:       values);
                        });

                    await master.InvokeIdempotentAsync("setup/monitoring-cortex-ready",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "wait for", message: "cortex");

                            await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonMonitor, "cortex", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                        });
                });
        }

        /// <summary>
        /// Installs Loki to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallLokiAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;
            
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster        = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s            = GetK8sClient(controller);
            var clusterAdvice  = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice  = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Loki);

            await master.InvokeIdempotentAsync("setup/monitoring-loki",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "loki");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    
                    values.Add($"replicas", serviceAdvice.ReplicaCount);
                    values.Add($"serviceMonitor.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"serviceMonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);
                    
                    if (cluster.Definition.Nodes.Where(node => node.Labels.Metrics).Count() >= 3)
                    {
                        values.Add($"config.common.ring.kvstore.store", "etcd");
                        values.Add($"config.common.ring.kvstore.replication_factor", 3);

                    }

                    if (cluster.Definition.IsDesktopCluster)
                    {
                        values.Add($"config.limits_config.reject_old_samples_max_age", "15m");
                    }

                    if (serviceAdvice.PodMemoryRequest != null && serviceAdvice.PodMemoryLimit != null)
                    {
                        values.Add($"resources.requests.memory", ToSiString(serviceAdvice.PodMemoryRequest.Value));
                        values.Add($"resources.limits.memory", ToSiString(serviceAdvice.PodMemoryLimit.Value));
                    }

                    await master.InstallHelmChartAsync(controller, "loki", 
                        releaseName:  "loki",
                        @namespace:   KubeNamespaces.NeonMonitor,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/monitoring-loki-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "loki");

                    await k8s.WaitForStatefulSetAsync(KubeNamespaces.NeonMonitor, "loki", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs Tempo to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallTempoAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster        = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s            = GetK8sClient(controller);
            var clusterAdvice  = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var advice         = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Tempo);

            await master.InvokeIdempotentAsync("setup/monitoring-tempo",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "tempo");

                    var values = new Dictionary<string, object>();

                    values.Add("tempo.organization", KubeConst.LocalClusterRegistry);

                    values.Add($"replicas", advice.ReplicaCount);
                    values.Add($"serviceMonitor.enabled", advice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"serviceMonitor.interval", advice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                    if (cluster.Definition.Nodes.Where(node => node.Labels.Metrics).Count() >= 3)
                    {
                        values.Add($"config.ingester.lifecycler.ring.kvstore.store", "etcd");
                        values.Add($"config.ingester.lifecycler.ring.kvstore.replication_factor", 3);
                    }

                    if (advice.PodMemoryRequest != null && advice.PodMemoryLimit != null)
                    {
                        values.Add($"resources.requests.memory", ToSiString(advice.PodMemoryRequest.Value));
                        values.Add($"resources.limits.memory", ToSiString(advice.PodMemoryLimit.Value));
                    }

                    await master.InstallHelmChartAsync(controller, "tempo", 
                        releaseName:  "tempo", 
                        @namespace:   KubeNamespaces.NeonMonitor, 
                        prioritySpec: PriorityClass.NeonMonitor.Name, 
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/monitoring-tempo-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "tempo");

                    await k8s.WaitForStatefulSetAsync(KubeNamespaces.NeonMonitor, "tempo", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs Kube State Metrics to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallKubeStateMetricsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.KubeStateMetrics);

            await master.InvokeIdempotentAsync("setup/monitoring-kube-state-metrics",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "kube-state-metrics");

                    var values = new Dictionary<string, object>();

                    values.Add($"prometheus.monitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                    await master.InstallHelmChartAsync(controller, "kube-state-metrics", 
                        releaseName:  "kube-state-metrics",
                        @namespace:   KubeNamespaces.NeonMonitor,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/monitoring-kube-state-metrics-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "kube-state-metrics");

                    await k8s.WaitForStatefulSetAsync(KubeNamespaces.NeonMonitor, "kube-state-metrics", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs Reloader to the Neon system nnamespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallReloaderAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster        = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s            = GetK8sClient(controller);
            var clusterAdvice  = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice  = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Reloader);

            await master.InvokeIdempotentAsync("setup/reloader",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "reloader");

                    var values = new Dictionary<string, object>();

                    values.Add($"reloader.serviceMonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);
                    values.Add($"reloader.serviceMonitor.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);

                    await master.InstallHelmChartAsync(controller, "reloader",
                        releaseName:  "reloader", 
                        @namespace:   KubeNamespaces.NeonSystem, 
                        prioritySpec: $"reloader.deployment.priorityClassName={PriorityClass.NeonOperator.Name}",
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/reloader-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "reloader");

                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "reloader", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs Grafana to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallGrafanaAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Grafana);

            await master.InvokeIdempotentAsync("setup/monitoring-grafana",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "neon-grafana");

                    var values = new Dictionary<string, object>();

                    values.Add("cluster.name", cluster.Definition.Name);
                    values.Add("cluster.domain", cluster.Definition.Domain);
                    values.Add("ingress.subdomain", ClusterDomain.Grafana);
                    values.Add($"serviceMonitor.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"serviceMonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                    await master.InvokeIdempotentAsync("setup/db-credentials-grafana",
                        async () =>
                        {
                            var secret    = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);
                            var dexSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.DexSecret, KubeNamespaces.NeonSystem);

                            var monitorSecret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name        = KubeConst.GrafanaSecret,
                                    Annotations = new Dictionary<string, string>()
                                    {
                                        {  "reloader.stakater.com/match", "true" }
                                    }
                                },
                                Type = "Opaque",
                                Data = new Dictionary<string, byte[]>()
                                {
                                    { "DATABASE_PASSWORD", secret.Data["password"] },
                                    { "CLIENT_ID", Encoding.UTF8.GetBytes("grafana") },
                                    { "CLIENT_SECRET", dexSecret.Data["GRAFANA_CLIENT_SECRET"] },
                                }
                            };

                            await k8s.CreateNamespacedSecretAsync(monitorSecret, KubeNamespaces.NeonMonitor);
                        });

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
                    {
                        values.Add($"resources.requests.memory", ToSiString(serviceAdvice.PodMemoryRequest));
                        values.Add($"resources.limits.memory", ToSiString(serviceAdvice.PodMemoryLimit));
                    }

                    await master.InstallHelmChartAsync(controller, "grafana", 
                        releaseName:  "grafana",
                        @namespace:   KubeNamespaces.NeonMonitor, 
                        prioritySpec: PriorityClass.NeonMonitor.Name, 
                        values:       values);
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/grafana-secrets",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "renew grafana secrets");

                        var dbSecret      = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);
                        var grafanaSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.GrafanaSecret, KubeNamespaces.NeonMonitor);

                        grafanaSecret.Data["DATABASE_PASSWORD"] = dbSecret.Data["password"];
                        await k8s.UpsertSecretAsync(grafanaSecret, KubeNamespaces.NeonMonitor);

                        var grafanaAdminSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.GrafanaAdminSecret, KubeNamespaces.NeonMonitor);

                        grafanaAdminSecret.Data["GF_SECURITY_ADMIN_PASSWORD"] = Encoding.UTF8.GetBytes(NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
                        await k8s.UpsertSecretAsync(grafanaAdminSecret, KubeNamespaces.NeonMonitor);
                    });

                await master.InvokeIdempotentAsync("ready-to-go/grafana-clear-users",
                    async () =>
                    {
                        var master = (await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem, labelSelector: "app=neon-system-db")).Items.First();

                        var command = new string[]
                            {
                                "/bin/bash",
                                "-c",
                                $@"psql -U {KubeConst.NeonSystemDbAdminUser} grafana -t -c ""DELETE FROM public.user;"""
                            };

                        var result = await k8s.NamespacedPodExecAsync(
                            name:               master.Name(),
                            namespaceParameter: master.Namespace(),
                            container:          "postgres",
                            command:            command);
                    });

                await master.InvokeIdempotentAsync("ready-to-go/grafana-ingress",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update grafana ingress");

                        var virtualService = await k8s.GetNamespacedCustomObjectAsync<VirtualService>(KubeNamespaces.NeonIngress, "grafana-dashboard-virtual-service");

                        virtualService.Spec.Hosts =
                            new List<string>()
                            {
                                $"{ClusterDomain.Grafana}.{cluster.Definition.Domain}"
                            };

                        await k8s.ReplaceNamespacedCustomObjectAsync<VirtualService>(virtualService, KubeNamespaces.NeonIngress, virtualService.Name());
                    });

                await master.InvokeIdempotentAsync("ready-to-go/grafana-crd-config",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update grafana crd config");

                        var grafana = await k8s.GetNamespacedCustomObjectAsync<Grafana>(KubeNamespaces.NeonMonitor, "grafana");

                        grafana.Spec["config"]["auth.generic_oauth"]["api_url"]   = $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}/userinfo";
                        grafana.Spec["config"]["auth.generic_oauth"]["auth_url"]  = $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}/auth";
                        grafana.Spec["config"]["auth.generic_oauth"]["token_url"] = $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}/token";

                        grafana.Spec["config"]["server"]["root_url"] = $"https://{ClusterDomain.Grafana}.{cluster.Definition.Domain}";

                        await k8s.ReplaceNamespacedCustomObjectAsync(grafana, KubeNamespaces.NeonMonitor, grafana.Name());
                    });

                var grafana = await k8s.ReadNamespacedDeploymentAsync("grafana-deployment", KubeNamespaces.NeonMonitor);
                await grafana.RestartAsync(k8s);
            }

            await master.InvokeIdempotentAsync($"{readyToGoMode}/monitoring-grafana-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "neon-grafana");

                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            try
                            {
                                var configmap = await k8s.ReadNamespacedConfigMapAsync("grafana-datasources", KubeNamespaces.NeonMonitor);

                                if (configmap.Data == null || configmap.Data.Keys.Count < 3)
                                {
                                    await (await k8s.ReadNamespacedDeploymentAsync("grafana-operator", KubeNamespaces.NeonMonitor)).RestartAsync(k8s);
                                    return false;
                                }
                            } 
                            catch
                            {
                                return false;
                            }

                            return true;
                        }, TimeSpan.FromMinutes(5),
                        TimeSpan.FromSeconds(60));

                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonMonitor, "grafana-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonMonitor, "grafana-deployment", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });

            await master.InvokeIdempotentAsync($"{readyToGoMode}/monitoring-grafana-kiali-user",
                async () =>
                {
                    controller.LogProgress(master, verb: "create", message: "kiali-grafana-user");

                    var grafanaSecret   = await k8s.ReadNamespacedSecretAsync("grafana-admin-credentials", KubeNamespaces.NeonMonitor);
                    var grafanaUser     = Encoding.UTF8.GetString(grafanaSecret.Data["GF_SECURITY_ADMIN_USER"]);
                    var grafanaPassword = Encoding.UTF8.GetString(grafanaSecret.Data["GF_SECURITY_ADMIN_PASSWORD"]);
                    var kialiSecret     = await k8s.ReadNamespacedSecretAsync("kiali", KubeNamespaces.NeonSystem);
                    var kialiPassword   = Encoding.UTF8.GetString(kialiSecret.Data["grafanaPassword"]);

                    var cmd = new string[]
                    {
                        "/bin/bash",
                        "-c",
                        $@"wget -q -O- --post-data='{{""name"":""kiali"",""email"":""kiali@cluster.local"",""login"":""kiali"",""password"":""{kialiPassword}"",""OrgId"":1}}' --header='Content-Type:application/json' http://{grafanaUser}:{grafanaPassword}@localhost:3000/api/admin/users"
                    };

                    var pod = (await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonMonitor, labelSelector: "app=grafana")).Items.First();

                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            try
                            {
                                (await k8s.NamespacedPodExecAsync(pod.Namespace(), pod.Name(), "grafana", cmd)).EnsureSuccess();

                                return true;
                            }
                            catch
                            {
                                await (await k8s.ReadNamespacedDeploymentAsync("grafana-deployment", KubeNamespaces.NeonMonitor)).RestartAsync(k8s);
                                await (await k8s.ReadNamespacedDeploymentAsync("grafana-operator", KubeNamespaces.NeonMonitor)).RestartAsync(k8s);
                                return false;
                            }
                        },
                        timeout:      TimeSpan.FromMinutes(10),
                        pollInterval: TimeSpan.FromSeconds(15));
                });
        }

        /// <summary>
        /// Installs a Minio cluster to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMinioAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Minio);

            await master.InvokeIdempotentAsync("setup/minio-all",
                async () =>
                {
                    await CreateHostPathStorageClass(controller, master, "neon-internal-minio");

                    await master.InvokeIdempotentAsync("setup/minio",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "setup", message: "minio");

                            var values = new Dictionary<string, object>();

                            values.Add("cluster.name", cluster.Definition.Name);
                            values.Add("cluster.domain", cluster.Definition.Domain);
                            values.Add($"metrics.serviceMonitor.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                            values.Add($"metrics.serviceMonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);
                            values.Add("image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("mcImage.organization", KubeConst.LocalClusterRegistry);
                            values.Add("helmKubectlJqImage.organization", KubeConst.LocalClusterRegistry);
                            values.Add($"tenants[0].pools[0].servers", serviceAdvice.ReplicaCount);
                            values.Add("ingress.operator.subdomain", ClusterDomain.Minio);

                            if (serviceAdvice.ReplicaCount > 1)
                            {
                                values.Add($"mode", "distributed");
                            }

                            if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
                            {
                                values.Add($"tenants[0].pools[0].resources.requests.memory", ToSiString(serviceAdvice.PodMemoryRequest));
                                values.Add($"tenants[0].pools[0].resources.limits.memory", ToSiString(serviceAdvice.PodMemoryLimit));
                            }
                            
                            var accessKey = NeonHelper.GetCryptoRandomPassword(16);
                            var secretKey = NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength);

                            values.Add($"tenants[0].secrets.accessKey", accessKey);
                            values.Add($"clients.aliases.minio.accessKey", accessKey);
                            values.Add($"tenants[0].secrets.secretKey", secretKey);
                            values.Add($"clients.aliases.minio.secretKey", secretKey);

                            values.Add($"tenants[0].console.secrets.passphrase", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
                            values.Add($"tenants[0].console.secrets.salt", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
                            values.Add($"tenants[0].console.secrets.accessKey", NeonHelper.GetCryptoRandomPassword(16));
                            values.Add($"tenants[0].console.secrets.secretKey", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));

                            int i = 0;

                            foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelMetricsInternal, "true"))
                            {
                                values.Add($"tenants[0].pools[0].tolerations[{i}].key", serviceAdvice.ReplicaCount);
                                values.Add($"tenants[0].pools[0].tolerations[{i}].effect", serviceAdvice.ReplicaCount);
                                values.Add($"tenants[0].pools[0].tolerations[{i}].operator", serviceAdvice.ReplicaCount);

                                values.Add($"console.tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                                values.Add($"console.tolerations[{i}].effect", taint.Effect);
                                values.Add($"console.tolerations[{i}].operator", "Exists");

                                values.Add($"operator.tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                                values.Add($"operator.tolerations[{i}].effect", taint.Effect);
                                values.Add($"operator.tolerations[{i}].operator", "Exists");
                                i++;
                            }

                            values.Add("tenants[0].priorityClassName", PriorityClass.NeonStorage.Name);

                            await master.InstallHelmChartAsync(controller, "minio", 
                                releaseName:  "minio", 
                                @namespace:   KubeNamespaces.NeonSystem,
                                prioritySpec: PriorityClass.NeonStorage.Name,
                                values:       values);
                        });

                    await master.InvokeIdempotentAsync("configure/minio-secrets",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "configure", message: "minio secret");

                            var secret = await k8s.ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);

                            secret.Metadata.NamespaceProperty = "monitoring";

                            var monitoringSecret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name = secret.Name(),
                                    Annotations = new Dictionary<string, string>()
                                    {
                                        { "reloader.stakater.com/match", "true" }
                                    }
                                },
                                Data = secret.Data,
                            };
                            await k8s.CreateNamespacedSecretAsync(monitoringSecret, KubeNamespaces.NeonMonitor);
                        });

                    await master.InvokeIdempotentAsync("setup/minio-ready",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "wait for", message: "minio");

                            await NeonHelper.WaitAllAsync(
                                new List<Task>()
                                {
                                    k8s.WaitForStatefulSetAsync(KubeNamespaces.NeonSystem, labelSelector: "app=minio", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                                    k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "minio-console", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                                    k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "minio-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                                });
                        });

                    await master.InvokeIdempotentAsync("setup/minio-policy",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "wait for", message: "minio");

                            var minioPod = (await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem, labelSelector: "app.kubernetes.io/name=minio-operator")).Items.First();

                            await k8s.NamespacedPodExecAsync(
                                KubeNamespaces.NeonSystem,
                                minioPod.Name(),
                                "minio-operator",
                                new string[] {
                                    "/bin/bash",
                                    "-c",
                                    $@"echo '{{""Version"":""2012-10-17"",""Statement"":[{{""Effect"":""Allow"",""Action"":[""admin:*""]}},{{""Effect"":""Allow"",""Action"":[""s3:*""],""Resource"":[""arn:aws:s3:::*""]}}]}}' > /tmp/superadmin.json"
                                });

                            await k8s.NamespacedPodExecAsync(
                                KubeNamespaces.NeonSystem,
                                minioPod.Name(),
                                "minio-operator",
                                new string[] {
                                    "/bin/bash",
                                    "-c",
                                    $"/mc admin policy add minio superadmin /tmp/superadmin.json"
                                });
                        });
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/minio-secrets",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "renew minio secret");

                        var secret = await k8s.ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);

                        secret.Data["accesskey"] = Encoding.UTF8.GetBytes(NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
                        secret.Data["secretkey"] = Encoding.UTF8.GetBytes(NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
                        await k8s.ReplaceNamespacedSecretAsync(secret, "minio", KubeNamespaces.NeonSystem);

                        var monitoringSecret = await k8s.ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonMonitor);

                        monitoringSecret.Data["accesskey"] = secret.Data["accesskey"];
                        monitoringSecret.Data["secretkey"] = secret.Data["secretkey"];
                        await k8s.ReplaceNamespacedSecretAsync(monitoringSecret, monitoringSecret.Name(), KubeNamespaces.NeonMonitor);

                        var registrySecret = await k8s.ReadNamespacedSecretAsync("registry-minio", KubeNamespaces.NeonSystem);

                        registrySecret.Data["accesskey"] = secret.Data["accesskey"];
                        registrySecret.Data["secretkey"] = secret.Data["secretkey"];
                        registrySecret.Data["secret"] = secret.Data["secretkey"];
                        await k8s.ReplaceNamespacedSecretAsync(registrySecret, registrySecret.Name(), KubeNamespaces.NeonSystem);

                        // Delete certs so that they will be regenerated.

                        await k8s.DeleteNamespacedSecretAsync("operator-tls", KubeNamespaces.NeonSystem);
                        await k8s.DeleteNamespacedSecretAsync("operator-webhook-secret", KubeNamespaces.NeonSystem);

                        // Update configmap containing SSO urls

                        var configMap = await k8s.ReadNamespacedConfigMapAsync("minio-console", KubeNamespaces.NeonSystem);

                        configMap.Data["CONSOLE_IDP_URL"]      = $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}/.well-known/openid-configuration";
                        configMap.Data["CONSOLE_IDP_CALLBACK"] = $"https://{ClusterDomain.Minio}.{cluster.Definition.Domain}/oauth_callback";

                        await k8s.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Namespace());

                        // Restart minio components.

                        var minioOperator = await k8s.ReadNamespacedDeploymentAsync("minio-operator", KubeNamespaces.NeonSystem);

                        await minioOperator.RestartAsync(GetK8sClient(controller));

                        var minioStatefulSet = (await k8s.ListNamespacedStatefulSetAsync(KubeNamespaces.NeonSystem, labelSelector: "app=minio")).Items.FirstOrDefault();
                        await minioStatefulSet.RestartAsync(GetK8sClient(controller));
                    });

                await master.InvokeIdempotentAsync("ready-to-go/minio-ingress",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update minio ingress");

                        var virtualService = await k8s.GetNamespacedCustomObjectAsync<VirtualService>(KubeNamespaces.NeonIngress, "minio-operator-dashboard-virtual-service");

                        virtualService.Spec.Hosts =
                            new List<string>()
                            {
                                $"{ClusterDomain.Minio}.{cluster.Definition.Domain}"
                            };

                        await k8s.ReplaceNamespacedCustomObjectAsync<VirtualService>(virtualService, KubeNamespaces.NeonIngress, virtualService.Name());
                    });
            }
        }

        /// <summary>
        /// Installs an Neon Monitoring to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMonitoringAsync(ISetupController controller)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var master  = cluster.FirstMaster;
            var tasks   = new List<Task>();

            controller.LogProgress(master, verb: "setup", message: "cluster metrics");

            tasks.Add(WaitForPrometheusAsync(controller, master));
            tasks.Add(InstallCortexAsync(controller, master));
            tasks.Add(InstallLokiAsync(controller, master));
            tasks.Add(InstallKubeStateMetricsAsync(controller, master));
            tasks.Add(InstallTempoAsync(controller, master));
            tasks.Add(InstallGrafanaAsync(controller, master));

            await NeonHelper.WaitAllAsync(tasks);
        }

        /// <summary>
        /// Installs a harbor container registry and required components.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallRedisAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin  = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Redis);

            await master.InvokeIdempotentAsync("setup/redis",
                async () =>
                {
                    await SyncContext.ClearAsync;

                    controller.LogProgress(master, verb: "setup", message: "redis");

                    var values   = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    values.Add($"replicas", serviceAdvice.ReplicaCount);
                    values.Add($"haproxy.metrics.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"exporter.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"exporter.serviceMonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                    if (serviceAdvice.ReplicaCount < 2)
                    {
                        values.Add($"hardAntiAffinity", false);
                        values.Add($"sentinel.quorum", 1);
                    }

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelNeonSystemRegistry, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "redis-ha", 
                        releaseName:  "neon-redis", 
                        @namespace:   KubeNamespaces.NeonSystem, 
                        prioritySpec: PriorityClass.NeonData.Name,
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/redis-ready",
                async () =>
                {
                    await SyncContext.ClearAsync;

                    controller.LogProgress(master, verb: "wait for", message: "redis");

                    await k8s.WaitForStatefulSetAsync(KubeNamespaces.NeonSystem, "neon-redis-server", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs a harbor container registry and required components.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallHarborAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin  = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Harbor);

            await master.InvokeIdempotentAsync("configure/registry-minio-secret",
                async () =>
                {
                    controller.LogProgress(master, verb: "configure", message: "minio secret");

                    var minioSecret = await k8s.ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = "registry-minio",
                            NamespaceProperty = KubeNamespaces.NeonSystem,
                            Annotations       = new Dictionary<string, string>()
                            {
                                {  "reloader.stakater.com/match", "true" }
                            }
                        },
                        Type = "Opaque",
                        Data = new Dictionary<string, byte[]>()
                        {
                            { "secret", minioSecret.Data["secretkey"] }
                        }
                    };

                    await k8s.CreateNamespacedSecretAsync(secret, KubeNamespaces.NeonSystem);
                });

            await master.InvokeIdempotentAsync("setup/harbor-db",
                async () =>
                {
                    controller.LogProgress(master, verb: "configure", message: "harbor databases");

                    await CreateStorageClass(controller, master, "neon-internal-registry");

                    // Create the Harbor databases.

                    var dbSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);

                    var harborSecret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = KubeConst.RegistrySecretKey,
                            NamespaceProperty = KubeNamespaces.NeonSystem
                        },
                        Data       = new Dictionary<string, byte[]>(),
                        StringData = new Dictionary<string, string>()
                    };

                    if ((await k8s.ListNamespacedSecretAsync(KubeNamespaces.NeonSystem)).Items.Any(s => s.Metadata.Name == KubeConst.RegistrySecretKey))
                    {
                        harborSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.RegistrySecretKey, KubeNamespaces.NeonSystem);

                        if (harborSecret.Data == null)
                        {
                            harborSecret.Data = new Dictionary<string, byte[]>();
                        }

                        harborSecret.StringData = new Dictionary<string, string>();
                    }

                    if (!harborSecret.Data.ContainsKey("postgresql-password"))
                    {
                        harborSecret.Data["postgresql-password"] = dbSecret.Data["password"];

                        await k8s.UpsertSecretAsync(harborSecret, KubeNamespaces.NeonSystem);
                    }

                    if (!harborSecret.Data.ContainsKey("secret"))
                    {
                        harborSecret.StringData["secret"] = NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength);

                        await k8s.UpsertSecretAsync(harborSecret, KubeNamespaces.NeonSystem);
                    }
                });

                await master.InvokeIdempotentAsync("setup/harbor",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "configure", message: "harbor minio");

                        // Create the Harbor Minio bucket.

                        var minioSecret = await k8s.ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);
                        var accessKey   = Encoding.UTF8.GetString(minioSecret.Data["accesskey"]);
                        var secretKey   = Encoding.UTF8.GetString(minioSecret.Data["secretkey"]);
                        var serviceUser = await KubeHelper.GetClusterLdapUserAsync(k8s, "serviceuser");

                        await CreateMinioBucketAsync(controller, master, "harbor");

                        // Install the Harbor Helm chart.

                        var values = new Dictionary<string, object>();

                        values.Add("cluster.name", cluster.Definition.Name);
                        values.Add("cluster.domain", cluster.Definition.Domain);
                        values.Add($"metrics.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                        values.Add($"metrics.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                        values.Add("ingress.notary.subdomain", ClusterDomain.HarborNotary);
                        values.Add("ingress.registry.subdomain", ClusterDomain.HarborRegistry);
                        
                        values.Add($"storage.s3.accessKey", Encoding.UTF8.GetString(minioSecret.Data["accesskey"]));
                        values.Add($"storage.s3.secretKeyRef", "registry-minio");

                        var baseDN = $@"dc={string.Join($@"\,dc=", cluster.Definition.Domain.Split('.'))}";

                        values.Add($"ldap.baseDN", baseDN);
                        values.Add($"ldap.secret", serviceUser.Password);

                        int j = 0;

                        foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelNeonSystemRegistry, "true"))
                        {
                            values.Add($"tolerations[{j}].key", $"{taint.Key.Split("=")[0]}");
                            values.Add($"tolerations[{j}].effect", taint.Effect);
                            values.Add($"tolerations[{j}].operator", "Exists");
                            j++;
                        }

                        values.Add("nginx.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("portal.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("core.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("jobservice.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("registry.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("chartmuseum.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("clair.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("notary.server.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("notary.signer.priorityClassName", PriorityClass.NeonData.Name);
                        values.Add("trivy.priorityClassName", PriorityClass.NeonData.Name);

                        await master.InstallHelmChartAsync(controller, "harbor",
                            releaseName:  "registry-harbor",
                            @namespace:   KubeNamespaces.NeonSystem,
                            prioritySpec: PriorityClass.NeonData.Name,
                            values:       values);
                    });

                if (readyToGoMode == ReadyToGoMode.Setup)
                {
                    await master.InvokeIdempotentAsync($"ready-to-go/harbor-cluster",
                        async () =>
                        {
                            var harborCluster = await k8s.GetNamespacedCustomObjectAsync<HarborCluster>(KubeNamespaces.NeonSystem, "registry");
                            var minioSecret   = await k8s.ReadNamespacedSecretAsync("registry-minio", KubeNamespaces.NeonSystem);

                            harborCluster.Spec["expose"]["core"]["ingress"]["host"]    = $"https://registry.{clusterLogin.ClusterDefinition.Domain}";
                            harborCluster.Spec["expose"]["notary"]["ingress"]["host"]  = $"https://notary.{clusterLogin.ClusterDefinition.Domain}";
                            harborCluster.Spec["externalURL"]                          = $"https://registry.{clusterLogin.ClusterDefinition.Domain}";
                            harborCluster.Spec["imageChartStorage"]["s3"]["accesskey"] = Encoding.UTF8.GetString(minioSecret.Data["accesskey"]);

                            await k8s.ReplaceNamespacedCustomObjectAsync(harborCluster, KubeNamespaces.NeonSystem, harborCluster.Name());
                        });

                await master.InvokeIdempotentAsync($"ready-to-go/harbor-configuration",
                    async () =>
                    {
                        var harborConfig = await k8s.GetNamespacedCustomObjectAsync<HarborConfiguration>(KubeNamespaces.NeonSystem, "ldap-config");

                        var baseDN = $@"dc={string.Join($@",dc=", cluster.Definition.Domain.Split('.'))}";

                        harborConfig.Spec["configuration"]["ldapBaseDn"]       = $"cn=users,{baseDN}";
                        harborConfig.Spec["configuration"]["ldapGroupAdminDn"] = $"ou=superadmin,ou=groups,{baseDN}";
                        harborConfig.Spec["configuration"]["ldapGroupBaseDn"]  = $"ou=users,{baseDN}";
                        harborConfig.Spec["configuration"]["ldapSearchDn"]     = $"cn=serviceuser,ou=admin,{baseDN}";

                        await k8s.ReplaceNamespacedCustomObjectAsync(harborConfig, KubeNamespaces.NeonSystem, harborConfig.Name());
                    });

                await master.InvokeIdempotentAsync($"ready-to-go/harbor-ldap-secret",
                    async () =>
                    {
                        var serviceUser = await KubeHelper.GetClusterLdapUserAsync(k8s, "serviceuser");
                        var ldapSecret  = await k8s.ReadNamespacedSecretAsync("harbor-ldap", KubeNamespaces.NeonSystem);
                    
                        ldapSecret.Data["ldap_search_password"] = Encoding.UTF8.GetBytes(serviceUser.Password);
                    
                        await k8s.UpsertSecretAsync(ldapSecret, KubeNamespaces.NeonSystem);
                    });

                await master.InvokeIdempotentAsync($"ready-to-go/harbor-registry-configuration",
                    async () =>
                    {
                        var registryTemplateConfig = await k8s.ReadNamespacedConfigMapAsync("harbor-operator-config-template", KubeNamespaces.NeonSystem);
                        var registryTemplate       = registryTemplateConfig.Data["registry-config.yaml.tmpl"];

                        registryTemplate = Regex.Replace(registryTemplate, @"https:\/\/registry.*.neoncluster.io\/service\/token", $"https://{ClusterDomain.HarborRegistry}.{cluster.Definition.Domain}/service/token");

                        registryTemplateConfig.Data["registry-config.yaml.tmpl"] = registryTemplate;
                        await k8s.ReplaceNamespacedConfigMapAsync(registryTemplateConfig, registryTemplateConfig.Name(), registryTemplateConfig.Namespace());
                        
                        var harborChartmuseum = await k8s.ReadNamespacedDeploymentAsync("harbor-operator", KubeNamespaces.NeonSystem);

                        await harborChartmuseum.RestartAsync(GetK8sClient(controller));

                        await k8s.DeleteNamespacedConfigMapAsync("registry-harbor-harbor-registry", KubeNamespaces.NeonSystem);
                    });
            }


            await master.InvokeIdempotentAsync("setup/harbor-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "harbor");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-chartmuseum", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-core", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-jobservice", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-notaryserver", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-notarysigner", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-portal", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-registry", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-registryctl", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "registry-harbor-harbor-trivy", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval)
                        });
                });

            await master.InvokeIdempotentAsync($"{(readyToGoMode == ReadyToGoMode.Setup ? "ready-to-go" : "setup")}/harbor-credentials",
                async () =>
                {
                    controller.LogProgress(master, verb: "configure", message: "harbor credentials");

                    if (readyToGoMode == ReadyToGoMode.Setup)
                    {
                        var adminSecret   = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbAdminSecret, KubeNamespaces.NeonSystem);
                        var adminUsername = Encoding.UTF8.GetString(adminSecret.Data["username"]);
                        var adminPassword = Encoding.UTF8.GetString(adminSecret.Data["password"]);

                        var secret       = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);
                        var harborSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.RegistrySecretKey, KubeNamespaces.NeonSystem);

                        harborSecret.Data["postgresql-password"] = secret.Data["password"];
                        harborSecret.Data["secret"]              = Encoding.UTF8.GetBytes(NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));

                        await k8s.UpsertSecretAsync(harborSecret, harborSecret.Namespace());

                        // Delete secret so that the harbor operator creates a new one with the updated credential.

                        await k8s.DeleteNamespacedSecretAsync("registry-harbor-harbor-registry-basicauth", KubeNamespaces.NeonSystem);

                        var master = (await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem, labelSelector: "app=neon-system-db")).Items.First();

                        var command = new string[]
                            {
                                "/bin/bash",
                                "-c",
                                $@"psql -U {KubeConst.NeonSystemDbAdminUser} harbor_core -t -c ""UPDATE public.harbor_user SET password='', salt = '' WHERE user_id = 1;"""
                            };

                        var result = await k8s.NamespacedPodExecAsync(
                            name:               master.Name(),
                            namespaceParameter: master.Namespace(),
                            container:          "postgres",
                            command:            command);

                        // Restart registry components.

                        var harborChartmuseum = await k8s.ReadNamespacedDeploymentAsync("registry-harbor-harbor-chartmuseum", KubeNamespaces.NeonSystem);

                        await harborChartmuseum.RestartAsync(GetK8sClient(controller));

                        var harborCore = await k8s.ReadNamespacedDeploymentAsync("registry-harbor-harbor-core", KubeNamespaces.NeonSystem);

                        await harborCore.RestartAsync(GetK8sClient(controller));

                        var harborRegistry = await k8s.ReadNamespacedDeploymentAsync("registry-harbor-harbor-registry", KubeNamespaces.NeonSystem);

                        await harborRegistry.RestartAsync(GetK8sClient(controller));

                        var harborRegistryctl = await k8s.ReadNamespacedDeploymentAsync("registry-harbor-harbor-registryctl", KubeNamespaces.NeonSystem);

                        await harborRegistryctl.RestartAsync(GetK8sClient(controller));
                    }
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync($"{(readyToGoMode == ReadyToGoMode.Setup ? "ready-to-go" : "setup")}/harbor-ingress",
                    async () =>
                    {
                        var virtualService = await k8s.GetNamespacedCustomObjectAsync<VirtualService>(KubeNamespaces.NeonIngress, "harbor-virtual-service");

                        virtualService.Spec.Hosts =
                            new List<string>()
                            {
                                $"{ClusterDomain.HarborRegistry}.{cluster.Definition.Domain}",
                                $"{ClusterDomain.HarborNotary}.{cluster.Definition.Domain}",
                                KubeConst.LocalClusterRegistry
                            };

                        await k8s.ReplaceNamespacedCustomObjectAsync<VirtualService>(virtualService, KubeNamespaces.NeonIngress, virtualService.Name());
                    });
            }

            await master.InvokeIdempotentAsync($"{(readyToGoMode == ReadyToGoMode.Setup ? "ready-to-go" : "setup")}/harbor-login",
                async () =>
                {
                    var user       = await KubeHelper.GetClusterLdapUserAsync(k8s, "root");
                    var password   = user.Password;
                    var sbScript   = new StringBuilder();
                    var sbArgs     = new StringBuilder();

                    sbScript.AppendLineLinux("#!/bin/bash");
                    sbScript.AppendLineLinux($"echo '{password}' | podman login neon-registry.node.local --username {user.Name} --password-stdin");

                    foreach (var node in cluster.Nodes)
                    {
                        await NeonHelper.WaitForAsync(
                            async () =>
                            {
                                try
                                {
                                    master.SudoCommand(CommandBundle.FromScript(sbScript), RunOptions.None).EnsureSuccess();

                                    return await Task.FromResult(true);
                                }
                                catch
                                {
                                    return await Task.FromResult(false);
                                }
                            },
                            timeout:      TimeSpan.FromSeconds(300),
                            pollInterval: TimeSpan.FromSeconds(1));
                    }
                });
        }

        /// <summary>
        /// Installs <b>neon-cluster-operator</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallClusterOperatorAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var k8s = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/cluster-operator",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "neon-cluster-operator");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("image.tag", KubeVersions.NeonKubeContainerImageTag);

                    await master.InstallHelmChartAsync(controller, "neon-cluster-operator",
                        releaseName:  "neon-cluster-operator",
                        @namespace:    KubeNamespaces.NeonSystem,
                        prioritySpec: PriorityClass.NeonOperator.Name,
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/cluster-operator-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "neon-cluster-operator");

                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "neon-cluster-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs <b>neon-dashboard</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallNeonDashboardAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var k8s           = GetK8sClient(controller);
            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);

            await master.InvokeIdempotentAsync("setup/neon-dashboard",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "neon-dashboard");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("image.tag", KubeVersions.NeonKubeContainerImageTag);
                    values.Add("cluster.name", cluster.Definition.Name);
                    values.Add("cluster.domain", cluster.Definition.Domain);

                    await master.InstallHelmChartAsync(controller, "neon-dashboard", 
                        releaseName:  "neon-dashboard",
                        @namespace:   KubeNamespaces.NeonSystem,
                        prioritySpec: PriorityClass.NeonApp.Name,
                        values:       values);
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("setup/neon-dashboard-config",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update neon-dashboard config");

                        var configMap = await k8s.ReadNamespacedConfigMapAsync("neon-dashboard", KubeNamespaces.NeonSystem);
                        configMap.Data["CLUSTER_DOMAIN"] = cluster.Definition.Domain;

                        await k8s.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Namespace());
                    });

                await master.InvokeIdempotentAsync("setup/neon-dashboard-ingress",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update neon dashboard ingress");

                        var virtualService = await k8s.GetNamespacedCustomObjectAsync<VirtualService>(KubeNamespaces.NeonIngress, "neon-dashboard");

                        virtualService.Spec.Hosts =
                            new List<string>()
                            {
                                $"{cluster.Definition.Domain}"
                            };

                        await k8s.ReplaceNamespacedCustomObjectAsync<VirtualService>(virtualService, KubeNamespaces.NeonIngress, virtualService.Name());
                    });
            }
            
            await master.InvokeIdempotentAsync("setup/neon-dashboard-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "neon-dashboard");

                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "neon-dashboard", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Installs <b>neon-node-agent</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallNodeAgentAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var k8s = GetK8sClient(controller);

            await master.InvokeIdempotentAsync("setup/neon-node-agent",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "neon-node-agent");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("image.tag", KubeVersions.NeonKubeContainerImageTag);

                    await master.InstallHelmChartAsync(controller, "neon-node-agent",
                        releaseName:  "neon-node-agent",
                        @namespace:   KubeNamespaces.NeonSystem,
                        prioritySpec: PriorityClass.NeonOperator.Name,
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/neon-node-agent-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "neon-node-agent");

                    await k8s.WaitForDaemonsetAsync(KubeNamespaces.NeonSystem, "neon-node-agent", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });
        }

        /// <summary>
        /// Adds custom <see cref="V1ContainerRegistry"/> resources defined in the cluster definition to
        /// the cluster.  <b>neon-node-agent</b> will pick these up and regenerate the CRI-O configuration.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This must be called after <see cref="InstallClusterOperatorAsync(ISetupController, NodeSshProxy{NodeDefinition})"/>
        /// because that's where the cluster CRDs get installed.
        /// </note>
        /// </remarks>
        public static async Task InstallContainerRegistryResources(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);

            if (readyToGoMode == ReadyToGoMode.Prepare)
            {
                // Defer registry configuration until full cluster setup.

                return;
            }

            await master.InvokeIdempotentAsync("setup/container-registries",
                async () =>
                {
                    var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var k8s     = GetK8sClient(controller);

                    // We need to add the implict local cluster Harbor registry.

                    var localRegistries = new List<Registry>();
                    var localRegistry   = new Registry();
                    var harborCrioUser  = await KubeHelper.GetClusterLdapUserAsync(k8s, KubeConst.HarborCrioUser);

                    localRegistry.Name     = 
                    localRegistry.Prefix   =
                    localRegistry.Location = KubeConst.LocalClusterRegistry;
                    localRegistry.Blocked  = false;
                    localRegistry.Insecure = true;
                    localRegistry.Username = harborCrioUser.Name;
                    localRegistry.Password = harborCrioUser.Password;

                    localRegistries.Add(localRegistry);

                    // Add registries from the cluster definition.

                    foreach (var registry in cluster.Definition.Container.Registries)
                    {
                        localRegistries.Add(registry);
                    }

                    // Write the custom resources to the cluster.

                    foreach (var registry in localRegistries)
                    {
                        var clusterRegistry = new V1ContainerRegistry();

                        clusterRegistry.Spec.SearchOrder = cluster.Definition.Container.SearchRegistries.IndexOf(registry.Location);
                        clusterRegistry.Spec.Prefix      = registry.Prefix;
                        clusterRegistry.Spec.Location    = registry.Location;
                        clusterRegistry.Spec.Blocked     = registry.Blocked;
                        clusterRegistry.Spec.Insecure    = registry.Insecure;
                        clusterRegistry.Spec.Username    = registry.Username;
                        clusterRegistry.Spec.Password    = registry.Password;

                        await k8s.UpsertClusterCustomObjectAsync(clusterRegistry, registry.Name);
                    }
                });
        }

        /// <summary>
        /// Creates the required namespaces.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task<List<Task>> CreateNamespacesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await SyncContext.ClearAsync;

            var tasks = new List<Task>();

            tasks.Add(CreateNamespaceAsync(controller, master, KubeNamespaces.NeonMonitor, true));
            tasks.Add(CreateNamespaceAsync(controller, master, KubeNamespaces.NeonStorage, false));
            tasks.Add(CreateNamespaceAsync(controller, master, KubeNamespaces.NeonSystem, true));

            return await Task.FromResult(tasks);
        }

        /// <summary>
        /// Installs a Citus-postgres database used by neon-system services.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallSystemDbAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.NeonSystemDb);

            var values = new Dictionary<string, object>();

            values.Add($"metrics.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
            values.Add($"metrics.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

            if (cluster.Definition.IsDesktopCluster)
            {
                values.Add($"persistence.size", "1Gi");
            }

            await CreateStorageClass(controller, master, "neon-internal-system-db");

            if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"resources.requests.memory", ToSiString(serviceAdvice.PodMemoryRequest));
                values.Add($"resources.limits.memory", ToSiString(serviceAdvice.PodMemoryLimit));
            }

            await master.InvokeIdempotentAsync("setup/db-credentials-admin",
                async () =>
                {
                    var username = KubeConst.NeonSystemDbAdminUser;
                    var password = NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength);

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = KubeConst.NeonSystemDbAdminSecret,
                            Annotations = new Dictionary<string, string>()
                            {
                                {  "reloader.stakater.com/match", "true" }
                            }
                        },
                        Type = "Opaque",
                        StringData = new Dictionary<string, string>()
                        {
                            { "username", username },
                            { "password", password }
                        }
                    };

                    await k8s.CreateNamespacedSecretAsync(secret, KubeNamespaces.NeonSystem);
                });

            await master.InvokeIdempotentAsync("setup/db-credentials-service",
                async () =>
                {
                    var username = KubeConst.NeonSystemDbServiceUser;
                    var password = NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength);

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = KubeConst.NeonSystemDbServiceSecret,
                            Annotations = new Dictionary<string, string>()
                            {
                                {  "reloader.stakater.com/match", "true" } 
                            }
                        },
                        Type = "Opaque",
                        StringData = new Dictionary<string, string>()
                        {
                            { "username", username },
                            { "password", password }
                        }
                    };

                    await k8s.CreateNamespacedSecretAsync(secret, KubeNamespaces.NeonSystem);
                });

            await master.InvokeIdempotentAsync("setup/system-db",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "neon-system-db");

                    values.Add($"replicas", serviceAdvice.ReplicaCount);

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelNeonSystemDb, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    // We're going to set the pod priority class to the same value as 
                    // the postgres operator.

                    // $todo(jefflill):
                    //
                    // Commenting this out temporarily.  It appears that the Posgres operator
                    // is trying to create this priority?  This doesn't happen every time and
                    // this might also be a Helm chart issue:
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/1414

                    //values.Add("podPriorityClassName", PriorityClass.NeonData.Name);

                    await master.InstallHelmChartAsync(controller, "postgres-operator", 
                        releaseName:     "neon-system-db", 
                        @namespace:      KubeNamespaces.NeonSystem, 
                        prioritySpec:    PriorityClass.NeonData.Name,
                        values:          values, 
                        progressMessage: "neon-system-db");
                });

            await master.InvokeIdempotentAsync("setup/system-db-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "neon-system-db");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "neon-system-db-postgres-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                            k8s.WaitForStatefulSetAsync(KubeNamespaces.NeonSystem, "neon-system-db", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval),
                        });
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("setup/neon-system-db-ready-to-go",
                   async () =>
                   {
                       await ResetPostgresUserAsync(k8s, "postgres", "postgres.neon-system-db.credentials.postgresql", KubeNamespaces.NeonSystem, cluster.Definition.Security.PasswordLength);
                       await ResetPostgresUserAsync(k8s, "standby", "standby.neon-system-db.credentials.postgresql", KubeNamespaces.NeonSystem, cluster.Definition.Security.PasswordLength);
                       await ResetPostgresUserAsync(k8s, KubeConst.NeonSystemDbAdminUser, KubeConst.NeonSystemDbAdminSecret, KubeNamespaces.NeonSystem, cluster.Definition.Security.PasswordLength);
                       await ResetPostgresUserAsync(k8s, KubeConst.NeonSystemDbServiceUser, KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem, cluster.Definition.Security.PasswordLength);

                       await (await k8s.ReadNamespacedStatefulSetAsync("neon-system-db", KubeNamespaces.NeonSystem)).RestartAsync(k8s);
                   });
             }
        }

        private static async Task ResetPostgresUserAsync(IKubernetes k8s, string username, string secretName, string secretNamespace, int passwordLength = 20)
        {
            var secret = await k8s.ReadNamespacedSecretAsync(secretName, secretNamespace);
            var password = NeonHelper.GetCryptoRandomPassword(passwordLength);
            secret.Data["password"] = Encoding.UTF8.GetBytes(password);
            await k8s.UpsertSecretAsync(secret, secretNamespace);

            var postgres = (await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem, labelSelector: "app=neon-system-db")).Items.First();

            var command = new string[]
            {
                "/bin/bash",
                "-c",
                $@"psql -U {KubeConst.NeonSystemDbAdminUser} postgres -t -c ""ALTER ROLE {username} WITH PASSWORD '{password}';"""
            };

            var result = await k8s.NamespacedPodExecAsync(
                name:               postgres.Name(),
                namespaceParameter: postgres.Namespace(),
                container:          "postgres",
                command:            command);
        }

        /// <summary>
        /// Installs Keycloak.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallSsoAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);

            await InstallGlauthAsync(controller, master);
            await InstallDexAsync(controller, master);
            await InstallNeonSsoProxyAsync(controller, master);
            await InstallOauth2ProxyAsync(controller, master);
        }

        /// <summary>
        /// Installs Dex.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallDexAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Dex);
            var serviceUser   = await KubeHelper.GetClusterLdapUserAsync(k8s, "serviceuser");

            var values = new Dictionary<string, object>();

            values.Add("cluster.name", cluster.Definition.Name);
            values.Add("cluster.domain", cluster.Definition.Domain);
            values.Add("ingress.subdomain", ClusterDomain.Sso);

            values.Add("secrets.grafana", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
            values.Add("secrets.harbor", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
            values.Add("secrets.kubernetes", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
            values.Add("secrets.minio", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
            values.Add("secrets.ldap", serviceUser.Password);

            values.Add("config.issuer", $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}");

            // LDAP

            var baseDN = $@"dc={string.Join($@"\,dc=", cluster.Definition.Domain.Split('.'))}";

            values.Add("config.ldap.bindDN", $@"cn=serviceuser\,ou=admin\,{baseDN}");
            values.Add("config.ldap.userSearch.baseDN", $@"cn=users\,{baseDN}");
            values.Add("config.ldap.groupSearch.baseDN", $@"ou=users\,{baseDN}");

            if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"resources.requests.memory", ToSiString(serviceAdvice.PodMemoryRequest));
                values.Add($"resources.limits.memory", ToSiString(serviceAdvice.PodMemoryLimit));
            }

            await master.InvokeIdempotentAsync("setup/dex-install",
                async () =>
                {
                    await master.InstallHelmChartAsync(controller, "dex", 
                        releaseName:     "dex", 
                        @namespace:      KubeNamespaces.NeonSystem, 
                        prioritySpec:    PriorityClass.NeonApi.Name, 
                        values:          values, 
                        progressMessage: "dex");
                });

            await master.InvokeIdempotentAsync("setup/dex-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "neon-sso");

                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "neon-sso-dex", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/dex-secret",
                   async () =>
                   {
                       var secret = await k8s.ReadNamespacedSecretAsync(KubeConst.DexSecret, KubeNamespaces.NeonSystem);

                       foreach (var key in secret.Data.Keys)
                       {
                           secret.Data[key] = Encoding.UTF8.GetBytes(NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));
                       }

                       await k8s.ReplaceNamespacedSecretAsync(secret, secret.Name(), secret.Namespace());
                   });

                await master.InvokeIdempotentAsync("ready-to-go/dex-config",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update dex configuration");

                        var dexSecret  = await k8s.ReadNamespacedSecretAsync(KubeConst.DexSecret, KubeNamespaces.NeonSystem);
                        var configMap  = await k8s.ReadNamespacedConfigMapAsync("neon-sso-dex", KubeNamespaces.NeonSystem);

                        var yamlConfig = NeonHelper.YamlDeserialize<dynamic>(configMap.Data["config.yaml"]);
                        var dexConfig = (DexConfig)NeonHelper.JsonDeserialize<DexConfig>(NeonHelper.JsonSerialize(yamlConfig));

                        dexConfig.Issuer = $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}";

                        var ldapConnector = dexConfig.Connectors.Where(c => c.Type == DexConnectorType.Ldap).FirstOrDefault() as DexLdapConnector;
                        var baseDN        = $@"dc={string.Join($@",dc=", cluster.Definition.Domain.Split('.'))}";
                        var serviceUser   = await KubeHelper.GetClusterLdapUserAsync(k8s, "serviceuser");

                        ldapConnector.Config.BindDN             = $@"cn=serviceuser,ou=admin,{baseDN}";
                        ldapConnector.Config.BindPW             = serviceUser.Password;
                        ldapConnector.Config.UserSearch.BaseDN  = $@"cn=users,{baseDN}";
                        ldapConnector.Config.GroupSearch.BaseDN = $@"ou=users,{baseDN}";

                        foreach (var client in dexConfig.StaticClients)
                        {
                            switch (client.Name)
                            {
                                case "Grafana":

                                    client.Secret = Encoding.UTF8.GetString(dexSecret.Data["GRAFANA_CLIENT_SECRET"]);
                                    client.RedirectUris = new List<string>()
                                    {
                                        $"https://{ClusterDomain.Grafana}.{cluster.Definition.Domain}/login/generic_oauth",
                                    };
                                    client.TrustedPeers = new List<string>() { "kubernetes", "harbor", "minio" };
                                    break;

                                case "Kubernetes":

                                    client.Secret = Encoding.UTF8.GetString(dexSecret.Data["KUBERNETES_CLIENT_SECRET"]);
                                    client.RedirectUris = new List<string>()
                                    {
                                        $"https://{ClusterDomain.KubernetesDashboard}.{cluster.Definition.Domain}/oauth2/callback",
                                        $"https://{ClusterDomain.Kiali}.{cluster.Definition.Domain}/oauth2/callback",
                                        $"https://{cluster.Definition.Domain}/oauth2/callback"
                                    };
                                    client.TrustedPeers = new List<string>() { "grafana", "harbor", "minio" };
                                    break;

                                case "Harbor":

                                    client.Secret = Encoding.UTF8.GetString(dexSecret.Data["KUBERNETES_CLIENT_SECRET"]);
                                    client.RedirectUris = new List<string>()
                                    {
                                        $"https://{ClusterDomain.HarborRegistry}.{cluster.Definition.Domain}/oauth_callback",
                                    };
                                    client.TrustedPeers = new List<string>() { "grafana", "kubernetes", "minio" };
                                    break;

                                case "Minio":

                                    client.Secret = Encoding.UTF8.GetString(dexSecret.Data["MINIO_CLIENT_SECRET"]);
                                    client.RedirectUris = new List<string>()
                                    {
                                        $"https://{ClusterDomain.Minio}.{cluster.Definition.Domain}/oauth_callback",
                                    };
                                    client.TrustedPeers = new List<string>() { "grafana", "kubernetes", "harbor" };
                                    break;
                            }
                        }

                        configMap.Data["config.yaml"] = NeonHelper.ToLinuxLineEndings(NeonHelper.YamlSerialize(dexConfig));
                        await k8s.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Namespace());
                    });
            }
        }

        /// <summary>
        /// Installs Neon SSO Session Proxy.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallNeonSsoProxyAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.NeonSsoSessionProxy);

            var values = new Dictionary<string, object>();

            values.Add("cluster.name", cluster.Definition.Name);
            values.Add("cluster.domain", cluster.Definition.Domain);
            values.Add("ingress.subdomain", ClusterDomain.Sso);
            values.Add("secrets.cipherKey", AesCipher.GenerateKey(256));
            values.Add($"metrics.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);

            if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"resources.requests.memory", ToSiString(serviceAdvice.PodMemoryRequest));
                values.Add($"resources.limits.memory", ToSiString(serviceAdvice.PodMemoryLimit));
            }

            await master.InvokeIdempotentAsync("setup/neon-sso-session-proxy-install",
                async () =>
                {
                    await master.InstallHelmChartAsync(controller, "neon-sso-session-proxy", 
                        releaseName:  "neon-sso-session-proxy", 
                        @namespace:   KubeNamespaces.NeonSystem,
                        prioritySpec: PriorityClass.NeonNetwork.Name,
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/neon-sso-session-proxy",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "neon-sso-session-proxy");

                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "neon-sso-session-proxy", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/neon-sso-ingress",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update neon sso ingress");

                        var virtualService = await k8s.GetNamespacedCustomObjectAsync<VirtualService>(KubeNamespaces.NeonIngress, "neon-sso-session-proxy");

                        virtualService.Spec.Hosts =
                            new List<string>()
                            {
                                $"{ClusterDomain.Sso}.{cluster.Definition.Domain}"
                            };

                        await k8s.ReplaceNamespacedCustomObjectAsync<VirtualService>(virtualService, KubeNamespaces.NeonIngress, virtualService.Name());
                    });

                await master.InvokeIdempotentAsync("ready-to-go/neon-sso-ingress",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update neon sso secret");

                        var sessionSecret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSsoSessionProxySecret, KubeNamespaces.NeonSystem);
                        sessionSecret.StringData["CIPHER_KEY"] = AesCipher.GenerateKey(256);

                        await k8s.ReplaceNamespacedSecretAsync(sessionSecret, sessionSecret.Name(), sessionSecret.Namespace());
                    });
            }
        }

        /// <summary>
        /// Installs Glauth.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallGlauthAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin  = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Glauth);
            var values        = new Dictionary<string, object>();
            var dbSecret      = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);
            var dbPassword    = Encoding.UTF8.GetString(dbSecret.Data["password"]);

            values.Add("cluster.name", cluster.Definition.Name);
            values.Add("cluster.domain", cluster.Definition.Domain);

            values.Add("config.backend.baseDN", $"dc={string.Join($@"\,dc=", cluster.Definition.Domain.Split('.'))}");
            values.Add("config.backend.database.user", KubeConst.NeonSystemDbServiceUser);
            values.Add("config.backend.database.password", dbPassword);

            values.Add("users.root.password", clusterLogin.SsoPassword);
            values.Add("users.serviceuser.password", NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength));

            if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"resources.requests.memory", ToSiString(serviceAdvice.PodMemoryRequest));
                values.Add($"resources.limits.memory", ToSiString(serviceAdvice.PodMemoryLimit));
            }

            await master.InvokeIdempotentAsync("setup/glauth-install",
                async () =>
                {
                    await master.InstallHelmChartAsync(controller, "glauth", 
                        releaseName:  "glauth", 
                        @namespace:   KubeNamespaces.NeonSystem,
                        prioritySpec: PriorityClass.NeonApp.Name,
                        values:       values);
                });

            await master.InvokeIdempotentAsync("setup/glauth-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "glauth");

                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "neon-sso-glauth", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });

            await master.InvokeIdempotentAsync("setup/glauth-users",
                async () =>
                {
                    controller.LogProgress(master, verb: "create", message: "glauth users");

                    controller.LogProgress(master, verb: "ready-to-go", message: "update glauth users");

                    var users  = await k8s.ReadNamespacedSecretAsync("glauth-users", KubeNamespaces.NeonSystem);
                    var groups = await k8s.ReadNamespacedSecretAsync("glauth-groups", KubeNamespaces.NeonSystem);

                    var postgres = (await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem, labelSelector: "app=neon-system-db")).Items.First();
                    
                    foreach (var g in groups.Data.Keys)
                    {
                        var group = NeonHelper.YamlDeserialize<GlauthGroup>(Encoding.UTF8.GetString(groups.Data[g]));

                        var command = new string[]
                        {
                                "/bin/bash",
                                "-c",
                                $@"psql -U {KubeConst.NeonSystemDbAdminUser} glauth -t -c ""
                                        INSERT INTO groups(name,gidnumber)
                                            VALUES('{group.Name}','{group.GidNumber}') 
                                                    ON CONFLICT (name) DO UPDATE
                                                        SET gidnumber = '{group.GidNumber}';"""
                        };

                        var result = await k8s.NamespacedPodExecAsync(
                            name: postgres.Name(),
                            namespaceParameter: postgres.Namespace(),
                            container: "postgres",
                            command: command);
                    }

                    foreach (var user in users.Data.Keys)
                    {
                        var userData     = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(users.Data[user]));
                        var name         = userData.Name;
                        var givenname    = userData.Name;
                        var mail         = $"{userData.Name}@{cluster.Definition.Domain}";
                        var uidnumber    = userData.UidNumber;
                        var primarygroup = userData.PrimaryGroup;
                        var passsha256   = CryptoHelper.ComputeSHA256String(userData.Password);

                        var command = new string[]
                        {
                                "/bin/bash",
                                "-c",
                                $@"psql -U {KubeConst.NeonSystemDbAdminUser} glauth -t -c ""
                                        INSERT INTO users(name,givenname,mail,uidnumber,primarygroup,passsha256)
                                            VALUES('{name}','{givenname}','{mail}','{uidnumber}','{primarygroup}','{passsha256}')
                                                    ON CONFLICT (name) DO UPDATE
                                                        SET 
                                                            givenname     = '{givenname}',
                                                            mail          = '{mail}',
                                                            uidnumber     = '{uidnumber}',
                                                            primarygroup  = '{primarygroup}',
                                                            passsha256    = '{passsha256}';"""
                        };

                        var result = await k8s.NamespacedPodExecAsync(
                            name: postgres.Name(),
                            namespaceParameter: postgres.Namespace(),
                            container: "postgres",
                            command: command);

                        if (userData.Capabilities != null)
                        {
                            foreach (var c in userData.Capabilities)
                            {
                                command = new string[]
                                {
                                "/bin/bash",
                                "-c",
                                $@"psql -U {KubeConst.NeonSystemDbAdminUser} glauth -t -c ""
                                        INSERT INTO capabilities(userid,action,object)
                                            VALUES('{uidnumber}','{c.Action}','{c.Object}');"""
                                };

                                result = await k8s.NamespacedPodExecAsync(
                                    name: postgres.Name(),
                                    namespaceParameter: postgres.Namespace(),
                                    container: "postgres",
                                    command: command);
                            }
                        }
                    }
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/glauth-config",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update glauth config");

                        var config      = await k8s.ReadNamespacedSecretAsync("glauth", KubeNamespaces.NeonSystem);
                        var dbSecret    = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);
                        var usersConfig = config.Data["config.cfg"];
                        var doc         = Toml.Parse(Encoding.UTF8.GetString(usersConfig));
                        var table       = doc.Tables.Where(table => table.Name.Key.ToString() == "backend").First();
                        var baseDN      = $@"dc={string.Join($@",dc=", cluster.Definition.Domain.Split('.'))}";
                        var dbString    = $"host=neon-system-db port=5432 dbname=glauth user={KubeConst.NeonSystemDbServiceUser} password={Encoding.UTF8.GetString(dbSecret.Data["password"])} sslmode=disable";
                        
                        var items           = table.Items.Where(i => i.Key.ToString().Trim() == "baseDN");
                        items.First().Value = new StringValueSyntax(baseDN);

                        items               = table.Items.Where(i => i.Key.ToString().Trim() == "database");
                        items.First().Value = new StringValueSyntax(dbString);

                        config.Data["config.cfg"] = Encoding.UTF8.GetBytes(doc.ToString());

                        await k8s.ReplaceNamespacedSecretAsync(config, config.Name(), config.Namespace());
                    });

                await master.InvokeIdempotentAsync("ready-to-go/glauth-users",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update glauth users");

                        var users    = await k8s.ReadNamespacedSecretAsync("glauth-users", KubeNamespaces.NeonSystem);
                        var postgres = (await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem, labelSelector: "app=neon-system-db")).Items.First();
                        
                        foreach (var user in users.Data.Keys)
                        {
                            var userData = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(users.Data[user]));

                            userData.Password = NeonHelper.GetCryptoRandomPassword(cluster.Definition.Security.PasswordLength);

                            var password = CryptoHelper.ComputeSHA256String(userData.Password);

                            if (user == "root")
                            {
                                userData.Password = clusterLogin.SsoPassword; 
                                password          = CryptoHelper.ComputeSHA256String(clusterLogin.SsoPassword);
                            }

                            var command = new string[]
                            {
                                "/bin/bash",
                                "-c",
                                $@"psql -U {KubeConst.NeonSystemDbAdminUser} glauth -t -c ""UPDATE users SET passsha256 = '{password}' WHERE name = '{user}';"""
                            };

                            var result = await k8s.NamespacedPodExecAsync(
                                name:               postgres.Name(),
                                namespaceParameter: postgres.Namespace(),
                                container:          "postgres",
                                command:            command);

                            users.Data[user] = Encoding.UTF8.GetBytes(NeonHelper.YamlSerialize(userData));
                        }

                        await k8s.UpsertSecretAsync(users, KubeNamespaces.NeonSystem);
                    });
            }
        }

        /// <summary>
        /// Installs Oauth2-proxy.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallOauth2ProxyAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var readyToGoMode = controller.Get<ReadyToGoMode>(KubeSetupProperty.ReadyToGoMode);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.Oauth2Proxy);


            await master.InvokeIdempotentAsync("setup/oauth2-proxy",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "oauth2 proxy");

                    var values = new Dictionary<string, object>();

                    values.Add("cluster.name", cluster.Definition.Name);
                    values.Add("cluster.domain", cluster.Definition.Domain);
                    values.Add("config.cookieSecret", NeonHelper.ToBase64(NeonHelper.GetCryptoRandomPassword(24)));
                    values.Add($"metrics.enabled", serviceAdvice.MetricsEnabled ?? clusterAdvice.MetricsEnabled);
                    values.Add($"metrics.servicemonitor.interval", serviceAdvice.MetricsInterval ?? clusterAdvice.MetricsInterval);

                    await master.InstallHelmChartAsync(controller, "oauth2-proxy", 
                        releaseName:     "neon-sso", 
                        @namespace:      KubeNamespaces.NeonSystem, 
                        prioritySpec:    PriorityClass.NeonApi.Name,
                        values:          values, 
                        progressMessage: "neon-sso-oauth2-proxy");
                });

            await master.InvokeIdempotentAsync("setup/oauth2-proxy-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait for", message: "oauth2 proxy");

                    await k8s.WaitForDeploymentAsync(KubeNamespaces.NeonSystem, "neon-sso-oauth2-proxy", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval);
                });

            if (readyToGoMode == ReadyToGoMode.Setup)
            {
                await master.InvokeIdempotentAsync("ready-to-go/oauth2-proxy-secret",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update neon sso oauth2 secret");

                        var oauth2Secret = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSsoOauth2Proxy, KubeNamespaces.NeonSystem);

                        oauth2Secret.Data["cookie-secret"] = Encoding.UTF8.GetBytes(NeonHelper.GetCryptoRandomPassword(24));

                        await k8s.ReplaceNamespacedSecretAsync(oauth2Secret, oauth2Secret.Name(), oauth2Secret.Namespace());
                    });

                await master.InvokeIdempotentAsync("ready-to-go/oauth2-proxy-config",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "update neon sso oauth2 config");

                        var configMap = await k8s.ReadNamespacedConfigMapAsync(KubeConst.NeonSsoOauth2Proxy, KubeNamespaces.NeonSystem);

                        configMap.Data["loginUrl"]  = $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}";
                        configMap.Data["issuerUrl"] = $"https://{ClusterDomain.Sso}.{cluster.Definition.Domain}";

                        await k8s.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Namespace());
                    });

                await master.InvokeIdempotentAsync("ready-to-go/oauth2-proxy-restart",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "ready-to-go", message: "wait for oauth2 proxy");

                        var deployment = await k8s.ReadNamespacedDeploymentAsync("neon-sso-oauth2-proxy", KubeNamespaces.NeonSystem);
                        await deployment.RestartAsync(k8s);
                    });

                
            }
        }

        /// <summary>
        /// Returns the Postgres connection string for the default database for the
        /// cluster's <see cref="KubeService.NeonSystemDb"/> deployment.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The connection string.</returns>
        public static async Task<string> GetSystemDatabaseConnectionStringAsync(ISetupController controller)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var k8s        = GetK8sClient(controller);
            var secret     = await k8s.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbAdminSecret, KubeNamespaces.NeonSystem);
            var username   = Encoding.UTF8.GetString(secret.Data["username"]);
            var password   = Encoding.UTF8.GetString(secret.Data["password"]);
            var dbHost     = KubeService.NeonSystemDb;
            var dbPort     = NetworkPorts.Postgres;
            var connString = $"Host={dbHost};Port={dbPort};Username={username};Password={password};Database=postgres";

            if (controller.Get<bool>(KubeSetupProperty.Redact, true))
            {
                controller.LogGlobal($"System database connection string: [{connString.Replace(password, "REDACTED")}]");
            }
            else
            {
                controller.LogGlobal($"System database connection string: [{connString}]");
            }

            return connString;
        }

        /// <summary>
        /// Deploys a Kubernetes job that runs Grafana setup.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task SetupGrafanaAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);

            // Perform the Grafana Minio configuration.

            await master.InvokeIdempotentAsync("setup/minio-loki",
                async () =>
                {
                    master.Status = "create: grafana [loki] minio bucket";

                    await CreateMinioBucketAsync(controller, master, "loki", clusterAdvice.LogsQuota);
                });

            await master.InvokeIdempotentAsync("setup/minio-cortex",
                async () =>
                {
                    master.Status = "create: grafana [cortex] minio bucket";

                    await CreateMinioBucketAsync(controller, master, "cortex", clusterAdvice.MetricsQuota);
                });

            await master.InvokeIdempotentAsync("setup/minio-alertmanager",
                async () =>
                {
                    master.Status = "create: grafana [alertmanager] minio bucket";

                    await CreateMinioBucketAsync(controller, master, "alertmanager");
                });

            await master.InvokeIdempotentAsync("setup/minio-cortex-ruler",
                async () =>
                {
                    master.Status = "create: grafana [cortex-ruler] minio bucket";

                    await CreateMinioBucketAsync(controller, master, "cortex-ruler");
                });

            await master.InvokeIdempotentAsync("setup/minio-tempo",
                async () =>
                {
                    master.Status = "create: grafana [tempo] minio bucket";

                    await CreateMinioBucketAsync(controller, master, "tempo", clusterAdvice.TracesQuota);
                });
        }

        /// <summary>
        /// Creates a minio bucket by using the mc client on one of the minio server pods.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new bucket name.</param>
        /// <param name="quota">The bucket quota.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateMinioBucketAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master, string name, string quota = null)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));
            Covenant.Requires < ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var minioSecret = await GetK8sClient(controller).ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);
            var accessKey   = Encoding.UTF8.GetString(minioSecret.Data["accesskey"]);
            var secretKey   = Encoding.UTF8.GetString(minioSecret.Data["secretkey"]);
            var k8s         = GetK8sClient(controller);
            var minioPod    = (await k8s.ListNamespacedPodAsync(KubeNamespaces.NeonSystem, labelSelector: "app.kubernetes.io/name=minio-operator")).Items.First();

            await master.InvokeIdempotentAsync($"setup/minio-bucket-{name}",
                async () =>
                {
                    await k8s.NamespacedPodExecAsync(
                        KubeNamespaces.NeonSystem,
                        minioPod.Name(),
                        "minio-operator",
                        new string[] {
                            "/bin/bash",
                            "-c",
                            $"/mc mb minio/{name}"
                        });

                    if (!string.IsNullOrEmpty(quota))
                    {
                        await k8s.NamespacedPodExecAsync(
                            KubeNamespaces.NeonSystem,
                            minioPod.Name(),
                            "minio-operator",
                            new string[] {
                            "/bin/bash",
                            "-c",
                            $"/mc admin bucket quota minio/{name} --fifo {quota}"
                        });
                    }
                });
        }

        /// <summary>
        /// Converts a <c>decimal</c> into a nice byte units string.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <returns>The formatted output (or <c>null</c>).</returns>
        public static string ToSiString(decimal? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return new ResourceQuantity(value.GetValueOrDefault(), 0, ResourceQuantity.SuffixFormat.BinarySI).CanonicalizeString();
        }

        /// <summary>
        /// Converts a <c>double</c> value into a nice byte units string.
        /// </summary>
        /// <param name="value">The input value (or <c>null</c>).</param>
        /// <returns>The formatted output (or <c>null</c>).</returns>
        public static string ToSiString(double? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return new ResourceQuantity((decimal)value.GetValueOrDefault(), 0, ResourceQuantity.SuffixFormat.BinarySI).CanonicalizeString();
        }

        /// <summary>
        /// Returns the built-in cluster definition for a local neonDESKTOP cluster provisioned on WSL2.
        /// </summary>
        /// <returns>The cluster definition text.</returns>
        public static ClusterDefinition GetLocalWsl2ClusterDefintion()
        {
            var yaml =
@"
name: neon-desktop
datacenter: wsl2
environment: development
timeSources:
- pool.ntp.org
kubernetes:
  allowPodsOnMasters: true
hosting:
  environment: wsl2
nodes:
  master:
    role: master
";
            return ClusterDefinition.FromYaml(yaml);
        }
    }
}
