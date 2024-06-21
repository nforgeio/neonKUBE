//-----------------------------------------------------------------------------
// FILE:        ClusterAdvisor.cs
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
using Neon.Kube.ClusterDef;
using Neon.Kube.SSH;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Holds cluster configuration advice initialized early during cluster deployment.
    /// This is used to centralize the decisions about things like resource limitations
    /// and node taints/affinity based on the overall resources available to the cluster.
    /// </summary>
    public class ClusterAdvisor
    {
        //---------------------------------------------------------------------
        // Service name constants.

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>AlertManager</b> service.
        /// </summary>
        public const string AlertManager = "alertmanager";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>BlackboxExporter</b> service.
        /// </summary>
        public const string BlackboxExporter = "blackbox-exporter";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Cilium</b> service.
        /// </summary>
        public const string Cilium = "cilium";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>CertManager</b> service manager nodes.
        /// </summary>
        public const string CertManager = "cert-manager";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>CoreDNS</b> service.
        /// </summary>
        public const string CoreDns = "coredns";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Dex</b> service.
        /// </summary>
        public const string Dex = "dex";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>FluentBit</b> service.
        /// </summary>
        public const string FluentBit = "fluentbit";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Glauth</b> service.
        /// </summary>
        public const string Glauth = "glauth";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Grafana</b> service.
        /// </summary>
        public const string Grafana = "grafana";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Grafana Agent</b> service.
        /// </summary>
        public const string GrafanaAgent = "grafana-agent";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Grafana Agent</b> daemonset service.
        /// </summary>
        public const string GrafanaAgentNode = "grafana-agent-node";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Grafana Agent</b> service.
        /// </summary>
        public const string GrafanaAgentOperator = "grafana-agent-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor</b> service.
        /// </summary>
        public const string Harbor = "harbor";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor Chartmuseum</b> service.
        /// </summary>
        public const string HarborChartmuseum = "harbor-chartmuseum";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor Clair</b> service.
        /// </summary>
        public const string HarborClair = "harbor-clair";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor Core</b> service.
        /// </summary>
        public const string HarborCore = "harbor-core";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor Jobservice</b> service.
        /// </summary>
        public const string HarborJobservice = "harbor-jobservice";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor Notary Server</b> service.
        /// </summary>
        public const string HarborNotaryServer = "harbor-notary-server";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor Notary Signer</b> service.
        /// </summary>
        public const string HarborNotarySigner = "harbor-notary-signer";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor Portal</b> service.
        /// </summary>
        public const string HarborPortal = "harbor-portal";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Harbor Registry</b> service.
        /// </summary>
        public const string HarborRegistry = "harbor-registry";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Istio Proxy</b> service.
        /// </summary>
        public const string IstioProxy = "istio-proxy";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Istio Ingress Gateway</b> service.
        /// </summary>
        public const string IstioIngressGateway = "istio-ingressgateway";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Istio Pilot</b> service.
        /// </summary>
        public const string IstioPilot = "istio-pilot";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Kubernetes Dashboard</b> service.
        /// </summary>
        public const string KubernetesDashboard = "kubernetes-dashboard";

        /// <summary>
        /// Identifies the <b>Kube State Metrics</b> service.
        /// </summary>
        public const string KubeStateMetrics = "kube-state-metrics";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Kiali</b> service.
        /// </summary>
        public const string Kiali = "kiali";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki</b> service.
        /// </summary>
        public const string Loki = "loki";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki Compactor</b> service.
        /// </summary>
        public const string LokiCompactor = "loki-compactor";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki Distributor</b> service.
        /// </summary>
        public const string LokiDistributor = "loki-distributor";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki Index Gateway</b> service.
        /// </summary>
        public const string LokiIndexGateway = "loki-index-gateway";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki Ingester</b> service.
        /// </summary>
        public const string LokiIngester = "loki-ingester";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki Querier</b> service.
        /// </summary>
        public const string LokiQuerier = "loki-querier";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki Query Frontend</b> service.
        /// </summary>
        public const string LokiQueryFrontend = "loki-query-frontend";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki Ruler</b> service.
        /// </summary>
        public const string LokiRuler = "loki-ruler";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Loki Table Manager</b> service.
        /// </summary>
        public const string LokiTableManager = "loki-table-manager";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Memcached</b> service.
        /// </summary>
        public const string Memcached = "memcached";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Metrics-Server</b> service.
        /// </summary>
        public const string MetricsServer = "metrics-server";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir</b> service.
        /// </summary>
        public const string Mimir = "mimir";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir Alertmanager</b> service.
        /// </summary>
        public const string MimirAlertmanager = "mimir-alertmanager";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir Compactor</b> service.
        /// </summary>
        public const string MimirCompactor = "mimir-compactor";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir Distributor</b> service.
        /// </summary>
        public const string MimirDistributor = "mimir-distributor";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir Ingester</b> service.
        /// </summary>
        public const string MimirIngester = "mimir-ingester";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir OverridesExporter</b> service.
        /// </summary>
        public const string MimirOverridesExporter = "mimir-overrides-exporter";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir Querier</b> service.
        /// </summary>
        public const string MimirQuerier = "mimir-querier";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir Query Frontend</b> service.
        /// </summary>
        public const string MimirQueryFrontend = "mimir-query-frontend";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir Alertmanager</b> service.
        /// </summary>
        public const string MimirRuler = "mimir-ruler";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Mimir Alertmanager</b> service.
        /// </summary>
        public const string MimirStoreGateway = "mimir-store-gateway";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Minio</b> service.
        /// </summary>
        public const string Minio = "minio";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Minio Operator</b> service.
        /// </summary>
        public const string MinioOperator = "minio-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>neon-acme</b> service.
        /// </summary>
        public const string NeonAcme = "neon-acme";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>neon-cluster-operator</b> service.
        /// </summary>
        public const string NeonClusterOperator = "neon-cluster-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>neon-node-agent</b> service.
        /// </summary>
        public const string NeonNodeAgent = "neon-node-agent";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>neon-sso-session-proxy</b> service.
        /// </summary>
        public const string NeonSsoSessionProxy = "neon-sso-session-proxy";

        /// <summary>
        /// Identifies the NeonKUBE cluster system database.
        /// </summary>
        public const string NeonSystemDb = "neon-system-db";

        /// <summary>
        /// Identifies the NeonKUBE cluster system database metrics sidecar.
        /// </summary>
        public const string NeonSystemDbMetrics = "neon-system-db-metrics";

        /// <summary>
        /// Identifies the NeonKUBE cluster system database operator.
        /// </summary>
        public const string NeonSystemDbOperator = "neon-system-db-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster system database pooler.
        /// </summary>
        public const string NeonSystemDbPooler = "neon-system-db-pooler";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Node Problem Detector</b> service.
        /// </summary>
        public const string NodeProblemDetector = "node-problem-detector";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>oauth2-proxy</b> service.
        /// </summary>
        public const string Oauth2Proxy = "oauth2-proxy";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS cStor</b> service.
        /// </summary>
        public const string OpenEbsCstor = "openebs-cstor";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS Admission Server</b> service.
        /// </summary>
        public const string OpenEbsCstorAdmissionServer = "openebs-sdmission-server";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS cStor CSI Controller</b> service.
        /// </summary>
        public const string OpenEbsCstorCsiController = "openebs-cstor-csi-controller";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS cStor CSI Node</b> service.
        /// </summary>
        public const string OpenEbsCstorCsiNode = "openebs-cstor-csi-node";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS cStor CSPC Operator</b> service.
        /// </summary>
        public const string OpenEbsCstorCspcOperator = "openebs-cstor-cspc-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS cStor CVC Operator</b> service.
        /// </summary>
        public const string OpenEbsCstorCvcOperator = "openebs-cstor-cvc-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS Jiva CSI Controller</b> service.
        /// </summary>
        public const string OpenEbsJivaCsiController = "openebs-jiva-csi-controller";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS Jiva CSI Operator</b> service.
        /// </summary>
        public const string OpenEbsJivaOperator = "openebs-jiva-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS Local PV Provisioner</b> service.
        /// </summary>
        public const string OpenEbsLocalPvProvisioner = "openebs-localpv-provisioner";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS Node Disk Manager</b> service.
        /// </summary>
        public const string OpenEbsNdm = "openebs-ndm";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS Node Disk Manager Operator</b> service.
        /// </summary>
        public const string OpenEbsNdmOperator = "openebs-ndm-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>OpenEBS NFS Provisioner</b> service.
        /// </summary>
        public const string OpenEbsNfsProvisioner = "openebs-nfs-provisioner";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Prometheus</b> service.
        /// </summary>
        public const string Prometheus = "prometheus";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Prometheus Operator</b> service.
        /// </summary>
        public const string PrometheusOperator = "prometheus-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Redis</b> service.
        /// </summary>
        public const string Redis = "redis";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Redis HA</b> service.
        /// </summary>
        public const string RedisHA = "redis-ha";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Reloader</b> service.
        /// </summary>
        public const string Reloader = "reloader";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo</b> service.
        /// </summary>
        public const string Tempo = "tempo";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo Alertmanager</b> service.
        /// </summary>
        public const string TempoAlertmanager = "tempo-alertmanager";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo Compactor</b> service.
        /// </summary>
        public const string TempoCompactor = "tempo-compactor";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo Distributor</b> service.
        /// </summary>
        public const string TempoDistributor = "tempo-distributor";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo Ingester</b> service.
        /// </summary>
        public const string TempoIngester = "tempo-ingester";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo OverridesExporter</b> service.
        /// </summary>
        public const string TempoOverridesExporter = "tempo-overrides-exporter";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo Querier</b> service.
        /// </summary>
        public const string TempoQuerier = "tempo-querier";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo Query Frontend</b> service.
        /// </summary>
        public const string TempoQueryFrontend = "tempo-query-frontend";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo Alertmanager</b> service.
        /// </summary>
        public const string TempoRuler = "tempo-ruler";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Tempo Alertmanager</b> service.
        /// </summary>
        public const string TempoStoreGateway = "tempo-store-gateway";

        /// <summary>
        /// <para>
        /// Creates and initializes the cluster deployment advice for the cluster that will be deployed
        /// using the cluster definition passed.
        /// </para>
        /// <note>
        /// This method may modify the cluster definition in some ways to reflect
        /// the advice computed for the cluster.
        /// </note>
        /// </summary>
        /// <param name="clusterDefinition">Spoecifies the target cluster definition.</param>
        /// <returns>The <see cref="ClusterAdvisor"/>.</returns>
        public static ClusterAdvisor Create(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            return new ClusterAdvisor(clusterDefinition);
        }

        //---------------------------------------------------------------------
        // Instance members

        private ClusterDefinition   clusterDefinition;
        private int                 nodeCount;
        private int                 controlNodeCount;
        private int                 workerNodeCount;
        private int                 storageNodeCount;
        private int                 metricsNodeCount;
        private string              controlNodeSelector;
        private string              workerNodeSelector;
        private string              storageNodeSelector;

        /// <summary>
        /// Default constructor used for deserialization only.
        /// </summary>
        public ClusterAdvisor()
        {
        }

        /// <summary>
        /// Parameterized constructor.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        private ClusterAdvisor(ClusterDefinition clusterDefinition)
        {
            this.clusterDefinition   = clusterDefinition;
            this.nodeCount           = clusterDefinition.Nodes.Count();
            this.controlNodeCount    = clusterDefinition.ControlNodes.Count();
            this.workerNodeCount     = clusterDefinition.Workers.Count();
            this.storageNodeCount    = clusterDefinition.Nodes.Where(node => node.OpenEbsStorage).Count();
            this.metricsNodeCount    = clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count();
            this.controlNodeSelector = ToObjectYaml(NodeLabel.LabelRole, NodeRole.ControlPlane); ;
            this.workerNodeSelector  = ToObjectYaml(NodeLabel.LabelRole, NodeRole.Worker);

            if (workerNodeCount == 0)
            {
                this.storageNodeSelector = "{}";
            }
            else
            {
                this.storageNodeSelector = ToObjectYaml(NodeLabel.LabelRole, NodeRole.Worker);
            }

            InitializeAdvice();
        }

        /// <summary>
        /// Specifies whether cluster metrics are enabled by default.
        /// </summary>
        [JsonProperty(PropertyName = "MetricsEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "metricsEnabled", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool MetricsEnabled { get; set; } = true;

        /// <summary>
        /// Specifies the cluster default Metrics scrape interval.
        /// </summary>
        [JsonProperty(PropertyName = "MetricsInterval", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "metricsInterval", ApplyNamingConventions = false)]
        [DefaultValue("60s")]
        public string MetricsInterval { get; set; } = "60s";

        /// <summary>
        /// Specifies the cluster default metrics quota.
        /// </summary>
        [JsonProperty(PropertyName = "MetricsQuota", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "metricsQuota", ApplyNamingConventions = false)]
        [DefaultValue("10Gi")]
        public string MetricsQuota { get; set; } = "10Gi";

        /// <summary>
        /// Specifies the cluster default logs quota.
        /// </summary>
        [JsonProperty(PropertyName = "LogsQuota", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "logsQuota", ApplyNamingConventions = false)]
        [DefaultValue("10Gi")]
        public string LogsQuota { get; set; } = "10Gi";

        /// <summary>
        /// Specifies the cluster default traces quota.
        /// </summary>
        [JsonProperty(PropertyName = "TracesQuota", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tracesQuota", ApplyNamingConventions = false)]
        [DefaultValue("10Gi")]
        public string TracesQuota { get; set; } = "10Gi";

        /// <summary>
        /// Specifies the default watch cache size for the Kubernetes API Server.
        /// </summary>
        [JsonProperty(PropertyName = "KubeApiServerWatchCacheSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubeApiServerWatchCacheSize", ApplyNamingConventions = false)]
        [DefaultValue(5)]
        public int KubeApiServerWatchCacheSize { get; set; } = 5;

        /// <summary>
        /// Determines whether we should consider the cluster to be small.
        /// </summary>
        /// <returns><c>true</c> for small clusters.</returns>
        [JsonProperty(PropertyName = "IsSmallCluster", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "isSmallCluster", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool IsSmallCluster { get; set; } = false;

        /// <summary>
        /// Called after deserialization to rehydrate any referenced cluster and node definitions
        /// so we don't have to serialize those multiple times because we already serialize the
        /// cluster definition in the setup state.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the parent <see cref="ClusterAdvisor"/>.</param>
        public void Rehydrate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            this.clusterDefinition = clusterDefinition;

            foreach (var serviceAdvice in ServicesAdvice.Values)
            {
                serviceAdvice.Rehydrate(this);
            }

            foreach (var nodeDefinition in clusterDefinition.Nodes)
            {
                NodesAdvice[nodeDefinition.Name].Rehydrate(this, nodeDefinition);
            }
        }

        /// <summary>
        /// Converts a name/value pair into single line YAML object.
        /// </summary>
        /// <param name="key">Specifies the object property name.</param>
        /// <param name="value">Specifies the object property value.</param>
        /// <returns>The single-line YAML.</returns>
        private string ToObjectYaml(string key, string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), nameof(key));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(value), nameof(value));

            return $"{{ {key}: \"{value}\" }}";
        }

        /// <summary>
        /// Determines resource and other recommendations for the cluster globally as well
        /// as for cluster components and nodes based on the cluster definition passed to
        /// the constructor.
        /// </summary>
        private void InitializeAdvice()
        {
            // Initialize global cluster advisor properties.

            MetricsEnabled  = true;
            MetricsInterval = nodeCount > 6 ? "60s" : "5m";
            MetricsQuota    = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            LogsQuota       = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            TracesQuota     = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            IsSmallCluster  = clusterDefinition.IsDesktop || controlNodeCount == 1 || nodeCount <= 10;

            //-----------------------------------------------------------------
            // Initialize the node advice.

            foreach (var nodeDefinition in clusterDefinition.NodeDefinitions.Values)
            {
                AddNodeAdvice(nodeDefinition, new NodeAdvice(this, nodeDefinition));
            }

            //-----------------------------------------------------------------
            // OpenEBS node configuration.

            // Clusters require that at least one node has [OpenEbsStorage=true] set for the
            // Mayastor engine.   We'll do this here when the user hasn't already done so.
            //
            // We're going to favor deploying Mayastor engines on up to three workers when
            // there are any workers, otherwise we're going to deploy this to all control-plane
            // nodes when there are no workers.
            //
            // We're also going to set [NodeAdvice.OpenEbsHugePages2MiB] for the nodes that
            // will be hosting the Mayastor engine.

            if (clusterDefinition.Storage.OpenEbs.Mayastor)
            {
                if (!clusterDefinition.Nodes.Any(node => node.OpenEbsStorage))
                {
                    if (clusterDefinition.Workers.Count() > 0)
                    {
                        foreach (var worker in clusterDefinition.Workers.TakeUpTo(3))
                        {
                            worker.OpenEbsStorage = true;
                        }
                    }
                    else
                    {
                        foreach (var controlNode in clusterDefinition.ControlNodes)
                        {
                            controlNode.OpenEbsStorage = true;
                        }
                    }
                }

                // Mayastor hugepage configuration.

                foreach (var nodeDefinition in clusterDefinition.Nodes.Where(nodeDefinition => nodeDefinition.OpenEbsStorage))
                {
                    var nodeAdvice = GetNodeAdvice(nodeDefinition.Name);

                    nodeAdvice.OpenEbsHugePages = clusterDefinition.Storage.OpenEbs.MayastorHugepages2Gi;
                }
            }

            //-----------------------------------------------------------------
            // Initialize the service advice.

            SetAlertManagerServiceAdvice();
            SetBlackboxExporterServiceAdvice();
            SetCiliumServiceAdvice();
            SetCertManagerServiceAdvice();
            SetCoreDnsServiceAdvice();
            SetDexServiceAdvice();
            SetGlauthServiceAdvice();
            SetGrafanaServiceAdvice();
            SetGrafanaAgentServiceAdvice();
            SetGrafanaAgentNodeServiceAdvice();
            SetGrafanaAgentOperatorServiceAdvice();
            SetHarborServiceAdvice();
            SetHarborChartmuseumServiceAdvice();
            SetHarborClairServiceAdvice();
            SetHarborCoreServiceAdvice();
            SetHarborJobserviceServiceAdvice();
            SetHarborNotaryServerServiceAdvice();
            SetHarborNotarySignerServiceAdvice();
            SetHarborPortalServiceAdvice();
            SetRedisServiceAdvice();
            SetHarborRegistryServiceAdvice();
            SetIstioIngressGatewayServiceAdvice();
            SetIstioProxyServiceAdvice();
            SetIstioPilotServiceAdvice();
            SetKialiServiceAdvice();
            SetKubernetesDashboardServiceAdvice();
            SetKubeStateMetricsServiceAdvice();
            SetLokiServiceAdvice();
            SetLokiCompactorServiceAdvice();
            SetLokiDistributorServiceAdvice();
            SetLokiIndexGatewayServiceAdvice();
            SetLokiIngesterServiceAdvice();
            SetLokiQuerierServiceAdvice();
            SetLokiQueryFrontendServiceAdvice();
            SetLokiRulerServiceAdvice();
            SetLokiTableManagerServiceAdvice();
            SetMemcachedServiceAdvice();
            SetMetricsServerServiceAdvice();
            SetMimirServiceAdvice();
            SetMimirAlertmanagerServiceAdvice();
            SetMimirCompactorServiceAdvice();
            SetMimirDistributorServiceAdvice();
            SetMimirIngesterServiceAdvice();
            SetMimirOverridesExporterServiceAdvice();
            SetMimirQuerierServiceAdvice();
            SetMimirQueryFrontendServiceAdvice();
            SetMimirRulerServiceAdvice();
            SetMimirStoreGatewayServiceAdvice();
            SetMinioServiceAdvice();
            SetMinioOperatorServiceAdvice();
            SetNeonAcmeServiceAdvice();
            SetNeonClusterOperatorServiceAdvice();
            SetNeonNodeAgentServiceAdvice();
            SetNeonSsoSessionProxyServiceAdvice();
            SetNeonSystemDbServiceAdvice();
            SetNeonSystemDbOperatorServiceAdvice();
            SetNeonSystemDbMetricsServiceAdvice();
            SetNeonSystemDbPoolerServiceAdvice();
            SetNodeProblemDetectorServiceAdvice();
            SetOauth2ProxyServiceAdvice();
            SetOpenEbsCstorServiceAdvice();
            SetOpenEbsCstorAdmissionServerServiceAdvice();
            SetOpenEbsCstorCsiControllerServiceAdvice();
            SetOpenEbsCstorCsiNodeServiceAdvice();
            SetOpenEbsCstorCspcOperatorServiceAdvice();
            SetOpenEbsCstorCvcOperatorServiceAdvice();
            SetOpenEbsJivaCsiControllerServiceAdvice();
            SetOpenEbsJivaOperatorServiceAdvice();
            SetOpenEbsLocalPvProvisionerServiceAdvice();
            SetOpenEbsNdmServiceAdvice();
            SetOpenEbsNdmOperatorServiceAdvice();
            SetOpenEbsNfsProvisionerServiceAdvice();
            SetPrometheusServiceAdvice();
            SetPrometheusOperatorServiceAdvice();
            SetReloaderServiceAdvice();
            SetTempoServiceAdvice();
            SetTempoAlertmanagerServiceAdvice();
            SetTempoCompactorServiceAdvice();
            SetTempoDistributorServiceAdvice();
            SetTempoIngesterServiceAdvice();
            SetTempoOverridesExporterServiceAdvice();
            SetTempoQuerierServiceAdvice();
            SetTempoQueryFrontendServiceAdvice();
            SetTempoRulerServiceAdvice();
            SetTempoStoreGatewayServiceAdvice();
        }

        //---------------------------------------------------------------------
        // Service advice

        /// <summary>
        /// Holds the advice for the cluster services.
        /// </summary>
        [JsonProperty(PropertyName = "ServicesAdvice", Required = Required.Always)]
        [YamlMember(Alias = "servicesAdvice", ApplyNamingConventions = false)]
        public Dictionary<string, ServiceAdvice> ServicesAdvice { get; set; } = new Dictionary<string, ServiceAdvice>(StringComparer.CurrentCultureIgnoreCase);

        /// <summary>
        /// Adds the <see cref="ServiceAdvice"/> for the named service.
        /// </summary>
        /// <param name="serviceName">Identifies the service (one of the constants defined by this class).</param>
        /// <param name="serviceAdvice">Specifies the <see cref="ServiceAdvice"/> instance for the service</param>
        private void AddServiceAdvice(string serviceName, ServiceAdvice serviceAdvice)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceName));
            Covenant.Requires<ArgumentNullException>(serviceAdvice != null);

            ServicesAdvice.Add(serviceName, serviceAdvice);
        }

        /// <summary>
        /// Returns the <see cref="ServiceAdvice"/> for the specified service.
        /// </summary>
        /// <param name="serviceName">Identifies the service (one of the service constants defined by this class).</param>
        /// <returns>The <see cref="ServiceAdvice"/> instance for the service.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when there's no advice for the service.</exception>
        public ServiceAdvice GetServiceAdvice(string serviceName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceName));

            return ServicesAdvice[serviceName];
        }

        /// <summary>
        /// Returns the <see cref="NodeAdvice"/> for the named node.
        /// </summary>
        /// <param name="nodeName">Specifies the node name.</param>
        /// <returns>The <see cref="NodeAdvice"/> instance for the service.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when there's no advice for the service.</exception>
        public NodeAdvice GetNodeAdvice(string nodeName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nodeName), nameof(nodeName));

            return NodesAdvice[nodeName];
        }

        /// <summary>
        /// Returns the <see cref="NodeAdvice"/> for the specified node.
        /// </summary>
        /// <param name="node">Identifies the node.</param>
        /// <returns>The <see cref="NodeAdvice"/> instance for the service.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when there's no advice for the service.</exception>
        public NodeAdvice GetNodeAdvice(NodeSshProxy<NodeDefinition> node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));

            return NodesAdvice[node.Name];
        }

        private void SetAlertManagerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, AlertManager);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetBlackboxExporterServiceAdvice()
        {
            var advice = new ServiceAdvice(this, BlackboxExporter);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetCiliumServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Cilium);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetCertManagerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, CertManager);

            advice.Replicas = 1;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("64Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetNeonSystemDbServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSystemDb);

            advice.Replicas = controlNodeCount;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetNeonSystemDbOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSystemDbOperator);

            advice.Replicas = 1;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("50Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetNeonSystemDbMetricsServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSystemDbMetrics);

            advice.Replicas = controlNodeCount;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetNeonSystemDbPoolerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSystemDbPooler);

            advice.Replicas = controlNodeCount;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetCoreDnsServiceAdvice()
        {
            var advice = new ServiceAdvice(this, CoreDns);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("170Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("30Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetGrafanaServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Grafana);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("350Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.Replicas         = Math.Min(3, metricsNodeCount);
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("512Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetGrafanaAgentServiceAdvice()
        {
            var advice = new ServiceAdvice(this, GrafanaAgent);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("2Gi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("1Gi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetGrafanaAgentNodeServiceAdvice()
        {
            var advice = new ServiceAdvice(this, GrafanaAgentNode);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("1Gi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetGrafanaAgentOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, GrafanaAgentOperator);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("100Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Harbor);

            if (IsSmallCluster)
            {
                advice.MetricsInterval = "5m";
            }

            advice.MetricsEnabled = true;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborChartmuseumServiceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborChartmuseum);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborClairServiceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborClair);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborCoreServiceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborCore);
            
            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborJobserviceServiceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborJobservice);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborNotaryServerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborNotaryServer);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborNotarySignerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborNotarySigner);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborPortalServiceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborPortal);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetRedisServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Redis);

            advice.MetricsEnabled = true;
            advice.Replicas       = Math.Min(3, controlNodeCount);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetHarborRegistryServiceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborRegistry);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetIstioIngressGatewayServiceAdvice()
        {
            var advice = new ServiceAdvice(this, IstioIngressGateway);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("160Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetIstioProxyServiceAdvice()
        {
            var advice = new ServiceAdvice(this, IstioProxy);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("2Gi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetIstioPilotServiceAdvice()
        {
            var advice = new ServiceAdvice(this, IstioPilot);

            advice.PodCpuLimit      = 0.5;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("2Gi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetDexServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Dex);

            advice.MetricsEnabled   = true;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetGlauthServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Glauth);

            advice.MetricsEnabled   = true;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetKialiServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Kiali);

            advice.MetricsEnabled = true;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetKubernetesDashboardServiceAdvice()
        {
            var advice = new ServiceAdvice(this, KubernetesDashboard);

            advice.MetricsEnabled   = true;
            advice.Replicas         = Math.Min(3, nodeCount);
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("128Mi");

            if (IsSmallCluster)
            {
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetKubeStateMetricsServiceAdvice()
        {
            var advice = new ServiceAdvice(this, KubeStateMetrics);

            advice.MetricsEnabled = true;
            advice.Replicas       = Math.Min(3, nodeCount);

            if (IsSmallCluster)
            {
                advice.MetricsInterval = "5m";
                advice.Replicas        = 1;
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMemcachedServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Memcached);

            advice.MetricsEnabled   = true;
            advice.MetricsInterval  = "60s";
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMetricsServerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MetricsServer);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Mimir);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirAlertmanagerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirAlertmanager);

            advice.MetricsEnabled   = false;
            advice.Replicas         = 1;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirCompactorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirCompactor);

            advice.MetricsEnabled   = false;
            advice.Replicas         = 1;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirDistributorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirDistributor);

            advice.Replicas = Math.Min(3, metricsNodeCount);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("256Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
                advice.Replicas         = 1;
            }
            else
            {
                advice.Replicas         = 3;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("512Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirIngesterServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirIngester);

            advice.Replicas = Math.Min(3, metricsNodeCount);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("128Mi");
                advice.Replicas         = 1;
            }
            else
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("2Gi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("2Gi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirOverridesExporterServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirOverridesExporter);

            advice.Replicas         = 1;
            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirQuerierServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirQuerier);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("128Mi");
            }

            advice.Replicas = Math.Min(3, metricsNodeCount);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirQueryFrontendServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirQueryFrontend);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirRulerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirRuler);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMimirStoreGatewayServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MimirStoreGateway);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }
        private void SetLokiServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Loki);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }
        private void SetLokiCompactorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, LokiCompactor);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas        = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetLokiDistributorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, LokiDistributor);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetLokiIndexGatewayServiceAdvice()
        {
            var advice = new ServiceAdvice(this, LokiIndexGateway);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetLokiIngesterServiceAdvice()
        {
            var advice = new ServiceAdvice(this, LokiIngester);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("1Gi");
            }

            advice.Replicas = Math.Min(3, metricsNodeCount);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetLokiQuerierServiceAdvice()
        {
            var advice = new ServiceAdvice(this, LokiQuerier);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("128Mi");
            }

            advice.Replicas = Math.Min(3, metricsNodeCount);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetLokiQueryFrontendServiceAdvice()
        {
            var advice = new ServiceAdvice(this, LokiQueryFrontend);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetLokiRulerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, LokiRuler);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetLokiTableManagerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, LokiTableManager);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMinioServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Minio);

            if (clusterDefinition.Nodes.Where(node => node.Labels.SystemMinioServices).Count() >= 3)
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("4Gi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("4Gi");
                advice.MetricsInterval  = "1m";

                if (clusterDefinition.Nodes.Where(node => node.Labels.SystemMinioServices).Count() >= 4)
                {
                    advice.Replicas = clusterDefinition.Nodes.Where(node => node.Labels.SystemMinioServices).Count();
                }
                else
                {
                    advice.Replicas = 1;
                }
            }
            else
            {
                advice.Replicas = 1;
            }

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("768Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("256Mi");
            }

            advice.MetricsEnabled = true;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetMinioOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, MinioOperator);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("40Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetNeonClusterOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NeonClusterOperator);

            advice.MetricsEnabled   = true;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("115Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetNeonAcmeServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NeonAcme);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("50Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetNeonNodeAgentServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NeonNodeAgent);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("350Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }
        
        private void SetNeonSsoSessionProxyServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSsoSessionProxy);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("60Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetNodeProblemDetectorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, NodeProblemDetector);

            advice.MetricsInterval = "1m";

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOauth2ProxyServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Oauth2Proxy);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsCstorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstor);

            advice.MetricsEnabled = true;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsCstorAdmissionServerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorAdmissionServer);

            advice.NodeSelector      = ToObjectYaml(NodeLabel.LabelOpenEbsStorage, "true");
            advice.PriorityClassName = PriorityClass.NeonStorage.Name;
            advice.Replicas          = storageNodeCount;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsCstorCsiControllerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorCsiController);

            // We're going to schedule the CSI controller on the storage
            // nodes for cStor.

            if (clusterDefinition.Storage.OpenEbs.Mayastor)
            {
                advice.NodeSelector = ToObjectYaml(NodeLabel.LabelOpenEbsStorage, "true");
            }

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsCstorCsiNodeServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorCsiNode);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsCstorCspcOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorCspcOperator);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsCstorCvcOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorCvcOperator);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsJivaCsiControllerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsJivaCsiController);

            advice.MetricsEnabled = true;

            if (workerNodeCount == 0)
            {
                advice.Replicas = 1;
            }
            else
            {
                advice.NodeSelector = ToObjectYaml(NodeLabel.LabelRole, NodeRole.Worker);
                advice.Replicas     = Math.Max(1, Math.Min(3, storageNodeCount));
            }

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsJivaOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsJivaOperator);

            advice.MetricsEnabled = true;

            if (workerNodeCount == 0)
            {
                advice.Replicas = 1;
            }
            else
            {
                advice.NodeSelector = ToObjectYaml(NodeLabel.LabelRole, NodeRole.Worker);
                advice.Replicas     = Math.Max(1, Math.Min(3, storageNodeCount));
            }

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsLocalPvProvisionerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsLocalPvProvisioner);

            advice.Replicas          = Math.Min(1, Math.Max(3, workerNodeCount));
            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsNdmServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsNdm);

            advice.PodMemoryLimit    = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest  = (long)ByteUnits.Parse("16Mi");
            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsNdmOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsNdmOperator);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            // We're going to schedule the node operator on the storage
            // nodes for cStor or Mayastor engines with up to three replicas.

            if (clusterDefinition.Storage.OpenEbs.Mayastor)
            {
                advice.NodeSelector = storageNodeSelector;
                advice.Replicas     = Math.Min(3, storageNodeCount);
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetOpenEbsNfsProvisionerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsNfsProvisioner);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            // $note(jefflill):
            //
            // The Helm template for this currently hardcodes: replicas = 1

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetPrometheusServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Prometheus);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetPrometheusOperatorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, PrometheusOperator);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetReloaderServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Reloader);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("128Mi");
            advice.Replicas         = 1;

            if (IsSmallCluster)
            {
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoServiceAdvice()
        {
            var advice = new ServiceAdvice(this, Tempo);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoAlertmanagerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoAlertmanager);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoCompactorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoCompactor);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoDistributorServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoDistributor);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("32Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoIngesterServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoIngester);

            advice.Replicas = Math.Min(3, metricsNodeCount);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("1Gi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoOverridesExporterServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoOverridesExporter);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoQuerierServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoQuerier);

            advice.Replicas = Math.Min(3, metricsNodeCount);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = (long)ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = (long)ByteUnits.Parse("128Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoQueryFrontendServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoQueryFrontend);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoRulerServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoRuler);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void SetTempoStoreGatewayServiceAdvice()
        {
            var advice = new ServiceAdvice(this, TempoStoreGateway);

            advice.PodMemoryLimit   = (long)ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = (long)ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        //---------------------------------------------------------------------
        // Node advice

        /// <summary>
        /// Holds the advice for the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "NodesAdvice", Required = Required.Always)]
        [YamlMember(Alias = "nodesAdvice", ApplyNamingConventions = false)]
        public Dictionary<string, NodeAdvice> NodesAdvice { get; set; }  = new Dictionary<string, NodeAdvice>(StringComparer.CurrentCultureIgnoreCase);

        /// <summary>
        /// Adds the <see cref="NodeAdvice"/> for the specified node.
        /// </summary>
        /// <param name="nodeDefinition">Identifies the target node definition.</param>
        /// <param name="nodeAdvice">Specifies the <see cref="NodeAdvice"/> instance for the node</param>
        private void AddNodeAdvice(NodeDefinition nodeDefinition, NodeAdvice nodeAdvice)
        {
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));
            Covenant.Requires<ArgumentNullException>(nodeAdvice != null, nameof(nodeAdvice));
            Covenant.Assert(object.ReferenceEquals(nodeDefinition, nodeAdvice.NodeDefinition), "Node definition references must be the same.");

            NodesAdvice.Add(nodeAdvice.NodeDefinition.Name, nodeAdvice);
        }
    }
}
