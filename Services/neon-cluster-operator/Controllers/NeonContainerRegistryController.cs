//-----------------------------------------------------------------------------
// FILE:	    NeonContainerRegistryController.cs
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
using Neon.Kube.Glauth;
using Neon.Kube.Operator;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using Dex;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Configures Neon SSO using <see cref="V1NeonContainerRegistry"/>.
    /// </para>
    /// </summary>
    public class NeonContainerRegistryController : IOperatorController<V1NeonContainerRegistry>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<NeonContainerRegistryController>();

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonContainerRegistryController()
        {
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;
        private readonly IFinalizerManager<V1NeonContainerRegistry> finalizerManager;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonContainerRegistryController(
            IKubernetes k8s,
            IFinalizerManager<V1NeonContainerRegistry> manager)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(manager != null, nameof(manager));

            this.k8s              = k8s;
            this.finalizerManager = manager;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx("[IDLE]");

            try
            {
                await k8s.CustomObjects.ReadClusterCustomObjectAsync<V1NeonContainerRegistry>(KubeConst.LocalClusterRegistryProject);
            }
            catch (Exception e)
            {
                log.LogErrorEx(e);
                await CreateNeonLocalRegistryAsync();
            }
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonContainerRegistry resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonContainerRegistry)));

                await finalizerManager.RegisterAllFinalizersAsync(resource);

                log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1NeonContainerRegistry resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("delete", attributes => attributes.Add("customresource", nameof(V1NeonContainerRegistry)));

                if (resource.Name() == KubeConst.LocalClusterRegistryProject)
                {
                    await CreateNeonLocalRegistryAsync();
                }

                log.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        /// <inheritdoc/>
        public async Task OnPromotionAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"PROMOTED");
        }

        /// <inheritdoc/>
        public async Task OnDemotionAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"DEMOTED");
        }

        /// <inheritdoc/>
        public async Task OnNewLeaderAsync(string identity)
        {
            await SyncContext.Clear;

            log.LogInformationEx(() => $"NEW LEADER: {identity}");
        }

        private async Task CreateNeonLocalRegistryAsync()
        {
            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                log.LogInformationEx(() => $"Upserting registry: [registry.neon.local]");

                // todo(marcusbooyah): make this use robot accounts.

                var secret   = await k8s.CoreV1.ReadNamespacedSecretAsync("glauth-users", KubeNamespace.NeonSystem);
                var rootUser = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(secret.Data["root"]));

                var registry = new V1NeonContainerRegistry()
                {
                    Metadata = new V1ObjectMeta()
                    {
                        Name = KubeConst.LocalClusterRegistryProject
                    },
                    Spec = new V1NeonContainerRegistry.RegistrySpec()
                    {
                        Blocked     = false,
                        Insecure    = true,
                        Location    = KubeConst.LocalClusterRegistryHostName,
                        Password    = rootUser.Password,
                        Prefix      = KubeConst.LocalClusterRegistryHostName,
                        SearchOrder = -1,
                        Username    = rootUser.Name
                    }
                };

                await k8s.CustomObjects.UpsertClusterCustomObjectAsync(registry, registry.Name());
            }
        }
    }
}
