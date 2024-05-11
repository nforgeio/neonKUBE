//-----------------------------------------------------------------------------
// FILE:        ServiceAdvice.cs
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
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Holds cluster configuration advice initialized early during cluster setup.  This
    /// is used to centralize the decisions about things like resource limitations and 
    /// node taints/affinity based on the overall resources available to the cluster.
    /// </summary>
    public class ClusterAdvice
    {
        /// <summary>
        /// Identifies the NeonKUBE cluster <b>AlertManager</b> service.
        /// </summary>
        public const string AlertManager = "alertmanager";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>BlackboxExporter</b> service.
        /// </summary>
        public const string BlackboxExporter = "blackbox-exporter";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>Calico</b> service.
        /// </summary>
        public const string Cilium = "cilium";

        /// <summary>
        /// Identifies the NeonKUBE cluster <b>CertManager</b> service manager nodes.
        /// </summary>
        public const string CertManager = "cert-manager";

        /// <summary>
        /// Identifies the NeonKUBE cluster system database.
        /// </summary>
        public const string NeonSystemDb = "neon-system-db";

        /// <summary>
        /// Identifies the NeonKUBE cluster system database operator.
        /// </summary>
        public const string NeonSystemDbOperator = "neon-system-db-operator";

        /// <summary>
        /// Identifies the NeonKUBE cluster system database pooler.
        /// </summary>
        public const string NeonSystemDbPooler = "neon-system-db-pooler";

        /// <summary>
        /// Identifies the NeonKUBE cluster system database metrics sidecar.
        /// </summary>
        public const string NeonSystemDbMetrics = "neon-system-db-metrics";

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
        /// Identifies the NeonKUBE cluster <b>Redis</b> service.
        /// </summary>
        public const string Redis = "redis";

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
        /// Identifies the NeonKUBE cluster <b>Redis HA</b> service.
        /// </summary>
        public const string RedisHA = "redis-ha";

        /// <summary>
        /// <para>
        /// Computes the cluster deployment advice for the cluster specified by the
        /// cluster definition passed.
        /// </para>
        /// <note>
        /// This method may modify the cluster definition in some ways to reflect
        /// advice computed for the cluster.
        /// </note>
        /// </summary>
        /// <param name="clusterDefinition">Spoecifies the target cluster definition.</param>
        /// <returns></returns>
        public static ClusterAdvice Compute(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            return new ClusterAdvice(clusterDefinition);
        }

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, ServiceAdvice>   services   = new Dictionary<string, ServiceAdvice>(StringComparer.CurrentCultureIgnoreCase);
        private bool                                isReadOnly = false;
        private ClusterDefinition                   clusterDefinition;
        private int                                 nodeCount;
        private int                                 controlNodeCount;
        private int                                 workerNodeCount;
        private int                                 storageNodeCount;
        private int                                 metricsNodeCount;
        private string                              controlNodeSelector;
        private string                              workerNodeSelector;
        private string                              storageNodeSelector;

        /// <summary>
        /// public constructor.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        private ClusterAdvice(ClusterDefinition clusterDefinition)
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

            Compute();
        }

        /// <summary>
        /// Specifies whether cluster metrics are enabled by default.
        /// </summary>
        public bool MetricsEnabled { get; set; } = true;

        /// <summary>
        /// Specifies the cluster default Metrics scrape interval.
        /// </summary>
        public string MetricsInterval { get; set; } = "60s";

        /// <summary>
        /// Specifies the cluster default Metrics quota.
        /// </summary>
        public string MetricsQuota { get; set; } = "10Gi";

        /// <summary>
        /// Specifies the cluster default Logs quota.
        /// </summary>
        public string LogsQuota { get; set; } = "10Gi";

        /// <summary>
        /// Specifies the cluster default Traces quota.
        /// </summary>
        public string TracesQuota { get; set; } = "10Gi";

        /// <summary>
        /// Specifies the default watch cache size for the Kubernetes API Server.
        /// </summary>
        public int KubeApiServerWatchCacheSize { get; set; } = 5;

        /// <summary>
        /// <para>
        /// Cluster advice is designed to be configured once during cluster setup and then be
        /// considered to be <b>read-only</b> thereafter.  This property should be set to 
        /// <c>true</c> after the advice is intialized to prevent it from being modified
        /// again.
        /// </para>
        /// <note>
        /// This is necessary because setup is performed on multiple threads and this class
        /// is not inheritly thread-safe.  This also fits with the idea that the logic behind
        /// this advice is to be centralized.
        /// </note>
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when attempting to make the instance read/write aftyer being set to read-only.</exception>
        public bool IsReadOnly
        {
            get => isReadOnly;

            set
            {
                if (!value && isReadOnly)
                {
                    throw new InvalidOperationException($"[{nameof(ClusterAdvice)}] cannot be made read/write after being set to read-only.");
                }

                isReadOnly = value;

                foreach (var serviceAdvice in services.Values)
                {
                    serviceAdvice.IsReadOnly = value;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="ServiceAdvice"/> for the specified service.
        /// </summary>
        /// <param name="serviceName">Identifies the service (one of the constants defined by this class).</param>
        /// <returns>The <see cref="ServiceAdvice"/> instance for the service.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when there's no advice for the service.</exception>
        public ServiceAdvice GetServiceAdvice(string serviceName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceName));

            return services[serviceName];
        }

        /// <summary>
        /// Adds the <see cref="ServiceAdvice"/> for the specified service.
        /// </summary>
        /// <param name="serviceName">Identifies the service (one of the constants defined by this class).</param>
        /// <param name="advice">The <see cref="ServiceAdvice"/> instance for the service</param>
        public void AddServiceAdvice(string serviceName, ServiceAdvice advice)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceName));
            Covenant.Requires<ArgumentNullException>(advice != null);

            services.Add(serviceName, advice);
        }

        /// <summary>
        /// Determines whether we should consider the cluster to be small.
        /// </summary>
        /// <returns><c>true</c> for small clusters.</returns>
        private bool IsSmallCluster =>
            clusterDefinition.IsDesktop ||
            controlNodeCount == 1 ||
            nodeCount <= 10;

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
        /// Converts a collection of key/value pairs into single line YAML object.
        /// </summary>
        /// <param name="items"></param>
        /// <returns>The single-line YAML.</returns>
        private string ToObjectYaml(params KeyValuePair<string, string>[] items)
        {
            if (items == null || items.Count() > 1)
            {
                return "{}";
            }

            var sb = new StringBuilder();

            foreach (var item in items)
            {
                sb.AppendWithSeparator($"{item.Key}: {item.Value}", ", ");
            }

            return $"{{ {sb} }}";
        }

        /// <summary>
        /// Determines resource and other recommendations for the cluster globally as well
        /// as for cluster components based on the cluster definition passed to the constructor.
        /// </summary>
        private void Compute()
        {
            // Initialize global cluster advice.

            MetricsEnabled  = true;
            MetricsInterval = nodeCount > 6 ? "60s" : "5m";
            MetricsQuota    = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            LogsQuota       = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            TracesQuota     = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";

            // Initialize service advice.

            CalculateAlertManagerAdvice();
            CalculateBlackboxExporterAdvice();
            CalculateCiliumAdvice();
            CalculateCertManagerAdvice();
            CalculateCoreDnsAdvice();
            CalculateDexAdvice();
            CalculateGlauthAdvice();
            CalculateGrafanaAdvice();
            CalculateGrafanaAgentAdvice();
            CalculateGrafanaAgentNodeAdvice();
            CalculateGrafanaAgentOperatorAdvice();
            CalculateHarborAdvice();
            CalculateHarborChartmuseumAdvice();
            CalculateHarborClairAdvice();
            CalculateHarborCoreAdvice();
            CalculateHarborJobserviceAdvice();
            CalculateHarborNotaryServerAdvice();
            CalculateHarborNotarySignerAdvice();
            CalculateHarborPortalAdvice();
            CalculateRedisAdvice();
            CalculateHarborRegistryAdvice();
            CalculateIstioIngressGatewayAdvice();
            CalculateIstioProxyAdvice();
            CalculateIstioPilotAdvice();
            CalculateKialiAdvice();
            CalculateKubernetesDashboardAdvice();
            CalculateKubeStateMetricsAdvice();
            CalculateLokiAdvice();
            CalculateLokiCompactorAdvice();
            CalculateLokiDistributorAdvice();
            CalculateLokiIndexGatewayAdvice();
            CalculateLokiIngesterAdvice();
            CalculateLokiQuerierAdvice();
            CalculateLokiQueryFrontendAdvice();
            CalculateLokiRulerAdvice();
            CalculateLokiTableManagerAdvice();
            CalculateMemcachedAdvice();
            CalculateMetricsServerAdvice();
            CalculateMimirAdvice();
            CalculateMimirAlertmanagerAdvice();
            CalculateMimirCompactorAdvice();
            CalculateMimirDistributorAdvice();
            CalculateMimirIngesterAdvice();
            CalculateMimirOverridesExporterAdvice();
            CalculateMimirQuerierAdvice();
            CalculateMimirQueryFrontendAdvice();
            CalculateMimirRulerAdvice();
            CalculateMimirStoreGatewayAdvice();
            CalculateMinioAdvice();
            CalculateMinioOperatorAdvice();
            CalculateNeonAcmeAdvice();
            CalculateNeonClusterOperatorAdvice();
            CalculateNeonNodeAgentAdvice();
            CalculateNeonSsoSessionProxyAdvice();
            CalculateNeonSystemDbAdvice();
            CalculateNeonSystemDbOperatorAdvice();
            CalculateNeonSystemDbMetricsAdvice();
            CalculateNeonSystemDbPoolerAdvice();
            CalculateNodeProblemDetectorAdvice();
            CalculateOauth2ProxyAdvice();
            CalculateOpenEbsCstorAdvice();
            CalculateOpenEbsCstorAdmissionServerAdvice();
            CalculateOpenEbsCstorCsiControllerAdvice();
            CalculateOpenEbsCstorCsiNodeAdvice();
            CalculateOpenEbsCstorCspcOperatorAdvice();
            CalculateOpenEbsCstorCvcOperatorAdvice();
            CalculateOpenEbsJivaCsiControllerAdvice();
            CalculateOpenEbsJivaOperatorAdvice();
            CalculateOpenEbsLocalPvProvisionerAdvice();
            CalculateOpenEbsNdmAdvice();
            CalculateOpenEbsNdmOperatorAdvice();
            CalculateOpenEbsNfsProvisionerAdvice();
            CalculatePrometheusAdvice();
            CalculatePrometheusOperatorAdvice();
            CalculateReloaderAdvice();
            CalculateTempoAdvice();
            CalculateTempoAlertmanagerAdvice();
            CalculateTempoCompactorAdvice();
            CalculateTempoDistributorAdvice();
            CalculateTempoIngesterAdvice();
            CalculateTempoOverridesExporterAdvice();
            CalculateTempoQuerierAdvice();
            CalculateTempoQueryFrontendAdvice();
            CalculateTempoRulerAdvice();
            CalculateTempoStoreGatewayAdvice();

            // Since advice related classes cannot handle updates performed on multiple threads 
            // and cluster setup is multi-threaded, we're going to mark the advice as read-only
            // to prevent any changes in subsequent steps.

            IsReadOnly = true;
        }

        private void CalculateAlertManagerAdvice()
        {
            var advice = new ServiceAdvice(this, AlertManager);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateBlackboxExporterAdvice()
        {
            var advice = new ServiceAdvice(this, BlackboxExporter);

            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateCiliumAdvice()
        {
            var advice = new ServiceAdvice(this, Cilium);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateCertManagerAdvice()
        {
            var advice = new ServiceAdvice(this, CertManager);

            advice.Replicas = 1;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("64Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateNeonSystemDbAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSystemDb);

            advice.Replicas = controlNodeCount;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateNeonSystemDbOperatorAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSystemDbOperator);

            advice.Replicas = 1;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("50Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateNeonSystemDbMetricsAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSystemDbMetrics);

            advice.Replicas = controlNodeCount;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateNeonSystemDbPoolerAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSystemDbPooler);

            advice.Replicas = controlNodeCount;

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateCoreDnsAdvice()
        {
            var advice = new ServiceAdvice(this, CoreDns);

            advice.PodMemoryLimit   = ByteUnits.Parse("170Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("30Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateGrafanaAdvice()
        {
            var advice = new ServiceAdvice(this, Grafana);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("350Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.Replicas         = Math.Min(3, metricsNodeCount);
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("512Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateGrafanaAgentAdvice()
        {
            var advice = new ServiceAdvice(this, GrafanaAgent);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateGrafanaAgentNodeAdvice()
        {
            var advice = new ServiceAdvice(this, GrafanaAgentNode);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateGrafanaAgentOperatorAdvice()
        {
            var advice = new ServiceAdvice(this, GrafanaAgentOperator);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("100Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborAdvice()
        {
            var advice = new ServiceAdvice(this, Harbor);

            if (IsSmallCluster)
            {
                advice.MetricsInterval = "5m";
            }

            advice.MetricsEnabled = true;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborChartmuseumAdvice()
        {
            var advice = new ServiceAdvice(this, HarborChartmuseum);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborClairAdvice()
        {
            var advice = new ServiceAdvice(this, HarborClair);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborCoreAdvice()
        {
            var advice = new ServiceAdvice(this, HarborCore);
            
            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborJobserviceAdvice()
        {
            var advice = new ServiceAdvice(this, HarborJobservice);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborNotaryServerAdvice()
        {
            var advice = new ServiceAdvice(this, HarborNotaryServer);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborNotarySignerAdvice()
        {
            var advice = new ServiceAdvice(this, HarborNotarySigner);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborPortalAdvice()
        {
            var advice = new ServiceAdvice(this, HarborPortal);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateRedisAdvice()
        {
            var advice = new ServiceAdvice(this, Redis);

            advice.MetricsEnabled = true;
            advice.Replicas       = Math.Min(3, controlNodeCount);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateHarborRegistryAdvice()
        {
            var advice = new ServiceAdvice(this, HarborRegistry);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateIstioIngressGatewayAdvice()
        {
            var advice = new ServiceAdvice(this, IstioIngressGateway);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("160Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateIstioProxyAdvice()
        {
            var advice = new ServiceAdvice(this, IstioProxy);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateIstioPilotAdvice()
        {
            var advice = new ServiceAdvice(this, IstioPilot);

            advice.PodCpuLimit      = 0.5;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateDexAdvice()
        {
            var advice = new ServiceAdvice(this, Dex);

            advice.MetricsEnabled   = true;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateGlauthAdvice()
        {
            var advice = new ServiceAdvice(this, Glauth);

            advice.MetricsEnabled   = true;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateKialiAdvice()
        {
            var advice = new ServiceAdvice(this, Kiali);

            advice.MetricsEnabled = true;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateKubernetesDashboardAdvice()
        {
            var advice = new ServiceAdvice(this, KubernetesDashboard);

            advice.MetricsEnabled   = true;
            advice.Replicas         = Math.Min(3, nodeCount);
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("128Mi");

            if (IsSmallCluster)
            {
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateKubeStateMetricsAdvice()
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

        private void CalculateMemcachedAdvice()
        {
            var advice = new ServiceAdvice(this, Memcached);

            advice.MetricsEnabled   = true;
            advice.MetricsInterval  = "60s";
            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMetricsServerAdvice()
        {
            var advice = new ServiceAdvice(this, MetricsServer);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirAdvice()
        {
            var advice = new ServiceAdvice(this, Mimir);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirAlertmanagerAdvice()
        {
            var advice = new ServiceAdvice(this, MimirAlertmanager);

            advice.MetricsEnabled   = false;
            advice.Replicas         = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirCompactorAdvice()
        {
            var advice = new ServiceAdvice(this, MimirCompactor);

            advice.MetricsEnabled   = false;
            advice.Replicas         = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirDistributorAdvice()
        {
            var advice = new ServiceAdvice(this, MimirDistributor);

            advice.Replicas = Math.Min(3, metricsNodeCount);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.Replicas         = 1;
            }
            else
            {
                advice.Replicas         = 3;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("512Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirIngesterAdvice()
        {
            var advice = new ServiceAdvice(this, MimirIngester);

            advice.Replicas = Math.Min(3, metricsNodeCount);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
                advice.Replicas         = 1;
            }
            else
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("2Gi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirOverridesExporterAdvice()
        {
            var advice = new ServiceAdvice(this, MimirOverridesExporter);

            advice.Replicas         = 1;
            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirQuerierAdvice()
        {
            var advice = new ServiceAdvice(this, MimirQuerier);

            advice.MetricsEnabled = false;

            if (IsSmallCluster)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            advice.Replicas = Math.Min(3, metricsNodeCount);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirQueryFrontendAdvice()
        {
            var advice = new ServiceAdvice(this, MimirQueryFrontend);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirRulerAdvice()
        {
            var advice = new ServiceAdvice(this, MimirRuler);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMimirStoreGatewayAdvice()
        {
            var advice = new ServiceAdvice(this, MimirStoreGateway);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }
        private void CalculateLokiAdvice()
        {
            var advice = new ServiceAdvice(this, Loki);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }
        private void CalculateLokiCompactorAdvice()
        {
            var advice = new ServiceAdvice(this, LokiCompactor);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas        = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateLokiDistributorAdvice()
        {
            var advice = new ServiceAdvice(this, LokiDistributor);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateLokiIndexGatewayAdvice()
        {
            var advice = new ServiceAdvice(this, LokiIndexGateway);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateLokiIngesterAdvice()
        {
            var advice = new ServiceAdvice(this, LokiIngester);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
            }

            advice.Replicas = Math.Min(3, metricsNodeCount);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateLokiQuerierAdvice()
        {
            var advice = new ServiceAdvice(this, LokiQuerier);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            advice.Replicas = Math.Min(3, metricsNodeCount);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateLokiQueryFrontendAdvice()
        {
            var advice = new ServiceAdvice(this, LokiQueryFrontend);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateLokiRulerAdvice()
        {
            var advice = new ServiceAdvice(this, LokiRuler);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateLokiTableManagerAdvice()
        {
            var advice = new ServiceAdvice(this, LokiTableManager);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMinioAdvice()
        {
            var advice = new ServiceAdvice(this, Minio);

            if (clusterDefinition.Nodes.Where(node => node.Labels.SystemMinioServices).Count() >= 3)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("4Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("4Gi");
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
                advice.PodMemoryLimit   = ByteUnits.Parse("768Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("256Mi");
            }

            advice.MetricsEnabled = true;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateMinioOperatorAdvice()
        {
            var advice = new ServiceAdvice(this, MinioOperator);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("40Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateNeonClusterOperatorAdvice()
        {
            var advice = new ServiceAdvice(this, NeonClusterOperator);

            advice.MetricsEnabled   = true;
            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("115Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateNeonAcmeAdvice()
        {
            var advice = new ServiceAdvice(this, NeonAcme);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("50Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateNeonNodeAgentAdvice()
        {
            var advice = new ServiceAdvice(this, NeonNodeAgent);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("350Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }
        
        private void CalculateNeonSsoSessionProxyAdvice()
        {
            var advice = new ServiceAdvice(this, NeonSsoSessionProxy);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("60Mi");

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateNodeProblemDetectorAdvice()
        {
            var advice = new ServiceAdvice(this, NodeProblemDetector);

            advice.MetricsInterval = "1m";

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOauth2ProxyAdvice()
        {
            var advice = new ServiceAdvice(this, Oauth2Proxy);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled = false;
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsCstorAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstor);

            advice.MetricsEnabled = true;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsCstorAdmissionServerAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorAdmissionServer);

            advice.NodeSelector      = ToObjectYaml(NodeLabel.LabelOpenEbsStorage, "true");
            advice.PriorityClassName = PriorityClass.NeonStorage.Name;
            advice.Replicas          = storageNodeCount;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsCstorCsiControllerAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorCsiController);

            // We're going to schedule the CSI controller on the storage
            // nodes for cStor.

            if (clusterDefinition.Storage.OpenEbs.Engine == OpenEbsEngine.cStor)
            {
                advice.NodeSelector = ToObjectYaml(NodeLabel.LabelOpenEbsStorage, "true");
            }

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsCstorCsiNodeAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorCsiNode);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsCstorCspcOperatorAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorCspcOperator);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsCstorCvcOperatorAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsCstorCvcOperator);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsJivaCsiControllerAdvice()
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

        private void CalculateOpenEbsJivaOperatorAdvice()
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

        private void CalculateOpenEbsLocalPvProvisionerAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsLocalPvProvisioner);

            advice.Replicas          = Math.Min(1, Math.Max(3, workerNodeCount));
            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsNdmAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsNdm);

            advice.PodMemoryLimit    = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest  = ByteUnits.Parse("16Mi");
            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsNdmOperatorAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsNdmOperator);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            // We're going to schedule the node operator on the storage
            // nodes for cStor or Mayastor engines with up to three replicas.

            switch (clusterDefinition.Storage.OpenEbs.Engine)
            {
                case OpenEbsEngine.Jiva:
                case OpenEbsEngine.HostPath:

                    if (workerNodeCount > 0)
                    {
                        advice.NodeSelector = storageNodeSelector;
                        advice.Replicas     = Math.Min(3, workerNodeCount);
                    }
                    break;

                case OpenEbsEngine.cStor:
                case OpenEbsEngine.Mayastor:

                    advice.NodeSelector = storageNodeSelector;
                    advice.Replicas     = Math.Min(3, storageNodeCount);
                    break;

                default:

                    throw new NotImplementedException();
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateOpenEbsNfsProvisionerAdvice()
        {
            var advice = new ServiceAdvice(this, OpenEbsNfsProvisioner);

            advice.PriorityClassName = PriorityClass.NeonStorage.Name;

            // $note(jefflill):
            //
            // The Helm template for this currently hardcodes: replicas = 1

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculatePrometheusAdvice()
        {
            var advice = new ServiceAdvice(this, Prometheus);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculatePrometheusOperatorAdvice()
        {
            var advice = new ServiceAdvice(this, PrometheusOperator);

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateReloaderAdvice()
        {
            var advice = new ServiceAdvice(this, Reloader);

            advice.MetricsEnabled   = false;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            advice.Replicas         = 1;

            if (IsSmallCluster)
            {
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoAdvice()
        {
            var advice = new ServiceAdvice(this, Tempo);

            advice.MetricsEnabled = false;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoAlertmanagerAdvice()
        {
            var advice = new ServiceAdvice(this, TempoAlertmanager);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoCompactorAdvice()
        {
            var advice = new ServiceAdvice(this, TempoCompactor);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoDistributorAdvice()
        {
            var advice = new ServiceAdvice(this, TempoDistributor);

            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoIngesterAdvice()
        {
            var advice = new ServiceAdvice(this, TempoIngester);

            advice.Replicas = Math.Min(3, metricsNodeCount);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoOverridesExporterAdvice()
        {
            var advice = new ServiceAdvice(this, TempoOverridesExporter);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoQuerierAdvice()
        {
            var advice = new ServiceAdvice(this, TempoQuerier);

            advice.Replicas = Math.Min(3, metricsNodeCount);

            if (IsSmallCluster)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }
            else
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoQueryFrontendAdvice()
        {
            var advice = new ServiceAdvice(this, TempoQueryFrontend);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoRulerAdvice()
        {
            var advice = new ServiceAdvice(this, TempoRuler);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }

        private void CalculateTempoStoreGatewayAdvice()
        {
            var advice = new ServiceAdvice(this, TempoStoreGateway);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.Replicas         = 1;

            AddServiceAdvice(advice.ServiceName, advice);
        }
    }
}
