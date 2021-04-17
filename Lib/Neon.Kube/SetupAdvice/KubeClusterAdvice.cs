//-----------------------------------------------------------------------------
// FILE:	    KubeSetupAdvice.cs
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
    /// <summary>
    /// Holds cluster configuration advice initialized early during cluster setup.  This
    /// is used to centralize the decisions about things like resource limitations and 
    /// node taints/affinity based on the overall resources available to the cluster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="KubeClusterAdvice"/> maintains a dictionary of <see cref="KubeServiceAdvice"/> 
    /// instances keyed by the service identity (one of the service identify constants defined
    /// here).  The constructor initializes empty advice instances for each of the known
    /// neonKUBE services.
    /// </para>
    /// <para>
    /// The basic idea here is that an early setup step will be executed that constructs a
    /// <see cref="KubeClusterAdvice"/> instance, determines resource and other limitations
    /// holistically based on the cluster hosting environment (e.g. WSL2) as well as the
    /// total resources available to the cluster, potentially priortizing resource assignments
    /// to some services over others.  The step will persist the <see cref="KubeClusterAdvice"/>
    /// to the setup controller state as the <see cref="KubeSetupProperty.ClusterAdvice"/>
    /// peoperty so this information will be available to all other deployment steps.
    /// </para>
    /// <para>
    /// <see cref="KubeServiceAdvice"/> inherits from <see cref="ObjectDictionary"/> and can
    /// hold arbitrary key/values.  The idea is to make it easy to add custom values to the
    /// advice for a service that can be picked up in subsequent deployment steps and used
    /// for things like initializing Helm chart values.
    /// </para>
    /// <para>
    /// Although <see cref="KubeServiceAdvice"/> can hold arbitrary key/values, we've
    /// defined class properties to manage the common service properties:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="KubeServiceAdvice.PodCpuLimit"/></term>
    ///     <description>
    ///     <see cref="double"/>: Identifies the property specifying the maximum
    ///     CPU to assign to each service pod.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="KubeServiceAdvice.PodCpuRequest"/></term>
    ///     <description>
    ///     <see cref="double"/>: Identifies the property specifying the CPU to 
    ///     reserve for each service pod.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="KubeServiceAdvice.PodMemoryLimit"/></term>
    ///     <description>
    ///     <see cref="decimal"/>: Identifies the property specifying the maxumum
    ///  bytes RAM that can be consumed by each service pod.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="KubeServiceAdvice.PodMemoryRequest"/></term>
    ///     <description>
    ///     <see cref="decimal"/>: Identifies the property specifying the bytes of
    ///     RAM to be reserved for each service pod.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="KubeServiceAdvice.ReplicaCount"/></term>
    ///     <description>
    ///     <see cref="int"/>: Identifies the property specifying how many pods
    ///     should be deployed for the service.
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public class KubeClusterAdvice
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>AlertManager</b> service.
        /// </summary>
        public static string AlertManager = "alertmanager";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Calico</b> service.
        /// </summary>
        public static string Calico = "calico";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Citus Postres</b> service manager nodes.
        /// </summary>
        public static string CitusPostgresSqlManager = "citus-postgressql-manager";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Citus Postres</b> service master nodes.
        /// </summary>
        public static string CitusPostgresSqlMaster = "citus-postgressql-master";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Citus Postres</b> service master nodes.
        /// </summary>
        public static string CitusPostgresSqlWorker = "citus-postgressql-worker";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Cortex</b> service.
        /// </summary>
        public static string Cortex = "cortex";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Etc nodes</b> service.
        /// </summary>
        public static string EtcdCluster = "etcd-cluster";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Etcd Operatoir</b> service.
        /// </summary>
        public static string EtcdOperator = "etcd-operator";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>FluentBit</b> service.
        /// </summary>
        public static string FluentBit = "fluentbit";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Grafana</b> service.
        /// </summary>
        public static string Grafana = "grafana";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor</b> service.
        /// </summary>
        public static string Harbor = "harbor";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Chartmuseum</b> service.
        /// </summary>
        public static string HarborChartmuseum = "harbor-chartmuseum";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Clair</b> service.
        /// </summary>
        public static string HarborClair = "harbor-clair";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Core</b> service.
        /// </summary>
        public static string HarborCore = "harbor-core";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Jobservice</b> service.
        /// </summary>
        public static string HarborJobservice = "harbor-jobservice";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Notary Server</b> service.
        /// </summary>
        public static string HarborNotaryServer = "harbor-notary-server";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Notary Signer</b> service.
        /// </summary>
        public static string HarborNotarySigner = "harbor-notary-signer";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Portal</b> service.
        /// </summary>
        public static string HarborPortal = "harbor-portal";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Registry</b> service.
        /// </summary>
        public static string HarborRegistry = "harbor-registry";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Harbor Redis</b> service.
        /// </summary>
        public static string HarborRedis = "harbor-redis";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Istio Proxy</b> service.
        /// </summary>
        public static string IstioProxy = "istio-proxy";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Istio Ingress Gateway</b> service.
        /// </summary>
        public static string IstioIngressGateway = "istio-ingressgateway";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Jaeger</b> service.
        /// </summary>
        public static string Jaeger = "jaeger";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Kubernetes Dashboard</b> service.
        /// </summary>
        public static string KubernetesDashboard = "kubernetes-dashboard";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Kaili</b> service.
        /// </summary>
        public static string Kiali = "kaili";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Loki</b> service.
        /// </summary>
        public static string Loki = "loki";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Metrics-Server</b> service.
        /// </summary>
        public static string MetricsServer = "metrics-server";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Minio</b> service.
        /// </summary>
        public static string Minio = "minio";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>neon-cluster-operator</b> service.
        /// </summary>
        public static string NeonClusterOperator = "neon-cluster-operator";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS Admission Server</b> service.
        /// </summary>
        public static string OpenEbsAdmissionServer = "openebs-admission-server";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS API Server</b> service.
        /// </summary>
        public static string OpenEbsApiServer = "openebs-api-server";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS cStor Admission Server</b> service.
        /// </summary>
        public static string OpenEbsCstorAdmissionServer = "openebs-cstor-admission-server";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS cStor CSI Controller</b> service.
        /// </summary>
        public static string OpenEbsCstorCsiController = "openebs-cstor-csi-controller";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS cStor CSI Node</b> service.
        /// </summary>
        public static string OpenEbsCstorCsiNode = "openebs-cstor-csi-node";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS cStor CSPC Operator</b> service.
        /// </summary>
        public static string OpenEbsCstorCspcOperator = "openebs-cstor-cspc-operator";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS cStor CVC Operator</b> service.
        /// </summary>
        public static string OpenEbsCstorCvcOperator = "openebs-cstor-cvc-operator";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS Local PV Provisioner</b> service.
        /// </summary>
        public static string OpenEbsLocalPvProvisioner = "openebs-localpv-provisioner";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS Node Disk Manager Operator</b> service.
        /// </summary>
        public static string OpenEbsNdmOperator = "openebs-ndm-operator";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS Node Disk Manager</b> service.
        /// </summary>
        public static string OpenEbsNdm = "openebs-ndm";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS API Server</b> service.
        /// </summary>
        public static string OpenEbsProvisioner = "openebs-provisioner";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS Snapshot Operator</b> service.
        /// </summary>
        public static string OpenEbsSnapshotOperator = "openebs-snapshot-operator";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>OpenEBS Snapshot Webhook</b> service.
        /// </summary>
        public static string OpenEbsWebhook = "openebs-webhook";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Prometheus</b> service.
        /// </summary>
        public static string Prometheus = "prometheus";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Prometheus Operator</b> service.
        /// </summary>
        public static string PrometheusOperator = "prometheus-operator";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>PromTail</b> service.
        /// </summary>
        public static string Promtail = "promtail";

        /// <summary>
        /// Identifies the neonKUBE cluster's <b>Redis HA</b> service.
        /// </summary>
        public static string RedisHA = "redis-ha";

        //---------------------------------------------------------------------
        // Instance members

        private Dictionary<string, KubeServiceAdvice>   services   = new Dictionary<string, KubeServiceAdvice>(StringComparer.CurrentCultureIgnoreCase);
        private bool                                    isReadOnly = false;

        /// <summary>
        /// Constructs an instance by initialize empty <see cref="KubeServiceAdvice"/> instances
        /// for each cluster service defined above.
        /// </summary>
        public KubeClusterAdvice()
        {
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
                    throw new InvalidOperationException($"[{nameof(KubeClusterAdvice)}] cannot be made read/write after being set to read-only.");
                }

                isReadOnly = value;

                foreach (var serviceAdvice in services.Values)
                {
                    serviceAdvice.IsReadOnly = value;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="KubeServiceAdvice"/> for the specified service.
        /// </summary>
        /// <param name="identity">Identifies the service (one of the constants defined by this class).</param>
        /// <returns>The <see cref="KubeServiceAdvice"/> instance for the service.</returns>
        /// <exception cref="KeyNotFoundException">Thrown when there's no advice for the service.</exception>
        public KubeServiceAdvice GetServiceAdvice(string identity)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(identity));

            return services[identity];
        }

        /// <summary>
        /// Adds the <see cref="KubeServiceAdvice"/> for the specified service.
        /// </summary>
        /// <param name="identity">Identifies the service (one of the constants defined by this class).</param>
        /// <param name="advice">The <see cref="KubeServiceAdvice"/> instance for the service</param>
        public void AddServiceAdvice(string identity, KubeServiceAdvice advice)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(identity));
            Covenant.Requires<ArgumentNullException>(advice != null);

            services.Add(identity, advice);
        }
    }
}
