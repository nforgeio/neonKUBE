//-----------------------------------------------------------------------------
// FILE:	    SsoClientFinalizer.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Resources;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Minio;
using Dex;

namespace NeonClusterOperator
{
    /// <summary>
    /// Finalizes deletion of <see cref="V1NeonSsoClient"/> resources.
    /// </summary>
    public class NeonSsoClientFinalizer : IResourceFinalizer<V1NeonSsoClient>
    {
        private ILogger logger;
        private IKubernetes k8s;
        private Dex.Dex.DexClient dexClient;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="k8s">The Kubernetes client.</param>
        /// <param name="dexClient">The Dex client.</param>
        public NeonSsoClientFinalizer(
            ILogger logger,
            IKubernetes k8s,
            Dex.Dex.DexClient dexClient)
        {
            Covenant.Requires(logger != null, nameof(logger));
            Covenant.Requires(k8s != null, nameof(k8s));
            Covenant.Requires(dexClient != null, nameof(dexClient));

            this.logger    = logger;
            this.k8s       = k8s;
            this.dexClient = dexClient;
        }

        /// <inheritdoc/>
        public async Task FinalizeAsync(V1NeonSsoClient resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                logger.LogInformationEx(() => $"Finalizing {resource.Name()}");

                await dexClient.DeleteClientAsync(new DeleteClientReq()
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

                logger.LogInformationEx(() => $"Finalized: {resource.Name()}");
            }
        }
    }
}
