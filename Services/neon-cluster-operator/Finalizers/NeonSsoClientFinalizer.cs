//-----------------------------------------------------------------------------
// FILE:        SsoClientFinalizer.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

using Dex;

using k8s;
using k8s.Models;

using Minio;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Oauth2Proxy;
using Neon.Operator.Finalizers;
using Neon.Kube.Resources.Cluster;
using Neon.Tasks;

namespace NeonClusterOperator
{
    /// <summary>
    /// Finalizes deletion of <see cref="V1NeonSsoClient"/> resources.
    /// </summary>
    public class NeonSsoClientFinalizer : ResourceFinalizerBase<V1NeonSsoClient>
    {
        private readonly IKubernetes                     k8s;
        private readonly ILogger<NeonSsoClientFinalizer> logger;
        private readonly Dex.Dex.DexClient               dexClient;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">The Kubernetes client.</param>
        /// <param name="logger">The logger.</param>
        /// <param name="dexClient">The Dex client.</param>
        public NeonSsoClientFinalizer(
            ILogger<NeonSsoClientFinalizer> logger,
            IKubernetes                     k8s,
            Dex.Dex.DexClient               dexClient)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));
            Covenant.Requires<ArgumentNullException>(dexClient != null, nameof(dexClient));

            this.k8s       = k8s;
            this.logger    = logger;
            this.dexClient = dexClient;
        }

        /// <inheritdoc/>
        public override async Task FinalizeAsync(V1NeonSsoClient resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger.LogInformationEx(() => $"Finalizing {resource.Name()}");

                await dexClient.DeleteClientAsync(new DeleteClientReq()
                {
                    Id = resource.Spec.Id
                });

                logger.LogInformationEx(() => $"Finalized: {resource.Name()}");
            }
        }
    }
}
