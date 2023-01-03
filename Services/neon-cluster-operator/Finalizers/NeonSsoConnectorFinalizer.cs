//-----------------------------------------------------------------------------
// FILE:	    SsoConnectorFinalizer.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Kube.Resources.Dex;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Minio;

namespace NeonClusterOperator
{
    /// <summary>
    /// Finalizes deletion of <see cref="V1NeonSsoConnector"/> resources.
    /// </summary>
    public class NeonSsoConnectorFinalizer : IResourceFinalizer<V1NeonSsoConnector>
    {
        private ILogger logger;
        private IKubernetes k8s;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="logger">The logger.</param>
        /// <param name="k8s">The Kubernetes client.</param>
        public NeonSsoConnectorFinalizer(
            ILogger logger,
            IKubernetes k8s)
        {
            Covenant.Requires(logger != null, nameof(logger));
            Covenant.Requires(k8s != null, nameof(k8s));

            this.logger = logger;
            this.k8s = k8s;
        }

        /// <inheritdoc/>
        public async Task FinalizeAsync(V1NeonSsoConnector resource)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource.StartActivity())
            {
                logger.LogInformationEx(() => $"Finalizing {resource.Name()}");

                var configMap = await k8s.CoreV1.ReadNamespacedConfigMapAsync("neon-sso-dex", KubeNamespace.NeonSystem);
                var dexConfig = NeonHelper.YamlDeserializeViaJson<DexConfig>(configMap.Data["config.yaml"]);

                if (dexConfig.Connectors.Any(connector => connector.Id == resource.Spec.Id))
                {
                    var connector = dexConfig.Connectors.Where(connector => connector.Id == resource.Spec.Id).Single();

                    dexConfig.Connectors.Remove(connector);
                }

                configMap.Data["config.yaml"] = NeonHelper.YamlSerialize(dexConfig);

                await k8s.CoreV1.ReplaceNamespacedConfigMapAsync(configMap, configMap.Name(), configMap.Namespace());

                logger.LogInformationEx(() => $"Finalized: {resource.Name()}");
            }
        }
    }
}
