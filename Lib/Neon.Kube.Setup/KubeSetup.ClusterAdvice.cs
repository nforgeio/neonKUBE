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

using Newtonsoft.Json.Linq;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube.Proxy;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

namespace Neon.Kube.Setup
{
    public static partial class KubeSetup
    {
        /// <summary>
        /// <para>
        /// Executed very early during cluster setup to determine service/pod requests and
        /// limits as a <see cref="KubeClusterAdvice"/> instance that will then be made
        /// available to the subquent setup steps as the <see cref="KubeSetupProperty.ClusterAdvice"/>
        /// property value.
        /// </para>
        /// <para>
        /// This gives cluster setup a chance to holistically examine the services as well as the
        /// resources available to the entire cluster to configure these values.
        /// </para>
        /// </summary>
        /// <param name="controller">The setup controller.</param>
        public static void CalculateResourceRequirements(ISetupController controller)
        {
            Covenant.Requires<ArgumentException>(controller != null, nameof(controller));

            var cluster = controller.Get<ClusterProxy>(KubeSetupProperty.ClusterProxy);

            var clusterAdvice = new KubeClusterAdvice();

            clusterAdvice.MetricsEnabled  = true;
            clusterAdvice.MetricsInterval = cluster.SetupState.ClusterDefinition.Nodes.Count() > 6 ? "60s" : "5m";
            clusterAdvice.MetricsQuota    = cluster.SetupState.ClusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            clusterAdvice.LogsQuota       = cluster.SetupState.ClusterDefinition.IsDesktop ? "1Gi" : "10Gi";
            clusterAdvice.TracesQuota     = cluster.SetupState.ClusterDefinition.IsDesktop ? "1Gi" : "10Gi";

            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.AlertManager, CalculateAlertManagerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.BlackboxExporter, CalculateBlackboxExporterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Calico, CalculateCalicoAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.CertManager, CalculateCertManagerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.CoreDns, CalculateCoreDnsAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Dex, CalculateDexAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.EtcdCluster, CalculateEtcdClusterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Glauth, CalculateGlauthAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Grafana, CalculateGrafanaAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.GrafanaAgent, CalculateGrafanaAgentAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.GrafanaAgentNode, CalculateGrafanaAgentNodeAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.GrafanaAgentOperator, CalculateGrafanaAgentOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Harbor, CalculateHarborAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborChartmuseum, CalculateHarborChartmuseumAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborClair, CalculateHarborClairAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborCore, CalculateHarborCoreAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborJobservice, CalculateHarborJobserviceAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborNotaryServer, CalculateHarborNotaryServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborNotarySigner, CalculateHarborNotarySignerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborPortal, CalculateHarborPortalAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Redis, CalculateRedisAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborRegistry, CalculateHarborRegistryAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.IstioIngressGateway, CalculateIstioIngressGatewayAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.IstioProxy, CalculateIstioProxyAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.IstioPilot, CalculateIstioPilotAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Kiali, CalculateKialiAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.KubernetesDashboard, CalculateKubernetesDashboardAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.KubeStateMetrics, CalculateKubeStateMetricsAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Loki, CalculateLokiAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.LokiCompactor, CalculateLokiCompactorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.LokiDistributor, CalculateLokiDistributorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.LokiIndexGateway, CalculateLokiIndexGatewayAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.LokiIngester, CalculateLokiIngesterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.LokiQuerier, CalculateLokiQuerierAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.LokiQueryFrontend, CalculateLokiQueryFrontendAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.LokiRuler, CalculateLokiRulerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.LokiTableManager, CalculateLokiTableManagerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Memcached, CalculateMemcachedAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MetricsServer, CalculateMetricsServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Mimir, CalculateMimirAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirAlertmanager, CalculateMimirAlertmanagerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirCompactor, CalculateMimirCompactorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirDistributor, CalculateMimirDistributorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirIngester, CalculateMimirIngesterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirOverridesExporter, CalculateMimirOverridesExporterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirQuerier, CalculateMimirQuerierAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirQueryFrontend, CalculateMimirQueryFrontendAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirRuler, CalculateMimirRulerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MimirStoreGateway, CalculateMimirStoreGatewayAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Minio, CalculateMinioAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MinioOperator, CalculateMinioOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NeonAcme, CalculateNeonAcmeAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NeonClusterOperator, CalculateNeonClusterOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NeonNodeAgent, CalculateNeonNodeAgentAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NeonSsoSessionProxy, CalculateNeonSsoSessionProxyAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NeonSystemDb, CalculateNeonSystemDbAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NeonSystemDbMetrics, CalculateNeonSystemDbMetricsAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NeonSystemDbPooler, CalculateNeonSystemDbPoolerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NodeProblemDetector, CalculateNodeProblemDetectorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Oauth2Proxy, CalculateOauth2ProxyAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsAdmissionServer, CalculateOpenEbsAdmissionServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsApiServer, CalculateOpenEbsApiServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstor, CalculateOpenEbsCstorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorAdmissionServer, CalculateOpenEbsCstorAdmissionServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorCsiController, CalculateOpenEbsCstorCsiControllerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorCsiNode, CalculateOpenEbsCstorCsiNodeAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorCspcOperator, CalculateOpenEbsCstorCspcOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorCvcOperator, CalculateOpenEbsCstorCvcOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorPool, CalculateOpenEbsCstorPoolAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorPoolAux, CalculateOpenEbsCstorPoolAuxAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsJiva, CalculateOpenEbsJivaAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsLocalPvProvisioner, CalculateOpenEbsLocalPvProvisionerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsNdm, CalculateOpenEbsNdmAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsNdmOperator, CalculateOpenEbsNdmOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsProvisioner, CalculateOpenEbsProvisionerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsSnapshotOperator, CalculateOpenEbsSnapshotOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsWebhook, CalculateOpenEbsWebhookAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Prometheus, CalculatePrometheusAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.PrometheusOperator, CalculatePrometheusOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Reloader, CalculateReloaderAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Tempo, CalculateTempoAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoAlertmanager, CalculateTempoAlertmanagerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoCompactor, CalculateTempoCompactorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoDistributor, CalculateTempoDistributorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoIngester, CalculateTempoIngesterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoOverridesExporter, CalculateTempoOverridesExporterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoQuerier, CalculateTempoQuerierAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoQueryFrontend, CalculateTempoQueryFrontendAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoRuler, CalculateTempoRulerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.TempoStoreGateway, CalculateTempoStoreGatewayAdvice(cluster));

            // Make the advice available to subsequent setup steps.

            controller.Add(KubeSetupProperty.ClusterAdvice, clusterAdvice);

            // Since advice related classes cannot handle updates performed on multiple threads 
            // and cluster setup is multi-threaded, we're going to mark the advice as read-only
            // to prevent any changes in subsequent steps.

            clusterAdvice.IsReadOnly = true;
        }

        private static KubeServiceAdvice CalculateAlertManagerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.AlertManager);

            return advice;
        }

        private static KubeServiceAdvice CalculateBlackboxExporterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.BlackboxExporter);

            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateCalicoAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Calico);

            advice.MetricsEnabled = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateCertManagerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.CertManager);

            advice.ReplicaCount = 1;

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("64Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.MetricsEnabled   = false;
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateNeonSystemDbAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NeonSystemDb);

            advice.ReplicaCount = cluster.SetupState.ClusterDefinition.ControlNodes.Count();

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateNeonSystemDbMetricsAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NeonSystemDbMetrics);

            advice.ReplicaCount = cluster.SetupState.ClusterDefinition.ControlNodes.Count();

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateNeonSystemDbPoolerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NeonSystemDbPooler);

            advice.ReplicaCount = cluster.SetupState.ClusterDefinition.ControlNodes.Count();

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateCoreDnsAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.CoreDns);

            advice.PodMemoryRequest = ByteUnits.Parse("30Mi");
            advice.PodMemoryLimit   = ByteUnits.Parse("170Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateEtcdClusterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.EtcdCluster);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.MetricsEnabled   = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateGrafanaAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Grafana);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("350Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.MetricsEnabled   = false;
            }
            else
            {
                advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("512Mi");
                advice.MetricsEnabled   = false;
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateGrafanaAgentAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.GrafanaAgent);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateGrafanaAgentNodeAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.GrafanaAgentNode);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateGrafanaAgentOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.GrafanaAgentOperator);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("100Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Harbor);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsInterval = "5m";
            }

            advice.MetricsEnabled = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborChartmuseumAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborChartmuseum);

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborClairAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborClair);

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborCoreAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborCore);

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborJobserviceAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborJobservice);

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborNotaryServerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborNotaryServer);

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborNotarySignerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborNotarySigner);

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborPortalAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborPortal);

            return advice;
        }

        private static KubeServiceAdvice CalculateRedisAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Redis);

            advice.ReplicaCount   = Math.Min(3, cluster.SetupState.ClusterDefinition.ControlNodes.Count());
            advice.MetricsEnabled = true;

            return advice;
        }

        private static KubeServiceAdvice CalculateHarborRegistryAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborRegistry);

            return advice;
        }

        private static KubeServiceAdvice CalculateIstioIngressGatewayAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.IstioIngressGateway);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("160Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateIstioProxyAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.IstioProxy);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateIstioPilotAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.IstioPilot);

            advice.PodCpuLimit      = 0.5;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("2Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateDexAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Dex);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.MetricsEnabled = true;

            return advice;
        }

        private static KubeServiceAdvice CalculateGlauthAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Glauth);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.MetricsEnabled = true;

            return advice;
        }

        private static KubeServiceAdvice CalculateKialiAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Kiali);

            advice.MetricsEnabled = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateKubernetesDashboardAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.KubernetesDashboard);

            advice.ReplicaCount = Math.Max(1, cluster.SetupState.ClusterDefinition.Nodes.Count() / 10);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("128Mi");

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }

            advice.MetricsEnabled = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateKubeStateMetricsAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.KubeStateMetrics);

            advice.ReplicaCount   = Math.Max(1, cluster.SetupState.ClusterDefinition.Nodes.Count() / 10);
            advice.MetricsEnabled = true;

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsInterval = "5m";
                advice.ReplicaCount    = 1;
            }
            
            return advice;
        }

        private static KubeServiceAdvice CalculateMemcachedAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Memcached);

            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            advice.MetricsEnabled   = true;
            advice.MetricsInterval  = "60s";

            return advice;
        }

        private static KubeServiceAdvice CalculateMetricsServerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MetricsServer);

            advice.MetricsEnabled = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateMimirAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Mimir);

            advice.MetricsEnabled = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateMimirAlertmanagerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirAlertmanager);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateMimirCompactorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirCompactor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateMimirDistributorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirDistributor);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateMimirIngesterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirIngester);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateMimirOverridesExporterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirOverridesExporter);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateMimirQuerierAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirQuerier);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateMimirQueryFrontendAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirQueryFrontend);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("256Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateMimirRulerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirRuler);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateMimirStoreGatewayAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MimirStoreGateway);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }
        private static KubeServiceAdvice CalculateLokiAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Loki);

            advice.MetricsEnabled = false;

            return advice;
        }
        private static KubeServiceAdvice CalculateLokiCompactorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.LokiCompactor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateLokiDistributorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.LokiDistributor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateLokiIndexGatewayAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.LokiIndexGateway);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateLokiIngesterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.LokiIngester);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateLokiQuerierAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.LokiQuerier);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateLokiQueryFrontendAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.LokiQueryFrontend);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateLokiRulerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.LokiRuler);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateLokiTableManagerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.LokiTableManager);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateMinioAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Minio);

            if (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MinioInternal).Count() >= 3)
            {
                if (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MinioInternal).Count() >= 4)
                {
                    advice.ReplicaCount = cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MinioInternal).Count();
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

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("768Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("256Mi");
                advice.MetricsEnabled   = false;
                advice.ReplicaCount     = 1;
            }

            advice.MetricsEnabled = true;

            return advice;
        }

        private static KubeServiceAdvice CalculateMinioOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MinioOperator);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("40Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateNeonClusterOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NeonClusterOperator);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = true;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("115Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateNeonAcmeAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NeonAcme);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("50Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateNeonNodeAgentAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NeonNodeAgent);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("350Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            return advice;
        }
        
        private static KubeServiceAdvice CalculateNeonSsoSessionProxyAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NeonSsoSessionProxy);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("60Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateNodeProblemDetectorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NodeProblemDetector);

            advice.MetricsInterval = "1m";

            return advice;
        }

        private static KubeServiceAdvice CalculateOauth2ProxyAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Oauth2Proxy);

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled = false;
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsAdmissionServerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsAdmissionServer);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsApiServerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsApiServer);

            advice.ReplicaCount = Math.Max(1, cluster.SetupState.ClusterDefinition.Workers.Count() / 3);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsCstorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsCstor);

            advice.MetricsEnabled = true;

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsCstorAdmissionServerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsCstorAdmissionServer);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsCstorCsiControllerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsCstorCsiController);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsCstorCsiNodeAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsCstorCsiNode);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsCstorCspcOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsCstorCspcOperator);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsCstorCvcOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsCstorCvcOperator);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsCstorPoolAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsCstorPool);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(n => n.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateOpenEbsCstorPoolAuxAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsCstorPoolAux);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(n => n.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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
                advice.MetricsEnabled = false;
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsJivaAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsJiva);

            advice.MetricsEnabled = true;

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsLocalPvProvisionerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsLocalPvProvisioner);

            advice.ReplicaCount = Math.Max(1, cluster.SetupState.ClusterDefinition.Workers.Count() / 3);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsNdmAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsNdm);

            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsNdmOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsNdmOperator);

            advice.ReplicaCount = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsProvisionerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsProvisioner);

            advice.ReplicaCount = Math.Max(1, cluster.SetupState.ClusterDefinition.Workers.Count() / 3);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsSnapshotOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsSnapshotOperator);

            advice.ReplicaCount = Math.Max(1, cluster.SetupState.ClusterDefinition.Workers.Count() / 3);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsWebhookAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsWebhook);

            advice.ReplicaCount = Math.Max(1, cluster.SetupState.ClusterDefinition.Workers.Count() / 3);

            return advice;
        }

        private static KubeServiceAdvice CalculatePrometheusAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Prometheus);

            return advice;
        }

        private static KubeServiceAdvice CalculatePrometheusOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.PrometheusOperator);

            return advice;
        }

        private static KubeServiceAdvice CalculateReloaderAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Reloader);

            advice.MetricsEnabled   = false;
            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("128Mi");

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Tempo);

            advice.MetricsEnabled = false;

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoAlertmanagerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoAlertmanager);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoCompactorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoCompactor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoDistributorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoDistributor);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("32Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoIngesterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoIngester);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
            {
                advice.MetricsEnabled   = false;
                advice.PodMemoryLimit   = ByteUnits.Parse("512Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("64Mi");
                advice.ReplicaCount     = 1;
            }
            else
            {
                advice.MetricsEnabled = true;
                advice.PodMemoryLimit = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoOverridesExporterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoOverridesExporter);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoQuerierAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoQuerier);

            advice.ReplicaCount = Math.Min(3, (cluster.SetupState.ClusterDefinition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            if (cluster.SetupState.ClusterDefinition.IsDesktop
                || cluster.SetupState.ClusterDefinition.ControlNodes.Count() == 1
                || cluster.SetupState.ClusterDefinition.Nodes.Count() <= 10)
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

        private static KubeServiceAdvice CalculateTempoQueryFrontendAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoQueryFrontend);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("24Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoRulerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoRuler);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }

        private static KubeServiceAdvice CalculateTempoStoreGatewayAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.TempoStoreGateway);

            advice.ReplicaCount     = 1;
            advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
            advice.PodMemoryRequest = ByteUnits.Parse("16Mi");
            advice.ReplicaCount     = 1;

            return advice;
        }
    }
}
