//-----------------------------------------------------------------------------
// FILE:	    SsoConnectorValidatingWebhook.cs
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
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;

using k8s;
using k8s.Models;

using Quartz.Logging;
using Neon.Tasks;

namespace NeonClusterOperator
{
    /// <summary>
    /// Webhook validate SSO connectors.
    /// </summary>
    [Webhook(
        name: "connectors.sso.neonkube.io",
        serviceName: KubeService.NeonClusterOperator,
        @namespace: KubeNamespace.NeonSystem,
        certificate: $"{KubeNamespace.NeonSystem}/{KubeService.NeonClusterOperator}",
        admissionReviewVersions: "v1",
        failurePolicy: "Fail")]
    [WebhookRule(
        apiGroups: V1NeonSsoConnector.KubeGroup,
        apiVersions: V1NeonSsoConnector.KubeApiVersion,
        operations: AdmissionOperations.Create | AdmissionOperations.Update,
        resources: V1NeonSsoConnector.KubePlural,
        scope: "*")]
    public class NeonSsoConnectorValidatingWebhook : IValidatingWebhook<V1NeonSsoConnector>
    {
        /// <inheritdoc/>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonSsoConnectorValidatingWebhook()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger"></param>
        public NeonSsoConnectorValidatingWebhook(ILogger logger)
            : base()
        {
            this.Logger = logger;
        }

        /// <inheritdoc/>
        public async Task<ValidationResult> CreateAsync(V1NeonSsoConnector entity, bool dryRun)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Logger?.LogInformationEx(() => $"Received request for V1NeonSsoConnector {entity.Name()}");

                if (entity.Metadata.Name != entity.Spec.Id)
                {
                    Logger?.LogInformationEx(() => $"Entity name must match connector ID. [{entity.Namespace()}/{entity.Name()}]");

                    return ValidationResult.Fail(statusCode: 500, statusMessage: "Entity name must match connector ID.");
                }

                return ValidationResult.Success();
            }
        }

        /// <inheritdoc/>
        public async Task<ValidationResult> UpdateAsync(V1NeonSsoConnector entity, V1NeonSsoConnector oldEntity, bool dryRun)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Logger?.LogInformationEx(() => $"Received request for V1NeonSsoConnector {entity.Namespace()}/{entity.Name()}");

                if (entity.Metadata.Name != entity.Spec.Id)
                {
                    Logger?.LogInformationEx(() => $"Entity name must match connector ID. [{entity.Namespace()}/{entity.Name()}]");

                    return ValidationResult.Fail(statusCode: 500, statusMessage: "Entity name must match connector ID.");
                }

                return ValidationResult.Success();
            }
        }
    }
}