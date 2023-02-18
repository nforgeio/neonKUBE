//-----------------------------------------------------------------------------
// FILE:	    DeploymentWebhook.cs
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

using k8s;
using k8s.Models;

using Quartz.Logging;

namespace NeonClusterOperator
{
    /// <summary>
    /// Webhook to set istio injection on neon deployments.
    /// </summary>
    [Webhook(
        name: "deployment-policy.neonkube.io",
        admissionReviewVersions: "v1",
        failurePolicy: "Ignore")]
    [WebhookRule(
        apiGroups: V1Deployment.KubeGroup,
        apiVersions: V1Deployment.KubeApiVersion, 
        operations: AdmissionOperations.Create | AdmissionOperations.Update, 
        resources: V1Deployment.KubePluralName,
        scope: "*")]
    public class DeploymentWebhook : IMutatingWebhook<V1Deployment>
    {
        private ILogger<IMutatingWebhook<V1Deployment>> logger { get; set; }

        private bool modified = false;
        private readonly Service service;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="service"></param>
        public DeploymentWebhook(
            ILogger<IMutatingWebhook<V1Deployment>> logger,
            Service service)
            : base()
        {
            this.logger = logger;
            this.service = service;
        }

        /// <inheritdoc/>
        public async Task<MutationResult> CreateAsync(V1Deployment entity, bool dryRun)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger?.LogInformationEx(() => $"Received request for deployment {entity.Namespace()}/{entity.Name()}");

                if (!entity.Metadata.Namespace().StartsWith("neon-"))
                {
                    logger?.LogInformationEx(() => $"Deployment not in neon- namespace.");

                    return MutationResult.NoChanges();
                }

                CheckSidecarInjection(entity);

                if (modified)
                {
                    return await Task.FromResult(MutationResult.Modified(entity));
                }

                return await Task.FromResult(MutationResult.NoChanges());
            }
        }

        /// <inheritdoc/>
        public async Task<MutationResult> UpdateAsync(V1Deployment entity, V1Deployment oldEntity, bool dryRun)
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger?.LogInformationEx(() => $"Received request for deployment {entity.Namespace()}/{entity.Name()}");

                if (!entity.Metadata.Namespace().StartsWith("neon-"))
                {
                    logger?.LogInformationEx(() => $"Deployment not in neon- namespace.");

                    return MutationResult.NoChanges();
                }

                CheckSidecarInjection(entity);

                if (modified)
                {
                    return await Task.FromResult(MutationResult.Modified(entity));
                }

                return await Task.FromResult(MutationResult.NoChanges());
            }
        }

        private void CheckSidecarInjection(V1Deployment entity)
        {
            if (service.ClusterInfo.FeatureOptions.ServiceMesh)
            {
                return;
            }

            try
            {
                if (entity.Metadata.EnsureLabels().ContainsKey("goharbor.io/operator-version"))
                {
                    if (entity.Spec.Template.Metadata.EnsureAnnotations().TryAdd("sidecar.istio.io/inject", "false"))
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