//-----------------------------------------------------------------------------
// FILE:	    FinalizerManager.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;

using k8s;
using k8s.Models;
using Neon.Common;

namespace Neon.Kube.Operator
{
    internal class FinalizerManager<TEntity> : IFinalizerManager<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        private readonly IKubernetes client;
        private readonly ILogger logger;
        private readonly ComponentRegister componentRegister;
        private readonly IFinalizerBuilder finalizerInstanceBuilder;
        private readonly SemaphoreSlim semaphoreSlim;

        public FinalizerManager(
            IKubernetes client,
            ILogger logger,
            ComponentRegister componentRegister,
            IFinalizerBuilder finalizerInstanceBuilder)
        {
            this.client = client;
            this.logger = logger;
            this.componentRegister = componentRegister;
            this.finalizerInstanceBuilder = finalizerInstanceBuilder;
            this.semaphoreSlim = new SemaphoreSlim(1);
        }

        /// <inheritdoc/>
        public Task RegisterFinalizerAsync<TFinalizer>(TEntity entity)
            where TFinalizer : IResourceFinalizer<TEntity>
            => RegisterFinalizerInternalAsync(entity, finalizerInstanceBuilder.BuildFinalizer<TEntity, TFinalizer>());

        /// <inheritdoc/>
        public async Task RegisterAllFinalizersAsync(TEntity entity)
        {
            await Task.WhenAll(
                finalizerInstanceBuilder.BuildFinalizers<TEntity>()
                    .Select(f => RegisterFinalizerInternalAsync(entity, f)));
        }

        /// <inheritdoc/>
        public async Task RemoveFinalizerAsync<TFinalizer>(TEntity entity)
            where TFinalizer : IResourceFinalizer<TEntity>
        {
            var finalizer = finalizerInstanceBuilder.BuildFinalizer<TEntity, TFinalizer>();

            if (entity.RemoveFinalizer(finalizer.Identifier))
            {
                await UpdateEntityAsync(entity);
            }
        }

        private async Task FinalizeInternalAsync(IResourceFinalizer<TEntity> finalizer, TEntity entity)
        {
            try
            {
                await semaphoreSlim.WaitAsync();

                await finalizer.FinalizeAsync(entity);

                await RemoveFinalizerAsync(entity, finalizer);
            }
            catch (Exception e)
            {
                logger.LogErrorEx(e);
                throw;
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        private async Task RemoveFinalizerAsync(TEntity entity, IResourceFinalizer<TEntity> finalizer)
        {
            try
            {
                await NeonHelper.WaitForAsync(
                    async () =>
                    {
                        try
                        {
                            if (string.IsNullOrEmpty(entity.Metadata.NamespaceProperty))
                            {
                                entity = await client.ReadClusterCustomObjectAsync<TEntity>(entity.Name());
                            }
                            else
                            {
                                entity = await client.ReadNamespacedCustomObjectAsync<TEntity>(entity.Namespace(), entity.Name());
                            }

                            if (entity.RemoveFinalizer(finalizer.Identifier))
                            {
                                await UpdateEntityAsync(entity);
                            }

                            return true;
                        }
                        catch (Exception e)
                        {
                            logger.LogErrorEx(e);
                        }

                        return false;
                    },
                    timeout: TimeSpan.FromSeconds(30),
                    pollInterval: TimeSpan.FromSeconds(1));
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

                logger.LogErrorEx(e);

                throw new Exception($"Timed out while trying to remove finalizer [{finalizer.Identifier}] from entity [{entityName}]");
            }
            
        }

        /// <inheritdoc/>
        async Task IFinalizerManager<TEntity>.FinalizeAsync(TEntity entity)
        {
            var tasks = new List<Task>();

            foreach (var finalizer in finalizerInstanceBuilder.BuildFinalizers<TEntity>())
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
            if (entity.AddFinalizer(finalizer.Identifier))
            {
                await UpdateEntityAsync(entity);
            }
        }

        private async Task UpdateEntityAsync(TEntity entity)
        {
            var metadata = entity.GetKubernetesTypeMetadata();

            if (string.IsNullOrEmpty(entity.Metadata.NamespaceProperty))
            {
                await client.ReplaceClusterCustomObjectAsync<TEntity>(entity, entity.Name());
            }
            else
            {
                await client.ReplaceNamespacedCustomObjectAsync<TEntity>(entity, entity.Namespace(), entity.Name());
            }
        }
    }
}
