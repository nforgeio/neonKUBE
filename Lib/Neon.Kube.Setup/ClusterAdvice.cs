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
    /// <remarks>
    /// <para>
    /// <see cref="ClusterAdvice"/> maintains a dictionary of <see cref="ServiceAdvice"/> 
    /// instances keyed by the service identity (one of the service identify constants defined
    /// here).  The constructor initializes empty advice instances for each of the known
    /// NEONKUBE services.
    /// </para>
    /// <para>
    /// The basic idea here is that an early setup step will be executed that constructs a
    /// <see cref="ClusterAdvice"/> instance, determines resource and other limitations
    /// holistically based on the cluster hosting environment as well as the total resources 
    /// available to the cluster, potentially priortizing resource assignments to some services
    /// over others.  The step will persist the <see cref="ClusterAdvice"/> to the setup
    /// controller state as the <see cref="KubeSetupProperty.ClusterAdvice"/> property so this 
    /// information will be available to all other deployment steps.
    /// </para>
    /// <para>
    /// <see cref="ServiceAdvice"/> inherits from <see cref="ObjectDictionary"/> and can
    /// hold arbitrary key/values.  The idea is to make it easy to add custom values to the
    /// advice for a service that can be picked up in subsequent deployment steps and used
    /// for things like initializing Helm chart values.
    /// </para>
    /// <para>
    /// Although <see cref="ServiceAdvice"/> can hold arbitrary key/values, we've
    /// defined class properties to manage the common service properties:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="ServiceAdvice.PodCpuLimit"/></term>
    ///     <description>
    ///     <see cref="double"/>: Identifies the property specifying the maximum
    ///     CPU to assign to each service pod.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ServiceAdvice.PodCpuRequest"/></term>
    ///     <description>
    ///     <see cref="double"/>: Identifies the property specifying the CPU to 
    ///     reserve for each service pod.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ServiceAdvice.PodMemoryLimit"/></term>
    ///     <description>
    ///     <see cref="decimal"/>: Identifies the property specifying the maxumum
    ///  bytes RAM that can be consumed by each service pod.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ServiceAdvice.PodMemoryRequest"/></term>
    ///     <description>
    ///     <see cref="decimal"/>: Identifies the property specifying the bytes of
    ///     RAM to be reserved for each service pod.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ServiceAdvice.ReplicaCount"/></term>
    ///     <description>
    ///     <see cref="int"/>: Identifies the property specifying how many pods
    ///     should be deployed for the service.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public class ClusterAdvice
    {
        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>AlertManager</b> service.
        /// </summary>
        public const string AlertManager = "alertmanager";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>BlackboxExporter</b> service.
        /// </summary>
        public const string BlackboxExporter = "blackbox-exporter";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Calico</b> service.
        /// </summary>
        public const string Cilium = "cilium";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>CertManager</b> service manager nodes.
        /// </summary>
        public const string CertManager = "cert-manager";

        /// <summary>
        /// Identifies the NEONKUBE cluster's system database.
        /// </summary>
        public const string NeonSystemDb = "neon-system-db";

        /// <summary>
        /// Identifies the NEONKUBE cluster's system database operator.
        /// </summary>
        public const string NeonSystemDbOperator = "neon-system-db-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's system database pooler.
        /// </summary>
        public const string NeonSystemDbPooler = "neon-system-db-pooler";

        /// <summary>
        /// Identifies the NEONKUBE cluster's system database metrics sidecar.
        /// </summary>
        public const string NeonSystemDbMetrics = "neon-system-db-metrics";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>CoreDNS</b> service.
        /// </summary>
        public const string CoreDns = "coredns";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Dex</b> service.
        /// </summary>
        public const string Dex = "dex";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>etcd nodes</b> service.
        /// </summary>
        public const string EtcdCluster = "etcd-cluster";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Etcd Operator</b> service.
        /// </summary>
        public const string EtcdOperator = "etcd-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>FluentBit</b> service.
        /// </summary>
        public const string FluentBit = "fluentbit";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Glauth</b> service.
        /// </summary>
        public const string Glauth = "glauth";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Grafana</b> service.
        /// </summary>
        public const string Grafana = "grafana";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Grafana Agent</b> service.
        /// </summary>
        public const string GrafanaAgent = "grafana-agent";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Grafana Agent</b> daemonset service.
        /// </summary>
        public const string GrafanaAgentNode = "grafana-agent-node";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Grafana Agent</b> service.
        /// </summary>
        public const string GrafanaAgentOperator = "grafana-agent-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor</b> service.
        /// </summary>
        public const string Harbor = "harbor";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor Chartmuseum</b> service.
        /// </summary>
        public const string HarborChartmuseum = "harbor-chartmuseum";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor Clair</b> service.
        /// </summary>
        public const string HarborClair = "harbor-clair";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor Core</b> service.
        /// </summary>
        public const string HarborCore = "harbor-core";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor Jobservice</b> service.
        /// </summary>
        public const string HarborJobservice = "harbor-jobservice";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor Notary Server</b> service.
        /// </summary>
        public const string HarborNotaryServer = "harbor-notary-server";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor Notary Signer</b> service.
        /// </summary>
        public const string HarborNotarySigner = "harbor-notary-signer";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor Portal</b> service.
        /// </summary>
        public const string HarborPortal = "harbor-portal";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Harbor Registry</b> service.
        /// </summary>
        public const string HarborRegistry = "harbor-registry";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Redis</b> service.
        /// </summary>
        public const string Redis = "redis";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Istio Proxy</b> service.
        /// </summary>
        public const string IstioProxy = "istio-proxy";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Istio Ingress Gateway</b> service.
        /// </summary>
        public const string IstioIngressGateway = "istio-ingressgateway";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Istio Pilot</b> service.
        /// </summary>
        public const string IstioPilot = "istio-pilot";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Kubernetes Dashboard</b> service.
        /// </summary>
        public const string KubernetesDashboard = "kubernetes-dashboard";

        /// <summary>
        /// Identifies the <b>Kube State Metrics</b> service.
        /// </summary>
        public const string KubeStateMetrics = "kube-state-metrics";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Kiali</b> service.
        /// </summary>
        public const string Kiali = "kiali";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki</b> service.
        /// </summary>
        public const string Loki = "loki";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki Compactor</b> service.
        /// </summary>
        public const string LokiCompactor = "loki-compactor";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki Distributor</b> service.
        /// </summary>
        public const string LokiDistributor = "loki-distributor";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki Index Gateway</b> service.
        /// </summary>
        public const string LokiIndexGateway = "loki-index-gateway";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki Ingester</b> service.
        /// </summary>
        public const string LokiIngester = "loki-ingester";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki Querier</b> service.
        /// </summary>
        public const string LokiQuerier = "loki-querier";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki Query Frontend</b> service.
        /// </summary>
        public const string LokiQueryFrontend = "loki-query-frontend";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki Ruler</b> service.
        /// </summary>
        public const string LokiRuler = "loki-ruler";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Loki Table Manager</b> service.
        /// </summary>
        public const string LokiTableManager = "loki-table-manager";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Memcached</b> service.
        /// </summary>
        public const string Memcached = "memcached";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Metrics-Server</b> service.
        /// </summary>
        public const string MetricsServer = "metrics-server";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir</b> service.
        /// </summary>
        public const string Mimir = "mimir";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir Alertmanager</b> service.
        /// </summary>
        public const string MimirAlertmanager = "mimir-alertmanager";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir Compactor</b> service.
        /// </summary>
        public const string MimirCompactor = "mimir-compactor";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir Distributor</b> service.
        /// </summary>
        public const string MimirDistributor = "mimir-distributor";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir Ingester</b> service.
        /// </summary>
        public const string MimirIngester = "mimir-ingester";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir OverridesExporter</b> service.
        /// </summary>
        public const string MimirOverridesExporter = "mimir-overrides-exporter";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir Querier</b> service.
        /// </summary>
        public const string MimirQuerier = "mimir-querier";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir Query Frontend</b> service.
        /// </summary>
        public const string MimirQueryFrontend = "mimir-query-frontend";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir Alertmanager</b> service.
        /// </summary>
        public const string MimirRuler = "mimir-ruler";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Mimir Alertmanager</b> service.
        /// </summary>
        public const string MimirStoreGateway = "mimir-store-gateway";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Minio</b> service.
        /// </summary>
        public const string Minio = "minio";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Minio Operator</b> service.
        /// </summary>
        public const string MinioOperator = "minio-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>neon-acme</b> service.
        /// </summary>
        public const string NeonAcme = "neon-acme";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>neon-cluster-operator</b> service.
        /// </summary>
        public const string NeonClusterOperator = "neon-cluster-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>neon-node-agent</b> service.
        /// </summary>
        public const string NeonNodeAgent = "neon-node-agent";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>neon-sso-session-proxy</b> service.
        /// </summary>
        public const string NeonSsoSessionProxy = "neon-sso-session-proxy";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Node Problem Detector</b> service.
        /// </summary>
        public const string NodeProblemDetector = "node-problem-detector";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>oauth2-proxy</b> service.
        /// </summary>
        public const string Oauth2Proxy = "oauth2-proxy";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS cStor</b> service.
        /// </summary>
        public const string OpenEbsCstor = "openebs-cstor";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS cStor CSI Controller</b> service.
        /// </summary>
        public const string OpenEbsCstorCsiController = "openebs-cstor-csi-controller";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS cStor CSI Node</b> service.
        /// </summary>
        public const string OpenEbsCstorCsiNode = "openebs-cstor-csi-node";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS cStor CSPC Operator</b> service.
        /// </summary>
        public const string OpenEbsCstorCspcOperator = "openebs-cstor-cspc-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS cStor CVC Operator</b> service.
        /// </summary>
        public const string OpenEbsCstorCvcOperator = "openebs-cstor-cvc-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS Jiva</b> service.
        /// </summary>
        public const string OpenEbsJiva = "openebs-jiva";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS Local PV Provisioner</b> service.
        /// </summary>
        public const string OpenEbsProvisionerLocalPv = "openebs-localpv-provisioner";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS Node Disk Manager Operator</b> service.
        /// </summary>
        public const string OpenEbsNdmOperator = "openebs-ndm-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS Node Disk Manager</b> service.
        /// </summary>
        public const string OpenEbsNdm = "openebs-ndm";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS Snapshot Webhook</b> service.
        /// </summary>
        public const string OpenEbsWebhook = "openebs-webhook";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS Cstor Pool</b> containers.
        /// </summary>
        public const string OpenEbsCstorPool = "openebs-cstor-pool";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>OpenEBS Cstor Pool</b> sidecar containers.
        /// </summary>
        public const string OpenEbsCstorPoolAux = "openebs-cstor-pool-aux";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Prometheus</b> service.
        /// </summary>
        public const string Prometheus = "prometheus";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Prometheus Operator</b> service.
        /// </summary>
        public const string PrometheusOperator = "prometheus-operator";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Reloader</b> service.
        /// </summary>
        public const string Reloader = "reloader";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo</b> service.
        /// </summary>
        public const string Tempo = "tempo";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo Alertmanager</b> service.
        /// </summary>
        public const string TempoAlertmanager = "tempo-alertmanager";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo Compactor</b> service.
        /// </summary>
        public const string TempoCompactor = "tempo-compactor";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo Distributor</b> service.
        /// </summary>
        public const string TempoDistributor = "tempo-distributor";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo Ingester</b> service.
        /// </summary>
        public const string TempoIngester = "tempo-ingester";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo OverridesExporter</b> service.
        /// </summary>
        public const string TempoOverridesExporter = "tempo-overrides-exporter";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo Querier</b> service.
        /// </summary>
        public const string TempoQuerier = "tempo-querier";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo Query Frontend</b> service.
        /// </summary>
        public const string TempoQueryFrontend = "tempo-query-frontend";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo Alertmanager</b> service.
        /// </summary>
        public const string TempoRuler = "tempo-ruler";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Tempo Alertmanager</b> service.
        /// </summary>
        public const string TempoStoreGateway = "tempo-store-gateway";

        /// <summary>
        /// Identifies the NEONKUBE cluster's <b>Redis HA</b> service.
        /// </summary>
        public const string RedisHA = "redis-ha";

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, ServiceAdvice>   services   = new Dictionary<string, ServiceAdvice>(StringComparer.CurrentCultureIgnoreCase);
        private bool                                    isReadOnly = false;
        private ClusterDefinition                       clusterDefinition;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        public ClusterAdvice(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            this.clusterDefinition = clusterDefinition;
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
        /// Used to obtain the metrics port exposed for a service when cluster
        /// metrics are enabled.  This is useful for setting Helm chart <b>metricsPort</b>
        /// values where setting zero disables metrics.
        /// </summary>
        /// <param name="port">
        /// Specifies the metrics port exposed for the service or zero when
        /// metrics are to be disabled for the service.d
        /// </param>
        /// <returns>Zero when cluster metrics are disabled or <paramref name="port"/> otherwise.</returns>
        public int GetMetricsPort(int port)
        {
            return MetricsEnabled ? port : 0;
        }

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
        /// Determines resource and other recommendations for the cluster globally as well
        /// as for cluster components based on the cluster definition passed to the constructor.
        /// </summary>
        private void SetComponmentRecomendations()
        {
            // Initialize global cluster advice.

            MetricsEnabled  = true;
            MetricsInterval = clusterDefinition.Nodes.Count() > 6 ? "60s" : "5m";
            MetricsQuota    = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            LogsQuota       = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            TracesQuota     = clusterDefinition.IsDesktop ? "1Gi" : "10Gi";

            // Initialize service advice.

            AddServiceAdvice(ClusterAdvice.AlertManager, CalculateAlertManagerAdvice());
            AddServiceAdvice(ClusterAdvice.BlackboxExporter, CalculateBlackboxExporterAdvice());
            AddServiceAdvice(ClusterAdvice.Cilium, CalculateCiliumAdvice());
            AddServiceAdvice(ClusterAdvice.CertManager, CalculateCertManagerAdvice());
            AddServiceAdvice(ClusterAdvice.CoreDns, CalculateCoreDnsAdvice());
            AddServiceAdvice(ClusterAdvice.Dex, CalculateDexAdvice());
            AddServiceAdvice(ClusterAdvice.EtcdCluster, CalculateEtcdClusterAdvice());
            AddServiceAdvice(ClusterAdvice.Glauth, CalculateGlauthAdvice());
            AddServiceAdvice(ClusterAdvice.Grafana, CalculateGrafanaAdvice());
            AddServiceAdvice(ClusterAdvice.GrafanaAgent, CalculateGrafanaAgentAdvice());
            AddServiceAdvice(ClusterAdvice.GrafanaAgentNode, CalculateGrafanaAgentNodeAdvice());
            AddServiceAdvice(ClusterAdvice.GrafanaAgentOperator, CalculateGrafanaAgentOperatorAdvice());
            AddServiceAdvice(ClusterAdvice.Harbor, CalculateHarborAdvice());
            AddServiceAdvice(ClusterAdvice.HarborChartmuseum, CalculateHarborChartmuseumAdvice());
            AddServiceAdvice(ClusterAdvice.HarborClair, CalculateHarborClairAdvice());
            AddServiceAdvice(ClusterAdvice.HarborCore, CalculateHarborCoreAdvice());
            AddServiceAdvice(ClusterAdvice.HarborJobservice, CalculateHarborJobserviceAdvice());
            AddServiceAdvice(ClusterAdvice.HarborNotaryServer, CalculateHarborNotaryServerAdvice());
            AddServiceAdvice(ClusterAdvice.HarborNotarySigner, CalculateHarborNotarySignerAdvice());
            AddServiceAdvice(ClusterAdvice.HarborPortal, CalculateHarborPortalAdvice());
            AddServiceAdvice(ClusterAdvice.Redis, CalculateRedisAdvice());
            AddServiceAdvice(ClusterAdvice.HarborRegistry, CalculateHarborRegistryAdvice());
            AddServiceAdvice(ClusterAdvice.IstioIngressGateway, CalculateIstioIngressGatewayAdvice());
            AddServiceAdvice(ClusterAdvice.IstioProxy, CalculateIstioProxyAdvice());
            AddServiceAdvice(ClusterAdvice.IstioPilot, CalculateIstioPilotAdvice());
            AddServiceAdvice(ClusterAdvice.Kiali, CalculateKialiAdvice());
            AddServiceAdvice(ClusterAdvice.KubernetesDashboard, CalculateKubernetesDashboardAdvice());
            AddServiceAdvice(ClusterAdvice.KubeStateMetrics, CalculateKubeStateMetricsAdvice());
            AddServiceAdvice(ClusterAdvice.Loki, CalculateLokiAdvice());
            AddServiceAdvice(ClusterAdvice.LokiCompactor, CalculateLokiCompactorAdvice());
            AddServiceAdvice(ClusterAdvice.LokiDistributor, CalculateLokiDistributorAdvice());
            AddServiceAdvice(ClusterAdvice.LokiIndexGateway, CalculateLokiIndexGatewayAdvice());
            AddServiceAdvice(ClusterAdvice.LokiIngester, CalculateLokiIngesterAdvice());
            AddServiceAdvice(ClusterAdvice.LokiQuerier, CalculateLokiQuerierAdvice());
            AddServiceAdvice(ClusterAdvice.LokiQueryFrontend, CalculateLokiQueryFrontendAdvice());
            AddServiceAdvice(ClusterAdvice.LokiRuler, CalculateLokiRulerAdvice());
            AddServiceAdvice(ClusterAdvice.LokiTableManager, CalculateLokiTableManagerAdvice());
            AddServiceAdvice(ClusterAdvice.Memcached, CalculateMemcachedAdvice());
            AddServiceAdvice(ClusterAdvice.MetricsServer, CalculateMetricsServerAdvice());
            AddServiceAdvice(ClusterAdvice.Mimir, CalculateMimirAdvice());
            AddServiceAdvice(ClusterAdvice.MimirAlertmanager, CalculateMimirAlertmanagerAdvice());
            AddServiceAdvice(ClusterAdvice.MimirCompactor, CalculateMimirCompactorAdvice());
            AddServiceAdvice(ClusterAdvice.MimirDistributor, CalculateMimirDistributorAdvice());
            AddServiceAdvice(ClusterAdvice.MimirIngester, CalculateMimirIngesterAdvice());
            AddServiceAdvice(ClusterAdvice.MimirOverridesExporter, CalculateMimirOverridesExporterAdvice());
            AddServiceAdvice(ClusterAdvice.MimirQuerier, CalculateMimirQuerierAdvice());
            AddServiceAdvice(ClusterAdvice.MimirQueryFrontend, CalculateMimirQueryFrontendAdvice());
            AddServiceAdvice(ClusterAdvice.MimirRuler, CalculateMimirRulerAdvice());
            AddServiceAdvice(ClusterAdvice.MimirStoreGateway, CalculateMimirStoreGatewayAdvice());
            AddServiceAdvice(ClusterAdvice.Minio, CalculateMinioAdvice());
            AddServiceAdvice(ClusterAdvice.MinioOperator, CalculateMinioOperatorAdvice());
            AddServiceAdvice(ClusterAdvice.NeonAcme, CalculateNeonAcmeAdvice());
            AddServiceAdvice(ClusterAdvice.NeonClusterOperator, CalculateNeonClusterOperatorAdvice());
            AddServiceAdvice(ClusterAdvice.NeonNodeAgent, CalculateNeonNodeAgentAdvice());
            AddServiceAdvice(ClusterAdvice.NeonSsoSessionProxy, CalculateNeonSsoSessionProxyAdvice());
            AddServiceAdvice(ClusterAdvice.NeonSystemDb, CalculateNeonSystemDbAdvice());
            AddServiceAdvice(ClusterAdvice.NeonSystemDbOperator, CalculateNeonSystemDbOperatorAdvice());
            AddServiceAdvice(ClusterAdvice.NeonSystemDbMetrics, CalculateNeonSystemDbMetricsAdvice());
            AddServiceAdvice(ClusterAdvice.NeonSystemDbPooler, CalculateNeonSystemDbPoolerAdvice());
            AddServiceAdvice(ClusterAdvice.NodeProblemDetector, CalculateNodeProblemDetectorAdvice());
            AddServiceAdvice(ClusterAdvice.Oauth2Proxy, CalculateOauth2ProxyAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsCstor, CalculateOpenEbsCstorAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsCstorCsiController, CalculateOpenEbsCstorCsiControllerAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsCstorCsiNode, CalculateOpenEbsCstorCsiNodeAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsCstorCspcOperator, CalculateOpenEbsCstorCspcOperatorAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsCstorCvcOperator, CalculateOpenEbsCstorCvcOperatorAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsCstorPool, CalculateOpenEbsCstorPoolAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsCstorPoolAux, CalculateOpenEbsCstorPoolAuxAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsJiva, CalculateOpenEbsJivaAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsProvisionerLocalPv, CalculateOpenEbsProvisionerLocalPvAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsNdm, CalculateOpenEbsNdmAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsNdmOperator, CalculateOpenEbsNdmOperatorAdvice());
            AddServiceAdvice(ClusterAdvice.OpenEbsWebhook, CalculateOpenEbsWebhookAdvice());
            AddServiceAdvice(ClusterAdvice.Prometheus, CalculatePrometheusAdvice());
            AddServiceAdvice(ClusterAdvice.PrometheusOperator, CalculatePrometheusOperatorAdvice());
            AddServiceAdvice(ClusterAdvice.Reloader, CalculateReloaderAdvice());
            AddServiceAdvice(ClusterAdvice.Tempo, CalculateTempoAdvice());
            AddServiceAdvice(ClusterAdvice.TempoAlertmanager, CalculateTempoAlertmanagerAdvice());
            AddServiceAdvice(ClusterAdvice.TempoCompactor, CalculateTempoCompactorAdvice());
            AddServiceAdvice(ClusterAdvice.TempoDistributor, CalculateTempoDistributorAdvice());
            AddServiceAdvice(ClusterAdvice.TempoIngester, CalculateTempoIngesterAdvice());
            AddServiceAdvice(ClusterAdvice.TempoOverridesExporter, CalculateTempoOverridesExporterAdvice());
            AddServiceAdvice(ClusterAdvice.TempoQuerier, CalculateTempoQuerierAdvice());
            AddServiceAdvice(ClusterAdvice.TempoQueryFrontend, CalculateTempoQueryFrontendAdvice());
            AddServiceAdvice(ClusterAdvice.TempoRuler, CalculateTempoRulerAdvice());
            AddServiceAdvice(ClusterAdvice.TempoStoreGateway, CalculateTempoStoreGatewayAdvice());

            // Since advice related classes cannot handle updates performed on multiple threads 
            // and cluster setup is multi-threaded, we're going to mark the advice as read-only
            // to prevent any changes in subsequent steps.

            IsReadOnly = true;
        }

        private ServiceAdvice CalculateAlertManagerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.AlertManager);

            return advice;
        }

        private ServiceAdvice CalculateBlackboxExporterAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.BlackboxExporter);

            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");

            return advice;
        }

        private ServiceAdvice CalculateCiliumAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Cilium);

            advice.MetricsEnabled = false;

            return advice;
        }

        private ServiceAdvice CalculateCertManagerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.CertManager);

            advice.ReplicaCount = 1;

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("64Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.MetricsEnabled   = false;
            }

            return advice;
        }

        private ServiceAdvice CalculateNeonSystemDbAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NeonSystemDb);

            advice.ReplicaCount = clusterDefinition.ControlNodes.Count();

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            return advice;
        }

        private ServiceAdvice CalculateNeonSystemDbOperatorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NeonSystemDbOperator);

            advice.ReplicaCount = 1;

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("50Mi");

            return advice;
        }

        private ServiceAdvice CalculateNeonSystemDbMetricsAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NeonSystemDbMetrics);

            advice.ReplicaCount = clusterDefinition.ControlNodes.Count();

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private ServiceAdvice CalculateNeonSystemDbPoolerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NeonSystemDbPooler);

            advice.ReplicaCount = clusterDefinition.ControlNodes.Count();

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private ServiceAdvice CalculateCoreDnsAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.CoreDns);

            advice.PodMemoryRequest = ByteUnits.Parse("30Mi");
            advice.PodMemoryLimit   = ByteUnits.Parse("170Mi");

            return advice;
        }

        private ServiceAdvice CalculateEtcdClusterAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.EtcdCluster);

            advice.ReplicaCount     = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));
            advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.MetricsEnabled   = false;

            return advice;
        }

        private ServiceAdvice CalculateGrafanaAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Grafana);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("350Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.MetricsEnabled   = false;
            }
            else
            {
                advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));

                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("512Mi");
                advice.MetricsEnabled   = false;
            }

            return advice;
        }

        private ServiceAdvice CalculateGrafanaAgentAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.GrafanaAgent);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.MetricsEnabled   = false;
            }
            else
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
                advice.MetricsEnabled   = false;
            }

            return advice;
        }

        private ServiceAdvice CalculateGrafanaAgentNodeAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.GrafanaAgentNode);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.MetricsEnabled   = false;
            }
            else
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
                advice.MetricsEnabled   = false;
            }

            return advice;
        }

        private ServiceAdvice CalculateGrafanaAgentOperatorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.GrafanaAgentOperator);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("100Mi");

            return advice;
        }

        private ServiceAdvice CalculateHarborAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Harbor);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsInterval = "5m";
            }

            advice.MetricsEnabled = false;

            return advice;
        }

        private ServiceAdvice CalculateHarborChartmuseumAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.HarborChartmuseum);

            return advice;
        }

        private ServiceAdvice CalculateHarborClairAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.HarborClair);

            return advice;
        }

        private ServiceAdvice CalculateHarborCoreAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.HarborCore);
            
            return advice;
        }

        private ServiceAdvice CalculateHarborJobserviceAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.HarborJobservice);

            return advice;
        }

        private ServiceAdvice CalculateHarborNotaryServerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.HarborNotaryServer);

            return advice;
        }

        private ServiceAdvice CalculateHarborNotarySignerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.HarborNotarySigner);

            return advice;
        }

        private ServiceAdvice CalculateHarborPortalAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.HarborPortal);

            return advice;
        }

        private ServiceAdvice CalculateRedisAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Redis);

            advice.ReplicaCount   = Math.Min(3, clusterDefinition.ControlNodes.Count());
            advice.MetricsEnabled = true;

            return advice;
        }

        private ServiceAdvice CalculateHarborRegistryAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.HarborRegistry);

            return advice;
        }

        private ServiceAdvice CalculateIstioIngressGatewayAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.IstioIngressGateway);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("160Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private ServiceAdvice CalculateIstioProxyAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.IstioProxy);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private ServiceAdvice CalculateIstioPilotAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.IstioPilot);

            advice.PodCpuLimit      = 0.5;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private ServiceAdvice CalculateDexAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Dex);

            advice.PodMemoryLimit = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.MetricsEnabled = true;

            return advice;
        }

        private ServiceAdvice CalculateGlauthAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Glauth);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.MetricsEnabled = true;

            return advice;
        }

        private ServiceAdvice CalculateKialiAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Kiali);

            advice.MetricsEnabled = false;

            return advice;
        }

        private ServiceAdvice CalculateKubernetesDashboardAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.KubernetesDashboard);

            advice.ReplicaCount     = Math.Max(1, clusterDefinition.Nodes.Count() / 10);
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("128Mi");

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }

            advice.MetricsEnabled = false;

            return advice;
        }

        private ServiceAdvice CalculateKubeStateMetricsAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.KubeStateMetrics);

            advice.ReplicaCount   = Math.Max(1, clusterDefinition.Nodes.Count() / 10);
            advice.MetricsEnabled = true;

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsInterval = "5m";
                advice.ReplicaCount    = 1;
            }

            return advice;
        }

        private ServiceAdvice CalculateMemcachedAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Memcached);

            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            advice.MetricsEnabled   = true;
            advice.MetricsInterval  = "60s";

            return advice;
        }

        private ServiceAdvice CalculateMetricsServerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MetricsServer);

            advice.MetricsEnabled = false;

            return advice;
        }

        private ServiceAdvice CalculateMimirAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Mimir);

            advice.MetricsEnabled = false;

            return advice;
        }

        private ServiceAdvice CalculateMimirAlertmanagerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirAlertmanager);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;

            return advice;
        }

        private ServiceAdvice CalculateMimirCompactorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirCompactor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;

            return advice;
        }

        private ServiceAdvice CalculateMimirDistributorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirDistributor);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.ReplicaCount     = 3;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("512Mi");
            }

            return advice;
        }

        private ServiceAdvice CalculateMimirIngesterAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirIngester);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("2Gi");
            }

            return advice;
        }

        private ServiceAdvice CalculateMimirOverridesExporterAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirOverridesExporter);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateMimirQuerierAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirQuerier);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled  = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            return advice;
        }

        private ServiceAdvice CalculateMimirQueryFrontendAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirQueryFrontend);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateMimirRulerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirRuler);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateMimirStoreGatewayAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MimirStoreGateway);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }
        private ServiceAdvice CalculateLokiAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Loki);

            advice.MetricsEnabled = false;

            return advice;
        }
        private ServiceAdvice CalculateLokiCompactorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.LokiCompactor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;

            return advice;
        }

        private ServiceAdvice CalculateLokiDistributorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.LokiDistributor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateLokiIndexGatewayAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.LokiIndexGateway);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateLokiIngesterAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.LokiIngester);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
            }

            return advice;
        }

        private ServiceAdvice CalculateLokiQuerierAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.LokiQuerier);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            return advice;
        }

        private ServiceAdvice CalculateLokiQueryFrontendAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.LokiQueryFrontend);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateLokiRulerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.LokiRuler);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateLokiTableManagerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.LokiTableManager);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateMinioAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Minio);

            if (clusterDefinition.Nodes.Where(node => node.Labels.SystemMinioServices).Count() >= 3)
            {
                if (clusterDefinition.Nodes.Where(node => node.Labels.SystemMinioServices).Count() >= 4)
                {
                    advice.ReplicaCount = clusterDefinition.Nodes.Where(node => node.Labels.SystemMinioServices).Count();
                }
                else
                {
                    advice.ReplicaCount = 1;
                }

                advice.PodMemoryLimit   = ByteUnits.Parse("4Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("4Gi");
                advice.MetricsInterval  = "1m";
            }
            else
            {
                advice.ReplicaCount = 1;
            }

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("768Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("256Mi");
                advice.MetricsEnabled   = false;
                advice.ReplicaCount     = 1;
            }

            advice.MetricsEnabled = true;

            return advice;
        }

        private ServiceAdvice CalculateMinioOperatorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.MinioOperator);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("40Mi");

            return advice;
        }

        private ServiceAdvice CalculateNeonClusterOperatorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NeonClusterOperator);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = true;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("115Mi");

            return advice;
        }

        private ServiceAdvice CalculateNeonAcmeAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NeonAcme);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("50Mi");

            return advice;
        }

        private ServiceAdvice CalculateNeonNodeAgentAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NeonNodeAgent);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("350Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            return advice;
        }
        
        private ServiceAdvice CalculateNeonSsoSessionProxyAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NeonSsoSessionProxy);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("60Mi");

            return advice;
        }

        private ServiceAdvice CalculateNodeProblemDetectorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.NodeProblemDetector);

            advice.MetricsInterval = "1m";

            return advice;
        }

        private ServiceAdvice CalculateOauth2ProxyAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Oauth2Proxy);

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsCstorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsCstor);

            advice.MetricsEnabled = true;

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsCstorCsiControllerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsCstorCsiController);

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsCstorCsiNodeAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsCstorCsiNode);

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsCstorCspcOperatorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsCstorCspcOperator);

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsCstorCvcOperatorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsCstorCvcOperator);

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsCstorPoolAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsCstorPool);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(n => n.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
                advice.MetricsEnabled   = false;
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("2Gi");
                advice.MetricsEnabled   = true;
            }

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsCstorPoolAuxAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsCstorPoolAux);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(n => n.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("300Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
                advice.MetricsEnabled   = false;
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("500Mi");
                advice.MetricsEnabled   = false;
            }

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsJivaAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsJiva);

            advice.MetricsEnabled = true;

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsProvisionerLocalPvAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsProvisionerLocalPv);

            advice.ReplicaCount = Math.Max(1, clusterDefinition.Workers.Count() / 3);

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsNdmAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsNdm);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsNdmOperatorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsNdmOperator);

            advice.ReplicaCount = 1;

            return advice;
        }

        private ServiceAdvice CalculateOpenEbsWebhookAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.OpenEbsWebhook);

            advice.ReplicaCount = Math.Max(1, clusterDefinition.Workers.Count() / 3);

            return advice;
        }

        private ServiceAdvice CalculatePrometheusAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Prometheus);

            return advice;
        }

        private ServiceAdvice CalculatePrometheusOperatorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.PrometheusOperator);

            return advice;
        }

        private ServiceAdvice CalculateReloaderAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Reloader);

            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("128Mi");

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }

            return advice;
        }

        private ServiceAdvice CalculateTempoAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.Tempo);

            advice.MetricsEnabled = false;

            return advice;
        }

        private ServiceAdvice CalculateTempoAlertmanagerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoAlertmanager);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            return advice;
        }

        private ServiceAdvice CalculateTempoCompactorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoCompactor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            return advice;
        }

        private ServiceAdvice CalculateTempoDistributorAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoDistributor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateTempoIngesterAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoIngester);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
            }

            return advice;
        }

        private ServiceAdvice CalculateTempoOverridesExporterAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoOverridesExporter);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateTempoQuerierAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoQuerier);

            advice.ReplicaCount = Math.Min(3, (clusterDefinition.Nodes.Where(node => node.Labels.SystemMetricServices).Count()));

            if (clusterDefinition.IsDesktop ||
                clusterDefinition.ControlNodes.Count() == 1 ||
                clusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.MetricsEnabled   = true;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            return advice;
        }

        private ServiceAdvice CalculateTempoQueryFrontendAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoQueryFrontend);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateTempoRulerAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoRuler);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private ServiceAdvice CalculateTempoStoreGatewayAdvice()
        {
            var advice = new ServiceAdvice(ClusterAdvice.TempoStoreGateway);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }
    }
}
