//-----------------------------------------------------------------------------
// FILE:	    NeonSsoClientController.cs
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

using Grpc.Core;
using Grpc.Net.Client;

using Prometheus;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Configures Neon SSO using <see cref="V1NeonSsoClient"/>.
    /// </para>
    /// </summary>
    [EntityRbac(typeof(V1NeonSsoClient), Verbs = RbacVerb.Get | RbacVerb.List | RbacVerb.Patch | RbacVerb.Watch | RbacVerb.Update)]
    public class NeonSsoClientController : IOperatorController<V1NeonSsoClient>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<NeonSsoClientController>();

        private static ResourceManager<V1NeonSsoClient, NeonSsoClientController> resourceManager;

        private static Dex.Dex.DexClient dexClient;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonSsoClientController()
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
                    leaseName: $"{Program.Service.Name}.ssoclient",
                    identity: Pod.Name,
                    promotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_promoted", "Leader promotions"),
                    demotionCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_demoted", "Leader demotions"),
                    newLeaderCounter: Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_new_leader", "Leadership changes"));

            var options = new ResourceManagerOptions()
            {
                ErrorMaxRetryCount = int.MaxValue,
                ErrorMaxRequeueInterval = TimeSpan.FromMinutes(10),
                ErrorMinRequeueInterval = TimeSpan.FromSeconds(5),
                IdleCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_idle", "IDLE events processed."),
                ReconcileCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_idle", "RECONCILE events processed."),
                DeleteCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_idle", "DELETED events processed."),
                StatusModifyCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_idle", "STATUS-MODIFY events processed."),
                FinalizeCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_finalize", "FINALIZE events processed."),
                IdleErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_idle_error", "Failed IDLE event processing."),
                ReconcileErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_reconcile_error", "Failed RECONCILE event processing."),
                DeleteErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_delete_error", "Failed DELETE event processing."),
                StatusModifyErrorCounter = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_statusmodify_error", "Failed STATUS-MODIFY events processing."),
                FinalizeErrorCounter     = Metrics.CreateCounter($"{Program.Service.MetricsPrefix}ssoclients_finalize_error", "Failed FINALIZE events processing.")
            };

            resourceManager = new ResourceManager<V1NeonSsoClient, NeonSsoClientController>(
                k8s,
                options: options,
                leaderConfig: leaderConfig,
                serviceProvider: serviceProvider);

            await resourceManager.StartAsync();

            var channel = GrpcChannel.ForAddress($"http://{KubeService.Dex}:5557");
            dexClient = new Dex.Dex.DexClient(channel);
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonSsoClientController(IKubernetes k8s)
        {
            Covenant.Requires(k8s != null, nameof(k8s));

            this.k8s = k8s;
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
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonSsoClient resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return null;
                }

                await UpsertClientAsync(resource);

                log.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public async Task DeletedAsync(V1NeonSsoClient resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                // Ignore all events when the controller hasn't been started.

                if (resourceManager == null)
                {
                    return;
                }

                await dexClient.DeleteClientAsync(new DeleteClientReq()
                {
                    Id = resource.Spec.Id
                });

                var oauth2ProxyConfig = await k8s.ReadNamespacedConfigMapAsync("neon-sso-oauth2-proxy", KubeNamespace.NeonSystem);

                var alphaConfig = NeonHelper.YamlDeserialize<Oauth2ProxyConfig>(oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"]);

                var provider = alphaConfig.Providers.Where(p => p.ClientId == "neon-sso").Single();

                if (provider.OidcConfig.ExtraAudiences.Contains(resource.Spec.Id))
                {
                    provider.OidcConfig.ExtraAudiences.Remove(resource.Spec.Id);
                }

                oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"] = NeonHelper.YamlSerialize(alphaConfig);

                await k8s.ReplaceNamespacedConfigMapAsync(oauth2ProxyConfig, oauth2ProxyConfig.Name(), KubeNamespace.NeonSystem);

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

        private async Task UpsertClientAsync(V1NeonSsoClient resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                var client = new Dex.Client()
                {
                    Id = resource.Spec.Id,
                    Name = resource.Spec.Name,
                    Public = resource.Spec.Public
                };

                if (resource.Spec.Secret != null)
                {
                    client.Secret = resource.Spec.Secret;
                }

                if (resource.Spec.LogoUrl != null)
                {
                    client.LogoUrl = resource.Spec.LogoUrl;
                }

                client.RedirectUris.AddRange(resource.Spec.RedirectUris);
                client.TrustedPeers.AddRange(resource.Spec.TrustedPeers);

                var createClientResp = await dexClient.CreateClientAsync(new CreateClientReq()
                {
                    Client = client,
                });

                if (createClientResp.AlreadyExists)
                {
                    using (var upsertActivity = TelemetryHub.ActivitySource.StartActivity("UpdateClient"))
                    {
                        var updateClientRequest = new UpdateClientReq()
                        {
                            Id = client.Id,
                            Name = client.Name,
                            LogoUrl = client.LogoUrl
                        };
                        updateClientRequest.RedirectUris.AddRange(client.RedirectUris);
                        updateClientRequest.TrustedPeers.AddRange(client.TrustedPeers);

                        var updateClientResp = await dexClient.UpdateClientAsync(updateClientRequest);
                    }
                }

                var oauth2ProxyConfig = await k8s.ReadNamespacedConfigMapAsync("neon-sso-oauth2-proxy", KubeNamespace.NeonSystem);

                var alphaConfig = NeonHelper.YamlDeserialize<Oauth2ProxyConfig>(oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"]);

                var provider = alphaConfig.Providers.Where(p => p.ClientId == "neon-sso").Single();
                
                if (resource.Spec.Id != "neon-sso"
                    && !provider.OidcConfig.ExtraAudiences.Contains(resource.Spec.Id))
                {
                    provider.OidcConfig.ExtraAudiences.Add(resource.Spec.Id);
                }

                oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"] = NeonHelper.YamlSerialize(alphaConfig);

                await k8s.ReplaceNamespacedConfigMapAsync(oauth2ProxyConfig, oauth2ProxyConfig.Name(), KubeNamespace.NeonSystem);
            }
        }
    }
}
