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

            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.AlertManager, CalculateAlertManagerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Calico, CalculateCalicoAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.CitusPostgresSqlManager, CalculateCitusManagerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.CitusPostgresSqlMaster, CalculateCitusMasterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.CitusPostgresSqlWorker, CalculateCitusWorkerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Cortex, CalculateCortexAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.EtcdCluster, CalculateEtcdClusterAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Grafana, CalculateGrafanaAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.GrafanaAgent, CalculateGrafanaAgentAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.GrafanaAgentNode, CalculateGrafanaAgentNodeAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.GrafanaAgentOperator, CalculateGrafanaAgentOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborChartmuseum, CalculateHarborChartmuseumAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborClair, CalculateHarborClairAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborCore, CalculateHarborCoreAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborJobservice, CalculateHarborJobserviceAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborNotaryServer, CalculateHarborNotaryServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborNotarySigner, CalculateHarborNotarySignerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborPortal, CalculateHarborPortalAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborRedis, CalculateHarborRedisAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.HarborRegistry, CalculateHarborRegistryAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.IstioIngressGateway, CalculateIstioIngressGatewayAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.IstioProxy, CalculateIstioProxyAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Jaeger, CalculateJaegerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Kiali, CalculateKialiAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.KubernetesDashboard, CalculateKubernetesDashboardAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.KubeStateMetrics, CalculateKubeStateMetricsAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Loki, CalculateLokiAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.MetricsServer, CalculateMetricsServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Minio, CalculateMinioAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.NeonClusterOperator, CalculateNeonClusterOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsAdmissionServer, CalculateOpenEbsAdmissionServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsApiServer, CalculateOpenEbsApiServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorAdmissionServer, CalculateOpenEbsCstorAdmissionServerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorCsiController, CalculateOpenEbsCstorCsiControllerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorCsiNode, CalculateOpenEbsCstorCsiNodeAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorCspcOperator, CalculateOpenEbsCstorCspcOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsCstorCvcOperator, CalculateOpenEbsCstorCvcOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsLocalPvProvisioner, CalculateOpenEbsLocalPvProvisionerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsNdm, CalculateOpenEbsNdmAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsNdmOperator, CalculateOpenEbsNdmOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsProvisioner, CalculateOpenEbsProvisionerAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsSnapshotOperator, CalculateOpenEbsSnapshotOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.OpenEbsWebhook, CalculateOpenEbsWebhookAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Prometheus, CalculatePrometheusAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.PrometheusOperator, CalculatePrometheusOperatorAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.Tempo, CalculateTempoAdvice(cluster));
            clusterAdvice.AddServiceAdvice(KubeClusterAdvice.RedisHA, CalculateRedisHAAdvice(cluster));


            //==================================================
            // $todo(marcusbooyah): INSERT YOUR MAGIC CODE HERE!
            //==================================================

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

        private static KubeServiceAdvice CalculateCalicoAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Calico);

            return advice;
        }

        private static KubeServiceAdvice CalculateCitusManagerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.CitusPostgresSqlManager);

            advice.ReplicaCount = 1;

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
               || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
               || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit = ByteUnits.Parse("128Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateCitusMasterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.CitusPostgresSqlMaster);

            advice.ReplicaCount = 1;

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
               || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
               || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit = ByteUnits.Parse("128Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateCitusWorkerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.CitusPostgresSqlWorker);

            advice.ReplicaCount = Math.Min(3, (cluster.Definition.Nodes.Where(node => node.Labels.NeonSystemDb).Count()));

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
               || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
               || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit = ByteUnits.Parse("128Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateCortexAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Cortex);

            advice.ReplicaCount = Math.Min(3, (cluster.Definition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            advice.PodMemoryLimit = ByteUnits.Parse("1Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("1Gi");

            return advice;
        }

        private static KubeServiceAdvice CalculateEtcdClusterAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.EtcdCluster);

            advice.ReplicaCount = Math.Min(3, (cluster.Definition.Nodes.Where(node => node.Labels.MetricsInternal).Count()));

            return advice;
        }

        private static KubeServiceAdvice CalculateGrafanaAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Grafana);

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
               || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
               || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit = ByteUnits.Parse("128Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }
            else
            {
                advice.PodMemoryLimit = ByteUnits.Parse("256Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("256Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateGrafanaAgentAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.GrafanaAgent);

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
               || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
               || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit = ByteUnits.Parse("256Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("256Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateGrafanaAgentNodeAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.GrafanaAgentNode);

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
               || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
               || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit = ByteUnits.Parse("1Gi");
                advice.PodMemoryRequest = ByteUnits.Parse("1Gi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateGrafanaAgentOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.GrafanaAgentOperator);

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

        private static KubeServiceAdvice CalculateHarborRedisAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.HarborRedis);

            advice.ReplicaCount = Math.Min(3, cluster.Definition.Masters.Count());

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
            advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateIstioProxyAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.IstioProxy);

            advice.PodCpuLimit      = 2;
            advice.PodCpuRequest    = 0.010;
            advice.PodMemoryLimit   = ByteUnits.Parse("1Gi");
            advice.PodMemoryRequest = ByteUnits.Parse("64Mi");

            return advice;
        }

        private static KubeServiceAdvice CalculateJaegerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Jaeger);

            return advice;
        }

        private static KubeServiceAdvice CalculateKialiAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Kiali);

            return advice;
        }

        private static KubeServiceAdvice CalculateKubernetesDashboardAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.KubernetesDashboard);

            advice.ReplicaCount = Math.Max(1, cluster.Definition.Nodes.Count() / 10);

            return advice;
        }

        private static KubeServiceAdvice CalculateKubeStateMetricsAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.KubeStateMetrics);

            advice.ReplicaCount = Math.Max(1, cluster.Definition.Nodes.Count() / 10);

            return advice;
        }

        private static KubeServiceAdvice CalculateLokiAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Loki);

            advice.ReplicaCount = Math.Min(3, (cluster.Definition.Nodes.Where(node => node.Labels.LogsInternal).Count()));

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
                || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
                || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit   = ByteUnits.Parse("128Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateMetricsServerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.MetricsServer);

            return advice;
        }

        private static KubeServiceAdvice CalculateMinioAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Minio);

            if (cluster.Definition.Nodes.Where(node => node.Labels.MetricsInternal).Count() >= 3)
            {
                advice.ReplicaCount = Math.Min(4, Math.Max(4, cluster.Definition.Nodes.Where(node => node.Labels.MetricsInternal).Count() / 4));
            }
            else
            {
                advice.ReplicaCount = 1;
            }

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
                || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
                || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit = ByteUnits.Parse("256Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("256Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateNeonClusterOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.NeonClusterOperator);

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

            advice.ReplicaCount = Math.Max(1, cluster.Definition.Workers.Count() / 3);

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

        private static KubeServiceAdvice CalculateOpenEbsLocalPvProvisionerAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsLocalPvProvisioner);

            advice.ReplicaCount = Math.Max(1, cluster.Definition.Workers.Count() / 3);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsNdmAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsNdm);

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

            advice.ReplicaCount = Math.Max(1, cluster.Definition.Workers.Count() / 3);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsSnapshotOperatorAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsSnapshotOperator);

            advice.ReplicaCount = Math.Max(1, cluster.Definition.Workers.Count() / 3);

            return advice;
        }

        private static KubeServiceAdvice CalculateOpenEbsWebhookAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.OpenEbsWebhook);

            advice.ReplicaCount = Math.Max(1, cluster.Definition.Workers.Count() / 3);

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

        private static KubeServiceAdvice CalculateTempoAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.Tempo);

            advice.ReplicaCount = Math.Min(3, (cluster.Definition.Nodes.Where(n => n.Labels.MetricsInternal).Count()));

            if (cluster.Definition.Name == KubeConst.NeonDesktopHyperVBuiltInVmName
                || cluster.Definition.Name == KubeConst.NeonDesktopWsl2BuiltInDistroName
                || cluster.Definition.Nodes.Count() == 1)
            {
                advice.PodMemoryLimit = ByteUnits.Parse("128Mi");
                advice.PodMemoryRequest = ByteUnits.Parse("128Mi");
            }

            return advice;
        }

        private static KubeServiceAdvice CalculateRedisHAAdvice(ClusterProxy cluster)
        {
            var advice = new KubeServiceAdvice(KubeClusterAdvice.RedisHA);

            return advice;
        }
    }
}
