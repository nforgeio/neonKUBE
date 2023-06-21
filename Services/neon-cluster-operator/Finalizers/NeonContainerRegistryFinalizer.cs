//-----------------------------------------------------------------------------
// FILE:        NeonContainerRegistryFinalizer.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube;
using Neon.Kube.Resources;
using Neon.Kube.Resources.Cluster;
using Neon.Tasks;

using k8s.Models;
using Neon.Kube.Operator.Finalizer;
using System.Diagnostics.Contracts;
using k8s;
using Neon.Kube.Operator.Util;
using OpenTelemetry.Resources;

namespace NeonClusterOperator
{
    /// <summary>
    /// Finalizes deletion of <see cref="V1NeonContainerRegistry"/> resources.
    /// </summary>
    public class NeonContainerRegistryFinalizer : IResourceFinalizer<V1NeonContainerRegistry>
    {
        private readonly IKubernetes                             k8s;
        private readonly ILogger<NeonContainerRegistryFinalizer> logger;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">Specifies the k8s client.</param>
        /// <param name="logger">Specifies the logger.</param>
        public NeonContainerRegistryFinalizer(
            IKubernetes k8s,
            ILogger<NeonContainerRegistryFinalizer> logger)
        { 
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));

            this.k8s    = k8s;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public async Task FinalizeAsync(V1NeonContainerRegistry resource)
        {
            await SyncContext.Clear;

            logger.LogInformationEx(() => $"Finalizing {resource.Name()}");

            var crioConfigList = await k8s.CustomObjects.ListClusterCustomObjectAsync<V1CrioConfiguration>();

            V1CrioConfiguration crioConfig;
            if (crioConfigList.Items.IsEmpty())
            {
                return;
            }

            crioConfig = crioConfigList.Items.Where(cfg => cfg.Metadata.Name == KubeConst.ClusterCrioConfigName).Single();

            if (crioConfig.Spec.Registries.Any(kvp => kvp.Key == resource.Uid()))
            {
                logger?.LogInformationEx(() => $"Registry [{resource.Namespace()}/{resource.Name()}] exists, removing.");

                var registry = crioConfig.Spec.Registries.Where(kvp => kvp.Key == resource.Uid()).FirstOrDefault();
                crioConfig.Spec.Registries.Remove(registry);
                
                var patch = OperatorHelper.CreatePatch<V1CrioConfiguration>();
                patch.Replace(path => path.Spec.Registries, crioConfig.Spec.Registries);

                await k8s.CustomObjects.PatchClusterCustomObjectAsync<V1CrioConfiguration>(
                    patch: OperatorHelper.ToV1Patch<V1CrioConfiguration>(patch),
                    name:  crioConfig.Name());
            }
        }
    }
}
