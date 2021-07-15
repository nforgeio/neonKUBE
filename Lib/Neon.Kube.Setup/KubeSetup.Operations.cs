//-----------------------------------------------------------------------------
// FILE:	    KubeSetup.Operations.cs
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

            controller.LogProgress(node, verb: "configure", message: "etc high availability");

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
    server {master.Name}         {master.Address}:{KubeNodePorts.IstioIngressHttp}");
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
    server {master.Name}         {master.Address}:{KubeNodePorts.IstioIngressHttps}");
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
      image: {KubeConst.LocalClusterRegistry}/haproxy:{KubeVersions.HaproxyVersion}
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
        public static async Task LabelNodesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            await master.InvokeIdempotentAsync("setup/label-nodes",
                async () =>
                {
                    controller.LogProgress(master, verb: "label", message: "nodes");

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
        /// <param name="controller">The setup controller.</param>
        /// <param name="maxParallel">
        /// The maximum number of operations on separate nodes to be performed in parallel.
        /// This defaults to <see cref="defaultMaxParallelNodes"/>.
        /// </param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task SetupClusterAsync(ISetupController controller, int maxParallel = defaultMaxParallelNodes)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentException>(maxParallel > 0, nameof(maxParallel));

            var cluster      = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var master       = cluster.FirstMaster;
            var debugMode    = controller.Get<bool>(KubeSetupProperty.DebugMode);

            cluster.ClearStatus();

            ConfigureKubernetes(controller, cluster.FirstMaster);
            ConfigureWorkstation(controller, master);
            ConnectCluster(controller);

            await ConfigureMasterTaintsAsync(controller, master);
            await TaintNodesAsync(controller);
            await LabelNodesAsync(controller, master);
            await CreateNamespacesAsync(controller, master);
            await CreateRootUserAsync(controller, master);
            await InstallCalicoCniAsync(controller, master);
            await InstallIstioAsync(controller, master);
            await InstallCertManagerAsync(controller, master);
            await InstallMetricsServerAsync(controller, master);

            if (cluster.Definition.Nodes.Where(node => node.Labels.Metrics).Count() >= 3)
            {
                await InstallEtcdAsync(controller, master);
            }

            await InstallKialiAsync(controller, master);
            await InstallKubeDashboardAsync(controller, master);
            await InstallOpenEBSAsync(controller, master);
            await InstallPrometheusAsync(controller, master);
            await InstallSystemDbAsync(controller, master);
            await InstallMinioAsync(controller, master);
            await InstallClusterOperatorAsync(controller, master);
            await InstallReloaderAsync(controller, master);
            await InstallContainerRegistryAsync(controller, master);
            await NeonHelper.WaitAllAsync(await SetupMonitoringAsync(controller));
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

                            // Configure the control plane's API server endpoint and initialize
                            // the certificate SAN names to include each master IP address as well
                            // as the HOSTNAME/ADDRESS of the API load balancer (if any).

                            controller.LogProgress(master, verb: "initialize", message: "cluster");

                            var controlPlaneEndpoint = $"kubernetes-masters:6442";
                            var sbCertSANs           = new StringBuilder();

                            if (hostingEnvironment == HostingEnvironment.Wsl2)
                            {
                                // Tweak the API server endpoint for WSL2.

                                controlPlaneEndpoint = $"neon-desktop:{KubeNodePorts.KubeApiServer}";
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

                            if (hostingEnvironment == HostingEnvironment.Wsl2)
                            {
                                sbCertSANs.AppendLine($"  - \"{Dns.GetHostName()}\"");
                                sbCertSANs.AppendLine($"  - \"neon-desktop\"");
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
{kubeletFailSwapOnLine}
");

                            clusterConfig.AppendLine($@"
---
apiVersion: kubeproxy.config.k8s.io/v1alpha1
kind: KubeProxyConfiguration
mode: ipvs");

                            var kubeInitScript =
$@"
systemctl enable kubelet.service
kubeadm init --config cluster.yaml --ignore-preflight-errors=DirAvailable--etc-kubernetes-manifests
";
                            var response = master.SudoCommand(CommandBundle.FromScript(kubeInitScript).AddFile("cluster.yaml", clusterConfig.ToString()));

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

                            controller.LogProgress(verb: "created", message: "cluster");
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
                                                   $"{KubeConst.LocalClusterRegistry}/haproxy:{KubeConst.NeonKubeImageTag}"
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
                                    controller.LogProgress(master, verb: "configure", message: "kubernetes api server");

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
                                            $"{KubeConst.LocalClusterRegistry}/haproxy:{KubeVersions.HaproxyVersion}",
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

            firstMaster.InvokeIdempotent("setup/workstation",
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
                }));
        }

        /// <summary>
        /// Installs the Calico CNI.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        public static async Task InstallCalicoCniAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;

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
                            var pods = await GetK8sClient(controller).ListPodForAllNamespacesAsync();

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
                    
                    await master.InvokeIdempotentAsync("setup/dnsutils",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "setup", message: "dnsutils");

                            var pods = await GetK8sClient(controller).CreateNamespacedPodAsync(
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
                                                Image           = $"{KubeConst.LocalClusterRegistry}/kubernetes-e2e-test-images-dnsutils:{KubeVersions.DnsUtilsVersion}",
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
                                }, KubeNamespaces.NeonSystem);
                        });

                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            var result = master.SudoCommand($"kubectl exec -n {KubeNamespaces.NeonSystem} -t dnsutils -- nslookup kubernetes.default", RunOptions.LogOutput);

                            if (result.Success)
                            {
                                await GetK8sClient(controller).DeleteNamespacedPodAsync("dnsutils", KubeNamespaces.NeonSystem);
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
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        public static async Task ConfigureMasterTaintsAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;

            await master.InvokeIdempotentAsync("setup/kubernetes-master-taints",
                async () =>
                {
                    controller.LogProgress(master, verb: "configure", message: "master taints");

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
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        public static async Task InstallMetricsServerAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = master.Cluster;

            await master.InvokeIdempotentAsync("setup/kubernetes-metrics-server",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "metrics-server");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", t.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "metrics_server", releaseName: "metrics-server", @namespace: KubeNamespaces.KubeSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/kubernetes-metrics-server-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for metrics-server");

                    await WaitForDeploymentAsync(controller, "kube-system", "metrics-server");
                });
        }

        /// <summary>
        /// Installs Istio.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        public static async Task InstallIstioAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var ingressAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioIngressGateway);
            var proxyAdvice   = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioProxy);

            await master.InvokeIdempotentAsync("setup/istio",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "istio");

                    var nodePorts = new StringBuilder();
                    nodePorts.Append(
$@"
          ports:
");

                    foreach (var rule in master.Cluster.Definition.Network.IngressRules)
                    {
                        nodePorts.Append(
$@"                        
          - name: {rule.Name}
            protocol: {rule.Protocol.ToString().ToUpper()}
            port: {rule.ExternalPort}
            targetPort: {rule.TargetPort}
            nodePort: {rule.NodePort}");
                    }

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

kubectl create ns {KubeNamespaces.NeonIngress}

istioctl operator init --istioNamespace={KubeNamespaces.NeonIngress} --operatorNamespace={KubeNamespaces.NeonIngress} --watchedNamespaces={KubeNamespaces.NeonIngress} --hub={KubeConst.LocalClusterRegistry} --tag={KubeVersions.IstioVersion}-distroless

cat <<EOF > istio-cni.yaml
apiVersion: install.istio.io/v1alpha1
kind: IstioOperator
metadata:
  namespace: {KubeNamespaces.NeonIngress}
  name: istiocontrolplane
spec:
  namespace: {KubeNamespaces.NeonIngress}
  hub: {KubeConst.LocalClusterRegistry}
  tag: {KubeVersions.IstioVersion}-distroless
  meshConfig:
    rootNamespace: {KubeNamespaces.NeonIngress}
    enablePrometheusMerge: false
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
{nodePorts}
        resources:
          requests:
            cpu: ""{ToSiString(ingressAdvice.PodCpuRequest)}""
            memory: {ToSiString(ingressAdvice.PodMemoryRequest)}
          limits:
            cpu: ""{ToSiString(ingressAdvice.PodCpuLimit)}""
            memory: {ToSiString(ingressAdvice.PodMemoryLimit)}
        strategy:
          rollingUpdate:
            maxSurge: ""100%""
            maxUnavailable: ""25%""
    cni:
      enabled: true
      namespace: kube-system
  values:
    global:
      istioNamespace: {KubeNamespaces.NeonIngress}
      logging:
        level: ""default:info""
      logAsJson: true
      imagePullPolicy: IfNotPresent
      jwtPolicy: third-party-jwt
      proxy:
        holdApplicationUntilProxyStarts: true
        resources:
          requests:
            cpu: ""{ToSiString(proxyAdvice.PodCpuRequest)}""
            memory: {ToSiString(proxyAdvice.PodMemoryRequest)}
          limits:
            cpu: ""{ToSiString(proxyAdvice.PodCpuLimit)}""
            memory: {ToSiString(proxyAdvice.PodMemoryLimit)}
      defaultNodeSelector: 
        neonkube.io/istio: true
      tracer:
        zipkin:
          address: jaeger-collector.monitoring.svc.cluster.local:9411
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
      coreDNSImage: {KubeConst.LocalClusterRegistry}/coredns-coredns
      coreDNSTag: {KubeVersions.CoreDNSVersion}
      coreDNSPluginImage: {KubeConst.LocalClusterRegistry}/coredns-plugin:{KubeVersions.CoreDNSPluginVersion}
    cni:
      excludeNamespaces:
       - {KubeNamespaces.NeonIngress}
       - kube-system
       - kube-node-lease
       - kube-public
      logLevel: info
EOF

kubectl apply -f istio-cni.yaml
";
                    master.SudoCommand(CommandBundle.FromScript(istioScript0), RunOptions.FaultOnError);
                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Installs Cert Manager.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        public static async Task InstallCertManagerAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice);
            var ingressAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioIngressGateway);
            var proxyAdvice = clusterAdvice.GetServiceAdvice(KubeClusterAdvice.IstioProxy);

            await master.InvokeIdempotentAsync("setup/cert-manager",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "cert-manager");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelIngress, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", t.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "cert_manager", releaseName: "cert-manager", @namespace: KubeNamespaces.NeonIngress, values: values);

                });


            await master.InvokeIdempotentAsync("setup/cert-manager-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for grafana agent");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonIngress, "cert-manager"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonIngress, "cert-manager-cainjector"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonIngress, "cert-manager-webhook"),
                        });
                });

            await master.InvokeIdempotentAsync("setup/neon-acme",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "neon-acme");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("clusterDomain", cluster.Definition.Domain);

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelIngress, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", t.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "neon_acme", releaseName: "neon-acme", @namespace: KubeNamespaces.NeonIngress, values: values);

                });
        }

        /// <summary>
        /// Configures the root Kubernetes user.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        public static async Task CreateRootUserAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/root-user",
                async () =>
                {
                    controller.LogProgress(master, verb: "create", message: "kubernetes root user");

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
                    master.KubectlApply(userYaml, RunOptions.FaultOnError);

                    await Task.CompletedTask;
                });
        }

        /// <summary>
        /// Configures the root Kubernetes user.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        public static async Task InstallKubeDashboardAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster      = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var clusterLogin = controller.Get<ClusterLogin>(KubeSetupProperty.ClusterLogin);
            var advice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.KubernetesDashboard);

            master.InvokeIdempotent("setup/kube-dashboard",
                () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "kubernetes dashboard");

                    if (clusterLogin.DashboardCertificate != null)
                    {
                        controller.LogProgress(master, verb: "generate", message: "kubernetes dashboard certificate");

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

                    controller.LogProgress(master, verb: "deploy", message: "kubernetes dashboard");

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
kind: ServiceAccount
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: {KubeNamespaces.NeonSystem}

---

kind: Service
apiVersion: v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: {KubeNamespaces.NeonSystem}
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
  namespace: {KubeNamespaces.NeonSystem}
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
  namespace: {KubeNamespaces.NeonSystem}
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
  namespace: {KubeNamespaces.NeonSystem}
type: Opaque

---

kind: ConfigMap
apiVersion: v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard-settings
  namespace: {KubeNamespaces.NeonSystem}

---

kind: Role
apiVersion: rbac.authorization.k8s.io/v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: {KubeNamespaces.NeonSystem}
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
  namespace: {KubeNamespaces.NeonSystem}
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: Role
  name: kubernetes-dashboard
subjects:
  - kind: ServiceAccount
    name: kubernetes-dashboard
    namespace: {KubeNamespaces.NeonSystem}

---

apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRoleBinding
metadata:
  name: kubernetes-dashboard
  namespace: {KubeNamespaces.NeonSystem}
roleRef:
  apiGroup: rbac.authorization.k8s.io
  kind: ClusterRole
  name: kubernetes-dashboard
subjects:
  - kind: ServiceAccount
    name: kubernetes-dashboard
    namespace: {KubeNamespaces.NeonSystem}

---

kind: Deployment
apiVersion: apps/v1
metadata:
  labels:
    k8s-app: kubernetes-dashboard
  name: kubernetes-dashboard
  namespace: {KubeNamespaces.NeonSystem}
spec:
  replicas: {advice.ReplicaCount}
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
          image: {KubeConst.LocalClusterRegistry}/kubernetesui-dashboard:v{KubeVersions.KubernetesDashboardVersion}
          imagePullPolicy: IfNotPresent
          ports:
            - containerPort: 8443
              protocol: TCP
          args:
            - --auto-generate-certificates=false
            - --tls-cert-file=cert.pem
            - --tls-key-file=key.pem
            - --namespace={KubeNamespaces.NeonSystem}
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
  namespace: {KubeNamespaces.NeonSystem}
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
  namespace: {KubeNamespaces.NeonSystem}
spec:
  replicas: {advice.ReplicaCount}
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
          image: {KubeConst.LocalClusterRegistry}/kubernetesui-metrics-scraper:{KubeVersions.KubernetesDashboardMetricsVersion}
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
                            StripComments     = false,
                            ProcessStatements = false
                        }
                    )
                    {
                        preprocessReader.SetYamlMode();

                        dashboardYaml = preprocessReader.ReadToEnd();
                    }

                    master.KubectlApply(dashboardYaml, RunOptions.FaultOnError);
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Adds the node taints.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
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
        private static async Task InstallKialiAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/kiali",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "kaili");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    values.Add("cr.spec.deployment.image_name", $"{KubeConst.LocalClusterRegistry}/kiali-kiali");

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelIstio, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", t.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "kiali", releaseName: "kiali-operator", @namespace: KubeNamespaces.NeonIngress, values: values);
                });

            await master.InvokeIdempotentAsync("setup/kiali-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for kaili");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonIngress, "kiali-operator"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonIngress, "kiali")
                        });
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Some initial kubernetes configuration.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task KubeSetupAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/initial-kubernetes", async
                () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "kubernetes");

                    await master.InstallHelmChartAsync(controller, "cluster_setup");
                });
        }

        /// <summary>
        /// Installs OpenEBS.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallOpenEBSAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var apiServerAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.OpenEbsApiServer);
            var provisionerAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.OpenEbsProvisioner);
            var localPvAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.OpenEbsLocalPvProvisioner);
            var snapshotOperatorAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.OpenEbsSnapshotOperator);
            var ndmOperatorAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.OpenEbsNdmOperator);
            var webhookAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.OpenEbsWebhook);

            await master.InvokeIdempotentAsync("setup/openebs-all",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "openebs");

                    await master.InvokeIdempotentAsync("setup/openebs",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "deploy", message: "openebs");

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

                            await master.InstallHelmChartAsync(controller, "openebs", releaseName: "openebs", values: values, @namespace: KubeNamespaces.NeonStorage);
                        });

                    if (cluster.HostingManager.HostingEnvironment != HostingEnvironment.Wsl2)
                    {
                        await master.InvokeIdempotentAsync("setup/openebs-cstor",
                            async () =>
                            {
                                controller.LogProgress(master, verb: "deploy", message: "openebs cstor");

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

                                await master.InstallHelmChartAsync(controller, "openebs_cstor_operator", releaseName: "openebs-cstor", values: values, @namespace: KubeNamespaces.NeonStorage);
                            });
                    }

                    await master.InvokeIdempotentAsync("setup/openebs-ready",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "wait", message: "for openebs");

                            await NeonHelper.WaitAllAsync(
                                new List<Task>()
                                {
                                    WaitForDaemonsetAsync(controller, KubeNamespaces.NeonStorage, "openebs-ndm"),
                                    WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-admission-server"),
                                    WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-apiserver"),
                                    WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-localpv-provisioner"),
                                    WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-ndm-operator"),
                                    WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-provisioner"),
                                    WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-snapshot-operator")
                                });
                        });

                    if (cluster.HostingManager.HostingEnvironment != HostingEnvironment.Wsl2)
                    {
                        controller.LogProgress(master, verb: "deploy", message: "openebs pool");

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

                            var blockDevices = ((JObject)await GetK8sClient(controller).ListNamespacedCustomObjectAsync("openebs.io", "v1alpha1", KubeNamespaces.NeonStorage, "blockdevices")).ToObject<V1CStorBlockDeviceList>();

                            foreach (var n in cluster.Definition.Nodes)
                            {
                                if (blockDevices.Items.Any(device => device.Spec.NodeAttributes.GetValueOrDefault("nodeName") == n.Name))
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

                                    foreach (var device in blockDevices.Items.Where(device => device.Spec.NodeAttributes.GetValueOrDefault("nodeName") == n.Name))
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

                            GetK8sClient(controller).CreateNamespacedCustomObject(cStorPoolCluster, "cstor.openebs.io", "v1", KubeNamespaces.NeonStorage, "cstorpoolclusters");
                        });

                        await master.InvokeIdempotentAsync("setup/openebs-cstor-ready",
                            async () =>
                            {
                                controller.LogProgress(master, verb: "wait", message: "for openebs cstor");

                                await NeonHelper.WaitAllAsync(
                                    new List<Task>()
                                    {
                                        WaitForDaemonsetAsync(controller, KubeNamespaces.NeonStorage, "openebs-cstor-csi-node"),
                                        WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-cstor-admission-server"),
                                        WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-cstor-cvc-operator"),
                                        WaitForDeploymentAsync(controller, KubeNamespaces.NeonStorage, "openebs-cstor-cspc-operator")
                                    });
                            });

                        var replicas = 3;

                        if (cluster.Definition.Nodes.Where(node => node.OpenEBS).Count() < 3)
                        {
                            replicas = 1;
                        }

                        await CreateCstorStorageClass(controller, master, "openebs-cstor", replicaCount: replicas);
                        await CreateCstorStorageClass(controller, master, "openebs-cstor-unreplicated", replicaCount: 1);
                    }
                    else
                    {
                        await CreateHostPathStorageClass(controller, master, "openebs-cstor");
                        await CreateHostPathStorageClass(controller, master, "openebs-cstor-unreplicated");
                    }
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync($"setup/namespace-{name}",
                async () =>
                {
                    await GetK8sClient(controller).CreateNamespaceAsync(new V1Namespace()
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
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <param name="name">The new <see cref="V1StorageClass"/> name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task CreateHostPathStorageClass(
            ISetupController                controller,
            NodeSshProxy<NodeDefinition>    master,
            string                          name)
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

                    await GetK8sClient(controller).CreateStorageClassAsync(storageClass);
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
            await master.InvokeIdempotentAsync($"setup/storage-class-cstor-{name}",
                async () =>
                {
                    if (master.Cluster.Definition.Nodes.Where(node => node.OpenEBS).Count() < replicaCount)
                    {
                        replicaCount = master.Cluster.Definition.Nodes.Where(node => node.OpenEBS).Count();
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

                    await GetK8sClient(controller).CreateStorageClassAsync(storageClass);
                });
        }

        /// <summary>
        /// Installs an Etcd cluster to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallEtcdAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var advice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.EtcdCluster);

            await master.InvokeIdempotentAsync("setup/monitoring-etc",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "etc");

                    await CreateCstorStorageClass(controller, master, "neon-internal-etcd");

                    var values = new Dictionary<string, object>();

                    values.Add($"replicas", advice.ReplicaCount);

                    values.Add($"volumeClaimTemplate.resources.requests.storage", "1Gi");

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", t.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "etcd_cluster", releaseName: "neon-etcd", @namespace: KubeNamespaces.NeonSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/setup/monitoring-etc-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for etc (monitoring)");

                    await WaitForStatefulSetAsync(controller, KubeNamespaces.NeonMonitor, "neon-system-etcd");
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            await master.InvokeIdempotentAsync("setup/monitoring-prometheus",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "prometheus");

                    var values = new Dictionary<string, object>();

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", t.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");

                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "grafana_agent", releaseName: "grafana-agent", @namespace: KubeNamespaces.NeonMonitor, values: values);
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            await master.InvokeIdempotentAsync("setup/monitoring-grafana-agent-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for grafana agent");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonMonitor, "grafana-agent-operator"),
                            WaitForDaemonsetAsync(controller, KubeNamespaces.NeonMonitor, "grafana-agent-node"),
                            WaitForStatefulSetAsync(controller, KubeNamespaces.NeonMonitor, "grafana-agent"),
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/monitoring-cortex-all",
                async () =>
                {
                    var cluster        = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
                    var advice  = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.Cortex);

                    var values         = new Dictionary<string, object>();

                    values.Add($"replicas", advice.ReplicaCount);

                    if (cluster.Definition.Nodes.Where(node => node.Labels.Metrics).Count() >= 3)
                    {
                        values.Add($"cortexConfig.ingester.lifecycler.ring.kvstore.store", "etcd");
                        values.Add($"cortexConfig.ingester.lifecycler.ring.kvstore.replication_factor", 3);
                    }

                    await master.InvokeIdempotentAsync("setup/monitoring-cortex",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "setup", message: "cortex");

                            if (cluster.Definition.Nodes.Any(node => node.Vm.GetMemory(cluster.Definition) < 4294965097L))
                            {
                                values.Add($"cortexConfig.ingester.retain_period", $"120s");
                                values.Add($"cortexConfig.ingester.metadata_retain_period", $"5m");
                                values.Add($"cortexConfig.querier.batch_iterators", true);
                                values.Add($"cortexConfig.querier.max_samples", 10000000);
                                values.Add($"cortexConfig.table_manager.retention_period", "12h");
                            }

                            int i = 0;
                            foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                            {
                                values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                                values.Add($"tolerations[{i}].effect", t.Effect);
                                values.Add($"tolerations[{i}].operator", "Exists");
                                i++;
                            }

                            values.Add("image.organization", KubeConst.LocalClusterRegistry);

                            await master.InstallHelmChartAsync(controller, "cortex", releaseName: "cortex", @namespace: KubeNamespaces.NeonMonitor, values: values);
                        });

                    await master.InvokeIdempotentAsync("setup/monitoring-cortex-ready",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "wait", message: "for cortex");

                            await WaitForDeploymentAsync(controller, KubeNamespaces.NeonMonitor, "cortex");
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var advice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.Loki);

            await master.InvokeIdempotentAsync("setup/monitoring-loki",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "loki");

                    var values = new Dictionary<string, object>();

                    values.Add("image.organization", KubeConst.LocalClusterRegistry);
                    
                    values.Add($"replicas", advice.ReplicaCount);

                    if (cluster.Definition.Nodes.Where(node => node.Labels.Metrics).Count() >= 3)
                    {
                        values.Add($"config.ingester.lifecycler.ring.kvstore.store", "etcd");
                        values.Add($"config.ingester.lifecycler.ring.kvstore.replication_factor", 3);
                    }

                    if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2
                        || cluster.Definition.Nodes.Count() == 1)
                    {
                        values.Add($"config.limits_config.reject_old_samples_max_age", "15m");
                    }

                    if (advice.PodMemoryRequest != null && advice.PodMemoryLimit != null)
                    {
                        values.Add($"resources.requests.memory", ToSiString(advice.PodMemoryRequest.Value));
                        values.Add($"resources.limits.memory", ToSiString(advice.PodMemoryLimit.Value));
                    }

                    await master.InstallHelmChartAsync(controller, "loki", releaseName: "loki", @namespace: KubeNamespaces.NeonMonitor, values: values);
                });

            await master.InvokeIdempotentAsync("setup/monitoring-loki-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for loki");

                    await WaitForStatefulSetAsync(controller, KubeNamespaces.NeonMonitor, "loki");
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var advice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.Tempo);

            await master.InvokeIdempotentAsync("setup/monitoring-tempo",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "tempo");

                    var values = new Dictionary<string, object>();

                    values.Add("tempo.organization", KubeConst.LocalClusterRegistry);

                    values.Add($"replicas", advice.ReplicaCount);

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

                    await master.InstallHelmChartAsync(controller, "tempo", releaseName: "tempo", @namespace: KubeNamespaces.NeonMonitor, values: values);
                });

            await master.InvokeIdempotentAsync("setup/monitoring-tempo-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for tempo");

                    await WaitForStatefulSetAsync(controller, KubeNamespaces.NeonMonitor, "tempo");
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var advice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.KubeStateMetrics);

            await master.InvokeIdempotentAsync("setup/monitoring-kube-state-metrics",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "kube-state-metrics");

                    var values = new Dictionary<string, object>();


                    await master.InstallHelmChartAsync(controller, "kube_state_metrics", releaseName: "kube-state-metrics", @namespace: KubeNamespaces.NeonMonitor, values: values);
                });

            await master.InvokeIdempotentAsync("setup/monitoring-kube-state-metrics-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for kube-state-metrics");

                    await WaitForStatefulSetAsync(controller, KubeNamespaces.NeonMonitor, "kube-state-metrics");
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var advice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.KubeStateMetrics);

            await master.InvokeIdempotentAsync("setup/reloader",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "reloader");

                    var values = new Dictionary<string, object>();


                    await master.InstallHelmChartAsync(controller, "reloader", releaseName: "reloader", @namespace: KubeNamespaces.NeonSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/reloader-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for reloader");

                    await WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "reloader");
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var advice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.Grafana);

            await master.InvokeIdempotentAsync("setup/monitoring-grafana",
                    async () =>
                    {
                        controller.LogProgress(master, verb: "setup", message: "grafana");

                        var values = new Dictionary<string, object>();

                        //values.Add("image.organization", KubeConst.LocalClusterRegistry);

                        var secret = await GetK8sClient(controller).ReadNamespacedSecretAsync(KubeConst.NeonSystemDbServiceSecret, KubeNamespaces.NeonSystem);

                        values.Add("neon.passwordSecret", KubeConst.NeonSystemDbServiceSecret);

                        int i = 0;
                        foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelMetrics, "true"))
                        {
                            values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                            values.Add($"tolerations[{i}].effect", t.Effect);
                            values.Add($"tolerations[{i}].operator", "Exists");
                            i++;
                        }

                        if (advice.PodMemoryRequest.HasValue && advice.PodMemoryLimit.HasValue)
                        {
                            values.Add($"resources.requests.memory", ToSiString(advice.PodMemoryRequest));
                            values.Add($"resources.limits.memory", ToSiString(advice.PodMemoryLimit));
                        }

                        await master.InstallHelmChartAsync(controller, "grafana", releaseName: "grafana", @namespace: KubeNamespaces.NeonMonitor, values: values);
                    });

            await master.InvokeIdempotentAsync("setup/monitoring-grafana-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for grafana");

                    await WaitForDeploymentAsync(controller, KubeNamespaces.NeonMonitor, "grafana");
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var advice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.Minio);

            await master.InvokeIdempotentAsync("setup/minio-all",
                async () =>
                {
                    await CreateHostPathStorageClass(controller, master, "neon-internal-minio");

                    await master.InvokeIdempotentAsync("setup/minio",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "deploy", message: "minio");

                            var values = new Dictionary<string, object>();

                            values.Add("image.organization", KubeConst.LocalClusterRegistry);
                            values.Add("mcImage.organization", KubeConst.LocalClusterRegistry);
                            values.Add("helmKubectlJqImage.organization", KubeConst.LocalClusterRegistry);
                            values.Add($"tenants[0].pools[0].servers", advice.ReplicaCount);

                            if (advice.ReplicaCount > 1)
                            {
                                values.Add($"mode", "distributed");
                            }

                            if (advice.PodMemoryRequest.HasValue && advice.PodMemoryLimit.HasValue)
                            {
                                values.Add($"resources.requests.memory", ToSiString(advice.PodMemoryRequest));
                                values.Add($"resources.limits.memory", ToSiString(advice.PodMemoryLimit));
                            }

                            values.Add($"tenants[0].secrets.accessKey", NeonHelper.GetCryptoRandomPassword(20));
                            values.Add($"tenants[0].secrets.secretKey", NeonHelper.GetCryptoRandomPassword(20));

                            values.Add($"tenants[0].console.secrets.passphrase", NeonHelper.GetCryptoRandomPassword(10));
                            values.Add($"tenants[0].console.secrets.salt", NeonHelper.GetCryptoRandomPassword(10));
                            values.Add($"tenants[0].console.secrets.accessKey", NeonHelper.GetCryptoRandomPassword(20));
                            values.Add($"tenants[0].console.secrets.secretKey", NeonHelper.GetCryptoRandomPassword(20));


                            await master.InstallHelmChartAsync(controller, "minio", releaseName: "minio", @namespace: KubeNamespaces.NeonSystem, values: values);
                        });

                    await master.InvokeIdempotentAsync("configure/minio-secrets",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "configure", message: "minio secret");

                            var secret = await GetK8sClient(controller).ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);

                            secret.Metadata.NamespaceProperty = "monitoring";

                            var monitoringSecret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name = secret.Name()
                                },
                                Data = secret.Data,
                            };
                            await GetK8sClient(controller).CreateNamespacedSecretAsync(monitoringSecret, KubeNamespaces.NeonMonitor);
                        });
                });
        }

        /// <summary>
        /// Installs an Neon Monitoring to the monitoring namespace.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task<List<Task>> SetupMonitoringAsync(ISetupController controller)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));

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

            return tasks;
        }

        /// <summary>
        /// Installs Jaeger
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <remarks>The tracking <see cref="Task"/>.</remarks>
        public static async Task InstallJaegerAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/monitoring-jaeger",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "jaeger");

                    var values = new Dictionary<string, object>();

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelLogs, "true"))
                    {
                        values.Add($"ingester.tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"ingester.tolerations[{i}].effect", t.Effect);
                        values.Add($"ingester.tolerations[{i}].operator", "Exists");

                        values.Add($"agent.tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"agent.tolerations[{i}].effect", t.Effect);
                        values.Add($"agent.tolerations[{i}].operator", "Exists");

                        values.Add($"collector.tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"collector.tolerations[{i}].effect", t.Effect);
                        values.Add($"collector.tolerations[{i}].operator", "Exists");

                        values.Add($"query.tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"query.tolerations[{i}].effect", t.Effect);
                        values.Add($"query.tolerations[{i}].operator", "Exists");

                        values.Add($"esIndexCleaner.tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"esIndexCleaner.tolerations[{i}].effect", t.Effect);
                        values.Add($"esIndexCleaner.tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "jaeger", releaseName: "jaeger", @namespace: KubeNamespaces.NeonMonitor, values: values);
                });

            await master.InvokeIdempotentAsync("setup/monitoring-jaeger-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for jaeger");

                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            var deployments = await GetK8sClient(controller).ListNamespacedDeploymentAsync(KubeNamespaces.NeonMonitor, labelSelector: "release=jaeger");
                            if (deployments == null || deployments.Items.Count < 2)
                            {
                                return false;
                            }

                            return deployments.Items.All(deployment => deployment.Status.AvailableReplicas == deployment.Spec.Replicas);
                        },
                        timeout:      clusterOpTimeout,
                        pollInterval: clusterOpRetryInterval);
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Installs a harbor container registry and required components.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallContainerRegistryAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var redisAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.HarborRedis);

            await master.InvokeIdempotentAsync("setup/harbor-redis",
                async () =>
                {
                    await SyncContext.ClearAsync;

                    controller.LogProgress(master, verb: "setup", message: "harbor redis");

                    var values   = new Dictionary<string, object>();
                    
                    values.Add("image.organization", KubeConst.LocalClusterRegistry);

                    values.Add($"replicas", redisAdvice.ReplicaCount);
                    
                    if (redisAdvice.ReplicaCount < 2)
                    {
                        values.Add($"hardAntiAffinity", false);
                        values.Add($"sentinel.quorum", 1);
                    }

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelNeonSystemRegistry, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", t.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "redis_ha", releaseName: "registry-redis", @namespace: KubeNamespaces.NeonSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/harbor-redis-ready",
                async () =>
                {
                    await SyncContext.ClearAsync;

                    controller.LogProgress(master, verb: "wait", message: "for harbor redis");

                    await WaitForStatefulSetAsync(controller, KubeNamespaces.NeonSystem, "registry-redis-server");
                });

            await master.InvokeIdempotentAsync("configure/registry-minio-secret",
                        async () =>
                        {
                            controller.LogProgress(master, verb: "configure", message: "minio secret");

                            var minioSecret = await GetK8sClient(controller).ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);

                            var secret = new V1Secret()
                            {
                                Metadata = new V1ObjectMeta()
                                {
                                    Name = "registry-minio",
                                    NamespaceProperty = KubeNamespaces.NeonSystem
                                },
                                Type = "Opaque",
                                Data = new Dictionary<string, byte[]>()
                                {
                                    { "secret", minioSecret.Data["secretkey"] }
                                }
                            };

                            await GetK8sClient(controller).CreateNamespacedSecretAsync(secret, KubeNamespaces.NeonSystem);
                        });

            await master.InvokeIdempotentAsync("setup/harbor",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "harbor");

                    var values = new Dictionary<string, object>();

                    if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2 || cluster.Definition.Nodes.Count() == 1)
                    {
                        await CreateHostPathStorageClass(controller, master, "neon-internal-registry");
                    }
                    else
                    {
                        await CreateCstorStorageClass(controller, master, "neon-internal-registry", replicaCount: 3);
                    }

                    values.Add($"clusterDomain", cluster.Definition.Domain);

                    var secret = await GetK8sClient(controller).ReadNamespacedSecretAsync("minio", KubeNamespaces.NeonSystem);
                    values.Add($"storage.s3.accessKey", Encoding.UTF8.GetString(secret.Data["accesskey"]));
                    values.Add($"storage.s3.secretKeyRef", "registry-minio");

                    int j = 0;
                    foreach (var taint in await GetTaintsAsync(controller, NodeLabels.LabelNeonSystemRegistry, "true"))
                    {
                        values.Add($"tolerations[{j}].key", $"{taint.Key.Split("=")[0]}");
                        values.Add($"tolerations[{j}].effect", taint.Effect);
                        values.Add($"tolerations[{j}].operator", "Exists");
                        j++;
                    }

                    await master.InstallHelmChartAsync(controller, "harbor", releaseName: "registry-harbor", @namespace: KubeNamespaces.NeonSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/harbor-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for harbor");

                    var startUtc = DateTime.UtcNow;

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "registry-harbor-chartmuseum"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "registry-harbor-clair"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "registry-harbor-core"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "registry-harbor-jobservice"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "registry-harbor-notary-server"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "registry-harbor-notary-signer"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "registry-harbor-portal"),
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "registry-harbor-registry")
                        });
                });
        }

        /// <summary>
        /// Installs the Neon Cluster Operator.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task InstallClusterOperatorAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
            await SyncContext.ClearAsync;

            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            await master.InvokeIdempotentAsync("setup/cluster-operator",
                async () =>
                {
                    controller.LogProgress(master, verb: "setup", message: "neon-cluster-operator");

                    var values = new Dictionary<string, object>();
                    
                    values.Add("image.organization", KubeConst.LocalClusterRegistry);

                    await master.InstallHelmChartAsync(controller, "neon_cluster_operator", releaseName: "neon-cluster-operator", @namespace: KubeNamespaces.NeonSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/cluster-operator-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for neon-cluster-operator");

                    await WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "neon-cluster-operator");
                });
        }

        /// <summary>
        /// Creates required namespaces.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="master">The master node where the operation will be performed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task<List<Task>> CreateNamespacesAsync(ISetupController controller, NodeSshProxy<NodeDefinition> master)
        {
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
            Covenant.Requires<ArgumentNullException>(controller != null, nameof(controller));
            Covenant.Requires<ArgumentNullException>(master != null, nameof(master));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);
            var managerAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.CitusPostgresSqlManager);
            var masterAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.CitusPostgresSqlMaster);
            var workerAdvice = controller.Get<KubeClusterAdvice>(KubeSetupProperty.ClusterAdvice).GetServiceAdvice(KubeClusterAdvice.CitusPostgresSqlWorker);

            var values = new Dictionary<string, object>();

            values.Add($"image.organization", KubeConst.LocalClusterRegistry);
            values.Add($"busybox.image.organization", KubeConst.LocalClusterRegistry);
            values.Add($"prometheus.image.organization", KubeConst.LocalClusterRegistry);
            values.Add($"manager.image.organization", KubeConst.LocalClusterRegistry);

            values.Add($"manager.namespace", KubeNamespaces.NeonSystem);

            if (cluster.HostingManager.HostingEnvironment == HostingEnvironment.Wsl2 || cluster.Definition.Nodes.Count() == 1)
            {
                await CreateHostPathStorageClass(controller, master, "neon-internal-citus-master");
                await CreateHostPathStorageClass(controller, master, "neon-internal-citus-worker");
            }
            else
            {
                await CreateCstorStorageClass(controller, master, "neon-internal-citus-master", replicaCount: 3);
                await CreateCstorStorageClass(controller, master, "neon-internal-citus-worker", replicaCount: 1);
            }

            if (managerAdvice.PodMemoryRequest.HasValue && managerAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"manager.resources.requests.memory", ToSiString(managerAdvice.PodMemoryRequest));
                values.Add($"manager.resources.limits.memory", ToSiString(managerAdvice.PodMemoryLimit));
            }

            if (masterAdvice.PodMemoryRequest.HasValue && masterAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"master.resources.requests.memory", ToSiString(masterAdvice.PodMemoryRequest));
                values.Add($"master.resources.limits.memory", ToSiString(masterAdvice.PodMemoryLimit));
            }

            if (workerAdvice.PodMemoryRequest.HasValue && workerAdvice.PodMemoryLimit.HasValue)
            {
                values.Add($"worker.resources.requests.memory", ToSiString(workerAdvice.PodMemoryRequest));
                values.Add($"worker.resources.limits.memory", ToSiString(workerAdvice.PodMemoryLimit));
            }

            await master.InvokeIdempotentAsync("setup/db-credentials-admin",
                async () =>
                {
                    var username = KubeConst.NeonSystemDbAdminUser;
                    var password = NeonHelper.GetCryptoRandomPassword(20);

                    values.Add($"superuser.password", password);
                    values.Add($"superuser.username", KubeConst.NeonSystemDbAdminUser);

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = KubeConst.NeonSystemDbAdminSecret
                        },
                        Type = "Opaque",
                        StringData = new Dictionary<string, string>()
                        {
                            { "username", username },
                            { "password", password }
                        }
                    };

                    await GetK8sClient(controller).CreateNamespacedSecretAsync(secret, KubeNamespaces.NeonSystem);
                });

            await master.InvokeIdempotentAsync("setup/db-credentials-service",
                async () =>
                {
                    var username = KubeConst.NeonSystemDbServiceUser;
                    var password = NeonHelper.GetCryptoRandomPassword(20);

                    var secret = new V1Secret()
                    {
                        Metadata = new V1ObjectMeta()
                        {
                            Name = KubeConst.NeonSystemDbServiceSecret
                        },
                        Type = "Opaque",
                        StringData = new Dictionary<string, string>()
                        {
                            { "username", username },
                            { "password", password }
                        }
                    };

                    await GetK8sClient(controller).CreateNamespacedSecretAsync(secret, KubeNamespaces.NeonSystem);
                });


            await master.InvokeIdempotentAsync("setup/system-db",
                async () =>
                {
                    controller.LogProgress(master, verb: "deploy", message: "system database");

                    values.Add($"manager.replicas", managerAdvice.ReplicaCount);
                    values.Add($"master.replicas", masterAdvice.ReplicaCount);
                    values.Add($"worker.replicas", workerAdvice.ReplicaCount);

                    if (workerAdvice.ReplicaCount < 3)
                    {
                        values.Add($"manager.minimumWorkers", "1");
                    }

                    if (workerAdvice.ReplicaCount < 3)
                    {
                        values.Add($"persistence.replicaCount", "1");
                    }

                    int i = 0;
                    foreach (var t in await GetTaintsAsync(controller, NodeLabels.LabelNeonSystemDb, "true"))
                    {
                        values.Add($"tolerations[{i}].key", $"{t.Key.Split("=")[0]}");
                        values.Add($"tolerations[{i}].effect", t.Effect);
                        values.Add($"tolerations[{i}].operator", "Exists");
                        i++;
                    }

                    await master.InstallHelmChartAsync(controller, "citus_postgresql", releaseName: "db", @namespace: KubeNamespaces.NeonSystem, values: values);
                });

            await master.InvokeIdempotentAsync("setup/system-db-ready",
                async () =>
                {
                    controller.LogProgress(master, verb: "wait", message: "for system database");

                    await NeonHelper.WaitAllAsync(
                        new List<Task>()
                        {
                            WaitForDeploymentAsync(controller, KubeNamespaces.NeonSystem, "db-citus-postgresql-manager"),
                            WaitForStatefulSetAsync(controller, KubeNamespaces.NeonSystem, "db-citus-postgresql-master"),
                            WaitForStatefulSetAsync(controller, KubeNamespaces.NeonSystem, "db-citus-postgresql-worker")
                        });
                });

            await Task.CompletedTask;
        }

        /// <summary>
        /// Waits for a service deployment to complete.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="namespace">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task WaitForDeploymentAsync(
            ISetupController    controller, 
            string              @namespace, 
            string              name          = null, 
            string              labelSelector = null,
            string              fieldSelector = null)
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
                        var deployments = await GetK8sClient(controller).ListNamespacedDeploymentAsync(@namespace, fieldSelector: fieldSelector, labelSelector: labelSelector);
                        if (deployments == null || deployments.Items.Count == 0)
                        {
                            return false;
                        }

                        return deployments.Items.All(deployment => deployment.Status.AvailableReplicas == deployment.Spec.Replicas);
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
        /// Waits for a stateful set deployment to complete.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="namespace">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task WaitForStatefulSetAsync(
            ISetupController    controller,
            string              @namespace,
            string              name          = null,
            string              labelSelector = null,
            string              fieldSelector = null)
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
                        var statefulsets = await GetK8sClient(controller).ListNamespacedStatefulSetAsync(@namespace, fieldSelector: fieldSelector, labelSelector: labelSelector);
                        if (statefulsets == null || statefulsets.Items.Count == 0)
                        {
                            return false;
                        }

                        return statefulsets.Items.All(@set => @set.Status.ReadyReplicas == @set.Spec.Replicas);
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
        /// Waits for a daemon set deployment to complete.
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        /// <param name="namespace">The namespace.</param>
        /// <param name="name">The deployment name.</param>
        /// <param name="labelSelector">The optional label selector.</param>
        /// <param name="fieldSelector">The optional field selector.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task WaitForDaemonsetAsync(
            ISetupController    controller,
            string              @namespace,
            string              name          = null,
            string              labelSelector = null,
            string              fieldSelector = null)
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
                        var daemonsets = await GetK8sClient(controller).ListNamespacedDaemonSetAsync(@namespace, fieldSelector: fieldSelector, labelSelector: labelSelector);
                        if (daemonsets == null || daemonsets.Items.Count == 0)
                        {
                            return false;
                        }

                        return daemonsets.Items.All(@set => @set.Status.NumberAvailable == @set.Status.DesiredNumberScheduled);
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
        /// Returns the string value for byte units.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToSiString(decimal? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return new ResourceQuantity(value.GetValueOrDefault(), 0, ResourceQuantity.SuffixFormat.BinarySI).CanonicalizeString();
        }

        /// <summary>
        /// Returns the string value for byte units.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static string ToSiString(double? value)
        {
            if (!value.HasValue)
            {
                return null;
            }

            return new ResourceQuantity((decimal)value.GetValueOrDefault(), 0, ResourceQuantity.SuffixFormat.BinarySI).CanonicalizeString();
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
