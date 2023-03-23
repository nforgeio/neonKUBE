//-----------------------------------------------------------------------------
// FILE:	    NeonSsoCallbackUrlController.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.JsonPatch.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using JsonDiffPatch;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Oauth2Proxy;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Resources;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using Dex;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Util;
using Neon.Kube.Resources.Cluster;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Grpc.Core;
using Grpc.Net.Client;

using Prometheus;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Configures Neon SSO using <see cref="V1NeonSsoCallbackUrl"/>.
    /// </para>
    /// </summary>
    [RbacRule<V1NeonSsoCallbackUrl>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    public class NeonSsoCallbackUrlController : IResourceController<V1NeonSsoCallbackUrl>
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
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(manager != null, nameof(manager));
            Covenant.Requires(logger != null, nameof(logger));

            this.k8s              = k8s;
            this.finalizerManager = manager;
            this.logger           = logger;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            await SyncContext.Clear;

            logger?.LogInformationEx("[IDLE]");
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonSsoCallbackUrl resource)
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
        public async Task DeletedAsync(V1NeonSsoCallbackUrl resource)
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
