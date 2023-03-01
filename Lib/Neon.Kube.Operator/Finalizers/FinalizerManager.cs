//-----------------------------------------------------------------------------
// FILE:	    FinalizerManager.cs
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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Operator.Attributes;
using Neon.Kube.Operator.Builder;
using Neon.Tasks;

using k8s;
using k8s.Models;

using Prometheus;
using System.Diagnostics.Contracts;

namespace Neon.Kube.Operator.Finalizer
{
    /// <summary>
    /// Implements a finalizer manager for an entity type.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the entity type.</typeparam>
    internal class FinalizerManager<TEntity> : IFinalizerManager<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        private readonly ILogger<FinalizerManager<TEntity>>    logger;
        private readonly IKubernetes                           k8s;
        private readonly IFinalizerBuilder                     finalizerInstanceBuilder;
        private readonly IServiceProvider                      serviceProvider;
        private readonly Dictionary<string, IFinalizerMetrics> metrics;
        private readonly OperatorSettings                      operatorSettings;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s">Specifies the Kubernetes client.</param>
        /// <param name="componentRegistration">Specifies the component register.</param>
        /// <param name="finalizerInstanceBuilder">Specifies the finalizer builder.</param>
        /// <param name="serviceProvider">Specifies the dependency injection service provider.</param>
        /// <param name="operatorSettings">Optionally specifies the operator settings.</param>
        /// <param name="loggerFactory">Optionally specifies the logger factory.</param>
        public FinalizerManager(
            IKubernetes               k8s,
            ComponentRegistration     componentRegistration,
            IFinalizerBuilder         finalizerInstanceBuilder,
            IServiceProvider          serviceProvider,
            OperatorSettings          operatorSettings = null,
            ILoggerFactory            loggerFactory    = null)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));
            Covenant.Requires<ArgumentNullException>(componentRegistration != null, nameof(componentRegistration));
            Covenant.Requires<ArgumentNullException>(finalizerInstanceBuilder != null, nameof(finalizerInstanceBuilder));
            Covenant.Requires<ArgumentNullException>(serviceProvider != null, nameof(serviceProvider));

            operatorSettings ??= new OperatorSettings();

            this.k8s                      = k8s;
            this.finalizerInstanceBuilder = finalizerInstanceBuilder;
            this.serviceProvider          = serviceProvider; 
            this.operatorSettings         = operatorSettings;
            this.logger                   = loggerFactory?.CreateLogger<FinalizerManager<TEntity>>();
            this.metrics                  = new Dictionary<string, IFinalizerMetrics>();
            
            foreach (var finalizer in finalizerInstanceBuilder.BuildFinalizers<TEntity>(serviceProvider.CreateScope().ServiceProvider))
            {
                var finalizerMetrics = new FinalizerMetrics<TEntity>(operatorSettings, finalizer.GetType());

                metrics.Add(finalizer.Identifier, finalizerMetrics);
            }
        }

        /// <inheritdoc/>
        public Task RegisterFinalizerAsync<TFinalizer>(TEntity entity)
            where TFinalizer : IResourceFinalizer<TEntity>
        {
            using var activity = TraceContext.ActivitySource?.StartActivity();

            logger?.LogInformationEx(() => $"Registering finalizer {typeof(TFinalizer)} to {entity.Uid()}");

            return RegisterFinalizerInternalAsync(entity, finalizerInstanceBuilder.BuildFinalizer<TEntity, TFinalizer>(serviceProvider.CreateScope().ServiceProvider));
        }

        /// <inheritdoc/>
        public async Task RegisterAllFinalizersAsync(TEntity entity)
        {
            await SyncContext.Clear;

            await Task.WhenAll(
                finalizerInstanceBuilder.BuildFinalizers<TEntity>(serviceProvider.CreateScope().ServiceProvider)
                    .Where(finalizer => (finalizer.GetType().GetCustomAttribute<FinalizerAttribute>()?.RegisterWithAll == true) || (finalizer.GetType().GetCustomAttribute<FinalizerAttribute>() == null))
                    .Select(f => RegisterFinalizerInternalAsync(entity, f)));
        }

        /// <inheritdoc/>
        public async Task RemoveFinalizerAsync<TFinalizer>(TEntity entity)
            where TFinalizer : IResourceFinalizer<TEntity>
        {
            await SyncContext.Clear;

            using var activity = TraceContext.ActivitySource?.StartActivity();

            logger?.LogInformationEx(() => $"Removing finalizer {typeof(TFinalizer)} from {entity.Uid()}");

            var finalizer = finalizerInstanceBuilder.BuildFinalizer<TEntity, TFinalizer>(serviceProvider.CreateScope().ServiceProvider);

            if (entity.RemoveFinalizer(finalizer.Identifier))
            {
                using (metrics[finalizer.Identifier].RemovalTimeSeconds.NewTimer())
                {
                    await UpdateEntityAsync(entity);

                    metrics[finalizer.Identifier].RemovalsTotal.Inc();
                }
            }
        }

        private async Task FinalizeInternalAsync(IResourceFinalizer<TEntity> finalizer, TEntity entity, CancellationToken cancellationToken = default)
        {
            await SyncContext.Clear;

            try
            {
                using (metrics[finalizer.Identifier].FinalizingCount.TrackInProgress())
                {
                    using (metrics[finalizer.Identifier].FinalizeTimeSeconds.NewTimer())
                    {
                        await finalizer.FinalizeAsync(entity);
                    }
                }

                await RemoveFinalizerAsync(entity, finalizer);

                metrics[finalizer.Identifier].FinalizedTotal.Inc();
            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e);
                throw;
            }
        }

        private async Task RemoveFinalizerAsync(TEntity entity, IResourceFinalizer<TEntity> finalizer)
        {
            await SyncContext.Clear;

            try
            {
                using (metrics[finalizer.Identifier].RemovalTimeSeconds.NewTimer())
                {
                    await NeonHelper.WaitForAsync(
                        async () =>
                        {
                            try
                            {
                                if (string.IsNullOrEmpty(entity.Metadata.NamespaceProperty))
                                {
                                    entity = await k8s.CustomObjects.ReadClusterCustomObjectAsync<TEntity>(name: entity.Name());
                                }
                                else
                                {
                                    entity = await k8s.CustomObjects.ReadNamespacedCustomObjectAsync<TEntity>(
                                        name:               entity.Name(),
                                        namespaceParameter: entity.Namespace());
                                }

                                if (entity.RemoveFinalizer(finalizer.Identifier))
                                {
                                    await UpdateEntityAsync(entity);
                                }

                                return true;
                            }
                            catch (Exception e)
                            {
                                logger?.LogErrorEx(e);
                            }

                            return false;
                        },
                        timeout:      TimeSpan.FromSeconds(30),
                        pollInterval: TimeSpan.FromSeconds(1));
                }

                metrics[finalizer.Identifier].RemovalsTotal.Inc();
            }
            catch (Exception e)
            {
                string entityName;

                if (string.IsNullOrEmpty(entity.Metadata.NamespaceProperty))
                {
                    entityName = entity.Name();
                }
                else
                {
                    entityName = $"{entity.Namespace()}/{entity.Name()}";
                }

                logger?.LogErrorEx(e);

                throw new Exception($"Timed out while trying to remove finalizer [{finalizer.Identifier}] from entity [{entityName}]");
            }
        }

        /// <inheritdoc/>
        async Task IFinalizerManager<TEntity>.FinalizeAsync(TEntity entity, IServiceScope scope)
        {
            await SyncContext.Clear;

            using var activity = TraceContext.ActivitySource?.StartActivity();

            var tasks = new List<Task>();

            foreach (var finalizer in finalizerInstanceBuilder.BuildFinalizers<TEntity>(scope.ServiceProvider))
            {
                if (entity.HasFinalizer(finalizer.Identifier))
                {
                    tasks.Add(FinalizeInternalAsync(finalizer, entity));
                }
            }

            await Task.WhenAll(tasks);
        }

        /// <summary>
        /// Registers a finalizer for an entity.
        /// </summary>
        /// <typeparam name="TFinalizer"></typeparam>
        /// <param name="entity">Specifies the entity.</param>
        /// <param name="finalizer">Specifies the finalizer.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task RegisterFinalizerInternalAsync<TFinalizer>(TEntity entity, TFinalizer finalizer)
            where TFinalizer : IResourceFinalizer<TEntity>
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(entity != null, nameof(entity));
            Covenant.Requires<ArgumentNullException>(finalizer != null, nameof(finalizer));

            using (metrics[finalizer.Identifier].RegistrationTimeSeconds.NewTimer())
            {
                if (entity.AddFinalizer(finalizer.Identifier))
                {
                    await UpdateEntityAsync(entity);
                    metrics[finalizer.Identifier].RegistrationsTotal.Inc();
                }
            }
        }

        /// <summary>
        /// Updates an entiry.
        /// </summary>
        /// <param name="entity">Specifies the entity.</param>
        /// <returns>THe tracking <see cref="Task"/>.</returns>
        private async Task UpdateEntityAsync(TEntity entity)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(entity != null, nameof(entity));

            try
            {
                var metadata = entity.GetKubernetesTypeMetadata();

                if (string.IsNullOrEmpty(entity.Metadata.NamespaceProperty))
                {
                    await k8s.CustomObjects.ReplaceClusterCustomObjectAsync(entity, entity.Name());
                }
                else
                {
                    await k8s.CustomObjects.ReplaceNamespacedCustomObjectAsync(
                        body:               entity, 
                        name:               entity.Name(),
                        namespaceParameter: entity.Namespace());
                }
            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e);
            }
        }
    }
}
