//-----------------------------------------------------------------------------
// FILE:        ClusterCertificateRenewalJob.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Operator.Util;
using Neon.Kube.Resources.Cluster;
using Neon.Tasks;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;

namespace NeonClusterOperator
{
    /// <summary>
    /// Handles renewal of the Kubernetes root certificate.
    /// </summary>
    [DisallowConcurrentExecution]
    public class ClusterCertificateRenewalJob : CronJob, IJob
    {
        private static readonly ILogger logger = TelemetryHub.CreateLogger<ClusterCertificateRenewalJob>();

        private static Random random   = new Random();

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClusterCertificateRenewalJob()
            : base(typeof(ClusterCertificateRenewalJob))
        {
        }
        
        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));


            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("execute", attributes => attributes.Add("cronjob", nameof(ClusterCertificateRenewalJob)));

                try
                {
                    var dataMap       = context.MergedJobDataMap;
                    var k8s           = (IKubernetes)dataMap["Kubernetes"];
                    var headendClient = (HeadendClient)dataMap["HeadendClient"];
                    var clusterInfo   = (ClusterInfo)dataMap["ClusterInfo"];
                    var ingressSecret = await k8s.CoreV1.ReadNamespacedSecretAsync("neon-cluster-certificate", KubeNamespace.IstioSystem);
                    var systemSecret  = await k8s.CoreV1.ReadNamespacedSecretAsync("neon-cluster-certificate", KubeNamespace.NeonSystem);

                    var ingressCertificate = X509Certificate2.CreateFromPem(
                        Encoding.UTF8.GetString(ingressSecret.Data["tls.crt"]),
                        Encoding.UTF8.GetString(ingressSecret.Data["tls.key"]));

                    var systemCertificate = X509Certificate2.CreateFromPem(
                        Encoding.UTF8.GetString(systemSecret.Data["tls.crt"]),
                        Encoding.UTF8.GetString(systemSecret.Data["tls.key"]));

                    if (ingressCertificate.NotAfter.CompareTo(DateTime.Now.AddDays(30)) < 0 || systemCertificate.NotAfter.CompareTo(DateTime.Now.AddDays(30)) < 0)
                    {
                        await Task.Delay(TimeSpan.FromMinutes(random.Next(90)));

                        IDictionary<string, byte[]> cert;
                        if (clusterInfo.IsDesktop)
                        {
                            cert = await headendClient.NeonDesktop.GetNeonDesktopCertificateAsync();
                        }
                        else
                        {
                            cert = await headendClient.Cluster.GetCertificateAsync(clusterInfo.ClusterId);
                        }

                        ingressSecret.Data = cert;
                        systemSecret.Data  = cert;

                        await k8s.CoreV1.ReplaceNamespacedSecretAsync(ingressSecret, ingressSecret.Name(), ingressSecret.Namespace());
                        await k8s.CoreV1.ReplaceNamespacedSecretAsync(systemSecret, systemSecret.Name(), systemSecret.Namespace());
                    }

                    var clusterOperator = await k8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonClusterJobs>(KubeService.NeonClusterOperator);
                    var patch           = OperatorHelper.CreatePatch<V1NeonClusterJobs>();

                    if (clusterOperator.Status == null)
                    {
                        patch.Replace(path => path.Status, new V1NeonClusterJobs.NeonClusterJobsStatus());
                    }

                    patch.Replace(path => path.Status.ClusterCertificateRenewal, new V1NeonClusterJobs.JobStatus());
                    patch.Replace(path => path.Status.ClusterCertificateRenewal.LastCompleted, DateTime.UtcNow);

                    await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonClusterJobs>(
                        patch: OperatorHelper.ToV1Patch<V1NeonClusterJobs>(patch),
                        name: clusterOperator.Name());
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                }
            }
        }
    }
}
