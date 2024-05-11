//-----------------------------------------------------------------------------
// FILE:        PodWebhook.cs
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
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Operator;
using Neon.Operator.Webhooks;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Quartz.Logging;

namespace NeonClusterOperator
{
    /// <summary>
    /// Webhook that sets priority classes for NeonKUBE pods.
    /// </summary>
    [Webhook(
        name:                    "pod-policy.neonkube.io",
        admissionReviewVersions: "v1",
        FailurePolicy            = FailurePolicy.Ignore)]
    [WebhookRule(
        apiGroups:   V1Pod.KubeGroup,
        apiVersions: V1Pod.KubeApiVersion, 
        operations:  AdmissionOperations.Create, 
        resources:   V1Pod.KubePluralName,
        scope:       "*")]
    public class PodWebhook : MutatingWebhookBase<V1Pod>
    {
        private ILogger<PodWebhook> logger { get; set; }

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
        /// <param name="logger">Optionally specifies the logger.</param>
        public PodWebhook(
            ILogger<PodWebhook> logger = null)
            : base()
        {
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override async Task<MutationResult> CreateAsync(V1Pod pod, bool dryRun, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                try
                {
                    var @namespace = pod.Namespace();
                    var name       = pod.Name() ?? pod.EnsureMetadata().GenerateName;

                    logger?.LogInformationEx(() => $"Received request for pod {@namespace}/{name}");

                    if (!pod.EnsureMetadata().NamespaceProperty.StartsWith("neon-"))
                    {
                        logger?.LogInformationEx(() => $"Pod not in a NeonKUBE namespace.");

                        return MutationResult.NoChanges();
                    }

                    CheckPriorityClass(pod);

                    if (modified)
                    {
                        return MutationResult.Modified(pod);
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
        /// Used to check whether a pod within a NeonKUBE namespace has a priority class
        /// assigned and sets the priority class and also <see cref="modified"/> when the
        /// pod has no priority class.  For harbor related pods, this sets the priority
        /// class to <see cref="PriorityClass.NeonStorage"/> or <see cref="PriorityClass.NeonMin"/>
        /// for all other pod types.
        /// </summary>
        /// <param name="pod">Specifies the target pod.</param>
        private void CheckPriorityClass(V1Pod pod)
        {
            using var activity = TelemetryHub.ActivitySource?.StartActivity();

            try
            {
                var @namespace = pod.Namespace();
                var name       = pod.Name() ?? pod.EnsureMetadata().GenerateName;

                if (string.IsNullOrEmpty(pod.Spec.PriorityClassName) || pod.Spec.PriorityClassName == PriorityClass.UserMedium.Name)
                {
                    modified = true;

                    if (pod.Metadata.Labels != null && pod.Metadata.Labels.ContainsKey("goharbor.io/operator-version"))
                    {
                        logger?.LogInformationEx(() => $"Setting priority class to [{PriorityClass.NeonStorage.Name}] for pod/{@namespace}/{name}");

                        pod.Spec.PriorityClassName = PriorityClass.NeonStorage.Name;
                        pod.Spec.Priority          = null;
                    }
                    else if (pod.Metadata.NamespaceProperty == "neon-storage"
                                || pod.Metadata.Name.StartsWith("init-pvc-"))
                    {
                        logger?.LogInformationEx(() => $"Ignoring pod/{@namespace}/{name}");

                        return;
                    }
                    else
                    {
                        logger?.LogInformationEx(() => $"Setting priority class to [{PriorityClass.NeonMin.Name}] for pod/{@namespace}/{name}.");

                        pod.Spec.PriorityClassName = PriorityClass.NeonMin.Name;
                        pod.Spec.Priority          = null;
                    }
                }
                else
                {
                    logger?.LogInformationEx(() => $"Priority class exists: [{pod.Spec.PriorityClassName}] for pod/{@namespace}/{name}.");
                }
            }
            catch (Exception e) 
            {
                logger?.LogErrorEx(e);
            }
        }
    }
}
