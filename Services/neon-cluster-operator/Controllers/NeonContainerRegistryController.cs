//-----------------------------------------------------------------------------
// FILE:        NeonContainerRegistryController.cs
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.K8s;
using Neon.Kube;
using Neon.Kube.Glauth;
using Neon.Kube.Resources.Cluster;
using Neon.Operator.Attributes;
using Neon.Operator.Controllers;
using Neon.Operator.Rbac;
using Neon.Operator.Util;
using Neon.Tasks;

using OpenTelemetry.Trace;

namespace NeonClusterOperator
{
    /// <summary>
    /// <para>
    /// Configures Neon SSO using <see cref="V1NeonContainerRegistry"/>.
    /// </para>
    /// </summary>
    [RbacRule<V1CrioConfiguration>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [RbacRule<V1NeonContainerRegistry>(Verbs = RbacVerb.All, Scope = EntityScope.Cluster, SubResources = "status")]
    [ResourceController(MaxConcurrentReconciles = 1)]
    public class NeonContainerRegistryController : ResourceControllerBase<V1NeonContainerRegistry>
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Static constructor.
        /// </summary>
        static NeonContainerRegistryController()
        {
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly IKubernetes                                k8s;
        private readonly ILogger<NeonContainerRegistryController>   logger;
        /// <summary>
        /// Constructor.
        /// </summary>
        public NeonContainerRegistryController(
            IKubernetes                                k8s,
            ILogger<NeonContainerRegistryController>   logger)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(logger != null, nameof(logger));

            this.k8s    = k8s;
            this.logger = logger;
        }

        /// <inheritdoc/>
        public override async Task<ResourceControllerResult> ReconcileAsync(V1NeonContainerRegistry resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("reconcile", attributes => attributes.Add("customresource", nameof(V1NeonContainerRegistry)));

                await SyncContext.Clear;

                logger?.LogInformationEx(() => $"Reconciling {resource.GetType().FullName} [{resource.Namespace()}/{resource.Name()}].");

                var crioConfigList = await k8s.CustomObjects.ListClusterCustomObjectAsync<V1CrioConfiguration>();

                V1CrioConfiguration crioConfig;
                if (crioConfigList.Items.IsEmpty())
                {
                    crioConfig                 = new V1CrioConfiguration().Initialize();
                    crioConfig.Metadata.Name   = KubeConst.ClusterCrioConfigName;
                    crioConfig.Spec            = new V1CrioConfiguration.CrioConfigurationSpec();
                    crioConfig.Spec.Registries = new List<KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>>();
                }
                else
                {
                    crioConfig                   = crioConfigList.Items.Where(cfg => cfg.Metadata.Name == KubeConst.ClusterCrioConfigName).Single();
                    crioConfig.Spec            ??= new V1CrioConfiguration.CrioConfigurationSpec();
                    crioConfig.Spec.Registries ??= new List<KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>>();
                }

                if (crioConfig.Spec.Registries.IsEmpty())
                {
                    crioConfig.Spec.Registries.Add(new KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>(resource.Uid(), resource.Spec));

                    await k8s.CustomObjects.UpsertClusterCustomObjectAsync(body: crioConfig, name: crioConfig.Name());

                    return null;
                }

                if (!crioConfig.Spec.Registries.Any(kvp => kvp.Key == resource.Uid()))
                {
                    logger?.LogInformationEx(() => $"Registry [{resource.Namespace()}/{resource.Name()}] deos not exist, adding.");

                    var addPatch = OperatorHelper.CreatePatch<V1CrioConfiguration>();

                    addPatch.Add(path => path.Spec.Registries, new KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>(resource.Uid(), resource.Spec));

                    await k8s.CustomObjects.PatchClusterCustomObjectAsync<V1CrioConfiguration>(
                        patch: OperatorHelper.ToV1Patch<V1CrioConfiguration>(addPatch),
                        name: crioConfig.Name());
                }
                else
                {
                    logger?.LogInformationEx(() => $"Registry [{resource.Namespace()}/{resource.Name()}] exists, checking for changes.");

                    var registry = crioConfig.Spec.Registries.Where(kvp => kvp.Key == resource.Uid()).Single();

                    if (registry.Value != resource.Spec)
                    {
                        logger?.LogInformationEx(() => $"Registry [{resource.Namespace()}/{resource.Name()}] changed, upserting.");

                        crioConfig.Spec.Registries.Remove(registry);
                        crioConfig.Spec.Registries.Add(new KeyValuePair<string, V1NeonContainerRegistry.RegistrySpec>(resource.Uid(), resource.Spec));

                        var patch =  OperatorHelper.CreatePatch<V1CrioConfiguration>();

                        patch.Replace(path => path.Spec.Registries, crioConfig.Spec.Registries);

                        await k8s.CustomObjects.PatchClusterCustomObjectAsync<V1CrioConfiguration>(
                            patch: OperatorHelper.ToV1Patch<V1CrioConfiguration>(patch),
                            name: crioConfig.Name());
                    }
                }

                logger?.LogInformationEx(() => $"RECONCILED: {resource.Name()}");

                return null;
            }
        }

        /// <inheritdoc/>
        public override async Task DeletedAsync(V1NeonContainerRegistry resource, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                Tracer.CurrentSpan?.AddEvent("delete", attributes => attributes.Add("customresource", nameof(V1NeonContainerRegistry)));

                if (resource.Name() == KubeConst.LocalClusterRegistryProject)
                {
                    await CreateNeonLocalRegistryAsync();
                }

                logger?.LogInformationEx(() => $"DELETED: {resource.Name()}");
            }
        }

        private async Task CreateNeonLocalRegistryAsync()
        {
            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                logger?.LogInformationEx(() => $"Upserting registry: [registry.neon.local]");

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
