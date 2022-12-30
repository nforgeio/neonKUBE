//-----------------------------------------------------------------------------
// FILE:	    CheckNeonDesktopCertificate.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Clients;
using Neon.Kube.Resources;

using k8s;
using k8s.Models;

using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

using Quartz;
using System.Security.Cryptography.X509Certificates;

namespace NeonClusterOperator
{
    /// <summary>
    /// Handles updating of the desktop.neoncluster.io certificate.
    /// </summary>
    public class CheckNeonDesktopCertificate : CronJob, IJob
    {
        private static Random random   = new Random();
        private static bool   updating = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public CheckNeonDesktopCertificate()
            : base(typeof(CheckNeonDesktopCertificate))
        {
        }
        
        /// <inheritdoc/>
        public async Task Execute(IJobExecutionContext context)
        {
            Covenant.Requires<ArgumentNullException>(context != null, nameof(context));

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("execute", attributes => attributes.Add("cronjob", nameof(CheckNeonDesktopCertificate)));

                if (updating)
                {
                    return;
                }

                try
                {
                    var dataMap       = context.MergedJobDataMap;
                    var k8s           = (IKubernetes)dataMap["Kubernetes"];
                    var headendClient = (HeadendClient)dataMap["HeadendClient"];
                    var ingressSecret = await k8s.CoreV1.ReadNamespacedSecretAsync("neon-cluster-certificate", KubeNamespace.NeonIngress);
                    var systemSecret  = await k8s.CoreV1.ReadNamespacedSecretAsync("neon-cluster-certificate", KubeNamespace.NeonSystem);

                    var ingressCertificate = X509Certificate2.CreateFromPem(
                        Encoding.UTF8.GetString(ingressSecret.Data["tls.crt"]),
                        Encoding.UTF8.GetString(ingressSecret.Data["tls.key"]));

                    var systemCertificate = X509Certificate2.CreateFromPem(
                        Encoding.UTF8.GetString(systemSecret.Data["tls.crt"]),
                        Encoding.UTF8.GetString(systemSecret.Data["tls.key"]));

                    if (ingressCertificate.NotAfter.CompareTo(DateTime.Now.AddDays(30)) < 0 || systemCertificate.NotAfter.CompareTo(DateTime.Now.AddDays(30)) < 0)
                    {
                        updating = true;

                        await Task.Delay(TimeSpan.FromMinutes(random.Next(90)));

                        var cert = await headendClient.NeonDesktop.GetNeonDesktopCertificateAsync();

                        ingressSecret.Data = cert;
                        systemSecret.Data  = cert;

                        await k8s.CoreV1.ReplaceNamespacedSecretAsync(ingressSecret, ingressSecret.Name(), ingressSecret.Namespace());
                        await k8s.CoreV1.ReplaceNamespacedSecretAsync(systemSecret, systemSecret.Name(), systemSecret.Namespace());
                    }
                }
                finally
                {
                    updating = false;
                }
            }
        }
    }
}
