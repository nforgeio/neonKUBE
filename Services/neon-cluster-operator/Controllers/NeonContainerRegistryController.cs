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
using Neon.Kube.Operator;
using Neon.Kube.ResourceDefinitions;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using Dex;

using k8s;
using k8s.Autorest;
using k8s.Models;

using KubeOps.Operator.Controller;
using KubeOps.Operator.Finalizer;
using KubeOps.Operator.Rbac;

using Newtonsoft.Json;

using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using Prometheus;
using Grpc.Net.Client;
using Renci.SshNet.Common;
using IdentityModel;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Configures Neon SSO using <see cref="V1NeonContainerRegistry"/>.
    /// </para>
    /// </summary>
    [EntityRbac(typeof(V1NeonContainerRegistry), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class NeonContainerRegistryController : IOperatorController<V1NeonContainerRegistry>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<NeonContainerRegistryController>();

        private static ResourceManager<V1NeonContainerRegistry, NeonContainerRegistryController> resourceManager;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonContainerRegistryController()
        {
        }

        /// <summary>
        /// Starts the controller.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client to use.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task StartAsync(
            IKubernetes k8s,
            IServiceProvider serviceProvider)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            // Load the configuration settings.

            var leaderConfig =
                new LeaderElectionConfig(
                    k8s,
                    @namespace: KubeNamespace.NeonSystem,
                    leaseName: $"{Program.Service.Name}.containerregistries",
                    identity: Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_promoted", "Leader promotions"),
                    demotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_newLeader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                ErrorMaxRetryCount = 3,
                ErrorMaxRequeueInterval = TimeSpan.FromSeconds(10),
                ErrorMinRequeueInterval = TimeSpan.FromSeconds(10),
                IdleCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_idle", "IDLE events processed."),
                ReconcileCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_idle", "RECONCILE events processed."),
                DeleteCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_idle", "DELETED events processed."),
                StatusModifyCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_idle", "STATUS-MODIFY events processed."),
                FinalizeCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_finalize", "FINALIZE events processed."),
                IdleErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_idle_error", "Failed Clustercontainerregistries IDLE event processing."),
                ReconcileErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_reconcile_error", "Failed Clustercontainerregistries RECONCILE event processing."),
                DeleteErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_delete_error", "Failed Clustercontainerregistries DELETE event processing."),
                StatusModifyErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_statusmodify_error", "Failed Clustercontainerregistries STATUS-MODIFY events processing."),
                FinalizeErrorCounter     = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}containerregistries_finalize_error", "Failed NodeTask FINALIZE events processing.")
            };

            resourceManager = new ResourceManager<V1NeonContainerRegistry, NeonContainerRegistryController>(
                k8s,
                options: options,
                leaderConfig: leaderConfig,
                serviceProvider: serviceProvider);

            await resourceManager.StartAsync();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;
        private readonly Neon.Kube.Operator.IFinalizerManager<V1NeonContainerRegistry> finalizerManager;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonContainerRegistryController(
            IKubernetes k8s,
            Neon.Kube.Operator.IFinalizerManager<V1NeonContainerRegistry> manager)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(manager != null, nameof(manager));

            this.k8s = k8s;
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
                await k8s.ReadClusterCustomObjectAsync<V1NeonContainerRegistry>("registry.neon.local");
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

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return null;
                }

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

                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return;
                }

                if (resource.Name() == "registry.neon.local")
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

                var secret = await k8s.ReadNamespacedSecretAsync("glauth-users", KubeNamespace.NeonSystem);
                var rootUser = NeonHelper.YamlDeserialize<GlauthUser>(Encoding.UTF8.GetString(secret.Data["root"]));

                var registry = new V1NeonContainerRegistry()
                {
                    Metadata = new V1ObjectMeta()
                    {
                        Name = "registry.neon.local"
                    },
                    Spec = new V1NeonContainerRegistry.RegistrySpec()
                    {
                        Blocked = false,
                        Insecure = true,
                        Location = "registry.neon.local",
                        Password = rootUser.Password,
                        Prefix = "registry.neon.local",
                        SearchOrder = -1,
                        Username = rootUser.Name
                    }
                };

                await k8s.UpsertClusterCustomObjectAsync(registry, registry.Name());
            }
        }
    }
}
