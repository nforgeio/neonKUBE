//-----------------------------------------------------------------------------
// FILE:        NeonSsoCallbackUrlController.cs
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
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.Resources.Cluster;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Finalizers;
using Neon.Operator.Rbac;
using Neon.Operator.Util;
using Neon.Tasks;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Configures Neon SSO using <see cref="V1NeonSsoCallbackUrl"/>.
    /// </para>
    /// </summary>
    [RbacRule<V1NeonSsoCallbackUrl>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [ResourceController(MaxConcurrentReconciles = 1)]
    public class NeonSsoCallbackUrlController : ResourceControllerBase<V1NeonSsoCallbackUrl>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<NeonSsoCallbackUrlController>();

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonSsoCallbackUrlController()
        {
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                             k8s;
        private readonly IFinalizerManager<V1NeonSsoCallbackUrl> finalizerManager;
        private readonly ILogger<NeonSsoCallbackUrlController>   logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonSsoCallbackUrlController(IKubernetes k8s,
            IFinalizerManager<V1NeonSsoCallbackUrl>     manager,
            ILogger<NeonSsoCallbackUrlController>       logger)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(manager != null, nameof(manager));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));

            this.k8s              = k8s;
            this.finalizerManager = manager;
            this.logger           = logger;
        }

        /// <inheritdoc/>
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonSsoCallbackUrl resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                var patch = OperatorHelper.CreatePatch<V1NeonSsoCallbackUrl>();

                if (resource.Status == null)
                {
                    patch.Replace(path => path.Status, new V1SsoCallbackUrlStatus());
                }
                patch.Replace(path => path.Status.State, "reconciling");
                await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonSsoCallbackUrl>(OperatorHelper.ToV1Patch<V1NeonSsoCallbackUrl>(patch), resource.Name());

                await UpsertAsync(resource);

                patch.Replace(path => path.Status, new V1SsoCallbackUrlStatus());
                patch.Replace(path => path.Status.State, "reconciled");
                patch.Replace(path => path.Status.LastAppliedSsoClient, resource.Spec.SsoClient);
                patch.Replace(path => path.Status.LastAppliedUrl, resource.Spec.Url);
                await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonSsoCallbackUrl>(OperatorHelper.ToV1Patch<V1NeonSsoCallbackUrl>(patch), resource.Name());

                logger?.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public override async Task DeletedAsync(V1NeonSsoCallbackUrl resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        private async Task UpsertAsync(V1NeonSsoCallbackUrl resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                if (resource.Spec.SsoClient != resource.Status?.LastAppliedSsoClient
                    && resource.Status?.LastAppliedSsoClient != null)
                {
                    var oldSsoClient = await k8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonSsoClient>(resource.Status.LastAppliedSsoClient);

                    if (oldSsoClient.Spec.RedirectUris.Contains(resource.Status.LastAppliedUrl))
                    {
                        oldSsoClient.Spec.RedirectUris.Remove(resource.Status.LastAppliedUrl);
                    }
                    
                    await k8s.CustomObjects.ReplaceClusterCustomObjectAsync(oldSsoClient, oldSsoClient.Name());
                }

                var ssoClient = await k8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonSsoClient>(resource.Spec.SsoClient);

                if (ssoClient.Spec.RedirectUris.Contains(resource.Status.LastAppliedUrl))
                {
                    ssoClient.Spec.RedirectUris.Remove(resource.Status.LastAppliedUrl);
                }

                if (!ssoClient.Spec.RedirectUris.Contains(resource.Spec.Url))
                {
                    ssoClient.Spec.RedirectUris.Add(resource.Spec.Url);
                }

                await k8s.CustomObjects.ReplaceClusterCustomObjectAsync(ssoClient, ssoClient.Name());
            }
        }
    }
}
