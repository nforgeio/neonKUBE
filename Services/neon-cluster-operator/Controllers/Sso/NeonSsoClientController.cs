//-----------------------------------------------------------------------------
// FILE:	    NeonSsoClientController.cs
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
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Resources;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

using Dex;

using k8s;
using k8s.Autorest;
using k8s.Models;

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
    /// Configures Neon SSO using <see cref="V1NeonSsoClient"/>.
    /// </para>
    /// </summary>
    public class NeonSsoClientController : IOperatorController<V1NeonSsoClient>
    {
        //---------------------------------------------------------------------
        // Static members

        private static readonly ILogger log = TelemetryHub.CreateLogger<NeonSsoClientController>();

        private Dex.Dex.DexClient dexClient;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonSsoClientController()
        {
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes k8s;
        private readonly IFinalizerManager<V1NeonSsoClient> finalizerManager;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonSsoClientController(IKubernetes k8s,
            IFinalizerManager<V1NeonSsoClient> manager,
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
        public async Task<ResourceControllerResult> ReconcileAsync(V1NeonSsoClient resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                // Ignore all events when the controller hasn't been started.

                await finalizerManager.RegisterAllFinalizersAsync(resource);

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

                await dexClient.DeleteClientAsync(new DeleteClientReq()
                {
                    Id = resource.Spec.Id
                });

                var oauth2ProxyConfig = await k8s.CoreV1.ReadNamespacedConfigMapAsync("neon-sso-oauth2-proxy", KubeNamespace.NeonSystem);

                var alphaConfig = NeonHelper.YamlDeserialize<Oauth2ProxyConfig>(oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"]);

                var provider = alphaConfig.Providers.Where(p => p.ClientId == "neon-sso").Single();

                if (provider.OidcConfig.ExtraAudiences.Contains(resource.Spec.Id))
                {
                    provider.OidcConfig.ExtraAudiences.Remove(resource.Spec.Id);
                }

                oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"] = NeonHelper.YamlSerialize(alphaConfig);

                await k8s.CoreV1.ReplaceNamespacedConfigMapAsync(oauth2ProxyConfig, oauth2ProxyConfig.Name(), KubeNamespace.NeonSystem);

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

                var oauth2ProxyConfig = await k8s.CoreV1.ReadNamespacedConfigMapAsync("neon-sso-oauth2-proxy", KubeNamespace.NeonSystem);

                var alphaConfig = NeonHelper.YamlDeserialize<Oauth2ProxyConfig>(oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"]);

                var provider = alphaConfig.Providers.Where(p => p.ClientId == "neon-sso").Single();
                
                if (resource.Spec.Id != "neon-sso"
                    && !provider.OidcConfig.ExtraAudiences.Contains(resource.Spec.Id))
                {
                    provider.OidcConfig.ExtraAudiences.Add(resource.Spec.Id);
                }

                oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"] = NeonHelper.YamlSerialize(alphaConfig);

                await k8s.CoreV1.ReplaceNamespacedConfigMapAsync(oauth2ProxyConfig, oauth2ProxyConfig.Name(), KubeNamespace.NeonSystem);
            }
        }
    }
}
