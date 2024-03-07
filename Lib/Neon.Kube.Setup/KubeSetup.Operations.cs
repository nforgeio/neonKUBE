//-----------------------------------------------------------------------------
// FILE:        KubeSetup.Operations.cs
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube.Clients;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.Kube.Deployment;
using Neon.Kube.Glauth;
using Neon.Kube.Hosting;
using Neon.Kube.K8s;
using Neon.Kube.Proxy;
using Neon.Kube.Resources.Calico;
using Neon.Kube.Resources.CertManager;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Resources.Istio;
using Neon.Kube.Resources.Minio;
using Neon.Kube.Resources.OpenEBS;
using Neon.Kube.Resources.Prometheus;
using Neon.Kube.SSH;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.Kube.Setup
{
    public static partial class KubeSetup
    {
        /// <summary>
        /// Configures a local HAProxy container that makes Kubernetes API server,
        /// etcd and istio ingress highly available.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="node">The node where the operation will be performed.</param>
        public static void SetupHaProxy(ISetupController controller, NodeSshProxy<NodeDefinition> node)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            controller.LogProgress(node, verb: "configure", message: "etcd HA");

            var sbHaProxyConfig = new StringBuilder();

            sbHaProxyConfig.Append(
$@"global
    daemon
    log stdout format raw local0 info
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

frontend kubernetes_controlNode
    bind                    *:6442
    mode                    tcp
    log                     global
    option                  tcplog
    default_backend         kubernetes_controlplane_backend

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

backend kubernetes_controlplane_backend
    mode                    tcp
    balance                 roundrobin");

            foreach (var controlNode in cluster.ControlNodes)
            {
                sbHaProxyConfig.Append(
$@"
    server                  {controlNode.Name} {controlNode.Metadata.Address}:{KubePort.KubeApiServer}");
            }

            sbHaProxyConfig.Append(
$@"

backend harbor_backend_http
    mode                    http
    balance                 roundrobin");

            foreach (var istioNode in cluster.Nodes.Where(n => n.Metadata.Labels.SystemIstioServices))
            {
                sbHaProxyConfig.Append(
$@"
    server                  {istioNode.Name} {istioNode.Metadata.Address}:{KubePort.IstioIngressHttp}");
            }

            sbHaProxyConfig.Append(
$@"

backend harbor_backend
    mode                    tcp
    balance                 roundrobin");

            foreach (var istioNode in cluster.Nodes.Where(n => n.Metadata.Labels.SystemIstioServices))
            {
                sbHaProxyConfig.Append(
$@"
    server                  {istioNode.Name} {istioNode.Metadata.Address}:{KubePort.IstioIngressHttps}");
            }

            node.UploadText("/etc/neonkube/neon-etcd-proxy.cfg", sbHaProxyConfig);

            var sbHaProxyPod = new StringBuilder();

            sbHaProxyPod.Append(
$@"
apiVersion: v1
kind: Pod
metadata:
  name: neon-etcd-proxy
  namespace: {KubeNamespace.KubeSystem}
  labels:
    app: neon-etcd-proxy
    role: neon-etcd-proxy
    release: neon-etcd-proxy
spec:
  enableServiceLinks: false
  volumes:
   - name: neon-etcd-proxy-config
     hostPath:
       path: /etc/neonkube/neon-etcd-proxy.cfg
       type: File
  hostNetwork: true
  priorityClassName: {PriorityClass.SystemNodeCritical.Name}
  containers:
    - name: web
      image: {KubeConst.LocalClusterRegistry}/haproxy:{KubeVersion.HAProxy}
      volumeMounts:
        - name: neon-etcd-proxy-config
          mountPath: /etc/haproxy/haproxy.cfg
      ports:
        - name: k8s-control
          containerPort: 6442
          protocol: TCP
");
            node.UploadText("/etc/kubernetes/manifests/neon-etcd-proxy.yaml", sbHaProxyPod, permissions: "600", owner: "root:root");
        }

        /// <summary>
        /// Adds the Kubernetes node labels.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The first control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task LabelNodesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/label-nodes",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "label", message: "nodes");

                    try
                    {
                        var k8sNodes = (await k8s.CoreV1.ListNodeAsync()).Items;

                        foreach (var node in cluster.Nodes)
                        {
                            controller.ThrowIfCancelled();

                            var k8sNode = k8sNodes.Where(n => n.Metadata.Name == node.Name).Single();

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

                            foreach (var label in node.Metadata.Labels.All)
                            {
                                if (label.Value != null)
                                {
                                    patch.Metadata.Labels[label.Key] = label.Value.ToString();
                                }
                            }

                            await k8s.CoreV1.PatchNodeAsync(new V1Patch(patch, V1Patch.PatchType.StrategicMergePatch), k8sNode.Metadata.Name);
                        }
                    }
                    finally
                    {
                        controlNode.Status = string.Empty;
                    }

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Initializes the cluster on the first manager, joins the remaining
        /// control-plane nodes and workers to the cluster and then performs the rest of
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
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            var cluster     = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var controlNode = cluster.DeploymentControlNode;
            var debugMode   = controller.Get<bool>(KubeSetupProperty.DebugMode);

            // We're still seeing occasional transient failures here so we're going to
            // wait for a while and then retry a few times after failures.  Hopefully,
            // doing this will make cluster setup reliable for end users.
            //
            // Note that this is possible because the setup operations are all idempotent.
            //
            // Note also that we're going to catch and log any exceptions for analysis.

            const int maxRetries = 3;

            var retryCount    = 0;
            var retryInterval = TimeSpan.FromSeconds(60);

            var globalRetry = new LinearRetryPolicy(
                e =>
                {
                    if (++retryCount < maxRetries)
                    {
                        var error = $"TRANSIENT ERROR: Pausing for [{retryInterval.TotalSeconds}] seconds; retry [{retryCount} of {maxRetries}]: {NeonHelper.ExceptionError(e)}";

                        controller.LogGlobalError(error);
                        controller.LogGlobalException(e);

                        cluster.ClearNodeStatus();
                        controller.SetGlobalStepStatus(error);
                    }

                    return true;
                },
                maxAttempts:   maxRetries,
                retryInterval: TimeSpan.FromSeconds(60));

            await globalRetry.InvokeAsync(
                async () =>
                {
                    controller.ClearStatus();
                    controller.SetGlobalStepStatus();

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    UploadAttributions(controller);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    ConfigureKubernetes(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    ConfigureWorkstation(controller, cluster.DeploymentControlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    ConnectCluster(controller);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await ConfigureControlPlaneTaintsAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await TaintNodesAsync(controller);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await LabelNodesAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await CreateNamespacesAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await UploadClusterInfoAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await UploadClusterManifestAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await SetClusterLockAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await SetClusterHealthAsync(controller, controlNode, ready: false);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallClusterCrdsAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await CreateRootUserAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await ConfigurePriorityClassesAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    InstallCilium(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    InstallIstio(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await ConfigureCoreDnsAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await ConfigureNamespaceTraceSamplingAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallMetricsServerAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallPrometheusAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallNeonCloudTokenAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallCertManagerAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await ConfigureApiserverIngressAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallKubernetesDashboardAsync(controller, controlNode);

                    if (cluster.SetupState.ClusterDefinition.Features.NodeProblemDetector)
                    {
                        controller.ClearStatus();
                        controller.ThrowIfCancelled();
                        await InstallNodeProblemDetectorAsync(controller, controlNode);
                    }

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallOpenEbsAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallReloaderAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallSystemDbAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallRedisAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallClusterOperatorAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallNodeAgentAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallSsoAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await CreateDashboardsAsync(controller, controlNode);

                    if (cluster.SetupState.ClusterDefinition.Features.Kiali)
                    {
                        controller.ClearStatus();
                        controller.ThrowIfCancelled();
                        await InstallKialiAsync(controller, controlNode);
                    }

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallMinioAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallHarborAsync(controller, controlNode);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallMonitoringAsync(controller);

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await InstallContainerRegistryResourcesAsync(controller, controlNode);

                    // IMPORTANT:
                    //
                    // This must be the last step because it indicates that the cluster
                    // has been successfully deployed.

                    controller.ClearStatus();
                    controller.ThrowIfCancelled();
                    await SetClusterHealthAsync(controller, controlNode, ready: true);
                },
                controller.CancellationToken);
        }

        /// <summary>
        /// Method to generate Kubernetes cluster configuration.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The YAML with the Kubernetes config used to initialize the cluster.</returns>
        public static string GenerateKubernetesClusterConfig(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster              = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var controlPlaneEndpoint = $"kubernetes-control-plane:6442";
            var sbCertSANs           = new StringBuilder();
            var clusterAdvice        = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);

            // Append the names to be included as the certificate SANs.  Note that
            // we're using a dictionary here to avoid duplicating names.

            var sanNames = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            sanNames["kubernetes-control-plane"] = null;

            if (!string.IsNullOrEmpty(cluster.SetupState.ClusterDomain))
            {
                sanNames[cluster.SetupState.ClusterDomain] = null;
            }

            foreach (var address in cluster.SetupState.PublicAddresses)
            {
                sanNames[address] = null;
            }

            foreach (var node in cluster.ControlNodes)
            {
                sanNames[node.Metadata.Address] = null;
                sanNames[node.Name] = null;
            }

            if (cluster.SetupState.ClusterDefinition.IsDesktop)
            {
                sanNames[cluster.Name] = null;
            }

            for (int i = 0; i < sanNames.Count; i++)
            {
                // Don't include line endings on the last entry line to
                // make the generated file look a bit nicer.

                if (i < sanNames.Count - 1)
                {
                    sbCertSANs.AppendLine($"  - \"{sanNames.Keys.ElementAt(i)}\"");
                }
                else
                {
                    sbCertSANs.Append($"  - \"{sanNames.Keys.ElementAt(i)}\"");
                }
            }

            // Append the InitConfiguration

            var clusterConfig = new StringBuilder();

            clusterConfig.AppendLine(
$@"
apiVersion: kubeadm.k8s.io/v1beta3
kind: InitConfiguration
nodeRegistration:
  criSocket: unix://{KubeConst.CrioSocketPath}
  imagePullPolicy: IfNotPresent

---
apiVersion: kubeadm.k8s.io/v1beta3
kind: ClusterConfiguration
clusterName: {cluster.Name}
etcd:
  local:
    imageRepository: ""{KubeConst.LocalClusterRegistry}""
    imageTag: ""{KubeVersion.Etcd}""
kubernetesVersion: ""v{KubeVersion.Kubernetes}""
imageRepository: ""{KubeConst.LocalClusterRegistry}""
apiServer:
  extraArgs:
    bind-address: 0.0.0.0
    advertise-address: 0.0.0.0
    logging-format: json
    default-not-ready-toleration-seconds: ""30""    # default 300
    default-unreachable-toleration-seconds: ""30""  # default 300
    allow-privileged: ""true""
    api-audiences: api
    service-account-issuer: https://kubernetes.default.svc
    service-account-key-file: /etc/kubernetes/pki/sa.key
    service-account-signing-key-file: /etc/kubernetes/pki/sa.key
    oidc-issuer-url: https://{ClusterHost.Sso}.{cluster.SetupState.ClusterDomain}
    oidc-client-id: {KubeConst.NeonSsoClientId}
    oidc-username-claim: email
    oidc-groups-claim: groups
    oidc-username-prefix: ""-""
    oidc-groups-prefix: """"
    default-watch-cache-size: ""{clusterAdvice.KubeApiServerWatchCacheSize}""
  certSANs:
{sbCertSANs}
controlPlaneEndpoint: ""{controlPlaneEndpoint}""
networking:
  podSubnet: ""{cluster.SetupState.ClusterDefinition.Network.PodSubnet}""
  serviceSubnet: ""{cluster.SetupState.ClusterDefinition.Network.ServiceSubnet}""
kubernetesVersion: ""v{KubeVersion.Kubernetes}""
controllerManager:
  extraArgs:
    logging-format: json
    node-monitor-grace-period: 15s                  # default 40s
    node-monitor-period: 5s                         # default 5s
    pod-eviction-timeout: 30s                       # default 300s
scheduler:
  extraArgs:
    logging-format: json");

            clusterConfig.AppendLine($@"
---
apiVersion: kubelet.config.k8s.io/v1beta1
kind: KubeletConfiguration
logging:
  format: json
nodeStatusUpdateFrequency: 10s
nodeStatusReportFrequency: 5m
nodeLeaseDurationSeconds: 40
volumePluginDir: /var/lib/kubelet/volume-plugins
cgroupDriver: systemd
runtimeRequestTimeout: 5m
maxPods: {cluster.SetupState.ClusterDefinition.Kubernetes.MaxPodsPerNode}
shutdownGracePeriod: {cluster.SetupState.ClusterDefinition.Kubernetes.ShutdownGracePeriodSeconds}s
shutdownGracePeriodCriticalPods: {cluster.SetupState.ClusterDefinition.Kubernetes.ShutdownGracePeriodCriticalPodsSeconds}s
rotateCertificates: true");

            clusterConfig.AppendLine($"systemReserved:");

            foreach (var systemReservedkey in cluster.SetupState.ClusterDefinition.Kubernetes.SystemReserved.Keys)
            {
                clusterConfig.AppendLine($"  {systemReservedkey}: {cluster.SetupState.ClusterDefinition.Kubernetes.SystemReserved[systemReservedkey]}");
            }

            clusterConfig.AppendLine($"kubeReserved:");

            foreach (var kubeReservedKey in cluster.SetupState.ClusterDefinition.Kubernetes.KubeReserved.Keys)
            {
                clusterConfig.AppendLine($"  {kubeReservedKey}: {cluster.SetupState.ClusterDefinition.Kubernetes.KubeReserved[kubeReservedKey]}");
            }

            clusterConfig.AppendLine($"evictionHard:");

            foreach (var evictionHardKey in cluster.SetupState.ClusterDefinition.Kubernetes.EvictionHard.Keys)
            {
                clusterConfig.AppendLine($"  {evictionHardKey}: {cluster.SetupState.ClusterDefinition.Kubernetes.EvictionHard[evictionHardKey]}");
            }

            // Append the KubeProxyConfiguration

            var kubeProxyMode = "ipvs";

            clusterConfig.AppendLine($@"
---
apiVersion: kubeproxy.config.k8s.io/v1alpha1
kind: KubeProxyConfiguration
mode: {kubeProxyMode}");

            return clusterConfig.ToString();
        }

        /// <summary>
        /// Uploads a copyright/trademark text files to all of the cluster nodes.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public static void UploadAttributions(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            const string folder        = "/usr/share/doc/neonkube/copyright";
            const string copyrightText =
@"Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
NeonKube™, Neon Desktop™, and NeonCli™ are trademarked by NEONFORGE LLC.
";
            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            foreach (var node in cluster.Nodes)
            {
                node.InvokeIdempotent("setup/copyright",
                    () =>
                    {
                        node.SudoCommand($"mkdir -p {folder}").EnsureSuccess();
                        node.UploadText($"{folder}/copyright.txt", NeonHelper.ToLinuxLineEndings(copyrightText), permissions: "644");
                    });
            }
        }

        /// <summary>
        /// Basic Kubernetes cluster initialization.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="firstControlNode">The first control-plane node in the cluster where the operation will be performed.</param>
        public static void ConfigureKubernetes(ISetupController controller, NodeSshProxy<NodeDefinition> firstControlNode)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(firstControlNode != null, nameof(firstControlNode));

            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);
            var cluster            = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            controller.ThrowIfCancelled();
            firstControlNode.InvokeIdempotent("setup/config-kubernetes",
                () =>
                {
                    //---------------------------------------------------------
                    // Initialize the cluster on the first control-plane node:

                    controller.LogProgress(firstControlNode, verb: "create", message: "cluster");

                    // Initialize Kubernetes:

                    controller.ThrowIfCancelled();
                    firstControlNode.InvokeIdempotent("setup/config-kubernetes-init",
                        () =>
                        {
                            controller.LogProgress(firstControlNode, verb: "reset", message: "kubernetes");

                            // It's possible that a previous cluster initialization operation
                            // was interrupted.  This command resets the state.  We also need
                            // to remove any kubeconfig andclear IPVS and other CNI network state.

                            firstControlNode.SudoCommand("kubeadm reset --force", RunOptions.None);
                            firstControlNode.SudoCommand("rm -r /root/.kube", RunOptions.None);
                            firstControlNode.SudoCommand($"rm -r /home/{KubeConst.SysAdminUser}/.kube", RunOptions.None);
                            firstControlNode.SudoCommand("ipvsadm --clear", RunOptions.None);
                            firstControlNode.SudoCommand("rm -r /etc/cni/net.d", RunOptions.None);

                            // Configure etcd.

                            SetupHaProxy(controller, firstControlNode);

                            // CRI-O needs to be running and listening on its unix domain socket so that
                            // Kubelet can start and the cluster can be initialized via [kubeadm].  CRI-O
                            // takes perhaps 20-30 seconds to start and we've run into occasional trouble
                            // with cluster setup failures because CRI-O hadn't started listening on its
                            // socket in time.
                            //
                            // We're going to wait for the presence of the CRI-O socket here.

                            controller.LogProgress(firstControlNode, verb: "wait", message: "for cri-o");

                            var retry = new LinearRetryPolicy(
                                transientDetector: null,
                                retryInterval:     TimeSpan.FromSeconds(1),
                                timeout:           TimeSpan.FromSeconds(60));

                            retry.Invoke(
                                () =>
                                {
                                    controller.ThrowIfCancelled();

                                    var socketResponse = firstControlNode.SudoCommand("cat", new object[] { "/proc/net/unix" })
                                        .EnsureSuccess();

                                    if (!socketResponse.OutputText.Contains(KubeConst.CrioSocketPath))
                                    {
                                        throw new TimeoutException("Cannot locate CRI-O socket.");
                                    }
                                },
                                cancellationToken: controller.CancellationToken);

                            // $note(jefflill):
                            //
                            // We've seen this fail occasionally with this message in the command response:
                            //
                            //      [wait-control-plane] Waiting for the kubelet to boot up the control plane as static Pods from directory "/etc/kubernetes/manifests". This can take up to 4m0s
                            //      [kubelet-check] Initial timeout of 40s passed.
                            //
                            // After some investigation, it looks like the second line is really just
                            // a warning and that kubeadm does continue waiting for the full 4 minutes,
                            // but sometimes this is not long enough.
                            //
                            // We're going to mitigate this by retrying 2 additional times.

                            var clusterConfig  = GenerateKubernetesClusterConfig(controller, firstControlNode);
                            var kubeInitScript =
$@"
if ! systemctl enable kubelet.service; then
    echo 'FAILED: systemctl enable kubelet.service' >&2
    exit 1
fi

# The first call doesn't specify [--ignore-preflight-errors=all]

if kubeadm init --config cluster.yaml --ignore-preflight-errors=DirAvailable --skip-phases=addon/kube-proxy; then
    exit 0
fi

# The additional two calls specify [--ignore-preflight-errors=all] to avoid detecting
# bogus conflicts with itself.

for count in {{1..2}}
do
    if kubeadm init --config cluster.yaml --ignore-preflight-errors=all --skip-phases=addon/kube-proxy; then
        exit 0
    fi
done

echo 'FAILED: kubeadm init...' >&2
exit 1
";
                            controller.LogProgress(firstControlNode, verb: "init", message: "kubernetes");

                            var response = firstControlNode.SudoCommand(CommandBundle.FromScript(kubeInitScript).AddFile("cluster.yaml", clusterConfig));

                            // Extract the cluster join command from the response.  We'll need this to join
                            // other nodes to the cluster.

                            var output = response.OutputText;
                            var pStart = output.IndexOf(joinCommandMarker, output.IndexOf(joinCommandMarker) + 1);

                            if (pStart == -1)
                            {
                                firstControlNode.LogLine("START: [kubeadm init ...] response ============================================");

                                using (var reader = new StringReader(response.AllText))
                                {
                                    foreach (var line in reader.Lines())
                                    {
                                        firstControlNode.LogLine(line);
                                    }
                                }

                                firstControlNode.LogLine("END: [kubeadm init ...] response ==============================================");

                                throw new NeonKubeException("Cannot locate the [kubeadm join ...] command in the [kubeadm init ...] response.");
                            }

                            var pEnd = output.Length;

                            if (pEnd == -1)
                            {
                                cluster.SetupState.ClusterJoinCommand = Regex.Replace(output.Substring(pStart).Trim(), @"\t|\n|\r|\\", "");
                            }
                            else
                            {
                                cluster.SetupState.ClusterJoinCommand = Regex.Replace(output.Substring(pStart, pEnd - pStart).Trim(), @"\t|\n|\r|\\", "");
                            }

                            cluster.SaveSetupState();
                            firstControlNode.UpdateKubernetesStaticManifests(controller);
                            controller.LogProgress(firstControlNode, verb: "created", message: "cluster");
                        });

                    controller.ThrowIfCancelled();
                    firstControlNode.InvokeIdempotent("setup/config-kubernetes-kubectl",
                        () =>
                        {
                            controller.LogProgress(firstControlNode, verb: "configure", message: "kubectl");

                            // Edit the Kubernetes configuration file to rename the context:
                            //
                            //       CLUSTERNAME-admin@kubernetes --> root@CLUSTERNAME
                            //
                            // rename the user:
                            //
                            //      CLUSTERNAME-admin --> CLUSTERNAME-root 

                            var adminConfig = firstControlNode.DownloadText("/etc/kubernetes/admin.conf");

                            adminConfig = adminConfig.Replace($"kubernetes-admin@{cluster.Name}", $"root@{cluster.SetupState.ClusterDefinition.Name}");
                            adminConfig = adminConfig.Replace("kubernetes-admin", $"root@{cluster.Name}");

                            firstControlNode.UploadText("/etc/kubernetes/admin.conf", adminConfig, permissions: "600", owner: "root:root");
                        });

                    // Download the control-plane files that will need to be provisioned on the remaining
                    // control-plane nodes and may also be needed for other purposes (if we haven't already
                    // downloaded these).

                    if (cluster.SetupState.ControlNodeFiles != null)
                    {
                        cluster.SetupState.ControlNodeFiles = new Dictionary<string, KubeFileDetails>();
                    }

                    if (cluster.SetupState.ControlNodeFiles.Count == 0)
                    {
                        cluster.SetupState.ControlNodeFiles = firstControlNode.GetControlPlaneFiles();
                    }

                    // Persist the cluster join command and downloaded control-plane files.

                    cluster.SaveSetupState();

                    //---------------------------------------------------------
                    // Join the remaining control-plane nodes to the cluster:

                    foreach (var controlNode in cluster.ControlNodes.Where(node => node != firstControlNode))
                    {
                        try
                        {
                            controller.ThrowIfCancelled();
                            controlNode.InvokeIdempotent("setup/config-kubernetes-kubectl",
                                () =>
                                {
                                    controller.LogProgress(controlNode, verb: "install", message: "kubectl");

                                    // It's possible that a previous cluster join operation
                                    // was interrupted.  This command resets the state.

                                    controlNode.SudoCommand("kubeadm reset --force");

                                    // The other (non-boot) control-plane nodes need files downloaded from the boot node.

                                    controller.LogProgress(controlNode, verb: "upload", message: "control-plane files");

                                    foreach (var file in cluster.SetupState.ControlNodeFiles)
                                    {
                                        controlNode.UploadText(file.Key, file.Value.Text, permissions: file.Value.Permissions, owner: file.Value.Owner);
                                    }

                                    // Join the cluster:

                                    controller.ThrowIfCancelled();
                                    controlNode.InvokeIdempotent("setup/config-kubernetes-join-control-plane",
                                        () =>
                                        {
                                            controller.LogProgress(controlNode, verb: "join", message: "control-plane node to cluster");

                                            SetupHaProxy(controller, controlNode);

                                            var joined = false;

                                            controller.LogProgress(controlNode, verb: "join", message: "as control-plane");

                                            controlNode.SudoCommand("podman run",
                                                   "--name=neon-etcd-proxy",
                                                   "--detach",
                                                   "--restart=always",
                                                   "-v=/etc/neonkube/neon-etcd-proxy.cfg:/etc/haproxy/haproxy.cfg",
                                                   "--network=host",
                                                   "--log-driver=k8s-file",
                                                   $"{KubeConst.LocalClusterRegistry}/haproxy:{KubeVersion.HAProxy}"
                                               );

                                            for (int attempt = 0; attempt < maxJoinAttempts; attempt++)
                                            {
                                                controller.ThrowIfCancelled();

                                                var response = controlNode.SudoCommand(cluster.SetupState.ClusterJoinCommand + " --control-plane --ignore-preflight-errors=DirAvailable--etc-kubernetes-manifests", RunOptions.Defaults & ~RunOptions.FaultOnError);

                                                if (response.Success)
                                                {
                                                    joined = true;
                                                    break;
                                                }

                                                Thread.Sleep(joinRetryDelay);
                                            }

                                            if (!joined)
                                            {
                                                throw new Exception($"Unable to join node [{controlNode.Name}] to the after [{maxJoinAttempts}] attempts.");
                                            }

                                            controller.ThrowIfCancelled();
                                            controlNode.SudoCommand("podman kill neon-etcd-proxy");
                                            controlNode.SudoCommand("podman rm neon-etcd-proxy");

                                            controller.ThrowIfCancelled();
                                            controlNode.UpdateKubernetesStaticManifests(controller);
                                        });
                                });
                        }
                        catch (Exception e)
                        {
                            controlNode.Fault(NeonHelper.ExceptionError(e));
                            controlNode.LogException(e);
                        }

                        controller.LogProgress(controlNode, verb: "joined", message: "to cluster");
                    }

                    cluster.ClearNodeStatus();

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
                                controller.ThrowIfCancelled();
                                worker.InvokeIdempotent("setup/config-kubernetes-worker-join",
                                    () =>
                                    {
                                        controller.LogProgress(worker, verb: "join", message: "worker to cluster");

                                        SetupHaProxy(controller, worker);

                                        var joined = false;

                                        controller.LogProgress(worker, verb: "join", message: "as worker");

                                        controller.ThrowIfCancelled();
                                        worker.SudoCommand("podman run",
                                            "--name=neon-etcd-proxy",
                                            "--detach",
                                            "--restart=always",
                                            "-v=/etc/neonkube/neon-etcd-proxy.cfg:/etc/haproxy/haproxy.cfg",
                                            "--network=host",
                                            "--log-driver=k8s-file",
                                            $"{KubeConst.LocalClusterRegistry}/haproxy:{KubeVersion.HAProxy}",
                                            RunOptions.FaultOnError);

                                        for (int attempt = 0; attempt < maxJoinAttempts; attempt++)
                                        {
                                            controller.ThrowIfCancelled();

                                            var response = worker.SudoCommand(cluster.SetupState.ClusterJoinCommand + " --ignore-preflight-errors=DirAvailable--etc-kubernetes-manifests", RunOptions.Defaults & ~RunOptions.FaultOnError);

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

                                        controller.ThrowIfCancelled();
                                        worker.SudoCommand("podman kill neon-etcd-proxy");
                                        worker.SudoCommand("podman rm neon-etcd-proxy");
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

            cluster.ClearNodeStatus();
        }

        /// <summary>
        /// Configures CoreDns.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ConfigureCoreDnsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            // NOTE: CoreDNS is deployed by [kubeadm]

            var cluster           = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s               = GetK8sClient(controller);
            var clusterDefinition = cluster.SetupState.ClusterDefinition;
            var clusterAdvice     = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var coreDnsAdvice     = clusterAdvice.GetServiceAdvice(ClusterAdvice.CoreDns);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/coredns",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "coredns");

                    // Wait for CoreDNS deployment to report that it's deployed.

                    controller.ThrowIfCancelled();
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.KubeSystem, "coredns",
                        timeout:           clusterOpTimeout,
                        pollInterval:      clusterOpPollInterval,
                        cancellationToken: controller.CancellationToken);

                    // Configure CoreDNS to deploy only on the control-plane, set the number of
                    // replicas to match the number of control-plane nodes, and also configure
                    // the CoreDNS memory request/limit.

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/coredns-configure",
                        async () =>
                        {
                            var coreDnsDeployment = await k8s.AppsV1.ReadNamespacedDeploymentAsync("coredns", KubeNamespace.KubeSystem);

                            // We're going to schedule this only on control-plane nodes (note that
                            // we also need to tolerate any control-plane node taints).

                            coreDnsDeployment.Spec.Template.Spec.NodeSelector = new Dictionary<string, string>()
                            {
                                { $"{NodeLabel.LabelRole}", "control-plane" }
                            };

                            coreDnsDeployment.Spec.Template.Spec.Tolerations = new List<V1Toleration>()
                            {
                                { new V1Toleration() { Effect = "NoSchedule", OperatorProperty = "Exists" } },
                                { new V1Toleration() { Effect = "NoExecute", OperatorProperty = "Exists" } }
                            };

                            // $todo(jefflill):
                            //
                            // We need to configure pod affinity/anti-affinity to prevent multiple
                            // replicas from being scheduled on the same control-plane node.

                            // Configure the memory request/limit.

                            coreDnsDeployment.Spec.Template.Spec.Containers.First().Resources.Requests["memory"] = new ResourceQuantity(KubeHelper.ToSiString(coreDnsAdvice.PodMemoryRequest));
                            coreDnsDeployment.Spec.Template.Spec.Containers.First().Resources.Limits["memory"]   = new ResourceQuantity(KubeHelper.ToSiString(coreDnsAdvice.PodMemoryLimit));

                            // Deploy a replica on every control-plane node.

                            coreDnsDeployment.Spec.Replicas = clusterDefinition.ControlNodes.Count();

                            await k8s.AppsV1.ReplaceNamespacedDeploymentAsync(coreDnsDeployment, "coredns", KubeNamespace.KubeSystem);
                        });

                    // Deploy a [dnsutils] container and then verify DNS is actually working.

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/coredns-utils",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "install", message: "dnsutils");

                            var pod = await k8s.CoreV1.CreateNamespacedPodAsync(
                                new V1Pod()
                                {
                                    Metadata = new V1ObjectMeta()
                                    {
                                        Name              = "dnsutils",
                                        NamespaceProperty = KubeNamespace.NeonSystem,
                                        Labels            = new Dictionary<string, string>()
                                        {
                                            { "neonkube.io/setup-pod", "dnsutils" }
                                        }
                                    },
                                    Spec = new V1PodSpec()
                                    {
                                        Containers = new List<V1Container>()
                                        {
                                            new V1Container()
                                            {
                                                Name            = "dnsutils",
                                                Image           = $"{KubeConst.LocalClusterRegistry}/kubernetes-e2e-test-images-dnsutils:{KubeVersion.DnsUtils}",
                                                Command         = new List<string>() {"sleep", "3600" },
                                                ImagePullPolicy = "IfNotPresent"
                                            }
                                        },
                                        RestartPolicy = "Always",
                                        Tolerations   = new List<V1Toleration>()
                                        {
                                            { new V1Toleration() { Effect = "NoSchedule", OperatorProperty = "Exists" } },
                                            { new V1Toleration() { Effect = "NoExecute", OperatorProperty = "Exists" } }
                                        }
                                    }
                                },
                                KubeNamespace.NeonSystem);

                            await k8s.CoreV1.WaitForPodAsync(
                                name:               pod.Name(),
                                namespaceParameter: pod.Namespace(),
                                timeout:            clusterOpTimeout,
                                pollInterval:       clusterOpPollInterval,
                                cancellationToken:  controller.CancellationToken);
                        });

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/coredns-verify",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "verify", message: "dns");

                            // Verify that [coredns] is actually working.

                            var cmd = new string[]
                            {
                                "nslookup",
                                "kubernetes.default"
                            };

                            var pod = await k8s.CoreV1.GetNamespacedRunningPodAsync(KubeNamespace.NeonSystem, labelSelector: "neonkube.io/setup-pod=dnsutils");

                            await operationRetry.InvokeAsync(
                                async () =>
                                {
                                    try
                                    {
                                        var result = await k8s.NamespacedPodExecWithRetryAsync(
                                            retryPolicy:        podExecRetry,
                                            name:               pod.Name(),
                                            namespaceParameter: pod.Namespace(),
                                            container:          "dnsutils",
                                            command:            cmd);

                                        result.EnsureSuccess();
                                    }
                                    catch
                                    {
                                        // Restart COREDNS and try again.

                                        var coredns = await k8s.AppsV1.ReadNamespacedDaemonSetAsync("coredns", KubeNamespace.KubeSystem);

                                        await coredns.RestartAsync(k8s);
                                        throw;
                                    }
                                },
                                cancellationToken: controller.CancellationToken);

                            await k8s.CoreV1.DeleteNamespacedPodAsync("dnsutils", KubeNamespace.NeonSystem);
                        });
                });

            // Enable CoreDNS metrics when enabled.

            if (coreDnsAdvice.MetricsEnabled)
            {
                controller.ThrowIfCancelled();
                await controlNode.InvokeIdempotentAsync("setup/coredns-metrics",
                    async () =>
                    {
                        var serviceMonitor = new V1ServiceMonitor()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Name              = "kube-dns",
                                NamespaceProperty = KubeNamespace.KubeSystem
                            },
                            Spec = new V1ServiceMonitorSpec()
                            {
                                Endpoints = new List<Endpoint>()
                                {
                                new Endpoint()
                                {
                                    Interval      = coreDnsAdvice.MetricsInterval,
                                    Path          = "/metrics",
                                    ScrapeTimeout = "10s",
                                    TargetPort    = 9153
                                }
                                },
                                NamespaceSelector = new NamespaceSelector()
                                {
                                    MatchNames = new List<string>() { { KubeNamespace.KubeSystem } }
                                },
                                Selector = new V1LabelSelector()
                                {
                                    MatchLabels = new Dictionary<string, string>()
                                    {
                                        { "k8s-app", "kube-dns"}
                                    }
                                }
                            }
                        };

                        await k8s.CustomObjects.CreateNamespacedCustomObjectAsync<V1ServiceMonitor>(
                            body:               serviceMonitor,
                            name:               serviceMonitor.Name(),
                            namespaceParameter: serviceMonitor.Namespace());
                    });
            }
        }

        /// <summary>
        /// Configures the local workstation.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="firstControlNode">The first control-plane node in the cluster where the operation will be performed.</param>
        public static void ConfigureWorkstation(ISetupController controller, NodeSshProxy<NodeDefinition> firstControlNode)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster          = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var desktopReadyToGo = controller.Get<bool>(KubeSetupProperty.DesktopReadyToGo);
            var kubeConfigPath   = KubeHelper.KubeConfigPath;

            // For NEONDESKTOP clusters, we need to obtain the control plane files from the node
            // and add them to the cluster login because we didn't so a full cluster setup when
            // this would normally happen.

            if (desktopReadyToGo)
            {
                cluster.SetupState.ControlNodeFiles = firstControlNode.GetControlPlaneFiles();
                cluster.SaveSetupState();
            }

            // Update kubeconfig by setting our custom extension properties and then merge the
            // new kubeconfig into the global config.

            var configText = cluster.SetupState.ControlNodeFiles["/etc/kubernetes/admin.conf"].Text;
            var port       = NetworkPorts.KubernetesApiServer;

            configText = configText.Replace("https://kubernetes-control-plane:6442", $"https://{cluster.SetupState.ClusterDomain}:{port}");

            var newConfig  = NeonHelper.YamlDeserialize<KubeConfig>(configText);
            var newUser    = newConfig.Users.Single();
            var newCluster = newConfig.Clusters.Single();
            var newContext = newConfig.Contexts.Single();

            newUser.ClusterName           = cluster.Name;

            newCluster.ClusterInfo        = cluster.SetupState.ToKubeClusterInfo();
            newCluster.HostingEnvironment = cluster.Hosting.Environment;
            newCluster.Hosting            = cluster.Hosting;
            newCluster.IsNeonDesktop      = desktopReadyToGo;
            newCluster.IsNeonKube         = true;
            newCluster.SsoUsername        = cluster.SetupState.SsoUsername;
            newCluster.SsoPassword        = cluster.SetupState.SsoPassword;
            newCluster.SshUsername        = cluster.SetupState.SshUsername;
            newCluster.SshPassword        = cluster.SetupState.SshPassword;

            if (cluster.Hosting.Hypervisor != null)
            {
                newCluster.HostingNamePrefix = cluster.Hosting.Hypervisor.GetVmNamePrefix(cluster.SetupState.ClusterDefinition);
            }

            newContext.Name            = newUser.Name;
            newContext.Context.User    = newUser.Name;
            newContext.Context.Cluster = newCluster.Name;

            if (!File.Exists(kubeConfigPath))
            {
                newConfig.CurrentContext = newContext.Name;

                KubeHelper.SetConfig(newConfig);
            }
            else
            {
                // The user already has an existing kubeconfig, so we need
                // to merge in the new config.

                var existingConfig = KubeHelper.KubeConfig;

                // Remove any existing user, context, and cluster with the same names.
                // Note that we're assuming that there's only one of each in the config
                // we downloaded from the cluster.

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

            // Make sure that the config cached by [KubeHelper] and [ClusterProxy] is up to date.

            cluster.KubeConfig = KubeHelper.LoadConfig().Clone();

            // Save the cluster node SSH certificate in the user's [~/.ssh] folder.

            File.WriteAllText(Path.Combine(KubeHelper.UserSshFolder, cluster.SetupState.ContextName), cluster.SetupState.SshKey.PrivatePEM);
        }

        /// <summary>
        /// Adds the NEONKUBE standard priority classes to the cluster.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ConfigurePriorityClassesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster = controlNode.Cluster;
            var k8s     = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            controlNode.InvokeIdempotent("setup/priorityclass",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "priority classes");

                    foreach (var priorityClassDef in PriorityClass.Values.Where(priorityClass => !priorityClass.IsSystem))
                    {
                        var priorityClass = new V1PriorityClass()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Name = priorityClassDef.Name
                            },
                            Value            = priorityClassDef.Value,
                            Description      = priorityClassDef.Description,
                            PreemptionPolicy = "PreemptLowerPriority",
                            GlobalDefault    = priorityClassDef.IsDefault
                        };

                        await k8s.SchedulingV1.CreatePriorityClassAsync(priorityClass);
                    }
                });
        }

        /// <summary>
        /// Uploads cluster related metadata to cluster nodes to <b>/etc/neonkube/metadata</b>
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="node">The target cluster node.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallClusterManifestAsync(ISetupController controller, NodeSshProxy<NodeDefinition> node)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            controller.ThrowIfCancelled();
            node.InvokeIdempotent("cluster-metadata",
                () =>
                {
                    var debugMode = controller.Get<bool>(KubeSetupProperty.DebugMode);

                    node.UploadText(LinuxPath.Combine(KubeNodeFolder.Config, "metadata", "cluster-manifest.json"), NeonHelper.JsonSerialize(ClusterManifest(debugMode), Formatting.Indented));
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Configures pods to be schedule on control-plane nodes when enabled.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ConfigureControlPlaneTaintsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster = controlNode.Cluster;
            var k8s     = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/kubernetes-control-plane-taints",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "control-plane taints");

                    if (cluster.SetupState.ClusterDefinition.Kubernetes.AllowPodsOnControlPlane.GetValueOrDefault())
                    {
                        var nodes = (V1NodeList)null;

                        await operationRetry.InvokeAsync(
                           async () =>
                           {
                               nodes = await k8s.CoreV1.ListNodeAsync(labelSelector: "node-role.kubernetes.io/control-plane=");

                               if (!(nodes.Items.All(n => n.Status.Conditions.Any(condition => condition.Type == "Ready" && condition.Status == "True"))))
                               {
                                   throw new TimeoutException("Waiting for control-plane nodes.");
                               }
                           },
                           cancellationToken: controller.CancellationToken);

                        foreach (var controlNode in nodes.Items)
                        {
                            controller.ThrowIfCancelled();

                            if (controlNode.Spec.Taints == null)
                            {
                                continue;
                            }

                            var patch = new V1Node()
                            {
                                Spec = new V1NodeSpec()
                                {
                                    Taints = controlNode.Spec.Taints
                                        .Where(taint => taint.Key != "node-role.kubernetes.io/control-plane" && taint.Key != "node-role.kubernetes.io/master")
                                        .ToList()
                                }
                            };

                            await k8s.CoreV1.PatchNodeAsync(new V1Patch(patch, V1Patch.PatchType.StrategicMergePatch), controlNode.Metadata.Name);
                        }
                    }
                });
        }

        /// <summary>
        /// Installs Cilium.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        public static void InstallCilium(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster          = controlNode.Cluster;
            var hostingManager   = cluster.HostingManager;
            var firstControlNode = cluster.ControlNodes.First();
            var k8s              = GetK8sClient(controller);
            var clusterAdvice    = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var ciliumAdvice     = clusterAdvice.GetServiceAdvice(ClusterAdvice.Cilium);
            var coreDnsAdvice    = clusterAdvice.GetServiceAdvice(ClusterAdvice.CoreDns);
            var mtu              = hostingManager.NodeMtu;

            controller.ThrowIfCancelled();
            controlNode.InvokeIdempotent("setup/install-cilium",
                () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "cilium");

                    var script =
$@"
set -euo pipefail

# Install Cilium using the CLI.
#
# NOTE: The [cilium-cli] appears to select different operator container
#       images when deployed to cloud native Kubernetes platforms and
#       a generic image for other environments.  NEONKUBE deploys the
#       generic image.  [cilium-cli] appends [-generic] to the
#       [cilium-operator] container name below for us.

cilium install --version {KubeVersion.Cilium} \
    --chart-directory={KubeNodeFolder.Helm}/cilium \
    --set cni.exclusive=false \
    --set ipam.mode=Kubernetes \
    --set kubeProxyReplacement=strict \
    --set k8sServiceHost=127.0.0.1 \
    --set k8sServicePort={NetworkPorts.KubernetesApiServer} \
    --set MTU={mtu} \
    --set image.repository={KubeConst.LocalClusterRegistry}/cilium \
    --set image.useDigest=false \
    --set operator.image.repository={KubeConst.LocalClusterRegistry}/cilium-operator \
    --set operator.image.useDigest=false \
    --set socketLB.hostNamespaceOnly=true

# Wait for the Cilium installation to complete.

cilium status --wait --wait-duration={clusterOpTimeoutSeconds}s

# We need to restart CRI-O, presumably so it can initialize using
# the new [cilium-proxy] CNI.

systemctl restart cri-o
";
                    firstControlNode.SudoCommand(CommandBundle.FromScript(script), RunOptions.Defaults | RunOptions.FaultOnError);
                });
        }

        /// <summary>
        /// Installs Istio.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        public static void InstallIstio(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var ingressAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.IstioIngressGateway);
            var proxyAdvice   = clusterAdvice.GetServiceAdvice(ClusterAdvice.IstioProxy);
            var pilotAdvice   = clusterAdvice.GetServiceAdvice(ClusterAdvice.IstioPilot);

            // Install Istio.

            controller.ThrowIfCancelled();
            controlNode.InvokeIdempotent("setup/istio-install",
                () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "istio");

                    // Construct the installation manifest.

                    var sbNodePorts = new StringBuilder();
                    var indent      = new string(' ', 12);

                    foreach (var rule in controlNode.Cluster.SetupState.ClusterDefinition.Network.IngressRules
                        .Where(rule => rule.TargetPort != 0))   // [TargetPort=0] indicates that traffic does not route through ingress gateway
                    {
                        string istioProtocol;

                        switch (rule.Protocol)
                        {
                            case IngressProtocol.Http:
                            case IngressProtocol.Https:
                            case IngressProtocol.Tcp:

                                istioProtocol = "TCP";
                                break;

                            case IngressProtocol.Udp:

                                istioProtocol = "UDP";
                                break;

                            default:

                                throw new NotImplementedException();
                        }

                        sbNodePorts.AppendLine($"{indent}- name: {rule.Name}");
                        sbNodePorts.AppendLine($"{indent}  protocol: {istioProtocol}");
                        sbNodePorts.AppendLine($"{indent}  port: {rule.ExternalPort}");
                        sbNodePorts.AppendLine($"{indent}  targetPort: {rule.TargetPort}");
                        sbNodePorts.AppendLine($"{indent}  nodePort: {rule.NodePort}");
                    }

                    var manifest =
$@"
apiVersion: install.istio.io/v1alpha1
kind: IstioOperator
spec:
  hub: {KubeConst.LocalClusterRegistry}
  tag: {KubeVersion.Istio}
  meshConfig:
    enablePrometheusMerge: false
    defaultConfig:
      proxyMetadata:
        SECRET_TTL: 2160h
      tracing:
        sampling: 1.0
        openCensusAgent:
          address: grafana-agent-node.neon-monitor:{KubePort.GrafanaNodeAgentOpenCensus}
          context: [""W3C_TRACE_CONTEXT"", ""GRPC_BIN"", ""CLOUD_TRACE_CONTEXT"", ""B3""]
    defaultProviders:
      tracing:
        - opencensus
    extensionProviders:
      - name: access-log
        envoyFileAccessLog:
          path: '/dev/stdout'
          logFormat:
            labels:
              authority: ""%REQ(:AUTHORITY)%""
              client_ip: ""%REQ(X-FORWARDED-FOR)%""
              forwarded_host: ""%REQ(X-FORWARDED-HOST)%""
              forwarded_proto: ""%REQ(X-FORWARDED-PROTO)%""
              host: ""%REQ(:Host)%""
              method: ""%REQ(:METHOD)%""
              mode: ""%PROTOCOL%""
              path: ""%REQ(X-ENVOY-ORIGINAL-PATH?:PATH)%""
              referer: ""%REQ(:Referer)%""
              request_id: ""%REQ(X-REQUEST-ID)%""
              requested_server_name: ""%REQUESTED_SERVER_NAME%""
              response_code: ""%RESPONSE_CODE%""
              trace_id: ""%REQ(traceparent)%""
              user_agent: ""%REQ(USER-AGENT)%""
      - name: opencensus
        opencensus:
          context:
            - W3C_TRACE_CONTEXT
            - GRPC_BIN
            - CLOUD_TRACE_CONTEXT
            - B3
          port: {KubePort.GrafanaNodeAgentOpenCensus}
          service: grafana-agent-node.neon-monitor.svc.cluster.local
      - name: neon-sso-service
        envoyExtAuthzHttp:
          headersToDownstreamOnDeny:
            - content-type
            - set-cookie
          headersToUpstreamOnAllow:
            - authorization
            - path
            - x-auth-request-user
            - x-auth-request-email
            - x-auth-request-access-token
          includeHeadersInCheck:
            - authorization
            - cookie
          port: {KubePort.NeonSsoService}
          service: neon-sso-oauth2-proxy.neon-system.svc.cluster.local
  components:
    pilot:
      k8s:
        nodeSelector:
          {NodeLabel.LabelSystemIstioServices}: ""true""
        env:
          - name: ""GOGC""
            value: ""25""
        priorityClassName: {PriorityClass.NeonNetwork.Name}
        tolerations:
          - operator: Exists
            effect: NoSchedule
          - operator: Exists
            effect: NoExecute
        resources:
          requests:
            cpu: ""{KubeHelper.ToSiString(pilotAdvice.PodCpuRequest)}""
            memory: ""{KubeHelper.ToSiString(pilotAdvice.PodMemoryRequest)}""
          limits:
            cpu: ""{KubeHelper.ToSiString(pilotAdvice.PodCpuLimit)}""
            memory: ""{KubeHelper.ToSiString(pilotAdvice.PodMemoryLimit)}""
        hpaSpec:
          maxReplicas: 1
    ingressGateways:
    - name: istio-ingressgateway
      enabled: true
      k8s:
        nodeSelector:
          {NodeLabel.LabelSystemIstioServices}: ""true""
        env:
          - name: ""GOGC""
            value: ""25""
        priorityClassName: {PriorityClass.NeonNetwork.Name}
        tolerations:
          - operator: Exists
            effect: NoSchedule
          - operator: Exists
            effect: NoExecute
        overlays:
          - apiVersion: apps/v1
            kind: Deployment
            name: istio-ingressgateway
            patches:
              - path: kind
                value: DaemonSet
              - path: spec.strategy
        hpaSpec:
          maxReplicas: 1
        service:
          ports:
{sbNodePorts.ToStringWithoutLastNewLine()}
        resources:
          requests:
            cpu: ""{KubeHelper.ToSiString(ingressAdvice.PodCpuRequest)}""
            memory: ""{KubeHelper.ToSiString(ingressAdvice.PodMemoryRequest)}""
          limits:
            cpu: ""{KubeHelper.ToSiString(ingressAdvice.PodCpuLimit)}""
            memory: ""{KubeHelper.ToSiString(ingressAdvice.PodMemoryLimit)}""
    cni:
      k8s:
        priorityClassName: {PriorityClass.NeonNetwork.Name}
        tolerations:
          - operator: Exists
            effect: NoSchedule
          - operator: Exists
            effect: NoExecute
        env:
          - name: ""GOGC""
            value: ""25""
      enabled: true
      namespace: kube-system
  values:
    sidecarInjectorWebhook:
      enableNamespacesByDefault: false
    global:
      logging:
        level: ""default:info""
      logAsJson: true
      imagePullPolicy: IfNotPresent
      jwtPolicy: third-party-jwt
    gateways:
      istio-ingressgateway:
        type: NodePort
        externalTrafficPolicy: Local
    cni:
      excludeNamespaces:
        - {KubeNamespace.IstioSystem}
        - {KubeNamespace.KubeNodeLease}
        - {KubeNamespace.KubePublic}
      logLevel: info
";
                    // Install Istio.

                    const string script =
$@"
set -euo pipefail

istioctl install --verify -y -f manifest.yaml
";
                    controlNode.SudoCommand(CommandBundle.FromScript(script)
                        .AddFile("manifest.yaml", manifest))
                        .EnsureSuccess();
                });
        }

    /// <summary>
    /// Configures the default trace sampling rates for cluster namespaces. 
    /// </summary>
    /// <param name="controller">The setup controller.</param>
    /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
    /// <returns>The tracking <see cref="Task"/>.</returns>
    public static async Task ConfigureNamespaceTraceSamplingAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/namespace-trace-sampling",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "namespace trace sampling");

                    Func<string, double, Task> ConfigureNamespaceTracing =
                        async (@namespace, samplePercentage) =>
                        {
                            var telemetry = new V1Telemetry()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name              = "default",
                                    NamespaceProperty = @namespace
                                },
                                Spec = new V1TelemetrySpec()
                                {
                                    Tracing = new List<Tracing>()
                                    {
                                        new Tracing()
                                        {
                                            Providers = new List<TracingProvider>()
                                            {
                                                new TracingProvider()
                                                {
                                                    Name = "opencensus"
                                                }
                                            },
                                            RandomSamplingPercentage = samplePercentage
                                        }
                                    }
                                }
                            };

                            await k8s.CustomObjects.CreateNamespacedCustomObjectAsync<V1Telemetry>(telemetry, name: telemetry.Name(), namespaceParameter: telemetry.Namespace());
                        };

                    await controlNode.InvokeIdempotentAsync($"setup/telemetry-{KubeNamespace.Default}", async () => await ConfigureNamespaceTracing(KubeNamespace.Default, cluster.SetupState.ClusterDefinition.Monitor.Trace.DefaultNamespaceSamplingPercentage));
                    await controlNode.InvokeIdempotentAsync($"setup/telemetry-{KubeNamespace.IstioSystem}", async () => await ConfigureNamespaceTracing(KubeNamespace.IstioSystem, cluster.SetupState.ClusterDefinition.Monitor.Trace.KubeIstioSystemNamespaceSamplingPercentage));
                    await controlNode.InvokeIdempotentAsync($"setup/telemetry-{KubeNamespace.KubePublic}", async () => await ConfigureNamespaceTracing(KubeNamespace.KubePublic, cluster.SetupState.ClusterDefinition.Monitor.Trace.KubePublicNamespaceSamplingPercentage));
                    await controlNode.InvokeIdempotentAsync($"setup/telemetry-{KubeNamespace.KubeSystem}", async () => await ConfigureNamespaceTracing(KubeNamespace.KubeSystem, cluster.SetupState.ClusterDefinition.Monitor.Trace.KubeSystemNamespaceSamplingPercentage));
                    await controlNode.InvokeIdempotentAsync($"setup/telemetry-{KubeNamespace.NeonMonitor}", async () => await ConfigureNamespaceTracing(KubeNamespace.NeonMonitor, cluster.SetupState.ClusterDefinition.Monitor.Trace.NeonMonitorNamespaceSamplingPercentage));
                    await controlNode.InvokeIdempotentAsync($"setup/telemetry-{KubeNamespace.NeonStatus}", async () => await ConfigureNamespaceTracing(KubeNamespace.NeonStatus, cluster.SetupState.ClusterDefinition.Monitor.Trace.NeonStatusNamespaceSamplingPercentage));
                    await controlNode.InvokeIdempotentAsync($"setup/telemetry-{KubeNamespace.NeonStorage}", async () => await ConfigureNamespaceTracing(KubeNamespace.NeonStorage, cluster.SetupState.ClusterDefinition.Monitor.Trace.NeonStorageNamespaceSamplingPercentage));
                    await controlNode.InvokeIdempotentAsync($"setup/telemetry-{KubeNamespace.NeonSystem}", async () => await ConfigureNamespaceTracing(KubeNamespace.NeonSystem, cluster.SetupState.ClusterDefinition.Monitor.Trace.NeonSystemNamespaceSamplingPercentage));
                });
        }

        /// <summary>
        /// Installs the Kubernetes Metrics Server service.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMetricsServerAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controlNode.Cluster;
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.MetricsServer);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/kubernetes-metrics-server",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "metrics-server");

                    var values = new Dictionary<string, object>();

                    values.Add("image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("serviceMonitor.enabled", serviceAdvice.MetricsEnabled);
                    values.Add("serviceMonitor.interval", serviceAdvice.MetricsInterval);

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemMetricServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "metrics-server",
                        @namespace: KubeNamespace.KubeSystem,
                        values:     values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/kubernetes-metrics-server-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "metrics-server");

                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.KubeSystem, "metrics-server",
                        timeout:           clusterOpTimeout,
                        pollInterval:      clusterOpPollInterval,
                        cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs CertManager.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallCertManagerAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster            = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s                = GetK8sClient(controller);
            var headendClient      = controller.Get<HeadendClient>(KubeSetupProperty.NeonCloudHeadendClient);
            var clusterAdvice      = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice      = clusterAdvice.GetServiceAdvice(ClusterAdvice.CertManager);
            var ingressAdvice      = clusterAdvice.GetServiceAdvice(ClusterAdvice.IstioIngressGateway);
            var proxyAdvice        = clusterAdvice.GetServiceAdvice(ClusterAdvice.IstioProxy);
            var hostingEnvironment = controller.Get<HostingEnvironment>(KubeSetupProperty.HostingEnvironment);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/cert-manager",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "cert-manager");

                    var values = new Dictionary<string, object>();

                    values.Add("image.registry", KubeConst.LocalClusterRegistry);
                    values.Add($"prometheus.servicemonitor.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"prometheus.servicemonitor.interval", serviceAdvice.MetricsInterval);

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemIstioServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "cert-manager",
                        @namespace:   KubeNamespace.IstioSystem,
                        prioritySpec: $"global.priorityClassName={PriorityClass.NeonNetwork.Name}",
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/cert-manager-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "cert-manager");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.IstioSystem, "cert-manager", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.IstioSystem, "cert-manager-cainjector", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.IstioSystem, "cert-manager-webhook", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                        },
                        timeoutMessage:   "setup/cert-manager-ready",
                        cancellationToken: controller.CancellationToken);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neon-acme",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "neon-acme");

                    var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var k8s           = GetK8sClient(controller);
                    var acmeOptions   = cluster.SetupState.ClusterDefinition.Network.AcmeOptions;
                    var acmeAdvice    = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonAcme);
                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelRole, NodeRole.ControlPlane }
                    };

                    var issuer = new ClusterIssuer()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = "neon-acme",
                            NamespaceProperty = KubeNamespace.IstioSystem
                        },
                        Spec = new V1IssuerSpec()
                        {
                            Acme = acmeOptions.Issuer
                        }
                    };

                    if (issuer.Spec.Acme.ExternalAccountBinding != null)
                    {
                        var secret = new V1Secret()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Name              = issuer.Spec.Acme.ExternalAccountBinding.KeySecretRef.Name,
                                NamespaceProperty = KubeNamespace.IstioSystem
                            },
                            StringData = new Dictionary<string, string>()
                            {
                                { issuer.Spec.Acme.ExternalAccountBinding.KeySecretRef.Key, issuer.Spec.Acme.ExternalAccountBinding.Key }
                            }
                        };

                        await k8s.CoreV1.UpsertSecretAsync(secret, secret.Namespace());

                        issuer.Spec.Acme.ExternalAccountBinding.Key = null;
                    }

                    if (!string.IsNullOrEmpty(issuer.Spec.Acme.PrivateKey))
                    {
                        var secret = new V1Secret()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Name              = issuer.Spec.Acme.PrivateKeySecretRef.Name,
                                NamespaceProperty = KubeNamespace.IstioSystem
                            },
                            StringData = new Dictionary<string, string>()
                            {
                                { issuer.Spec.Acme.PrivateKeySecretRef.Key, issuer.Spec.Acme.PrivateKey }
                            }
                        };

                        await k8s.CoreV1.UpsertSecretAsync(secret, secret.Namespace());

                        issuer.Spec.Acme.PrivateKey                  = null;
                        issuer.Spec.Acme.DisableAccountKeyGeneration = true;
                    }

                    foreach (var solver in issuer.Spec.Acme.Solvers)
                    {
                        if (solver.Dns01.Route53 != null)
                        {
                            var secret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name              = solver.Dns01.Route53.SecretAccessKeySecretRef.Name,
                                    NamespaceProperty = KubeNamespace.IstioSystem
                                },
                                StringData = new Dictionary<string, string>()
                                {
                                    { solver.Dns01.Route53.SecretAccessKeySecretRef.Key, solver.Dns01.Route53.SecretAccessKey }
                                }
                            };

                            await k8s.CoreV1.UpsertSecretAsync(secret, secret.Namespace());

                            solver.Dns01.Route53.SecretAccessKey = null;
                        }
                    }

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/neon-acme-issuer",
                        async () =>
                        {
                            await k8s.CustomObjects.UpsertClusterCustomObjectAsync<ClusterIssuer>(issuer, issuer.Name());
                        });

                    values.Add("image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("image.tag", KubeVersion.NeonKubeContainerImageTag);
                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
                    values.Add("certficateDuration", cluster.SetupState.ClusterDefinition.Network.AcmeOptions.CertificateDuration);
                    values.Add("certificateRenewBefore", cluster.SetupState.ClusterDefinition.Network.AcmeOptions.CertificateRenewBefore);
                    values.Add("isNeonDesktop", cluster.SetupState.ClusterDefinition.IsDesktop);
                    values.Add($"resources.requests.memory", KubeHelper.ToSiString(acmeAdvice.PodMemoryRequest));
                    values.Add($"resources.limits.memory", KubeHelper.ToSiString(acmeAdvice.PodMemoryLimit));
                    values.Add("dotnetGcServer", cluster.SetupState.ClusterDefinition.Nodes.Count() == 1 ? 0 : 1);

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;
                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelRole, NodeRole.ControlPlane))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "neon-acme",
                        @namespace:   KubeNamespace.IstioSystem,
                        prioritySpec: PriorityClass.NeonNetwork.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/cluster-certificates",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "neon-cluster-certificate");

                    var retry = new LinearRetryPolicy(
                        transientDetector: null,
                        retryInterval:     clusterOpPollInterval,
                        timeout:           clusterOpTimeout);

                    IDictionary<string, byte[]> cert = null;

                    await retry.InvokeAsync(
                        async () =>
                        {
                            if (cluster.SetupState.ClusterDefinition.IsDesktop)
                            {
                                cert = await headendClient.NeonDesktop.GetNeonDesktopCertificateAsync();
                            }
                            else
                            {
                                cert = await headendClient.Cluster.GetCertificateAsync(cluster.Id);
                            }
                        },
                        cancellationToken: controller.CancellationToken);

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = "neon-cluster-certificate",
                            NamespaceProperty = KubeNamespace.IstioSystem
                        },
                        Data = cert,
                        Type = "kubernetes.io/tls"
                    };

                    await k8s.CoreV1.CreateNamespacedSecretAsync(secret, secret.Namespace());

                    secret.Metadata.NamespaceProperty = KubeNamespace.NeonSystem;

                    await k8s.CoreV1.CreateNamespacedSecretAsync(secret, secret.Namespace());
                });
        }

        /// <summary>
        /// Configures external apiserver access.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task ConfigureApiserverIngressAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/apiserver-ingress-service",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "apiserver ingress service");

                    var service                        = new V1Service().Initialize();
                    service.Metadata.Name              = "kubernetes-apiserver";
                    service.Metadata.NamespaceProperty = KubeNamespace.IstioSystem;

                    service.Spec = new V1ServiceSpec()
                    {
                        Ports = new List<V1ServicePort>()
                        {
                            new V1ServicePort()
                            {
                                Name       = "https",
                                Protocol   = "TCP",
                                Port       = 443,
                                TargetPort = 443
                            }
                        },
                        Type         = "ExternalName",
                        ExternalName = "kubernetes.default.svc.cluster.local"
                    };

                    await k8s.CoreV1.CreateNamespacedServiceAsync(service, service.Namespace());

                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/apiserver-ingress-destination-rule",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "apiserver ingress destination rule");

                    var destinationRule = new V1DestinationRule().Initialize();

                    destinationRule.Metadata.Name              = "tls-kubernetes-apiserver";
                    destinationRule.Metadata.NamespaceProperty = KubeNamespace.IstioSystem;

                    destinationRule.Spec = new V1DestinationRuleSpec()
                    {
                        Host          = "kubernetes-apiserver",
                        TrafficPolicy = new TrafficPolicy()
                        {
                            Tls = new ClientTLSSettings()
                            {
                                Mode               = TLSMode.SIMPLE,
                                InsecureSkipVerify = false
                            }
                        }
                    };

                    await k8s.CustomObjects.UpsertNamespacedCustomObjectAsync(
                        body:               destinationRule,
                        name:               destinationRule.Name(),
                        namespaceParameter: destinationRule.Namespace());

                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/apiserver-ingress-virtual-service",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "apiserver ingress virtual service");

                    var virtualService                        = new V1VirtualService().Initialize();
                    virtualService.Metadata.Name              = "kubernetes-apiserver";
                    virtualService.Metadata.NamespaceProperty = KubeNamespace.IstioSystem;

                    virtualService.Spec = new V1VirtualServiceSpec()
                    {
                        Gateways = new List<string>() { $"{KubeNamespace.IstioSystem}/neoncluster-gateway" },
                        Hosts    = new List<string>() { cluster.SetupState.ClusterDomain },
                        Http     = new List<HTTPRoute>()
                        {
                            new HTTPRoute()
                            {
                                Match = new List<HTTPMatchRequest>()
                                {
                                    new HTTPMatchRequest()
                                    {
                                        Uri = new StringMatch()
                                        {
                                            Prefix = "/"
                                        }
                                    }
                                },
                                Route = new List<HTTPRouteDestination>()
                                {
                                    new HTTPRouteDestination()
                                    {
                                        Destination = new Destination()
                                        {
                                            Host = $"kubernetes-apiserver.{KubeNamespace.IstioSystem}.svc.cluster.local",
                                            Port = new PortSelector()
                                            {
                                                Number = 443
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    };

                    await k8s.CustomObjects.UpsertNamespacedCustomObjectAsync(
                        body:               virtualService,
                        name:               virtualService.Name(),
                        namespaceParameter: virtualService.Namespace());
                });
        }

        /// <summary>
        /// Installs tokens needed to authenticate with NeonCLOUD services.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallNeonCloudTokenAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neoncloud-token",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "neoncloud token");

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            NamespaceProperty = KubeNamespace.NeonSystem,
                            Name              = "neoncloud-headend-token"
                        },
                        StringData = new Dictionary<string, string>()
                        {
                            { "token", cluster.SetupState.NeonCloudToken }
                        }
                    };

                    await k8s.CoreV1.UpsertSecretAsync(secret, secret.Namespace());

                    secret.Metadata.NamespaceProperty = KubeNamespace.IstioSystem;

                    await k8s.CoreV1.CreateNamespacedSecretAsync(secret, secret.Namespace());
                });
        }

        /// <summary>
        /// Configures the root Kubernetes user.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateRootUserAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var k8s = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/root-user",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "create", message: "root user");

                    var serviceAccount = new V1ServiceAccount()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = $"{KubeConst.RootUser}-user",
                            NamespaceProperty = KubeNamespace.KubeSystem
                        }
                    };

                    await k8s.CoreV1.CreateNamespacedServiceAccountAsync(serviceAccount, serviceAccount.Namespace());

                    var clusterRoleBinding = new V1ClusterRoleBinding()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = $"{KubeConst.RootUser}-user",
                        },
                        RoleRef = new V1RoleRef()
                        {
                            ApiGroup = "rbac.authorization.k8s.io",
                            Kind     = "ClusterRole",
                            Name     = "cluster-admin"
                        },
                        Subjects = new List<V1Subject>()
                        {
                            new V1Subject()
                            {
                                Name              = $"{KubeConst.RootUser}-user",
                                Kind              = "ServiceAccount",
                                NamespaceProperty = KubeNamespace.KubeSystem
                            },
                            new V1Subject()
                            {
                                Name     = $"superadmin",
                                Kind     = "Group",
                                ApiGroup = "rbac.authorization.k8s.io"
                            }
                        }
                    };

                    await k8s.RbacAuthorizationV1.CreateClusterRoleBindingAsync(clusterRoleBinding);
                });
        }

        /// <summary>
        /// Configures the Kubernetes dashboard.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallKubernetesDashboardAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.KubernetesDashboard);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/kube-dashboard",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "kubernetes dashboard");

                    var values = new Dictionary<string, object>();

                    values.Add("replicas", serviceAdvice.Replicas);
                    values.Add("cluster.name", cluster.Name);
                    values.Add("settings.clusterName", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
                    values.Add("neonkube.clusterDomain.kubernetesDashboard", ClusterHost.KubernetesDashboard);
                    values.Add($"serviceMonitor.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"resources.requests.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest));
                    values.Add($"resources.limits.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit));
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

                    await controlNode.InstallHelmChartAsync(controller, "kubernetes-dashboard",
                        @namespace:      KubeNamespace.NeonSystem,
                        prioritySpec:    PriorityClass.NeonApp.Name,
                        values:          values,
                        progressMessage: "kubernetes-dashboard");

                });
        }

        /// <summary>
        /// Adds the node taints.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task TaintNodesAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster      = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s          = GetK8sClient(controller);
            var controlNode  = cluster.DeploymentControlNode;

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/taint-nodes",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "taint", message: "nodes");

                    var nodes = await k8s.CoreV1.ListNodeAsync();

                    foreach (var node in cluster.Nodes)
                    {
                        var patch = new V1Node()
                        {
                            Spec = new V1NodeSpec()
                            {
                                Taints = nodes.Items.Where(n => n.Name() == node.Name).FirstOrDefault().Spec.Taints
                            }
                        };

                        if (patch.Spec.Taints == null)
                        {
                            patch.Spec.Taints = new List<V1Taint>();
                        }

                        if (node.Metadata.Taints != null)
                        {
                            foreach (var taint in node.Metadata.Taints)
                            {
                                patch.Spec.Taints.Add(taint);
                            }
                        }

                        await k8s.CoreV1.PatchNodeAsync(new V1Patch(patch, V1Patch.PatchType.StrategicMergePatch), node.Metadata.Name);
                    }
                });
        }

        /// <summary>
        /// Installs cluster CRDs used later on in setup by various helm charts.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallClusterCrdsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/cluster-crds",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "Install", message: "Cluster CRDs");

                    await controlNode.InstallHelmChartAsync(controller, "cluster-crds",
                        @namespace:  KubeNamespace.NeonSystem);
                });
        }


        /// <summary>
        /// Deploy Kiali.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task InstallKialiAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Kiali);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/kiali",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "setup", message: "kiali");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelSystemIstioServices, "true" }
                    };

                    var secret = await k8s.CoreV1.ReadNamespacedSecretAsync(KubeConst.DexSecret, KubeNamespace.NeonSystem);

                    values.Add("oidc.secret", Encoding.UTF8.GetString(secret.Data["NEONSSO_CLIENT_SECRET"]));
                    values.Add("image.operator.registry", KubeConst.LocalClusterRegistry);
                    values.Add("image.operator.repository", "kiali-kiali-operator");
                    values.Add("image.kiali.registry", KubeConst.LocalClusterRegistry);
                    values.Add("image.kiali.repository", "kiali-kiali");
                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
                    values.Add("neonkube.clusterDomain.sso", ClusterHost.Sso);
                    values.Add("neonkube.clusterDomain.kiali", ClusterHost.Kiali);
                    values.Add($"neonkube.clusterDomain.grafana", ClusterHost.Grafana);
                    values.Add("grafanaPassword", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));
                    values.Add($"metrics.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"metrics.serviceMonitor.interval", serviceAdvice.MetricsInterval);

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemIstioServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "kiali",
                        releaseName:  "kiali-operator",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: PriorityClass.NeonApp.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/kiali-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "kiali");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "kiali-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "kiali", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken)
                        },
                        timeoutMessage: "setup/kiali-ready",
                        cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs the Node Problem Detector.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallNodeProblemDetectorAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.NodeProblemDetector);

            var values = new Dictionary<string, object>();

            values.Add("cluster.name", cluster.Name);
            values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
            values.Add($"metrics.serviceMonitor.enabled", serviceAdvice.MetricsEnabled);
            values.Add($"metrics.serviceMonitor.interval", serviceAdvice.MetricsInterval);
            values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/node-problem-detector",
                async () =>
                {
                    await controlNode.InstallHelmChartAsync(controller, "node-problem-detector",
                        prioritySpec: PriorityClass.NeonOperator.Name,
                        @namespace:   KubeNamespace.NeonSystem);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/node-problem-detector-ready",
                async () =>
                {
                    await k8s.AppsV1.WaitForDaemonsetAsync(KubeNamespace.NeonSystem, "node-problem-detector", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs OpenEBS.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallOpenEbsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            // $todo(jefflill):
            // 
            // We don't currently honor the service advice [MetricsEnabled] properties
            // for any OpenEBS components.  This may be a bit noisy for smaller clusters,
            // but it would be nice to see disk I/O stats for PVs.
            //
            // I've archived the relevant Helm files we used before the port to Kubernetes
            // v1.29.  Hopefully, we'll be able to adapt this technique and re-enable this.
            //
            //      ~\neonKUBE\Lib\Neon.Kube.Setup\Resources\Helm\openebs\archive-servicemonitors
            //
            // This project may also be of some help:
            //
            //      https://github.com/openebs/monitoring
            //      https://github.com/openebs/monitoring/blob/161b5af7f7525c223b9165a13db2d6b667d08aad/docs/guide.md?plain=1

            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var k8s                      = GetK8sClient(controller);
            var cluster                  = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterDefinition        = cluster.SetupState.ClusterDefinition;
            var clusterAdvice            = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var ndmAdvice                = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsNdm);
            var ndmOperatorAdvice        = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsNdmOperator);
            var provisionerLocalPvAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsLocalPvProvisioner);

            if (cluster.SetupState.ClusterDefinition.Storage.OpenEbs.Engine == OpenEbsEngine.Mayastor)
            {
                throw new NotImplementedException($"[{cluster.SetupState.ClusterDefinition.Storage.OpenEbs.Engine}]");
            }

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/openebs-all",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "openebs");

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/openebs",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "configure", message: "openebs-base");

                            var values = new Dictionary<string, object>();

                            AddOpenEbsCommonHelmValues(controller, values);

                            switch (clusterDefinition.Storage.OpenEbs.Engine)
                            {
                                case OpenEbsEngine.cStor:

                                    AddOpenEbsCstorHelmValues(controller, values);
                                    break;

                                case OpenEbsEngine.Jiva:

                                    AddOpenEbsJivaHelmValues(controller, values);
                                    break;

                                case OpenEbsEngine.HostPath:

                                    throw new NotImplementedException();
                                    break;

                                case OpenEbsEngine.Mayastor:
                                default:

                                    throw new NotImplementedException();
                            }

                            await controlNode.InstallHelmChartAsync(controller, "openebs",
                                @namespace:   KubeNamespace.NeonStorage,
                                values:       values,
                                mode:         HelmMode.Template);
                        });

                    await CreateStorageClasses(controller, controlNode, "default", isDefault: true);
                    await CreateHostPathStorageClass(controller, controlNode, "openebs-hostpath");
                    await WaitForOpenEbsReady(controller, controlNode);

                    await controlNode.InvokeIdempotentAsync("setup/openebs-nfs",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "configure", message: "openebs-nfs");

                            var values = new Dictionary<string, object>();

                            await CreateStorageClasses(controller, controlNode, "neon-internal-nfs");

                            values.Add("nfsStorageClass.backendStorageClass", "neon-internal-nfs");

                            await controlNode.InstallHelmChartAsync(controller, "openebs-nfs-provisioner",
                                @namespace: KubeNamespace.NeonStorage,
                                prioritySpec: PriorityClass.NeonStorage.Name,
                                values: values);
                        });

                    await controlNode.InvokeIdempotentAsync("setup/openebs-nfs-ready",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "wait for", message: "openebs");

                            await NeonHelper.WaitAllAsync(
                                new List<Task>()
                                {
                                        k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonStorage, "openebs-nfs-provisioner", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                                },
                                timeoutMessage: "setup/openebs-ready",
                                cancellationToken: controller.CancellationToken);
                        });
                });
        }

        /// <summary>
        /// Adds the Helm values required for deploying common OpenEBS components
        /// required by all engines.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="values">The target Helm values dictionary.</param>
        public static void AddOpenEbsCommonHelmValues(ISetupController controller, Dictionary<string, object> values)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(values != null, nameof(values));

            var clusterAdvice            = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var ndmAdvice                = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsNdm);
            var ndmOperatorAdvice        = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsNdmOperator);
            var localPvProvisionerAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsLocalPvProvisioner);

            //---------------------------------------------
            // Google Analytics: OpenEBS sends usage data to Google.  We're going to disable this.

            values.Add("analytics.enabled", false);

            //---------------------------------------------
            // openebs-ndm

            values.Add("ndm.filters.excludePaths", "/dev/loop,/dev/fd0,/dev/sr0,/dev/ram,/dev/dm-,/dev/md,/dev/rbd,/dev/zd,/dev/sda,/dev/xvda");
            values.Add("ndm.nodeSelector", ndmAdvice.NodeSelector);
            values.Add("ndm.priorityClassName", ndmAdvice.PriorityClassName);                                               // NEONKUBE CUSTOM VALUE
            values.Add("ndm.resources", ndmAdvice.Resources);
            values.Add("ndm.tolerations", ndmAdvice.Tolerations);
            //values.Add("openebsMonitoringAddon.ndm.serviceMonitor.enabled", ndmAdvice.MetricsEnabled);
            //values.Add("openebsMonitoringAddon.ndm.serviceMonitor.interval", ndmAdvice.MetricsInterval);

            //values.Add("ndm.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("ndm.image.repository", "openebs-node-disk-manager");
            //values.Add("ndm.image.tag", KubeVersion.OpenEbsNodeDiskManager);

            //---------------------------------------------
            // openebs-ndm-operator

            values.Add("ndmOperator.nodeSelector", ndmOperatorAdvice.NodeSelector);
            values.Add("ndmOperator.priorityClassName", ndmOperatorAdvice.PriorityClassName);                               // NEONKUBE CUSTOM VALUE
            values.Add("ndmOperator.replicas", ndmOperatorAdvice.Replicas);
            values.Add("ndmOperator.resources", ndmOperatorAdvice.Resources);
            values.Add("ndmOperator.tolerations", ndmOperatorAdvice.Tolerations);
            //values.Add("openebsMonitoringAddon.ndmOperator.serviceMonitor.enabled", ndmOperatorAdvice.MetricsEnabled);
            //values.Add("openebsMonitoringAddon.ndmOperator.serviceMonitor.interval", ndmOperatorAdvice.MetricsInterval);

            //values.Add("ndmOperator.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("ndmOperator.image.repository", "openebs-node-disk-operator");
            //values.Add("ndmOperator.image.tag", KubeVersion.OpenEbsNodeDiskOperator);

            //---------------------------------------------
            // openebs-localpv-provisioner

            values.Add("localpv-provisioner.nodeSelector", localPvProvisionerAdvice.NodeSelector);
            values.Add("localpv-provisioner.priorityClassName", localPvProvisionerAdvice.PriorityClassName);   // NEONKUBE CUSTOM VALUE
            values.Add("localpv-provisioner.replicas", localPvProvisionerAdvice.Replicas);
            values.Add("localpv-provisioner.resources", localPvProvisionerAdvice.Resources);
            values.Add("localpv-provisioner.tolerations", localPvProvisionerAdvice.Tolerations);
            //values.Add("openebsMonitoringAddon.localpv-provisioner.serviceMonitor.enabled", localPvProvisionerAdvice.MetricsEnabled);
            //values.Add("openebsMonitoringAddon.localpv-provisioner.serviceMonitor.interval", localPvProvisionerAdvice.MetricsInterval);

            //values.Add("localpv-provisioner.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("localpv-provisioner.image.repository", "openebs-provisioner-localpv");
            //values.Add("localpv-provisioner.image.tag", KubeVersion.OpenEbsProvisionerLocalPV);
        }

        /// <summary>
        /// Adds the Helm values required for deploying the cStor engine.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="values">The target Helm values dictionary.</param>
        private static void AddOpenEbsCstorHelmValues(ISetupController controller, Dictionary<string, object> values)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(values != null, nameof(values));

            var k8s                        = GetK8sClient(controller);
            var cluster                    = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterDefinition          = cluster.SetupState.ClusterDefinition;
            var clusterAdvice              = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var ndmAdvice                  = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsNdm);
            var ndmOperatorAdvice          = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsNdmOperator);
            var provisionerLocalPvAdvice   = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsLocalPvProvisioner);
            var cStorAdvice                = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsCstor);
            var cStorAdmissionServerAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsCstorAdmissionServer);

            values.Add("cstor.enabled", true);

            //---------------------------------------------
            // $todo(jefflill)

            values.Add("admissionServer.nodeSelector", cStorAdmissionServerAdvice.NodeSelector);

            //values.Add("admissionServer.image.registry", "");
            //values.Add("admissionServer.image.repository", "");
            //values.Add("admissionServer.image.tag", "");
        }

        /// <summary>
        /// Adds the Helm values required for deploying the Jiva engine.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="values">The target Helm values dictionary.</param>
        private static void AddOpenEbsJivaHelmValues(ISetupController controller, Dictionary<string, object> values)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(values != null, nameof(values));

            var clusterAdvice           = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var jivaOperatorAdvice      = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsJivaOperator);
            var jivaCsiControllerAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsJivaCsiController);
            var jivaCsiNodeAdvice       = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsCstorCsiNode);

            //---------------------------------------------
            // openebs-jiva-operator

            values.Add("jiva.jivaOperator.priorityClassName", jivaOperatorAdvice.PriorityClassName);        // NEONKUBE CUSTOM VALUE
            values.Add("jiva.jivaOperator.nodeSelector", jivaOperatorAdvice.NodeSelector);
            values.Add("jiva.jivaOperator.resources", jivaOperatorAdvice.Resources);
            values.Add("jiva.jivaOperator.replicas", jivaOperatorAdvice.Replicas);
            values.Add("jiva.jivaOperator.tolerations", jivaOperatorAdvice.Tolerations);
            //values.Add("openebsMonitoringAddon.jivaOperator.serviceMonitor.enabled", jivaOperatorAdvice.MetricsEnabled);
            //values.Add("openebsMonitoringAddon.jivaOperator.serviceMonitor.interval", jivaOperatorAdvice.MetricsInterval);

            //values.Add("jiva.jivaOperator.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.jivaOperator.image.repository", "openebs-jiva-operator");
            //values.Add("jiva.jivaOperator.image.tag", KubeVersion.OpenEbsJiva);

            //values.Add("jiva.jivaOperator.controller.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.jivaOperator.controller.image.repository", "openebs-jiva");
            //values.Add("jiva.jivaOperator.controller.image.tag", KubeVersion.OpenEbsJiva);

            //values.Add("jiva.jivaOperator.replica.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.jivaOperator.replica.image.repository", "openebs-jiva-operator");
            //values.Add("jiva.jivaOperator.replica.image.tag", KubeVersion.OpenEbsJiva);

            //values.Add("jiva.jivaOperator.exporter.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.jivaOperator.exporter.image.repository", "openebs-m-exporter");
            //values.Add("jiva.jivaOperator.exporter.image.tag", KubeVersion.OpenEbsJiva);

            //---------------------------------------------
            // openebs-jiva-csi-controller

            values.Add("jiva.enabled", true);
            values.Add("jiva.csiController.priorityClassName", jivaCsiControllerAdvice.PriorityClassName);  // NEONKUBE CUSTOM VALUE
            values.Add("jiva.csiController.nodeSelector", jivaCsiControllerAdvice.NodeSelector);
            values.Add("jiva.csiController.replicas", jivaCsiControllerAdvice.Replicas);
            values.Add("jiva.csiController.resources", jivaCsiControllerAdvice.Resources);
            values.Add("jiva.csiController.tolerations", jivaCsiControllerAdvice.Tolerations);
            //values.Add("openebsMonitoringAddon.csiController.serviceMonitor.enabled", jivaCsiControllerAdvice.MetricsEnabled);
            //values.Add("openebsMonitoringAddon.csiController.serviceMonitor.interval", jivaCsiControllerAdvice.MetricsInterval);

            //values.Add("jiva.csiController.attacher.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.csiController.attacher.image.repository", "k8scsi-csi-attacher");
            //values.Add("jiva.csiController.attacher.image.tag", KubeVersion.K8sCsiAttacher);

            //values.Add("jiva.csiController.livenessprobe.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.csiController.livenessprobe.image.repository", "k8scsi-livenessprobe");
            //values.Add("jiva.csiController.livenessprobe.image.tag", KubeVersion.K8sCsiLivenessProbe);

            //values.Add("jiva.csiController.csi-provisioner.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.csiController.csi-provisioner.image.repository", "k8scsi-csi-provisioner");
            //values.Add("jiva.csiController.csi-provisioner.image.tag", KubeVersion.K8sCsiProvisioner);

            //values.Add("jiva.csiController.csi-resizer.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.csiController.csi-resizer.image.repository", "k8scsi-csi-resizer");
            //values.Add("jiva.csiController.csi-resizer.image.tag", KubeVersion.K8sCsiResizer);

            //---------------------------------------------
            // openebs-jiva-csi-node

            values.Add("jiva.csiNode.priorityClassName", jivaCsiNodeAdvice.PriorityClassName);              // NEONKUBE CUSTOM VALUE
            values.Add("jiva.csiNode.nodeSelector", jivaCsiNodeAdvice.NodeSelector);
            values.Add("jiva.csiNode.resources", jivaCsiNodeAdvice.Resources);
            values.Add("jiva.csiNode.tolerations", jivaCsiNodeAdvice.Tolerations);
            //values.Add("openebsMonitoringAddon.csiNode.serviceMonitor.enabled", jivaCsiNodeAdvice.MetricsEnabled);
            //values.Add("openebsMonitoringAddon.csiNode.serviceMonitor.interval", jivaCsiNodeAdvice.MetricsInterval);

            //values.Add("jiva.csiNode.driverRegistrar.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.csiNode.driverRegistrar.image.repository", "k8scsi-csi-node-driver-registrar");
            //values.Add("jiva.csiNode.driverRegistrar.image.tag", KubeVersion.OpenEbsDriverRegistrar);

            //values.Add("jiva.jivaCSIPlugin.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.jivaCSIPlugin.image.repository", "k8scsi-csi-resizer");
            //values.Add("jiva.jivaCSIPlugin.image.tag", KubeVersion.K8sCsiResizer);

            //values.Add("jiva.csiNode.livenessprobe.image.registry", KubeConst.LocalClusterRegistry);
            //values.Add("jiva.csiNode.livenessprobe.image.repository", "k8scsi-livenessprobe");
            //values.Add("jiva.csiNode.livenessprobe.image.tag", KubeVersion.K8sCsiLivenessProbe);

            // $todo(jefflill): MORE IMAGES?
        }

#if TODO
        /// <summary>
        /// Deploys OpenEBS using the cStor engine.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task DeployOpenEbsWithcStor(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;

            var cluster            = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterAdvice      = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var k8s                = GetK8sClient(controller);
            var cstorPoolAdvice    = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsCstorPool);
            var cstorPoolAuxAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.OpenEbsCstorPoolAux);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/openebs-cstor",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "openebs-cstor");

                    var values = new Dictionary<string, object>();

                    values.Add("cspcOperator.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("cspcOperator.poolManager.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("cspcOperator.cstorPool.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("cspcOperator.cstorPoolExporter.image.registry", KubeConst.LocalClusterRegistry);

                    values.Add("cvcOperator.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("cvcOperator.target.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("cvcOperator.volumeMgmt.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("cvcOperator.volumeExporter.image.registry", KubeConst.LocalClusterRegistry);

                    values.Add("csiController.resizer.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.snapshotter.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.snapshotController.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.attacher.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.provisioner.image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("csiController.driverRegistrar.image.registry", KubeConst.LocalClusterRegistry);

                    values.Add("cstorCSIPlugin.image.registry", KubeConst.LocalClusterRegistry);

                    values.Add("csiNode.driverRegistrar.image.registry", KubeConst.LocalClusterRegistry);

                    values.Add("admissionServer.image.registry", KubeConst.LocalClusterRegistry);

                    await controlNode.InstallHelmChartAsync(controller, "openebs-cstor-operator",
                        releaseName: "openebs-cstor",
                        values:      values,
                        @namespace:  KubeNamespace.NeonStorage);
                });

            controller.LogProgress(controlNode, verb: "configure", message: "openebs-pool");

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/openebs-pool",
                async () =>
                {
                    var cStorPoolCluster = new V1CStorPoolCluster()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = "cspc-stripe",
                            NamespaceProperty = KubeNamespace.NeonStorage
                        },
                        Spec = new V1CStorPoolClusterSpec()
                        {
                            Pools     = new List<V1CStorPoolSpec>(),
                            Resources = new V1ResourceRequirements()
                            {
                                Limits   = new Dictionary<string, ResourceQuantity>() { { "memory", new ResourceQuantity(KubeHelper.ToSiString(cstorPoolAdvice.PodMemoryLimit)) } },
                                Requests = new Dictionary<string, ResourceQuantity>() { { "memory", new ResourceQuantity(KubeHelper.ToSiString(cstorPoolAdvice.PodMemoryRequest)) } },
                            },
                            AuxResources = new V1ResourceRequirements()
                            {
                                Limits   = new Dictionary<string, ResourceQuantity>() { { "memory", new ResourceQuantity(KubeHelper.ToSiString(cstorPoolAuxAdvice.PodMemoryLimit)) } },
                                Requests = new Dictionary<string, ResourceQuantity>() { { "memory", new ResourceQuantity(KubeHelper.ToSiString(cstorPoolAuxAdvice.PodMemoryRequest)) } },
                            }
                        }
                    };

                    var blockDevices = await k8s.CustomObjects.ListNamespacedCustomObjectAsync<V1CStorBlockDevice>(KubeNamespace.NeonStorage);

                    foreach (var node in cluster.SetupState.ClusterDefinition.Nodes.Where(n => n.OpenEbsStorage))
                    {
                        var disk = cluster.HostingManager.GetDataDisk(cluster.Nodes.Where(n => n.Name == node.Name).FirstOrDefault());

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
                                DataRaidGroupType = DataRaidGroupTypes.Stripe,
                                Tolerations       = new List<V1Toleration>()
                                {
                                    { new V1Toleration() { Effect = "NoSchedule", OperatorProperty = "Exists" } },
                                    { new V1Toleration() { Effect = "NoExecute", OperatorProperty = "Exists" } }
                                }
                            }
                        };

                        var device = blockDevices.Items.Where(
                            device => device.Spec.NodeAttributes.GetValueOrDefault("nodeName") == node.Name
                            && device.Spec.Path == disk).FirstOrDefault();

                        pool.DataRaidGroups.FirstOrDefault().BlockDevices.Add(
                            new V1CStorBlockDeviceRef()
                            {
                                BlockDeviceName = device.Metadata.Name
                            });

                        cStorPoolCluster.Spec.Pools.Add(pool);
                    }

                    await k8s.CustomObjects.CreateNamespacedCustomObjectAsync(
                        body:               cStorPoolCluster,
                        name:               cStorPoolCluster.Name(),
                        namespaceParameter: cStorPoolCluster.Namespace());
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/openebs-cstor-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "openebs cstor");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.AppsV1.WaitForDaemonsetAsync(KubeNamespace.NeonStorage, "openebs-cstor-csi-node", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonStorage, "openebs-cstor-admission-server", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonStorage, "openebs-cstor-cvc-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonStorage, "openebs-cstor-cspc-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken)
                        },
                        timeoutMessage:    "setup/openebs-cstor-ready",
                        cancellationToken: controller.CancellationToken);
                });

            var replicas = 3;

            if (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.OpenEbsStorage).Count() < replicas)
            {
                replicas = cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.OpenEbsStorage).Count();
            }

            controller.ThrowIfCancelled();
            await CreateCstorStorageClass(controller, controlNode, "openebs-cstor", replicaCount: replicas);
        }
#endif

        /// <summary>
        /// Waits for OpenEBS to become ready.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task WaitForOpenEbsReady(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;

            var k8s = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/openebs-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "openebs");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.AppsV1.WaitForDaemonsetAsync(KubeNamespace.NeonStorage, "openebs-ndm", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDaemonsetAsync(KubeNamespace.NeonStorage, "openebs-ndm-node-exporter", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDaemonsetAsync(KubeNamespace.NeonStorage, "openebs-jiva-operator-csi-node", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonStorage, "openebs-jiva-operator-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonStorage, "openebs-localpv-provisioner", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonStorage, "openebs-ndm-cluster-exporter", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonStorage, "openebs-ndm-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonStorage, "openebs-jiva-operator-csi-controller", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                        },
                        timeoutMessage:    "setup/openebs-ready",
                        cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Creates a Kubernetes namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <param name="name">The new WatchNamespace name.</param>
        /// <param name="istioInjectionEnabled">Whether Istio sidecar injection should be enabled.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateNamespaceAsync(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    controlNode,
            string                          name,
            bool                            istioInjectionEnabled = true)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var k8s = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync($"setup/namespace-{name}",
                async () =>
                {
                    await k8s.CoreV1.CreateNamespaceAsync(
                        new V1Namespace()
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
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="replicaCount">Specifies the data replication factor.</param>
        /// <param name="storagePool">Specifies the OpenEBS storage pool.</param>
        /// <param name="isDefault">Optionally indicates that this is the default storage class.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateJivaStorageClass(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    controlNode,
            string                          name,
            int                             replicaCount = 3,
            string                          storagePool  = "default",
            bool                            isDefault    = false)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(replicaCount > 0, nameof(replicaCount));

            var k8s = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync($"setup/storage-class-jiva-{name}",
                async () =>
                {
                    if (controlNode.Cluster.SetupState.ClusterDefinition.Nodes.Count() < replicaCount)
                    {
                        replicaCount = controlNode.Cluster.SetupState.ClusterDefinition.Nodes.Count();
                    }

                    var storageClass = new V1StorageClass()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name        = name
                        },
                        Parameters        = new Dictionary<string, string>()
                        {
                            {"cas-type", "jiva" },
                            {"policy", "openebs-jiva-default-policy" },
                        },
                        Provisioner       = "jiva.csi.openebs.io",
                        ReclaimPolicy     = "Delete",
                        VolumeBindingMode = "WaitForFirstConsumer"
                    };

                    if (isDefault)
                    {
                        storageClass.Metadata.Annotations = new Dictionary<string, string>()
                        {
                            { "storageclass.kubernetes.io/is-default-class", "true" }
                        };
                    }

                    await k8s.StorageV1.CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Creates a Kubernetes Storage Class.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="isDefault">Specifies whether this should be the default storage class.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateHostPathStorageClass(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    controlNode,
            string                          name,
            bool                            isDefault = false)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var k8s = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync($"setup/storage-class-hostpath-{name}",
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

                    if (isDefault)
                    {
                        storageClass.Metadata.Annotations.Add("storageclass.kubernetes.io/is-default-class", "true");
                    }

                    await k8s.StorageV1.CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Creates an OpenEBS cStor Kubernetes Storage Class.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="cstorPoolCluster">Specifies the cStor pool name.</param>
        /// <param name="replicaCount">Specifies the data replication factor.</param>
        /// <param name="isDefault">Specifies whether this should be the default storage class.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateCstorStorageClass(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    controlNode,
            string                          name,
            string                          cstorPoolCluster = "cspc-stripe",
            int                             replicaCount     = 3,
            bool                            isDefault        = false)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(replicaCount > 0, nameof(replicaCount));

            var k8s = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync($"setup/storage-class-cstor-{name}",
                async () =>
                {
                    if (controlNode.Cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.OpenEbsStorage).Count() < replicaCount)
                    {
                        replicaCount = controlNode.Cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.OpenEbsStorage).Count();
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

                    if (isDefault)
                    {
                        storageClass.Metadata.Annotations = new Dictionary<string, string>()
                        {
                            { "storageclass.kubernetes.io/is-default-class", "true" }
                        };
                    }

                    await k8s.StorageV1.CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Creates the approperiate OpenEBS Kubernetes Storage Class for the cluster.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <param name="replicaCount">Specifies the data replication factor.</param>
        /// <param name="isDefault">Specifies whether this should be the default storage class.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateStorageClasses(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    controlNode,
            string                          name,
            int                             replicaCount = 3,
            bool                            isDefault    = false)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(replicaCount > 0, nameof(replicaCount));

            var cluster           = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterDefinition = cluster.SetupState.ClusterDefinition;
            var storageOptions    = clusterDefinition.Storage;

            controller.ThrowIfCancelled();

            switch (storageOptions.OpenEbs.Engine)
            {
                case OpenEbsEngine.cStor:

                    await CreateHostPathStorageClass(controller, controlNode, name, isDefault: false);
                    await CreateCstorStorageClass(controller, controlNode, name, isDefault: isDefault);
                    break;

                case OpenEbsEngine.Default:

                    Covenant.Assert(false, $"[{nameof(OpenEbsEngine.Default)}] is not valid here.  This must be set to one of the other storage engines in [{nameof(OpenEbsOptions)}.Validate()].");
                    break;

                case OpenEbsEngine.HostPath:

                    await CreateHostPathStorageClass(controller, controlNode, name, isDefault: isDefault);
                    break;

                case OpenEbsEngine.Jiva:

                    await CreateHostPathStorageClass(controller, controlNode, name, isDefault: false);
                    await CreateJivaStorageClass(controller, controlNode, name, isDefault: isDefault);
                    break;

                case OpenEbsEngine.Mayastor:
                default:

                    await CreateHostPathStorageClass(controller, controlNode, name, isDefault: false);
                    throw new NotImplementedException($"[{cluster.SetupState.ClusterDefinition.Storage.OpenEbs.Engine}] OpenEBS storage engine is not supported yet.");
            };
        }

        /// <summary>
        /// Installs The Grafana Agent to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallPrometheusAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster         = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterAdvice   = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var agentAdvice     = clusterAdvice.GetServiceAdvice(ClusterAdvice.GrafanaAgent);
            var agentNodeAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.GrafanaAgentNode);
            var blackboxAdvice  = clusterAdvice.GetServiceAdvice(ClusterAdvice.BlackboxExporter);
            var istioAdvice     = clusterAdvice.GetServiceAdvice(ClusterAdvice.IstioProxy);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-prometheus",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "prometheus");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelSystemMetricServices, "true" }
                    };

                    values.Add($"cluster.name", cluster.Name);
                    values.Add($"cluster.domain", cluster.SetupState.ClusterDomain);
                    values.Add($"cluster.datacenter", cluster.SetupState.ClusterDefinition.Datacenter);
                    values.Add($"cluster.version", cluster.SetupState.ClusterDefinition.ClusterVersion);
                    values.Add($"cluster.hostingEnvironment", cluster.Hosting.Environment);

                    values.Add($"metrics.global.enabled", clusterAdvice.MetricsEnabled);
                    values.Add($"metrics.global.scrapeInterval", clusterAdvice.MetricsInterval);
                    values.Add($"metrics.crio.enabled", clusterAdvice.MetricsEnabled);
                    values.Add($"metrics.crio.scrapeInterval", clusterAdvice.MetricsInterval);
                    values.Add($"metrics.istio.enabled", istioAdvice.MetricsEnabled);
                    values.Add($"metrics.istio.scrapeInterval", istioAdvice.MetricsInterval);
                    values.Add($"metrics.kubelet.enabled", clusterAdvice.MetricsEnabled);
                    values.Add($"metrics.kubelet.scrapeInterval", clusterAdvice.MetricsInterval);
                    values.Add($"metrics.cadvisor.enabled", clusterAdvice.MetricsEnabled);
                    values.Add($"metrics.cadvisor.scrapeInterval", clusterAdvice.MetricsInterval);
                    values.Add($"tracing.enabled", cluster.SetupState.ClusterDefinition.Features.Tempo);
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

                    values.Add($"resources.agent.requests.memory", KubeHelper.ToSiString(agentAdvice.PodMemoryRequest));
                    values.Add($"resources.agent.limits.memory", KubeHelper.ToSiString(agentAdvice.PodMemoryLimit));

                    values.Add($"resources.agentNode.requests.memory", KubeHelper.ToSiString(agentNodeAdvice.PodMemoryRequest));
                    values.Add($"resources.agentNode.limits.memory", KubeHelper.ToSiString(agentNodeAdvice.PodMemoryLimit));

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemMetricServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "grafana-agent",
                        @namespace:   KubeNamespace.NeonMonitor,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-prometheus-blackbox-exporter",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "prometheus");

                    var values = new Dictionary<string, object>();
                    var i      = 0;

                    values.Add($"replicas", blackboxAdvice.Replicas);
                    values.Add($"serviceMesh.enabled", false);
                    values.Add($"resources.requests.memory", KubeHelper.ToSiString(blackboxAdvice.PodMemoryRequest));
                    values.Add($"resources.limits.memory", KubeHelper.ToSiString(blackboxAdvice.PodMemoryLimit));

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemMetricServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "blackbox-exporter",
                        @namespace:   KubeNamespace.NeonMonitor,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });
        }

        /// <summary>
        /// Waits for Prometheus to be fully ready.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task WaitForPrometheusAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-grafana-agent-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "grafana agent");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "grafana-agent-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDaemonsetAsync(KubeNamespace.NeonMonitor, "grafana-agent-node", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "grafana-agent", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                        },
                        timeoutMessage:    "setup/monitoring-grafana-agent-ready",
                        cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs Memcached to the neon-system namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMemcachedAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            controller.ThrowIfCancelled();
            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Memcached);

            var values = new Dictionary<string, object>();

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/memcached",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "memcached");

                    values.Add($"replicas", serviceAdvice.Replicas);
                    values.Add($"metrics.serviceMonitor.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"metrics.serviceMonitor.interval", serviceAdvice.MetricsInterval);
                    values.Add($"serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
                    values.Add($"resources.requests.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest));
                    values.Add($"resources.limits.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit));
                    values.Add($"server.memory", Decimal.ToInt32(serviceAdvice.PodMemoryLimit.Value / 1200000));

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelRole, "worker"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    values.Add("image.registry", KubeConst.LocalClusterRegistry);

                    await controlNode.InstallHelmChartAsync(controller, "memcached",
                        releaseName:  "neon-memcached",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/memcached-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "memcached");

                    await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonSystem, "neon-memcached", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs Mimir to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMimirAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            await CreateHostPathStorageClass(controller, controlNode, "neon-internal-mimir");

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-mimir-all",
                async () =>
                {
                    var cluster             = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var k8s                 = GetK8sClient(controller);
                    var clusterAdvice       = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
                    var mimirAdvice         = clusterAdvice.GetServiceAdvice(ClusterAdvice.Mimir);
                    var alertmanagerAdvice  = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirAlertmanager);
                    var compactorAdvice     = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirCompactor);
                    var distributorAdvice   = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirDistributor);
                    var ingesterAdvice      = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirIngester);
                    var overridesAdvice     = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirOverridesExporter);
                    var querierAdvice       = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirQuerier);
                    var queryFrontendAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirQueryFrontend);
                    var rulerAdvice         = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirRuler);
                    var storeGatewayAdvice  = clusterAdvice.GetServiceAdvice(ClusterAdvice.MimirStoreGateway);
                    var values              = new Dictionary<string, object>();

                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelSystemMetricServices, "true" }
                    };

                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);

                    values.Add($"alertmanager.replicas", alertmanagerAdvice.Replicas);
                    values.Add($"alertmanager.resources.requests.memory", KubeHelper.ToSiString(alertmanagerAdvice.PodMemoryRequest));
                    values.Add($"alertmanager.resources.limits.memory", KubeHelper.ToSiString(alertmanagerAdvice.PodMemoryLimit));
                    values.Add($"alertmanager.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"compactor.replicas", compactorAdvice.Replicas);
                    values.Add($"compactor.resources.requests.memory", KubeHelper.ToSiString(compactorAdvice.PodMemoryRequest));
                    values.Add($"compactor.resources.limits.memory", KubeHelper.ToSiString(compactorAdvice.PodMemoryLimit));
                    values.Add($"compactor.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"distributor.replicas", distributorAdvice.Replicas);
                    values.Add($"distributor.resources.requests.memory", KubeHelper.ToSiString(distributorAdvice.PodMemoryRequest));
                    values.Add($"distributor.resources.limits.memory", KubeHelper.ToSiString(distributorAdvice.PodMemoryLimit));
                    values.Add($"distributor.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"ingester.replicas", ingesterAdvice.Replicas);
                    values.Add($"ingester.resources.requests.memory", KubeHelper.ToSiString(ingesterAdvice.PodMemoryRequest));
                    values.Add($"ingester.resources.limits.memory", KubeHelper.ToSiString(ingesterAdvice.PodMemoryLimit));
                    values.Add($"ingester.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"overrides_exporter.replicas", overridesAdvice.Replicas);
                    values.Add($"overrides_exporter.resources.requests.memory", KubeHelper.ToSiString(overridesAdvice.PodMemoryRequest));
                    values.Add($"overrides_exporter.resources.limits.memory", KubeHelper.ToSiString(overridesAdvice.PodMemoryLimit));
                    values.Add($"overrides_exporter.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"querier.replicas", querierAdvice.Replicas);
                    values.Add($"querier.resources.requests.memory", KubeHelper.ToSiString(querierAdvice.PodMemoryRequest));
                    values.Add($"querier.resources.limits.memory", KubeHelper.ToSiString(querierAdvice.PodMemoryLimit));
                    values.Add($"querier.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"query_frontend.replicas", queryFrontendAdvice.Replicas);
                    values.Add($"query_frontend.resources.requests.memory", KubeHelper.ToSiString(queryFrontendAdvice.PodMemoryRequest));
                    values.Add($"query_frontend.resources.limits.memory", KubeHelper.ToSiString(queryFrontendAdvice.PodMemoryLimit));
                    values.Add($"query_frontend.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"ruler.replicas", rulerAdvice.Replicas);
                    values.Add($"ruler.resources.requests.memory", KubeHelper.ToSiString(rulerAdvice.PodMemoryRequest));
                    values.Add($"ruler.resources.limits.memory", KubeHelper.ToSiString(rulerAdvice.PodMemoryLimit));
                    values.Add($"ruler.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"store_gateway.replicas", storeGatewayAdvice.Replicas);
                    values.Add($"store_gateway.resources.requests.memory", KubeHelper.ToSiString(storeGatewayAdvice.PodMemoryRequest));
                    values.Add($"store_gateway.resources.limits.memory", KubeHelper.ToSiString(storeGatewayAdvice.PodMemoryLimit));
                    values.Add($"store_gateway.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"serviceMonitor.enabled", mimirAdvice.MetricsEnabled);
                    values.Add($"serviceMonitor.interval", mimirAdvice.MetricsInterval);
                    values.Add($"serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
                    values.Add($"tracing.enabled", cluster.SetupState.ClusterDefinition.Features.Tempo);
                    values.Add($"minio.enabled", true);
                    values.Add($"minio.bucket.mimirTsdb.quota", clusterAdvice.MetricsQuota);

                    if (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count() == 1)
                    {
                        values.Add($"blocksStorage.tsdb.block_ranges_period[0]", "1h0m0s");
                        values.Add($"blocksStorage.tsdb.retention_period", "2h0m0s");
                        values.Add($"limits.compactor_blocks_retention_period", "12h");
                        values.Add($"compactor.config.deletion_delay", "1h");
                        values.Add($"blocksStorage.bucketStore.ignore_deletion_mark_delay", "15m");
                    }

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/monitoring-mimir-secret",
                        async () =>
                        {
                            var dbSecret = await k8s.CoreV1.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespace.NeonSystem);

                            var citusSecret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name              = KubeConst.CitusSecretKey,
                                    NamespaceProperty = KubeNamespace.NeonMonitor
                                },
                                Data       = new Dictionary<string, byte[]>(),
                                StringData = new Dictionary<string, string>()
                            };

                            citusSecret.Data["username"] = dbSecret.Data["username"];
                            citusSecret.Data["password"] = dbSecret.Data["password"];

                            await k8s.CoreV1.UpsertSecretAsync(citusSecret, KubeNamespace.NeonMonitor);
                        }
                        );

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;
                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemMetricServices, "true"))
                    {
                        foreach (var component in new string[] { "alertmanager", "distributor", "ingester", "overrides_exporter", "ruler", "querier", "query_frontend", "store_gateway", "compactor" })
                        {
                            values.Add($"{component}.tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                            values.Add($"{component}.tolerations[{i}].effect", taint.Effect);
                            values.Add($"{component}.tolerations[{i}].operator", "Exists");
                        }

                        i++;
                    }

                    values.Add("image.registry", KubeConst.LocalClusterRegistry);

                    await controlNode.InstallHelmChartAsync(controller, "mimir",
                        @namespace: KubeNamespace.NeonMonitor,
                        values:     values);

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/monitoring-mimir-ready",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "wait for", message: "mimir");

                            await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "mimir-alertmanager", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                            await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "mimir-compactor", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                            await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "mimir-distributor", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                            await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "mimir-ingester", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                            await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "mimir-overrides-exporter", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                            await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "mimir-querier", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                            await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "mimir-query-frontend", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                            await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "mimir-ruler", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                            await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "mimir-store-gateway", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                        });
                });
        }

        /// <summary>
        /// Installs Loki to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallLokiAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster             = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s                 = GetK8sClient(controller);
            var clusterAdvice       = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var lokiAdvice          = clusterAdvice.GetServiceAdvice(ClusterAdvice.Loki);
            var compactorAdvice     = clusterAdvice.GetServiceAdvice(ClusterAdvice.LokiCompactor);
            var distributorAdvice   = clusterAdvice.GetServiceAdvice(ClusterAdvice.LokiDistributor);
            var ingesterAdvice      = clusterAdvice.GetServiceAdvice(ClusterAdvice.LokiIngester);
            var indexGatewayAdvice  = clusterAdvice.GetServiceAdvice(ClusterAdvice.LokiIndexGateway);
            var querierAdvice       = clusterAdvice.GetServiceAdvice(ClusterAdvice.LokiQuerier);
            var queryFrontendAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.LokiQueryFrontend);
            var rulerAdvice         = clusterAdvice.GetServiceAdvice(ClusterAdvice.LokiRuler);
            var tableManagerAdvice  = clusterAdvice.GetServiceAdvice(ClusterAdvice.LokiTableManager);

            await CreateHostPathStorageClass(controller, controlNode, "neon-internal-loki");

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-loki",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "loki");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelSystemLogServices, "true" }
                    };

                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);

                    values.Add($"compactor.replicas", compactorAdvice.Replicas);
                    values.Add($"compactor.resources.requests.memory", KubeHelper.ToSiString(compactorAdvice.PodMemoryRequest));
                    values.Add($"compactor.resources.limits.memory", KubeHelper.ToSiString(compactorAdvice.PodMemoryLimit));
                    values.Add($"compactor.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"distributor.replicas", distributorAdvice.Replicas);
                    values.Add($"distributor.resources.requests.memory", KubeHelper.ToSiString(distributorAdvice.PodMemoryRequest));
                    values.Add($"distributor.resources.limits.memory", KubeHelper.ToSiString(distributorAdvice.PodMemoryLimit));
                    values.Add($"distributor.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"indexGateway.replicas", indexGatewayAdvice.Replicas);
                    values.Add($"indexGateway.resources.requests.memory", KubeHelper.ToSiString(indexGatewayAdvice.PodMemoryRequest));
                    values.Add($"indexGateway.resources.limits.memory", KubeHelper.ToSiString(indexGatewayAdvice.PodMemoryLimit));
                    values.Add($"indexGateway.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"ingester.replicas", ingesterAdvice.Replicas);
                    values.Add($"ingester.resources.requests.memory", KubeHelper.ToSiString(ingesterAdvice.PodMemoryRequest));
                    values.Add($"ingester.resources.limits.memory", KubeHelper.ToSiString(ingesterAdvice.PodMemoryLimit));
                    values.Add($"ingester.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"querier.replicas", querierAdvice.Replicas);
                    values.Add($"querier.resources.requests.memory", KubeHelper.ToSiString(querierAdvice.PodMemoryRequest));
                    values.Add($"querier.resources.limits.memory", KubeHelper.ToSiString(querierAdvice.PodMemoryLimit));
                    values.Add($"querier.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"queryFrontend.replicas", queryFrontendAdvice.Replicas);
                    values.Add($"queryFrontend.resources.requests.memory", KubeHelper.ToSiString(queryFrontendAdvice.PodMemoryRequest));
                    values.Add($"queryFrontend.resources.limits.memory", KubeHelper.ToSiString(queryFrontendAdvice.PodMemoryLimit));
                    values.Add($"queryFrontend.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"ruler.replicas", rulerAdvice.Replicas);
                    values.Add($"ruler.resources.requests.memory", KubeHelper.ToSiString(rulerAdvice.PodMemoryRequest));
                    values.Add($"ruler.resources.limits.memory", KubeHelper.ToSiString(rulerAdvice.PodMemoryLimit));
                    values.Add($"ruler.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"serviceMonitor.enabled", lokiAdvice.MetricsEnabled);
                    values.Add($"serviceMonitor.interval", lokiAdvice.MetricsInterval);
                    values.Add($"serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
                    values.Add($"tracing.enabled", cluster.SetupState.ClusterDefinition.Features.Tempo);

                    values.Add($"minio.enabled", true);
                    values.Add($"minio.bucket.quota", clusterAdvice.LogsQuota);

                    if (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.SystemLogServices).Count() >= 3)
                    {
                        values.Add($"config.replication_factor", 3);
                    }

                    values.Add($"loki.schemaConfig.configs[0].object_store", "aws");
                    values.Add($"loki.storageConfig.boltdb_shipper.shared_store", "s3");

                    if (cluster.SetupState.ClusterDefinition.IsDesktop || cluster.SetupState.ClusterDefinition.Nodes.Count() == 1)
                    {
                        values.Add($"loki.storageConfig.boltdb_shipper.cache_ttl", "24h");
                        values.Add($"limits_config.retention_period", "24h");
                        values.Add($"limits_config.reject_old_samples_max_age", "6h");
                        values.Add($"table_manager.retention_period", "24h");
                    }

                    var replayMemoryCeiling = ByteUnits.Humanize(
                        size:            ingesterAdvice.PodMemoryLimit.Value * 0.75m,
                        powerOfTwo:      false,
                        spaceBeforeUnit: true,
                        removeByteUnit:  false);

                    var byteUnitParts = replayMemoryCeiling.Split(' ');
                    var bytes = double.Parse(byteUnitParts.First());
                    replayMemoryCeiling = $"{Math.Round(bytes)}{byteUnitParts.Last()}";

                    values.Add("ingester.config.wal.replay_memory_ceiling", replayMemoryCeiling);

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemLogServices, "true"))
                    {
                        foreach (var component in new string[] { "ingester", "distributor", "querier", "queryFrontend", "tableManager", "compactor", "ruler", "indexGateway" })
                        {
                            values.Add($"{component}.tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                            values.Add($"{component}.tolerations[{i}].effect", taint.Effect);
                            values.Add($"{component}.tolerations[{i}].operator", "Exists");
                        }

                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "loki",
                        @namespace:   KubeNamespace.NeonMonitor,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-loki-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "loki");

                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "loki-compactor", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "loki-distributor", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "loki-index-gateway", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "loki-ingester", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "loki-querier", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "loki-query-frontend", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "loki-ruler", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs Tempo to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallTempoAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));
            
            var cluster             = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s                 = GetK8sClient(controller);
            var clusterAdvice       = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var tempoAdvice         = clusterAdvice.GetServiceAdvice(ClusterAdvice.Tempo);
            var compactorAdvice     = clusterAdvice.GetServiceAdvice(ClusterAdvice.TempoCompactor);
            var distributorAdvice   = clusterAdvice.GetServiceAdvice(ClusterAdvice.TempoDistributor);
            var ingesterAdvice      = clusterAdvice.GetServiceAdvice(ClusterAdvice.TempoIngester);
            var querierAdvice       = clusterAdvice.GetServiceAdvice(ClusterAdvice.TempoQuerier);
            var queryFrontendAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.TempoQueryFrontend);

            await CreateHostPathStorageClass(controller, controlNode, "neon-internal-tempo");

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-tempo",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "tempo");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelTraceServices, "true" }
                    };

                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);

                    values.Add($"compactor.replicas", compactorAdvice.Replicas);
                    values.Add($"compactor.resources.requests.memory", KubeHelper.ToSiString(compactorAdvice.PodMemoryRequest));
                    values.Add($"compactor.resources.limits.memory", KubeHelper.ToSiString(compactorAdvice.PodMemoryLimit));
                    values.Add($"compactor.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"distributor.replicas", distributorAdvice.Replicas);
                    values.Add($"distributor.resources.requests.memory", KubeHelper.ToSiString(distributorAdvice.PodMemoryRequest));
                    values.Add($"distributor.resources.limits.memory", KubeHelper.ToSiString(distributorAdvice.PodMemoryLimit));
                    values.Add($"distributor.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"ingester.replicas", ingesterAdvice.Replicas);
                    values.Add($"ingester.resources.requests.memory", KubeHelper.ToSiString(ingesterAdvice.PodMemoryRequest));
                    values.Add($"ingester.resources.limits.memory", KubeHelper.ToSiString(ingesterAdvice.PodMemoryLimit));
                    values.Add($"ingester.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"querier.replicas", querierAdvice.Replicas);
                    values.Add($"querier.resources.requests.memory", KubeHelper.ToSiString(querierAdvice.PodMemoryRequest));
                    values.Add($"querier.resources.limits.memory", KubeHelper.ToSiString(querierAdvice.PodMemoryLimit));
                    values.Add($"querier.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"queryFrontend.replicas", queryFrontendAdvice.Replicas);
                    values.Add($"queryFrontend.resources.requests.memory", KubeHelper.ToSiString(queryFrontendAdvice.PodMemoryRequest));
                    values.Add($"queryFrontend.resources.limits.memory", KubeHelper.ToSiString(queryFrontendAdvice.PodMemoryLimit));
                    values.Add($"queryFrontend.priorityClassName", PriorityClass.NeonMonitor.Name);

                    values.Add($"serviceMonitor.enabled", tempoAdvice.MetricsEnabled);
                    values.Add($"serviceMonitor.interval", tempoAdvice.MetricsInterval);
                    values.Add($"serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
                    values.Add($"tracing.enabled", cluster.SetupState.ClusterDefinition.Features.Tempo);

                    if (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count() > 1)
                    {
                        values.Add($"storage.trace.backend", "s3");
                    }

                    values.Add($"minio.enabled", true);
                    values.Add($"minio.bucket.quota", clusterAdvice.TracesQuota);

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelTraceServices, "true"))
                    {
                        foreach (var component in new string[] { "ingester", "distributor", "compactor", "querier", "queryFrontend" })
                        {
                            values.Add($"{component}.tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                            values.Add($"{component}.tolerations[{i}].effect", taint.Effect);
                            values.Add($"{component}.tolerations[{i}].operator", "Exists");
                        }

                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "tempo",
                        @namespace:   KubeNamespace.NeonMonitor,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-tempo-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "tempo");

                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "tempo-compactor", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "tempo-distributor", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "tempo-ingester", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "tempo-querier", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "tempo-query-frontend", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs Kube State Metrics to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallKubeStateMetricsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.KubeStateMetrics);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-kube-state-metrics",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "deploy", message: "kube-state-metrics");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelSystemMetricServices, "true" }
                    };

                    values.Add($"prometheus.monitor.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"prometheus.monitor.interval", serviceAdvice.MetricsInterval);
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemMetricServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "kube-state-metrics",
                        @namespace:   KubeNamespace.NeonMonitor,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-kube-state-metrics-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "kube-state-metrics");

                    await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonMonitor, "kube-state-metrics", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs Reloader to the Neon system namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallReloaderAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Reloader);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/reloader",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "reloader");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelRole, NodeRole.ControlPlane }
                    };

                    values.Add($"reloader.serviceMonitor.interval", serviceAdvice.MetricsInterval);
                    values.Add($"reloader.serviceMonitor.enabled", serviceAdvice.MetricsEnabled);
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelRole, NodeRole.ControlPlane))
                    {
                        values.Add($"reloader.deployment.tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"reloader.deployment.tolerations[{i}].effect", taint.Effect);
                        values.Add($"reloader.deployment.tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "reloader",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: $"reloader.deployment.priorityClassName={PriorityClass.NeonOperator.Name}",
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/reloader-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "reloader");

                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "reloader", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs Grafana to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallGrafanaAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Grafana);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/monitoring-grafana",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "grafana");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelSystemMetricServices, "true" }
                    };

                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
                    values.Add($"cluster.datacenter", cluster.SetupState.ClusterDefinition.Datacenter);
                    values.Add($"cluster.version", cluster.SetupState.ClusterDefinition.ClusterVersion);
                    values.Add($"cluster.hostingEnvironment", cluster.Hosting.Environment);
                    values.Add("neonkube.clusterDomain.grafana", ClusterHost.Grafana);
                    values.Add("neonkube.clusterDomain.sso", ClusterHost.Sso);
                    values.Add($"serviceMonitor.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"serviceMonitor.interval", serviceAdvice.MetricsInterval);
                    values.Add($"tracing.enabled", cluster.SetupState.ClusterDefinition.Features.Tempo);
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
                    values.Add("replicas", serviceAdvice.Replicas);

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/db-credentials-grafana",
                        async () =>
                        {
                            var secret    = await k8s.CoreV1.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespace.NeonSystem);
                            var dexSecret = await k8s.CoreV1.ReadNamespacedSecretAsync(KubeConst.DexSecret, KubeNamespace.NeonSystem);

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

                            await k8s.CoreV1.CreateNamespacedSecretAsync(monitorSecret, KubeNamespace.NeonMonitor);
                        });

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemMetricServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
                    {
                        values.Add($"resources.requests.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest));
                        values.Add($"resources.limits.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit));
                    }

                    controller.ThrowIfCancelled();
                    await controlNode.InstallHelmChartAsync(controller, "grafana",
                        @namespace:   KubeNamespace.NeonMonitor,
                        prioritySpec: PriorityClass.NeonMonitor.Name,
                        values:       values);
                });

            await controlNode.InvokeIdempotentAsync("setup/monitoring-grafana-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "grafana");

                    controller.ThrowIfCancelled();
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "grafana-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);

                    controller.ThrowIfCancelled();
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonMonitor, "grafana-deployment", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });

            // $todo(jefflill): Do we need to change this into a Hubble user?

#if TODO
            if (cluster.SetupState.ClusterDefinition.Features.Kiali)
            {
                controller.ThrowIfCancelled();
                await controlNode.InvokeIdempotentAsync("setup/monitoring-grafana-kiali-user",
                    async () =>
                    {
                        controller.LogProgress(controlNode, verb: "create", message: "kiali-grafana-user");

                        var grafanaSecret   = await k8s.CoreV1.ReadNamespacedSecretAsync("grafana-admin-credentials", KubeNamespace.NeonMonitor);
                        var grafanaUser     = Encoding.UTF8.GetString(grafanaSecret.Data["GF_SECURITY_ADMIN_USER"]);
                        var grafanaPassword = Encoding.UTF8.GetString(grafanaSecret.Data["GF_SECURITY_ADMIN_PASSWORD"]);
                        var kialiSecret     = await k8s.CoreV1.ReadNamespacedSecretAsync("kiali", KubeNamespace.NeonSystem);
                        var kialiPassword   = Encoding.UTF8.GetString(kialiSecret.Data["grafanaPassword"]);

                        var cmd = new string[]
                        {
                            "/bin/bash",
                            "-c",
                            $@"curl -X POST http://{grafanaUser}:{grafanaPassword}@localhost:3000/api/admin/users -H 'Content-Type: application/json' -d '{{""name"":""kiali"",""email"":""kiali@cluster.local"",""login"":""kiali"",""password"":""{kialiPassword}"",""OrgId"":1}}'"
                        };

                        var pod = await k8s.CoreV1.GetNamespacedRunningPodAsync(KubeNamespace.NeonMonitor, labelSelector: "app=grafana");

                        controller.ThrowIfCancelled();

                        (await k8s.NamespacedPodExecWithRetryAsync(
                            retryPolicy:        podExecRetry,
                            name:               pod.Name(),
                            namespaceParameter: pod.Namespace(),
                            container:          "grafana",
                            command:            cmd)).EnsureSuccess();
                    });
            }
#endif

            await controlNode.InvokeIdempotentAsync("setup/monitoring-grafana-config",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "grafana");

                    var grafanaSecret   = await k8s.CoreV1.ReadNamespacedSecretAsync("grafana-admin-credentials", KubeNamespace.NeonMonitor);
                    var grafanaUser     = Encoding.UTF8.GetString(grafanaSecret.Data["GF_SECURITY_ADMIN_USER"]);
                    var grafanaPassword = Encoding.UTF8.GetString(grafanaSecret.Data["GF_SECURITY_ADMIN_PASSWORD"]);
                    var grafanaPod      = await k8s.CoreV1.GetNamespacedRunningPodAsync(KubeNamespace.NeonMonitor, labelSelector: "app=grafana");

                    var cmd = new string[]
                    {
                        "/bin/bash",
                        "-c",
                        $@"curl -X GET -H 'Content-Type: application/json' http://{grafanaUser}:{grafanaPassword}@localhost:3000/api/dashboards/uid/neonkube-default-dashboard"
                    };

                    var dashboardId = string.Empty;

                    await operationRetry.InvokeAsync(
                        async () =>
                        {
                            var grafanaPod      = await k8s.CoreV1.GetNamespacedRunningPodAsync(KubeNamespace.NeonMonitor, labelSelector: "app=grafana");

                            var defaultDashboard = (await k8s.NamespacedPodExecWithRetryAsync(
                                retryPolicy:        podExecRetry,
                                name:               grafanaPod.Name(),
                                namespaceParameter: grafanaPod.Namespace(),
                                container:          "grafana",
                                command:            cmd)).EnsureSuccess();

                            dashboardId = NeonHelper.JsonDeserialize<dynamic>(defaultDashboard.OutputText)["dashboard"]["id"];
                        },
                        cancellationToken: controller.CancellationToken);

                    cmd = new string[]
                    {
                        "/bin/bash",
                        "-c",
                        $@"curl -X PUT -H 'Content-Type: application/json' -d '{{""theme"":"""",""homeDashboardId"":{dashboardId},""timezone"":"""",""weekStart"":""""}}' http://{grafanaUser}:{grafanaPassword}@localhost:3000/api/org/preferences"
                    };

                    (await k8s.NamespacedPodExecWithRetryAsync(
                        retryPolicy:        podExecRetry,
                        name:               grafanaPod.Name(),
                        namespaceParameter: grafanaPod.Namespace(),
                        container:          "grafana",
                        command:            cmd)).EnsureSuccess();
                });
        }

        /// <summary>
        /// Installs a Minio cluster to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMinioAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster        = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s            = GetK8sClient(controller);
            var clusterAdvice  = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice  = clusterAdvice.GetServiceAdvice(ClusterAdvice.Minio);
            var operatorAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.MinioOperator);

            await controlNode.InvokeIdempotentAsync("setup/minio-all",
                async () =>
                {
                    controller.ThrowIfCancelled();
                    await CreateHostPathStorageClass(controller, controlNode, "neon-internal-minio");

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/minio",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "install", message: "minio");

                            var values        = new Dictionary<string, object>();
                            var nodeSelectors = new Dictionary<string, string>
                            {
                                { NodeLabel.LabelSystemMinioServices, "true" }
                            };

                            values.Add("cluster.name", cluster.Name);
                            values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
                            values.Add("neonkube.clusterDomain.minio", ClusterHost.Minio);
                            values.Add("neonkube.clusterDomain.sso", ClusterHost.Sso);
                            values.Add($"metrics.serviceMonitor.enabled", serviceAdvice.MetricsEnabled);
                            values.Add($"metrics.serviceMonitor.interval", serviceAdvice.MetricsInterval);
                            values.Add("image.registry", KubeConst.LocalClusterRegistry);
                            values.Add("mcImage.registry", KubeConst.LocalClusterRegistry);
                            values.Add("helmKubectlJqImage.registry", KubeConst.LocalClusterRegistry);
                            values.Add($"tenants[0].pools[0].servers", serviceAdvice.Replicas);
                            values.Add($"tenants[0].pools[0].volumesPerServer", cluster.SetupState.ClusterDefinition.Storage.Minio.VolumesPerNode);

                            var volumesize = ByteUnits.Humanize(
                                size:            ByteUnits.Parse(cluster.SetupState.ClusterDefinition.Storage.Minio.VolumeSize),
                                powerOfTwo:      true,
                                spaceBeforeUnit: false,
                                removeByteUnit:  true);

                            values.Add($"tenants[0].pools[0].size", volumesize);

                            if (serviceAdvice.Replicas > 1)
                            {
                                values.Add($"mode", "distributed");
                            }

                            values.Add($"tenants[0].pools[0].resources.requests.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest));
                            values.Add($"tenants[0].pools[0].resources.limits.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit));

                            values.Add($"operator.resources.requests.memory", KubeHelper.ToSiString(operatorAdvice.PodMemoryRequest));
                            values.Add($"operator.resources.limits.memory", KubeHelper.ToSiString(operatorAdvice.PodMemoryLimit));

                            var accessKey = NeonHelper.GetCryptoRandomPassword(16);
                            var secretKey = NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength);

                            values.Add($"tenants[0].secrets.accessKey", accessKey);
                            values.Add($"clients.aliases.minio.accessKey", accessKey);
                            values.Add($"tenants[0].secrets.secretKey", secretKey);
                            values.Add($"clients.aliases.minio.secretKey", secretKey);

                            values.Add($"tenants[0].console.secrets.passphrase", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));
                            values.Add($"tenants[0].console.secrets.salt", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));
                            values.Add($"tenants[0].console.secrets.accessKey", NeonHelper.GetCryptoRandomPassword(16));
                            values.Add($"tenants[0].console.secrets.secretKey", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));

                            int i = 0;

                            foreach (var selector in nodeSelectors)
                            {
                                values.Add($"nodeSelectors[{i}].key", selector.Key);
                                values.Add($"nodeSelectors[{i}].value", selector.Value);
                                i++;
                            }

                            i = 0;

                            foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemMinioServices, "true"))
                            {
                                foreach (var component in new string[] { "tenants[0].pools[0]", "console", "operator" })
                                {
                                    values.Add($"{component}.tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                                    values.Add($"{component}.tolerations[{i}].effect", taint.Effect);
                                    values.Add($"{component}.tolerations[{i}].operator", "Exists");
                                }

                                i++;
                            }

                            values.Add("tenants[0].priorityClassName", PriorityClass.NeonStorage.Name);

                            await controlNode.InstallHelmChartAsync(controller, "minio",
                                @namespace:   KubeNamespace.NeonSystem,
                                prioritySpec: PriorityClass.NeonStorage.Name,
                                values:       values);
                        });

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("configure/minio-secrets",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "configure", message: "minio secret");

                            var secret = await k8s.CoreV1.ReadNamespacedSecretAsync("minio", KubeNamespace.NeonSystem);

                            secret.Metadata.NamespaceProperty = "monitoring";

                            var monitoringSecret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name        = secret.Name(),
                                    Annotations = new Dictionary<string, string>()
                                    {
                                        { "reloader.stakater.com/match", "true" }
                                    }
                                },
                                Data = secret.Data,
                            };
                            await k8s.CoreV1.CreateNamespacedSecretAsync(monitoringSecret, KubeNamespace.NeonMonitor);
                        });

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/minio-ready",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "wait for", message: "minio");

                            await NeonHelper.WaitAllAsync(
                                new List<Task>()
                                {
                                    k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonSystem, labelSelector: "app=minio", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                                    k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "minio-console", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                                    k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "minio-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                                },
                                timeoutMessage:    "setup/minio-ready",
                                cancellationToken: controller.CancellationToken);
                        });

                    controller.ThrowIfCancelled();
                    await controlNode.InvokeIdempotentAsync("setup/minio-policy",
                        async () =>
                        {
                            controller.LogProgress(controlNode, verb: "wait for", message: "minio");

                            await operationRetry.InvokeAsync(
                                async () =>
                                {
                                    var minioPod = await k8s.CoreV1.GetNamespacedRunningPodAsync(KubeNamespace.NeonSystem, labelSelector: "app.kubernetes.io/name=minio-operator");

                                    (await k8s.NamespacedPodExecWithRetryAsync(
                                        retryPolicy:        podExecRetry,
                                        name:               minioPod.Name(),
                                        namespaceParameter: minioPod.Namespace(),
                                        container:          "minio-operator",
                                        command:            new string[] {
                                            "/bin/bash",
                                            "-c",
                                            $@"echo '{{""Version"":""2012-10-17"",""Statement"":[{{""Effect"":""Allow"",""Action"":[""admin:*""]}},{{""Effect"":""Allow"",""Action"":[""s3:*""],""Resource"":[""arn:aws:s3:::*""]}}]}}' > /tmp/superadmin.json"
                                        })).EnsureSuccess();

                                    controller.ThrowIfCancelled();
                                    (await cluster.ExecMinioCommandAsync(
                                        retryPolicy:    podExecRetry,
                                        mcCommand:      "admin policy create minio superadmin /tmp/superadmin.json")).EnsureSuccess();
                                },
                                controller.CancellationToken);
                        });
                });
        }

        /// <summary>
        /// Installs an Neon Monitoring to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallMonitoringAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster     = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var controlNode = cluster.DeploymentControlNode;
            var tasks       = new List<Task>();

            controller.LogProgress(controlNode, verb: "configure", message: "cluster monitoring");

            tasks.Add(WaitForPrometheusAsync(controller, controlNode));
            tasks.Add(InstallMemcachedAsync(controller, controlNode));
            tasks.Add(InstallMimirAsync(controller, controlNode));
            tasks.Add(InstallLokiAsync(controller, controlNode));
            tasks.Add(InstallKubeStateMetricsAsync(controller, controlNode));

            if (cluster.SetupState.ClusterDefinition.Features.Tempo)
            {
                tasks.Add(InstallTempoAsync(controller, controlNode));
            }

            tasks.Add(InstallGrafanaAsync(controller, controlNode));

            controller.LogProgress(controlNode, verb: "wait", message: "for cluster monitoring");

            await NeonHelper.WaitAllAsync(tasks,
                timeoutMessage:    "install-monitoring",
                cancellationToken: controller.CancellationToken);
        }

        /// <summary>
        /// Installs a harbor container registry and required components.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallRedisAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Redis);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/redis",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "redis");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelRole, NodeRole.ControlPlane }
                    };

                    values.Add("image.registry", KubeConst.LocalClusterRegistry);
                    values.Add($"replicas", serviceAdvice.Replicas);
                    values.Add($"haproxy.metrics.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"exporter.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"exporter.serviceMonitor.interval", serviceAdvice.MetricsInterval);
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

                    if (serviceAdvice.Replicas < 2)
                    {
                        values.Add($"hardAntiAffinity", false);
                        values.Add($"sentinel.quorum", 1);
                    }

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;
                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemRegistryServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "redis-ha",
                        releaseName:  "neon-redis",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: PriorityClass.NeonData.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/redis-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "redis");

                    await k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonSystem, "neon-redis-server", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs a harbor container registry and required components.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallHarborAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Harbor);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("configure/registry-minio-secret",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "minio secret");

                    var minioSecret = await k8s.CoreV1.ReadNamespacedSecretAsync("minio", KubeNamespace.NeonSystem);
                    var secret      = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = "registry-minio",
                            NamespaceProperty = KubeNamespace.NeonSystem,
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

                    await k8s.CoreV1.CreateNamespacedSecretAsync(secret, KubeNamespace.NeonSystem);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/harbor-db",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "harbor databases");

                    await CreateStorageClasses(controller, controlNode, "neon-internal-registry");

                    // Create the Harbor databases.

                    var dbSecret = await k8s.CoreV1.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespace.NeonSystem);

                    var harborSecret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name              = KubeConst.RegistrySecretKey,
                            NamespaceProperty = KubeNamespace.NeonSystem
                        },
                        Data       = new Dictionary<string, byte[]>(),
                        StringData = new Dictionary<string, string>()
                    };

                    if ((await k8s.CoreV1.ListNamespacedSecretAsync(KubeNamespace.NeonSystem)).Items.Any(s => s.Metadata.Name == KubeConst.RegistrySecretKey))
                    {
                        harborSecret = await k8s.CoreV1.ReadNamespacedSecretAsync(KubeConst.RegistrySecretKey, KubeNamespace.NeonSystem);

                        if (harborSecret.Data == null)
                        {
                            harborSecret.Data = new Dictionary<string, byte[]>();
                        }

                        harborSecret.StringData = new Dictionary<string, string>();
                    }

                    if (!harborSecret.Data.ContainsKey("postgresql-password"))
                    {
                        harborSecret.Data["postgresql-password"] = dbSecret.Data["password"];

                        await k8s.CoreV1.UpsertSecretAsync(harborSecret, KubeNamespace.NeonSystem);
                    }

                    if (!harborSecret.Data.ContainsKey("secret"))
                    {
                        harborSecret.StringData["secret"] = NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength);

                        await k8s.CoreV1.UpsertSecretAsync(harborSecret, KubeNamespace.NeonSystem);
                    }
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/harbor",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "harbor minio");

                    var minioSecret = await k8s.CoreV1.ReadNamespacedSecretAsync("minio", KubeNamespace.NeonSystem);
                    var accessKey   = Encoding.UTF8.GetString(minioSecret.Data["accesskey"]);
                    var secretKey   = Encoding.UTF8.GetString(minioSecret.Data["secretkey"]);
                    var serviceUser = await KubeHelper.GetClusterLdapUserAsync(k8s, "serviceuser");

                    // Install the Harbor Helm chart.

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelSystemRegistryServices, "true" }
                    };

                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
                    values.Add($"metrics.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"metrics.interval", serviceAdvice.MetricsInterval);
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

                    values.Add($"components.chartMuseum.enabled", cluster.SetupState.ClusterDefinition.Features.Harbor.ChartMuseum);
                    values.Add($"components.notary.enabled", cluster.SetupState.ClusterDefinition.Features.Harbor.Notary);
                    values.Add($"components.trivy.enabled", cluster.SetupState.ClusterDefinition.Features.Harbor.Trivy);
                    
                    values.Add("neonkube.clusterDomain.harborNotary", ClusterHost.HarborNotary);
                    values.Add("neonkube.clusterDomain.harborRegistry", ClusterHost.HarborRegistry);

                    values.Add($"storage.s3.accessKey", Encoding.UTF8.GetString(minioSecret.Data["accesskey"]));
                    values.Add($"storage.s3.secretKeyRef", "registry-minio");

                    var baseDN = $@"dc={string.Join($@"\,dc=", cluster.SetupState.ClusterDomain.Split('.'))}";

                    values.Add($"ldap.baseDN", baseDN);
                    values.Add($"ldap.secret", serviceUser.Password);

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemRegistryServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
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

                    await controlNode.InstallHelmChartAsync(controller, "harbor",
                        releaseName:  "registry-harbor",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: PriorityClass.NeonData.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/harbor-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "harbor");

                    var tasks = new List<Task>();

                    if (cluster.SetupState.ClusterDefinition.Features.Harbor.ChartMuseum)
                    {
                        tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-chartmuseum", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));
                    }

                    tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-core", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));
                    tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-jobservice", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));

                    if (cluster.SetupState.ClusterDefinition.Features.Harbor.Notary)
                    {
                        tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-notaryserver", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));
                        tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-notarysigner", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));
                    }

                    tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-portal", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));
                    tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-registry", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));
                    
                    if (cluster.SetupState.ClusterDefinition.Features.Harbor.Trivy)
                    {
                        tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-trivy", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));
                    }

                    if ((serviceAdvice.MetricsEnabled))
                    {
                        tasks.Add(k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "registry-harbor-harbor-exporter", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken));
                    }

                    await NeonHelper.WaitAllAsync(tasks,
                        timeoutMessage:    "setup/harbor-ready",
                        cancellationToken: controller.CancellationToken);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/harbor-login",
                async () =>
                {
                    // $todo(jefflill):
                    //
                    // We need to figure out a way to redact the password from the setup
                    // logs.  This is currently logged in the clear when we upoload the
                    // script.
                    //
                    // One idea is to have a way to add redactable secrets to the the node
                    // proxy and then have the proxy search and replace any secrets with
                    // "REDACTED" before logging the script.
                    //
                    //      https://github.com/nforgeio/neonKUBE/issues/1822

                    var user     = await KubeHelper.GetClusterLdapUserAsync(k8s, "root");
                    var password = user.Password;
                    var command  = $"echo '{password}' | podman login {KubeConst.LocalClusterRegistryHostName} --username {user.Name} --password-stdin";

                    foreach (var node in cluster.Nodes)
                    {
                        operationRetry.Invoke(
                            () =>
                            {
                                controlNode.SudoCommand(CommandBundle.FromScript(command), RunOptions.None)
                                    .EnsureSuccess();
                            },
                            cancellationToken: controller.CancellationToken);
                    }
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/harbor-login-workstation",
                async () =>
                {
                    var user     = await KubeHelper.GetClusterLdapUserAsync(k8s, "root");
                    var password = user.Password;

                    if (!string.IsNullOrEmpty(NeonHelper.DockerCli))
                    {
                        controller.LogProgress(controlNode, verb: "login", message: "workstation to Harbor");

                        NeonHelper.ExecuteCapture(NeonHelper.VerifiedDockerCli,
                            new object[]
                            {
                                "login",
                                $"{ClusterHost.HarborRegistry}.{cluster.SetupState.ClusterDomain}",
                                "--username",
                                "root",
                                "--password-stdin"
                            },
                            input: new StringReader(cluster.SetupState.SsoPassword));
                    }
                });
        }

        /// <summary>
        /// Iploads the cluster manifest as a config..
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task UploadClusterManifestAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var k8s       = GetK8sClient(controller);
            var debugMode = controller.Get<bool>(KubeSetupProperty.DebugMode);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/cluster-manifest",
                async () =>
                {
                    var configmap = new TypedConfigMap<ClusterManifest>(
                        name:       KubeConfigMapName.ClusterManifest, 
                        @namespace: KubeNamespace.NeonSystem, 
                        data:       KubeSetup.ClusterManifest(debugMode));

                    await k8s.CoreV1.CreateNamespacedTypedConfigMapAsync(configmap);
                });
        }

        /// <summary>
        /// Sets the  <see cref="KubeConfigMapName.ClusterLock"/> config map in the <see cref="KubeNamespace.NeonStatus"/> namespace
        /// using the lock setting from the cluster definition.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task SetClusterLockAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            await controlNode.InvokeIdempotentAsync("setup/cluster-lock",
                async () =>
                {
                    var clusterLockMap = new TypedConfigMap<ClusterLock>(
                        name:       KubeConfigMapName.ClusterLock,
                        @namespace: KubeNamespace.NeonStatus,
                        data:       new ClusterLock()
                        {
                            IsLocked = cluster.SetupState.ClusterDefinition.IsLocked,
                        });

                    await k8s.CoreV1.CreateNamespacedTypedConfigMapAsync(clusterLockMap);
                });
        }

        /// <summary>
        /// Installs <b>neon-cluster-operator</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallClusterOperatorAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var k8s           = GetK8sClient(controller);
            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonClusterOperator);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/cluster-operator",
                async () =>
                {
                    // Persist the cluster deployment information.

                    var clusterDeployment       = new ClusterDeployment(cluster.SetupState.ClusterDefinition, cluster.SetupState.ClusterId, cluster.SetupState.ClusterDomain);
                    var clusterDeploymentSecret = new TypedSecret<ClusterDeployment>(KubeSecretName.ClusterDeployment, KubeNamespace.NeonStatus, clusterDeployment);

                    await k8s.CoreV1.UpsertNamespacedTypedSecretAsync(clusterDeploymentSecret);

                    // Deploy: neon-cluster-operator

                    controller.LogProgress(controlNode, verb: "install", message: "neon-cluster-operator");

                    var values        = new Dictionary<string, object>();
                    var nodeSelectors = new Dictionary<string, string>
                    {
                        { NodeLabel.LabelRole, NodeRole.ControlPlane }
                    };

                    values.Add("image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("image.tag", KubeVersion.NeonKubeContainerImageTag);
                    values.Add("image.pullPolicy", "IfNotPresent");
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
                    values.Add("resources.requests.memory", $"{KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest)}");
                    values.Add("resources.limits.memory", $"{KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit)}");
                    values.Add("dotnetGcServer", cluster.SetupState.ClusterDefinition.Nodes.Count() == 1 ? 0 : 1);
                    values.Add("metrics.enabled", serviceAdvice.MetricsEnabled);
                    values.Add("metrics.servicemonitor.interval", serviceAdvice.MetricsInterval);

                    int i = 0;

                    foreach (var selector in nodeSelectors)
                    {
                        values.Add($"nodeSelectors[{i}].key", selector.Key);
                        values.Add($"nodeSelectors[{i}].value", selector.Value);
                        i++;
                    }

                    await controlNode.InstallHelmChartAsync(controller, "neon-cluster-operator",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: PriorityClass.NeonOperator.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/cluster-operator-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "neon-cluster-operator");

                    await k8s.AppsV1.WaitForDaemonsetAsync(KubeNamespace.NeonSystem, "neon-cluster-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.ApiextensionsV1.WaitForCustomResourceDefinitionAsync<V1MinioBucket>(),
                            k8s.ApiextensionsV1.WaitForCustomResourceDefinitionAsync<V1NeonClusterJobConfig>(),
                            k8s.ApiextensionsV1.WaitForCustomResourceDefinitionAsync<V1NeonContainerRegistry>(),
                            k8s.ApiextensionsV1.WaitForCustomResourceDefinitionAsync<V1NeonDashboard>(),
                            k8s.ApiextensionsV1.WaitForCustomResourceDefinitionAsync<V1NeonNodeTask>(),
                            k8s.ApiextensionsV1.WaitForCustomResourceDefinitionAsync<V1NeonSsoClient>()
                        });
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/jobs",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "create", message: "jobs");

                    var jobOptions  = cluster.SetupState.ClusterDefinition.Jobs;
                    var jobResource = new V1NeonClusterJobConfig()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = KubeService.NeonClusterOperator
                        },
                        Spec = new V1NeonClusterJobConfig.NeonClusterJobsSpec()
                        {
                            HarborImagePush                = jobOptions.HarborImagePush,
                            ControlPlaneCertificateRenewal = jobOptions.ControlPlaneCertificateRenewal,
                            NodeCaCertificateUpdate        = jobOptions.NodeCaCertificateRenewal,
                            LinuxSecurityPatches           = jobOptions.LinuxSecurityPatches,
                            TelemetryPing                  = jobOptions.TelemetryPing
                        }
                    };

                    if (cluster.SetupState.ClusterDefinition.IsDesktop)
                    {
                        jobResource.Spec.ClusterCertificateRenewal = jobOptions.ClusterCertificateRenewal;
                    }

                    await k8s.CustomObjects.UpsertClusterCustomObjectAsync<V1NeonClusterJobConfig>(jobResource, jobResource.Name());
                });
        }

        /// <summary>
        /// Creates the standard dashboard resources.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateDashboardsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var k8s           = GetK8sClient(controller);
            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonNodeAgent);
            
            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neon-dashboard-resources",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "neon-dashboards");

                    var displayOrder = 0;

                    if (cluster.SetupState.ClusterDefinition.Features.Grafana)
                    {
                        await CreateNeonDashboardAsync(
                            controller,
                            controlNode,
                            name:         "grafana",
                            url:          $"https://{ClusterHost.Grafana}.{cluster.SetupState.ClusterDomain}",
                            displayName:  "Grafana",
                            enabled:      true,
                            displayOrder: displayOrder++);
                    }

                    if (cluster.SetupState.ClusterDefinition.Features.Harbor.Enabled)
                    {
                        await CreateNeonDashboardAsync(
                            controller,
                            controlNode,
                            name:         "harbor",
                            url:          $"https://{ClusterHost.HarborRegistry}.{cluster.SetupState.ClusterDomain}",
                            displayName:  "Harbor",
                            enabled:      true,

                            displayOrder: displayOrder++);
                    }

                    if (cluster.SetupState.ClusterDefinition.Features.Kiali)
                    {
                        await CreateNeonDashboardAsync(
                            controller,
                            controlNode,
                            name:         "kiali",
                            url:          $"https://{ClusterHost.Kiali}.{cluster.SetupState.ClusterDomain}",
                            displayName:  "Kiali",
                            enabled:      true,
                            displayOrder: displayOrder++);
                    }

                    await CreateNeonDashboardAsync(
                            controller,
                            controlNode,
                            name:         "kubernetes",
                            url:          $"https://{ClusterHost.KubernetesDashboard}.{cluster.SetupState.ClusterDomain}",
                            displayName: "Kubernetes",
                            enabled:      true,
                            displayOrder: displayOrder++);

                    if (cluster.SetupState.ClusterDefinition.Features.Minio)
                    {
                        await CreateNeonDashboardAsync(
                            controller,
                            controlNode,
                            name:         "minio",
                            url:          $"https://{ClusterHost.Minio}.{cluster.SetupState.ClusterDomain}",
                            displayName:  "Minio",
                            enabled:      true,
                            displayOrder: displayOrder++);
                    }
                });
        }

        /// <summary>
        /// Installs <b>neon-node-agent</b>.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallNodeAgentAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var k8s           = GetK8sClient(controller);
            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonNodeAgent);
            
            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neon-node-agent",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "neon-node-agent");

                    var values = new Dictionary<string, object>();

                    values.Add("image.registry", KubeConst.LocalClusterRegistry);
                    values.Add("image.tag", KubeVersion.NeonKubeContainerImageTag);
                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
                    values.Add($"cluster.datacenter", cluster.SetupState.ClusterDefinition.Datacenter);
                    values.Add($"cluster.version", cluster.SetupState.ClusterDefinition.ClusterVersion);
                    values.Add($"cluster.hostingEnvironment", cluster.Hosting.Environment);
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
                    values.Add("metrics.enabled", serviceAdvice.MetricsEnabled);
                    values.Add("metrics.servicemonitor.interval", serviceAdvice.MetricsInterval);
                    values.Add("resources.requests.memory", $"{KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest)}");
                    values.Add("resources.limits.memory", $"{KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit)}");
                    values.Add("dotnetGcServer", cluster.SetupState.ClusterDefinition.Nodes.Count() == 1 ? 0 : 1);
                    values.Add("service.ports[0].name", "https-web");
                    values.Add("service.ports[0].protocol", "TCP");
                    values.Add("service.ports[0].port", 443);
                    values.Add("service.ports[0].targetPort", KubePort.NeonNodeAgent);
                    values.Add("metrics.port", KubePort.NeonNodeAgent);

                    await controlNode.InstallHelmChartAsync(controller, "neon-node-agent",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: PriorityClass.NeonOperator.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neon-node-agent-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "neon-node-agent");
                    await k8s.AppsV1.WaitForDaemonsetAsync(KubeNamespace.NeonSystem, "neon-node-agent", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Adds custom <see cref="V1NeonContainerRegistry"/> resources defined in the cluster definition to
        /// the cluster.  <b>neon-node-agent</b> will pick these up and regenerate the CRI-O configuration.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// This must be called after <see cref="InstallClusterOperatorAsync(ISetupController, NodeSshProxy{NodeDefinition})"/>
        /// because that's where the cluster CRDs get installed.
        /// </note>
        /// </remarks>
        public static async Task InstallContainerRegistryResourcesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/container-registries",
                async () =>
                {
                    var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var k8s     = GetK8sClient(controller);

                    await cluster.AddContainerRegistryResourcesAsync();
                });
        }

        /// <summary>
        /// Creates the required namespaces.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateNamespacesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            controller.ThrowIfCancelled();

            var tasks = new List<Task>();

            tasks.Add(CreateNamespaceAsync(controller, controlNode, KubeNamespace.IstioSystem, false));
            tasks.Add(CreateNamespaceAsync(controller, controlNode, KubeNamespace.NeonMonitor, true));
            tasks.Add(CreateNamespaceAsync(controller, controlNode, KubeNamespace.NeonStorage, false));
            tasks.Add(CreateNamespaceAsync(controller, controlNode, KubeNamespace.NeonStatus, false));
            tasks.Add(CreateNamespaceAsync(controller, controlNode, KubeNamespace.NeonSystem, true));

            await Task.FromResult(tasks);
        }

        /// <summary>
        /// Installs a Citus-postgres database used by neon-system services.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallSystemDbAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster        = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s            = GetK8sClient(controller);
            var clusterAdvice  = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var operatorAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonSystemDbOperator);
            var poolerAdvice   = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonSystemDbPooler);
            var metricsAdvice  = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonSystemDbMetrics);
            var databaseAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonSystemDb);

            var values = new Dictionary<string, object>();

            values.Add($"metrics.enabled", operatorAdvice.MetricsEnabled);
            values.Add($"metrics.interval", operatorAdvice.MetricsInterval);

            if (cluster.SetupState.ClusterDefinition.IsDesktop)
            {
                values.Add($"persistence.size", "1Gi");
            }

            controller.ThrowIfCancelled();
            await CreateHostPathStorageClass(controller, controlNode, "neon-internal-system-db");

            if (operatorAdvice.PodMemoryRequest.HasValue && operatorAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"resources.requests.memory", KubeHelper.ToSiString(operatorAdvice.PodMemoryRequest));
                values.Add($"resources.limits.memory", KubeHelper.ToSiString(operatorAdvice.PodMemoryLimit));
            }

            if (databaseAdvice.PodMemoryRequest.HasValue && databaseAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"neonSystemDb.resources.requests.memory", KubeHelper.ToSiString(databaseAdvice.PodMemoryRequest));
                values.Add($"neonSystemDb.resources.limits.memory", KubeHelper.ToSiString(databaseAdvice.PodMemoryLimit));
            }

            if (poolerAdvice.PodMemoryRequest.HasValue && poolerAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"configConnectionPooler.connection_pooler_default_memory_request", KubeHelper.ToSiString(poolerAdvice.PodMemoryRequest));
                values.Add($"configConnectionPooler.connection_pooler_default_memory_limit", KubeHelper.ToSiString(poolerAdvice.PodMemoryLimit));
            }

            if (metricsAdvice.PodMemoryRequest.HasValue && metricsAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"metrics.resources.requests.memory", KubeHelper.ToSiString(metricsAdvice.PodMemoryRequest));
                values.Add($"metrics.resources.limits.memory", KubeHelper.ToSiString(metricsAdvice.PodMemoryLimit));
            }

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/db-credentials-admin",
                async () =>
                {
                    var username = KubeConst.NeonSystemDbAdminUser;
                    var password = NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength);

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name        = KubeConst.NeonSystemDbAdminSecret,
                            Annotations = new Dictionary<string, string>()
                            {
                                {  "reloader.stakater.com/match", "true" }
                            }
                        },
                        Type       = "Opaque",
                        StringData = new Dictionary<string, string>()
                        {
                            { "username", username },
                            { "password", password }
                        }
                    };

                    await k8s.CoreV1.CreateNamespacedSecretAsync(secret, KubeNamespace.NeonSystem);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/db-credentials-service",
                async () =>
                {
                    var username = KubeConst.NeonSystemDbServiceUser;
                    var password = NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength);

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name        = KubeConst.NeonSystemDbServiceSecret,
                            Annotations = new Dictionary<string, string>()
                            {
                                {  "reloader.stakater.com/match", "true" }
                            }
                        },
                        Type       = "Opaque",
                        StringData = new Dictionary<string, string>()
                        {
                            { "username", username },
                            { "password", password }
                        }
                    };

                    await k8s.CoreV1.CreateNamespacedSecretAsync(secret, KubeNamespace.NeonSystem);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/system-db-volumes",
                async () =>
                {
                    var nodes = cluster.SetupState.ClusterDefinition.SortedControlNodes.ToList();

                    if (nodes.Count > operatorAdvice.Replicas)
                    {
                        operatorAdvice.Replicas = nodes.Count;
                    }

                    var labels = new Dictionary<string, string>()
                    {
                        { "app", KubeService.NeonSystemDb },
                        { "cluster-name", KubeService.NeonSystemDb }
                    };

                    for (int i=0; i < operatorAdvice.Replicas; i++)
                    {
                        var pvc = new V1PersistentVolumeClaim()
                        {
                            Metadata = new V1ObjectMeta()
                            {
                                Name              = $"pgdata-neon-system-db-{i}",
                                NamespaceProperty = KubeNamespace.NeonSystem,
                                Annotations       = new Dictionary<string, string>()
                                {
                                    { "volume.kubernetes.io/selected-node", nodes[i].Name }
                                },
                                Labels = labels
                            },
                            Spec = new V1PersistentVolumeClaimSpec()
                            {
                                AccessModes = new List<string>() { "ReadWriteOnce" },
                                Resources   = new V1ResourceRequirements()
                                {
                                    Requests = new Dictionary<string, ResourceQuantity>()
                                    {
                                        { "storage", new ResourceQuantity("1Gi") }
                                    }
                                },
                                StorageClassName = "neon-internal-system-db",
                                VolumeMode       = "Filesystem"
                            }
                        };

                        await k8s.CoreV1.CreateNamespacedPersistentVolumeClaimAsync(pvc, pvc.Namespace());
                    }
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/system-db",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "configure", message: "neon-system-db");

                    values.Add($"replicas", operatorAdvice.Replicas);
                    values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
                    values.Add("healthCheck.image.tag", KubeVersion.NeonKubeContainerImageTag);
                    values.Add($"neonSystemDb.enableConnectionPooler", true);

                    int i = 0;

                    foreach (var taint in await GetTaintsAsync(controller, NodeLabel.LabelSystemDbServices, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", taint.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    // We're going to set the pod priority class to the same value as 
                    // the postgres operator.

                    values.Add("podPriorityClassName", PriorityClass.NeonData.Name);

                    await controlNode.InstallHelmChartAsync(controller, "postgres-operator",
                        releaseName:     "neon-system-db",
                        @namespace:      KubeNamespace.NeonSystem,
                        prioritySpec:    PriorityClass.NeonData.Name,
                        values:          values,
                        progressMessage: "neon-system-db");
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/system-db-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "neon-system-db");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "neon-system-db-postgres-operator", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "neon-system-db-pooler", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                            k8s.AppsV1.WaitForStatefulSetAsync(KubeNamespace.NeonSystem, "neon-system-db", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken),
                        });
                });
        }

        /// <summary>
        /// Installs SSO related components.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallSsoAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            controller.ThrowIfCancelled();
            await InstallGlauthAsync(controller, controlNode);

            controller.ThrowIfCancelled();
            await InstallDexAsync(controller, controlNode);

            controller.ThrowIfCancelled();
            await InstallNeonSsoProxyAsync(controller, controlNode);

            controller.ThrowIfCancelled();
            await InstallOauth2ProxyAsync(controller, controlNode);
        }

        /// <summary>
        /// Installs Dex.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallDexAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            controller.LogProgress(controlNode, "install", "neon-sso-dex");

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Dex);
            var serviceUser   = await KubeHelper.GetClusterLdapUserAsync(k8s, "serviceuser");
            var values        = new Dictionary<string, object>();

            values.Add("cluster.name", cluster.Name);
            values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
            values.Add("neonkube.clusterDomain.grafana", ClusterHost.Grafana);
            values.Add("neonkube.clusterDomain.kiali", ClusterHost.Kiali);
            values.Add("neonkube.clusterDomain.minio", ClusterHost.Minio);
            values.Add("neonkube.clusterDomain.harborRegistry", ClusterHost.HarborRegistry);
            values.Add("neonkube.clusterDomain.kubernetesDashboard", ClusterHost.KubernetesDashboard);
            values.Add("neonkube.clusterDomain.sso", ClusterHost.Sso);
            values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

            values.Add("secrets.grafana", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));
            values.Add("secrets.harbor", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));
            values.Add("secrets.neonSso", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));
            values.Add("secrets.minio", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));
            values.Add("secrets.ldap", serviceUser.Password);

            values.Add("config.issuer", $"https://{ClusterHost.Sso}.{cluster.SetupState.ClusterDomain}");

            // LDAP

            var baseDN = $@"dc={string.Join($@"\,dc=", cluster.SetupState.ClusterDomain.Split('.'))}";

            values.Add("config.ldap.bindDN", $@"cn=serviceuser\,ou=admin\,{baseDN}");
            values.Add("config.ldap.userSearch.baseDN", $@"cn=users\,{baseDN}");
            values.Add("config.ldap.groupSearch.baseDN", $@"ou=users\,{baseDN}");

            if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"resources.requests.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest));
                values.Add($"resources.limits.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit));
            }

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/dex-install",
                async () =>
                {
                    await controlNode.InstallHelmChartAsync(controller, "dex",
                        @namespace:      KubeNamespace.NeonSystem,
                        prioritySpec:    PriorityClass.NeonApi.Name,
                        values:          values,
                        progressMessage: "dex");
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/dex-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "neon-sso-dex");

                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "neon-sso-dex", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/dex-sso-clients",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "neon-sso-clients");

                    var publicClient = new V1NeonSsoClient().Initialize();

                    publicClient.Metadata.Name = KubeConst.NeonSsoPublicClientId;
                    publicClient.Spec          = new V1SsoClientSpec()
                    {
                        Id           = KubeConst.NeonSsoPublicClientId,
                        Name         = KubeConst.NeonSsoPublicClientId,
                        Public       = true,
                        RedirectUris = new List<string>()
                    };

                    for (int port = KubePort.KubeFirstSso; port <= KubePort.KubeFirstSso; port++)
                    {
                        publicClient.Spec.RedirectUris.Add($"http://localhost:{port}");
                    }

                    await k8s.CustomObjects.UpsertClusterCustomObjectAsync(publicClient, publicClient.Name());
                });
        }

        /// <summary>
        /// Installs Neon SSO Session Proxy.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallNeonSsoProxyAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.NeonSsoSessionProxy);
            var values        = new Dictionary<string, object>();

            values.Add("image.registry", KubeConst.LocalClusterRegistry);
            values.Add("image.tag", KubeVersion.NeonKubeContainerImageTag);
            values.Add("cluster.name", cluster.Name);
            values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
            values.Add("neonkube.clusterDomain.sso", ClusterHost.Sso);
            values.Add("secrets.cipherKey", AesCipher.GenerateKey(256));
            values.Add($"metrics.enabled", serviceAdvice.MetricsEnabled);
            values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);
            values.Add("resources.requests.memory", $"{KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest)}");
            values.Add("resources.limits.memory", $"{KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit)}");
            values.Add("dotnetGcServer", cluster.SetupState.ClusterDefinition.Nodes.Count() == 1 ? 0 : 1);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neon-sso-session-proxy-install",
                async () =>
                {
                    controller.LogProgress(controlNode, "install", "neon-sso-proxy");

                    await controlNode.InstallHelmChartAsync(controller, "neon-sso-session-proxy",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: PriorityClass.NeonNetwork.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neon-sso-proxy-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "neon-sso-session-proxy");
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "neon-sso-session-proxy", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);
                });
        }

        /// <summary>
        /// Installs Glauth.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallGlauthAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Glauth);
            var values        = new Dictionary<string, object>();
            var dbSecret      = await k8s.CoreV1.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespace.NeonSystem);
            var dbPassword    = Encoding.UTF8.GetString(dbSecret.Data["password"]);

            values.Add("cluster.name", cluster.Name);
            values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
            values.Add("serviceMesh.enabled", cluster.SetupState.ClusterDefinition.Features.ServiceMesh);

            values.Add("config.backend.baseDN", $"dc={string.Join($@"\,dc=", cluster.SetupState.ClusterDomain.Split('.'))}");
            values.Add("config.backend.database.user", KubeConst.NeonSystemDbServiceUser);
            values.Add("config.backend.database.password", dbPassword);

            values.Add("users.root.password", cluster.SetupState.SsoPassword);
            values.Add("users.serviceuser.password", NeonHelper.GetCryptoRandomPassword(cluster.SetupState.ClusterDefinition.Security.PasswordLength));

            if (serviceAdvice.PodMemoryRequest.HasValue && serviceAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"resources.requests.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryRequest));
                values.Add($"resources.limits.memory", KubeHelper.ToSiString(serviceAdvice.PodMemoryLimit));
            }

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/glauth-install",
                async () =>
                {
                    controller.LogProgress(controlNode, "install", "neon-sso-glauth");

                    await controlNode.InstallHelmChartAsync(controller, "glauth",
                        @namespace:   KubeNamespace.NeonSystem,
                        prioritySpec: PriorityClass.NeonApp.Name,
                        values:       values);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/glauth-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "glauth");
                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "neon-sso-glauth", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);

                    // Wait for the [glauth postgres.so] plugin to initialize its database
                    // by quering the three tables we'll be modifying later below.  The database
                    // will be ready when these queries succeed.

                    await operationRetry.InvokeAsync(
                        async () =>
                        {
                            // Verify [groups] table.

                            var result = await cluster.ExecSystemDbCommandAsync("glauth", "SELECT * FROM groups;", noSuccessCheck: true);

                            if (result.ExitCode != 0)
                            {
                                throw new TimeoutException("Waiting for glauth [groups] table.");
                            }

                            // Verify [users] table.

                            controller.ThrowIfCancelled();

                            result = await cluster.ExecSystemDbCommandAsync("glauth", "SELECT * FROM users;", noSuccessCheck: true);

                            if (result.ExitCode != 0)
                            {
                                throw new TimeoutException("Waiting for glauth [users] table.");
                            }

                            // Verify [capabilities] table.

                            controller.ThrowIfCancelled();

                            result = await cluster.ExecSystemDbCommandAsync("glauth", "SELECT * FROM capabilities;", noSuccessCheck: true);

                            if (result.ExitCode != 0)
                            {
                                throw new TimeoutException("Waiting for glauth [capabilities] table.");
                            }
                        },
                        cancellationToken: controller.CancellationToken);
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/glauth-users",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "create", message: "glauth users");

                    var users  = await k8s.CoreV1.ReadNamespacedSecretAsync("glauth-users", KubeNamespace.NeonSystem);
                    var groups = await k8s.CoreV1.ReadNamespacedSecretAsync("glauth-groups", KubeNamespace.NeonSystem);

                    foreach (var key in groups.Data.Keys)
                    {
                        var group = NeonHelper.YamlDeserialize<GlauthGroup>(Encoding.UTF8.GetString(groups.Data[key]));

                        controller.ThrowIfCancelled();
                        await cluster.ExecSystemDbCommandAsync("glauth",
                            $@"INSERT INTO groups(name, gidnumber)
                                   VALUES('{group.Name}','{group.GidNumber}') 
                                       ON CONFLICT (name) DO UPDATE
                                           SET gidnumber = '{group.GidNumber}';");
                    }

                    foreach (var user in users.Data.Keys)
                    {
                        var userData     = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(users.Data[user]));
                        var name         = userData.Name;
                        var givenname    = userData.Name;
                        var mail         = $"{userData.Name}@{cluster.SetupState.ClusterDomain}";
                        var uidnumber    = userData.UidNumber;
                        var primarygroup = userData.PrimaryGroup;
                        var passsha256   = CryptoHelper.ComputeSHA256String(userData.Password);

                        controller.ThrowIfCancelled();
                        await cluster.ExecSystemDbCommandAsync("glauth",
                             $@"INSERT INTO users(name, givenname, mail, uidnumber, primarygroup, passsha256)
                                    VALUES('{name}','{givenname}','{mail}','{uidnumber}','{primarygroup}','{passsha256}')
                                        ON CONFLICT (name) DO UPDATE
                                            SET givenname    = '{givenname}',
                                                mail         = '{mail}',
                                                uidnumber    = '{uidnumber}',
                                                primarygroup = '{primarygroup}',
                                                passsha256   = '{passsha256}';");

                        if (userData.Capabilities != null)
                        {
                            foreach (var capability in userData.Capabilities)
                            {
                                controller.ThrowIfCancelled();
                                await cluster.ExecSystemDbCommandAsync("glauth",
                                    $@"INSERT INTO capabilities(userid, action, object)
                                           VALUES('{uidnumber}','{capability.Action}','{capability.Object}');");
                            }
                        }
                    }
                });
        }

        /// <summary>
        /// Installs Oauth2-proxy.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallOauth2ProxyAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));

            var cluster       = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s           = GetK8sClient(controller);
            var clusterAdvice = controller.Get<ClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var serviceAdvice = clusterAdvice.GetServiceAdvice(ClusterAdvice.Oauth2Proxy);

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neon-sso-oauth2-proxy",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "install", message: "neon-sso-oauth2-proxy");

                    var values = new Dictionary<string, object>();

                    values.Add("cluster.name", cluster.Name);
                    values.Add("cluster.domain", cluster.SetupState.ClusterDomain);
                    values.Add("config.cookieSecret", NeonHelper.ToBase64(NeonHelper.GetCryptoRandomPassword(24)));
                    values.Add("neonkube.clusterDomain.sso", ClusterHost.Sso);
                    values.Add("client.id", KubeConst.NeonSsoClientId);
                    values.Add($"metrics.enabled", serviceAdvice.MetricsEnabled);
                    values.Add($"metrics.servicemonitor.interval", serviceAdvice.MetricsInterval);

                    await controlNode.InstallHelmChartAsync(controller, "oauth2-proxy",
                        releaseName:     "neon-sso",
                        @namespace:      KubeNamespace.NeonSystem,
                        prioritySpec:    PriorityClass.NeonApi.Name,
                        values:          values,
                        progressMessage: "neon-sso-oauth2-proxy");
                });

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync("setup/neon-sso-oauth2-proxy-ready",
                async () =>
                {
                    controller.LogProgress(controlNode, verb: "wait for", message: "neon-sso-oauth2-proxy");

                    await k8s.AppsV1.WaitForDeploymentAsync(KubeNamespace.NeonSystem, "neon-sso-oauth2-proxy", timeout: clusterOpTimeout, pollInterval: clusterOpPollInterval, cancellationToken: controller.CancellationToken);

                });
        }

        /// <summary>
        /// Returns the Postgres connection string for the default database for the
        /// cluster's <see cref="KubeService.NeonSystemDb"/> deployment.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The connection string.</returns>
        public static async Task<string> GetSystemDatabaseConnectionStringAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var k8s        = GetK8sClient(controller);
            var secret     = await k8s.CoreV1.ReadNamespacedSecretAsync(KubeConst.NeonSystemDbAdminSecret, KubeNamespace.NeonSystem);
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
        /// Creates a <see cref="V1NeonDashboard"/> idempotently.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <param name="name">The new bucket name.</param>
        /// <param name="url">The dashboard URL</param>
        /// <param name="displayName">The Dashboard display name.</param>
        /// <param name="enabled">Optionally specify whether the dashboard is enabled.</param>
        /// <param name="displayOrder">Optionally specify the display order.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateNeonDashboardAsync(
            ISetupController                controller, 
            NodeSshProxy<NodeDefinition>    controlNode, 
            string                          name,
            string                          url, 
            string                          displayName  = null,
            bool                            enabled      = true,
            int                             displayOrder = int.MaxValue)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(controlNode != null, nameof(controlNode));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(url), nameof(url));

            controlNode.Status = $"create: [{name}] dashboard CRD";

            var cluster     = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s         = GetK8sClient(controller);

            var dashboard = new V1NeonDashboard()
            {
                Metadata = new V1ObjectMeta()
                {
                    Name = name
                },
                Spec = new V1NeonDashboard.NeonDashboardSpec()
                {
                    DisplayName  = displayName, 
                    Enabled      = enabled,
                    DisplayOrder = displayOrder,
                    Url          = url
                }
            };

            controller.ThrowIfCancelled();
            await controlNode.InvokeIdempotentAsync($"setup/neon-dashboard-{name}",
                async () =>
                {
                    await k8s.CustomObjects.CreateClusterCustomObjectAsync<V1NeonDashboard>(dashboard, dashboard.Name());
                });
        }


        /// <summary>
        /// Writes the <see cref="KubeConfigMapName.ClusterInfo"/>
        /// config map to the <see cref="KubeNamespace.NeonStatus"/> namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task UploadClusterInfoAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            await controlNode.InvokeIdempotentAsync("setup/cluster-info",
                async () =>
                {
                    var clusterManifestMap = new TypedConfigMap<ClusterInfo>(
                        name:       KubeConfigMapName.ClusterInfo,
                        @namespace: KubeNamespace.NeonStatus,
                        data:       await cluster.CreateClusterInfoAsync());

                    await k8s.CoreV1.CreateNamespacedTypedConfigMapAsync(clusterManifestMap);
                });
        }

        /// <summary>
        /// Writes the <see cref="KubeConfigMapName.ClusterHealth"/> and <see cref="KubeConfigMapName.ClusterLock"/> 
        /// config maps to the <see cref="KubeNamespace.NeonStatus"/> namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="controlNode">The control-plane node where the operation will be performed.</param>
        /// <param name="ready">
        /// Pass <c>false</c> early in the cluster setup process to indicate that the cluster isn't
        /// ready yet and then <c>true</c> as the last setup step indicating that the cluster is ready.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task SetClusterHealthAsync(ISetupController controller, NodeSshProxy<NodeDefinition> controlNode, bool ready)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var k8s     = GetK8sClient(controller);

            await controlNode.InvokeIdempotentAsync("setup/cluster-health" + (ready ? "-ready" : "-not-provisioning"),
                async () =>
                {
                    var clusterHealthMap = new TypedConfigMap<ClusterHealth>(
                        name:       KubeConfigMapName.ClusterHealth,
                        @namespace: KubeNamespace.NeonStatus,
                        data:       new ClusterHealth()
                        {
                            State   = ready ? ClusterState.Healthy : ClusterState.Provisoning,
                            Summary = ready ? "Cluster is healthy" : "Cluster is healthy"
                        });

                    if (!ready)
                    {
                        await k8s.CoreV1.CreateNamespacedTypedConfigMapAsync(clusterHealthMap);
                    }
                    else
                    {
                        await k8s.CoreV1.ReplaceNamespacedTypedConfigMapAsync(clusterHealthMap);
                    }
                });
        }

        /// <summary>
        /// Waits for the a NEONDESKTOP cluster to stabilize.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StabilizeClusterAsync(ISetupController controller)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Assert(!controller.Get<bool>(KubeSetupProperty.DesktopReadyToGo), $"[{nameof(StabilizeClusterAsync)}()] cannot be used for non NEONDESKTOP clusters.");

            var k8s   = GetK8sClient(controller);
            var retry = new LinearRetryPolicy(
                transientDetector: null,
                retryInterval:     clusterOpPollInterval,
                timeout:           clusterOpTimeout);

            await retry.InvokeAsync(
                async () =>
                {
                    var pods = await k8s.CoreV1.ListAllPodsAsync();

                    if (!pods.Items.All(pod => pod.Status.Phase.Equals("Running", StringComparison.InvariantCultureIgnoreCase)))
                    {
                        throw new TimeoutException("Waiting for all cluster pods to report as running.");
                    }
                },
                cancellationToken: controller.CancellationToken);
        }
    }
}
