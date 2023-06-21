//-----------------------------------------------------------------------------
// FILE:        DeploymentWebhook.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.Operator.Webhook;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Quartz.Logging;
using Octokit;

namespace NeonClusterOperator
{
    /// <summary>
    /// Webhook to set istio injection on NEONKUBE deployments.
    /// </summary>
    [Webhook(
        name:                    "deployment-policy.neonkube.io",
        admissionReviewVersions: "v1",
        failurePolicy:           "Ignore")]
    [WebhookRule(
        apiGroups:   V1Deployment.KubeGroup,
        apiVersions: V1Deployment.KubeApiVersion, 
        operations:  AdmissionOperations.Create | AdmissionOperations.Update, 
        resources:   V1Deployment.KubePluralName,
        scope:       "*")]
    public class DeploymentWebhook : IMutatingWebhook<V1Deployment>
    {
        private ILogger<IMutatingWebhook<V1Deployment>> logger { get; set; }
        private bool                                    modified = false;
        private readonly Service                        service;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="service">Specifies the parent neon-cluster-operator service.</param>
        /// <param name="logger">Optionally specifies a logger.</param>
        public DeploymentWebhook(
            Service                                 service,
            ILogger<IMutatingWebhook<V1Deployment>> logger = null)
            : base()
        {
            this.service = service;
            this.logger  = logger;
        }

        /// <inheritdoc/>
        public async Task<MutationResult> CreateAsync(V1Deployment deployment, bool dryRun)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                try
                {
                    logger?.LogInformationEx(() => $"Received request for deployment {deployment.Namespace()}/{deployment.Name()}");

                    if (!deployment.EnsureMetadata().NamespaceProperty.StartsWith("neon-"))
                    {
                        logger?.LogInformationEx(() => $"Deployment not in neon- namespace.");

                        return MutationResult.NoChanges();
                    }

                    InjectIstioSidecar(deployment);

                    if (modified)
                    {
                        return MutationResult.Modified(deployment);
                    }
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                }

                return MutationResult.NoChanges();
            }
        }

        /// <inheritdoc/>
        public async Task<MutationResult> UpdateAsync(V1Deployment deployment, V1Deployment oldDeployment, bool dryRun)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                try
                {
                    logger?.LogInformationEx(() => $"Received request for deployment {deployment.Namespace()}/{deployment.Name()}");

                    if (!deployment.EnsureMetadata().NamespaceProperty.StartsWith("neon-"))
                    {
                        logger?.LogInformationEx(() => $"Deployment not in neon- namespace.");

                        return MutationResult.NoChanges();
                    }

                    InjectIstioSidecar(deployment);

                    if (modified)
                    {
                        return MutationResult.Modified(deployment);
                    }
                }
                catch (Exception e) 
                {
                    logger?.LogErrorEx(e);
                }
                
                return MutationResult.NoChanges();
            }
        }

        /// <summary>
        /// Enables Istio service mesh sidecar injection for the deployment when the
        /// service mesg feature is enabled.
        /// </summary>
        /// <param name="deployment">Specifies the target deploy,ent.</param>
        private void InjectIstioSidecar(V1Deployment deployment)
        {
            Covenant.Requires<ArgumentNullException>(deployment != null, nameof(deployment));

            if (service.ClusterInfo.FeatureOptions.ServiceMesh)
            {
                return;
            }

            try
            {
                if (deployment.Metadata.EnsureLabels().ContainsKey("goharbor.io/operator-version"))
                {
                    if (deployment.Spec.Template.Metadata.EnsureAnnotations().TryAdd("sidecar.istio.io/inject", "false"))
                    {
                        modified = true;
                    }
                }
            }
            catch (Exception e) 
            {
                logger?.LogErrorEx(e);
            }
        }
    }
}
