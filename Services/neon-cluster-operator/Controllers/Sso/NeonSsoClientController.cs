//-----------------------------------------------------------------------------
// FILE:        NeonSsoClientController.cs
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
using System.Threading;
using System.Threading.Tasks;

using Dex;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.Oauth2Proxy;
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
    /// Configures Neon SSO using <see cref="V1NeonSsoClient"/>.
    /// </para>
    /// </summary>
    [RbacRule<V1NeonSsoClient>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1ConfigMap>(Verbs = RbacVerb.Get | RbacVerb.Update, Scope = EntityScope.Cluster)]
    [ResourceController(MaxConcurrentReconciles = 1)]
    public class NeonSsoClientController : ResourceControllerBase<V1NeonSsoClient>
    {
        private readonly IKubernetes                        k8s;
        private readonly IFinalizerManager<V1NeonSsoClient> finalizerManager;
        private readonly ILogger<NeonSsoClientController>   logger;
        private readonly Dex.Dex.DexClient                  dexClient;

        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonSsoClientController(IKubernetes k8s,
            IFinalizerManager<V1NeonSsoClient>     manager,
            ILogger<NeonSsoClientController>       logger,
            Dex.Dex.DexClient                      dexClient)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(manager != null, nameof(manager));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));
            Covenant.Requires<ArgumentNullException>(dexClient != null, nameof(dexClient));

            this.k8s              = k8s;
            this.finalizerManager = manager;
            this.logger           = logger;
            this.dexClient        = dexClient;
        }

        /// <inheritdoc/>
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonSsoClient resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                var patch = OperatorHelper.CreatePatch<V1NeonSsoClient>();

                patch.Replace(path => path.Status, new V1SsoClientStatus());
                patch.Replace(path => path.Status.State, "reconciling");
                await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonSsoClient>(OperatorHelper.ToV1Patch<V1NeonSsoClient>(patch), resource.Name());

                await UpsertClientAsync(resource);

                patch.Replace(path => path.Status, new V1SsoClientStatus());
                patch.Replace(path => path.Status.State, "reconciled");
                await k8s.CustomObjects.PatchClusterCustomObjectStatusAsync<V1NeonSsoClient>(OperatorHelper.ToV1Patch<V1NeonSsoClient>(patch), resource.Name());

                logger?.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public override async Task DeletedAsync(V1NeonSsoClient resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                await dexClient.DeleteClientAsync(
                    new DeleteClientReq()
                    {
                        Id = resource.Spec.Id
                    });

                var oauth2ProxyConfig = await k8s.CoreV1.ReadNamespacedConfigMapAsync("neon-sso-oauth2-proxy", KubeNamespace.NeonSystem);
                var alphaConfig       = NeonHelper.YamlDeserialize<Oauth2ProxyConfig>(oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"]);
                var provider          = alphaConfig.Providers.Where(p => p.ClientId == "neon-sso").Single();

                if (provider.OidcConfig.ExtraAudiences.Contains(resource.Spec.Id))
                {
                    provider.OidcConfig.ExtraAudiences.Remove(resource.Spec.Id);
                }

                oauth2ProxyConfig.Data["oauth2_proxy_alpha.cfg"] = NeonHelper.YamlSerialize(alphaConfig);

                await k8s.CoreV1.ReplaceNamespacedConfigMapAsync(oauth2ProxyConfig, oauth2ProxyConfig.Name(), KubeNamespace.NeonSystem);
                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        private async Task UpsertClientAsync(V1NeonSsoClient resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                var client = new Dex.Client()
                {
                    Id     = resource.Spec.Id,
                    Name   = resource.Spec.Name,
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

                if (resource.Spec.RedirectUris != null)
                {
                    client.RedirectUris.AddRange(resource.Spec.RedirectUris);
                }

                if (resource.Spec.TrustedPeers != null)
                {
                    client.TrustedPeers.AddRange(resource.Spec.TrustedPeers);
                }

                var createClientResp = await dexClient.CreateClientAsync(new CreateClientReq()
                {
                    Client = client,
                });

                if (createClientResp.AlreadyExists)
                {
                    using (var upsertActivity = TelemetryHub.ActivitySource?.StartActivity("UpdateClient"))
                    {
                        var updateClientRequest = new UpdateClientReq()
                        {
                            Id      = client.Id,
                            Name    = client.Name,
                            LogoUrl = client.LogoUrl
                        };

                        updateClientRequest.RedirectUris.AddRange(client.RedirectUris);
                        updateClientRequest.TrustedPeers.AddRange(client.TrustedPeers);

                        var updateClientResp = await dexClient.UpdateClientAsync(updateClientRequest);
                    }
                }
            }
        }
    }
}
