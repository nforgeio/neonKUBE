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

namespace Neon.Kube.Operator.Finalizer
{
    internal class FinalizerManager<TEntity> : IFinalizerManager<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        private readonly ILogger<FinalizerManager<TEntity>>    logger;
        private readonly IKubernetes                           client;
        private readonly IFinalizerBuilder                     finalizerInstanceBuilder;
        private readonly IServiceProvider                      serviceProvider;
        private readonly Dictionary<string, IFinalizerMetrics> metrics;
        private readonly OperatorSettings                      operatorSettings;
        
        public FinalizerManager(
            IKubernetes               client,
            ComponentRegister         componentRegister,
            IFinalizerBuilder         finalizerInstanceBuilder,
            IServiceProvider          serviceProvider,
            OperatorSettings          operatorSettings,
            ILoggerFactory            loggerFactory = null)
        {
            this.client                   = client;
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
            => RegisterFinalizerInternalAsync(entity, finalizerInstanceBuilder.BuildFinalizer<TEntity, TFinalizer>(serviceProvider.CreateScope().ServiceProvider));

        /// <inheritdoc/>
        public async Task RegisterAllFinalizersAsync(TEntity entity)
        {
            await SyncContext.Clear;

            await Task.WhenAll(
                finalizerInstanceBuilder.BuildFinalizers<TEntity>(serviceProvider.CreateScope().ServiceProvider)
                    .Where(f => 
                        (f.GetType().GetCustomAttribute<FinalizerAttribute>()?.RegisterWithAll == true)
                        || (f.GetType().GetCustomAttribute<FinalizerAttribute>() == null) 
                        )
                    .Select(f => RegisterFinalizerInternalAsync(entity, f)));
        }

        /// <inheritdoc/>
        public async Task RemoveFinalizerAsync<TFinalizer>(TEntity entity)
            where TFinalizer : IResourceFinalizer<TEntity>
        {
            await SyncContext.Clear;

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
                                    entity = await client.CustomObjects.ReadClusterCustomObjectAsync<TEntity>(entity.Name());
                                }
                                else
                                {
                                    entity = await client.CustomObjects.ReadNamespacedCustomObjectAsync<TEntity>(entity.Namespace(), entity.Name());
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
                        timeout: TimeSpan.FromSeconds(30),
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

        private async Task RegisterFinalizerInternalAsync<TFinalizer>(TEntity entity, TFinalizer finalizer)
            where TFinalizer : IResourceFinalizer<TEntity>
        {
            using (metrics[finalizer.Identifier].RegistrationTimeSeconds.NewTimer())
            {
                if (entity.AddFinalizer(finalizer.Identifier))
                {
                    await UpdateEntityAsync(entity);
                    metrics[finalizer.Identifier].RegistrationsTotal.Inc();
                }
            }
        }

        private async Task UpdateEntityAsync(TEntity entity)
        {
            try
            {
                var metadata = entity.GetKubernetesTypeMetadata();

                if (string.IsNullOrEmpty(entity.Metadata.NamespaceProperty))
                {
                    await client.CustomObjects.ReplaceClusterCustomObjectAsync(entity, entity.Name());
                }
                else
                {
                    await client.CustomObjects.ReplaceNamespacedCustomObjectAsync(entity, entity.Namespace(), entity.Name());
                }
            }
            catch (Exception e)
            {
                logger?.LogErrorEx(e);
            }
        }
    }
}
