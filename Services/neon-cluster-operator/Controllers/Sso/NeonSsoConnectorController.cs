//-----------------------------------------------------------------------------
// FILE:	    NeonSsoConnectorController.cs
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
using System.Threading;
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
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Kube.Resources.Dex;
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

using Grpc.Core;
using Grpc.Net.Client;

using Prometheus;
using System.IdentityModel.Tokens.Jwt;
using YamlDotNet.Serialization;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Configures Neon SSO using.
    /// </para>
    /// </summary>
    public class NeonSsoConnectorController : IOperatorController<V1NeonSsoConnector>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<NeonSsoConnectorController>();
        private static ResourceManager<V1NeonSsoConnector, NeonSsoConnectorController> resourceManager;

        private Dex.Dex.DexClient dexClient;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonSsoConnectorController()
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
                    leaseName: $"{Program.Service.Name}.ssoconnector",
                    identity: Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_promoted", "Leader promotions"),
                    demotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_new_leader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                ErrorMaxRetryCount       = int.MaxValue,
                ErrorMaxRequeueInterval  = TimeSpan.FromMinutes(10),
                ErrorMinRequeueInterval  = TimeSpan.FromSeconds(5),
                IdleCounter              = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_idle", "IDLE events processed."),
                ReconcileCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_idle", "RECONCILE events processed."),
                DeleteCounter            = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_idle", "DELETED events processed."),
                StatusModifyCounter      = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_idle", "STATUS-MODIFY events processed."),
                FinalizeCounter          = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_finalize", "FINALIZE events processed."),
                IdleErrorCounter         = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_idle_error", "Failed IDLE event processing."),
                ReconcileErrorCounter    = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_reconcile_error", "Failed RECONCILE event processing."),
                DeleteErrorCounter       = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_delete_error", "Failed DELETE event processing."),
                StatusModifyErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_statusmodify_error", "Failed STATUS-MODIFY events processing."),
                FinalizeErrorCounter     = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoconnectors_finalize_error", "Failed FINALIZE events processing.")
            };

            resourceManager = new ResourceManager<V1NeonSsoConnector, NeonSsoConnectorController>(
                k8s,
                options: options,
                leaderConfig: leaderConfig,
                serviceProvider: serviceProvider);

            await resourceManager.StartAsync();
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;
        private readonly IFinalizerManager<V1NeonSsoConnector> finalizerManager;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonSsoConnectorController(IKubernetes k8s,
            IFinalizerManager<V1NeonSsoConnector> manager,
            Dex.Dex.DexClient dexClient)
        {
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(manager != null, nameof(manager));
            Covenant.Requires(dexClient != null, nameof(dexClient));

            this.k8s              = k8s;
            this.finalizerManager = manager;
            this.dexClient        = dexClient;
        }

        /// <summary>
        /// Called periodically to allow the operator to perform global events.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task IdleAsync()
        {
            await SyncContext.Clear;

            log.LogInformationEx("[IDLE]");
        }

        /// <inheritdoc/>
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonSsoConnector resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return null;
                }

                await finalizerManager.RegisterAllFinalizersAsync(resource);

                var configMap = await k8s.CoreV1.ReadNamespacedConfigMapAsync("neon-sso-dex", KubeNamespace.NeonSystem);
                var dexConfig = NeonHelper.YamlDeserializeViaJson<DexConfig>(configMap.Data["config.yaml"]);

                if (dexConfig.Connectors == null)
                {
                    dexConfig.Connectors = new List<IV1DexConnector>();
                }
                else if (dexConfig.Connectors.Any(connector => connector.Id == resource.Spec.Id))
                {
                    var connector = dexConfig.Connectors.Where(connector => connector.Id == resource.Spec.Id).Single();

                    dexConfig.Connectors.Remove(connector);
                }

                dexConfig.Connectors.Add(resource.Spec);

                var yamlString = NeonHelper.YamlSerialize(dexConfig);
                configMap.Data["config.yaml"] = yamlString;

                await k8s.CoreV1.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Namespace());

                log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1NeonSsoConnector resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return;
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
    }
}
