//-----------------------------------------------------------------------------
// FILE:	    PodWebhook.cs
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
    /// Webhook to set priority classes on neon pods.
    /// </summary>
    [Webhook(
        name: "pod-policy.neonkube.io",
        serviceName: KubeService.NeonClusterOperator,
        @namespace: KubeNamespace.NeonSystem,
        certificate: $"{KubeNamespace.NeonSystem}/{KubeService.NeonClusterOperator}",
        admissionReviewVersions: "v1",
        failurePolicy: "Ignore")]
    [WebhookRule(
        apiGroups: V1Pod.KubeGroup,
        apiVersions: V1Pod.KubeApiVersion, 
        operations: AdmissionOperations.Create | AdmissionOperations.Update, 
        resources: V1Pod.KubePluralName,
        scope: "*")]
    public class PodWebhook : IMutatingWebhook<V1Pod>
    {
        /// <inheritdoc/>
        public ILogger Logger { get; set; }

        private bool modified = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public PodWebhook()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"></param>
        public PodWebhook(ILogger logger)
            : base()
        {
            this.Logger = logger;
        }

        /// <inheritdoc/>
        public async Task<MutationResult> CreateAsync(V1Pod entity, bool dryRun)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Logger?.LogInformationEx(() => $"Received request for pod {entity.Namespace()}/{entity.Name()}");

                if (!entity.Metadata.Namespace().StartsWith("neon-"))
                {
                    Logger?.LogInformationEx(() => $"Pod not in neon- namespace.");

                    return MutationResult.NoChanges();
                }

                CheckPriorityClass(entity);

                if (modified)
                {
                    return await Task.FromResult(MutationResult.Modified(entity));
                }

                return await Task.FromResult(MutationResult.NoChanges());
            }
        }

        /// <inheritdoc/>
        public async Task<MutationResult> UpdateAsync(V1Pod entity, V1Pod oldEntity, bool dryRun)
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Logger?.LogInformationEx(() => $"Received request for pod {entity.Namespace()}/{entity.Name()}");

                if (!entity.Metadata.Namespace().StartsWith("neon-"))
                {
                    Logger?.LogInformationEx(() => $"Pod not in neon- namespace.");

                    return MutationResult.NoChanges();
                }

                CheckPriorityClass(entity);

                if (modified)
                {
                    return await Task.FromResult(MutationResult.Modified(entity));
                }

                return await Task.FromResult(MutationResult.NoChanges());
            }
        }

        private void CheckPriorityClass(V1Pod entity)
        {
            try
            {
                if (string.IsNullOrEmpty(entity.Spec.PriorityClassName)
                    || entity.Spec.PriorityClassName == PriorityClass.UserMedium.Name)
                {
                    modified = true;

                    if (entity.Metadata.Labels != null
                        && entity.Metadata.Labels.ContainsKey("goharbor.io/operator-version"))
                    {
                        Logger?.LogInformationEx(() => $"Setting priority class for harbor pod.");

                        entity.Spec.PriorityClassName = PriorityClass.NeonStorage.Name;
                        entity.Spec.Priority = null;
                    }
                    else
                    {
                        Logger?.LogInformationEx(() => $"Setting default priority class to neon-min.");

                        entity.Spec.PriorityClassName = PriorityClass.NeonMin.Name;
                        entity.Spec.Priority = null;
                    }
                }
            }
            catch (Exception e) 
            {
                Logger?.LogErrorEx(e);
            }
        }
    }
}