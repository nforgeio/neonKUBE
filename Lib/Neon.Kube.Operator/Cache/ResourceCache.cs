//-----------------------------------------------------------------------------
// FILE:	    ResourceCache.cs
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Diagnostics;
using Neon.Tasks;
using Neon.Kube.Operator.ResourceManager;

using k8s;
using k8s.Models;

using KellermanSoftware.CompareNetObjects;

using Prometheus;

namespace Neon.Kube.Operator.Cache
{
    internal class ResourceCache<TEntity, TValue> : IResourceCache<TEntity, TValue>
        where TEntity : IKubernetesObject<V1ObjectMeta>
        where TValue : IKubernetesObject<V1ObjectMeta>
    {
        private readonly ILogger<ResourceCache<TEntity, TValue>> logger;
        private readonly ConcurrentDictionary<string, TValue>    cache;
        private readonly ConcurrentDictionary<string, TValue>    finalizingCache;
        private readonly CompareLogic                            comparelogLogic;
        private readonly ResourceCacheMetrics<TEntity>           metrics;

        public ResourceCache(
            ResourceCacheMetrics<TEntity> metrics,
            ILoggerFactory loggerFactory = null) 
        {
            cache           = new ConcurrentDictionary<string, TValue>();
            finalizingCache = new ConcurrentDictionary<string, TValue>();

            comparelogLogic = new CompareLogic(new ComparisonConfig()
            {
                AutoClearCache = false,
                MembersToIgnore = new List<string>()
                {
                    "ResourceVersion",
                    "ManagedFields"
                }
            });

            this.metrics = metrics;
            this.logger  = loggerFactory?.CreateLogger<ResourceCache<TEntity, TValue>>();
        }

        public void Clear()
        {
            cache.Clear();
            metrics.ItemsCount.DecTo(0);
        }

        public TValue Get(string id)
        {
            var result = cache.GetValueOrDefault(id);
            
            if (result == null)
            {
                metrics.HitsTotal.Inc();
            }

            return result;
        }

        public bool TryGet(string id, out TValue result)
        {
            result = cache.GetValueOrDefault(id);

            if (result == null)
            {
                metrics.HitsTotal.Inc();
            }

            return result != null;
        }

        public void Compare(TValue entity, out ModifiedEventType result)
        {
            result = CompareEntity(entity);
        }

        public void Remove(TValue entity)
        {
            if (cache.TryRemove(entity.Metadata.Uid, out _))
            {
                metrics.ItemsCount.Dec();
            }
        }

        public TValue Upsert(TValue entity, out ModifiedEventType result)
        {
            var id = entity.Metadata.Uid;

            logger?.LogDebugEx(() => $"Adding {typeof(TValue)}/{id} to cache.");

            result = CompareEntity(entity);

            cache.AddOrUpdate(
                key: id,
                addValueFactory: (id) =>
                {
                    metrics.ItemsCount.Inc();
                    metrics.ItemsTotal.Inc();
                    return Clone(entity);
                },
                updateValueFactory: (key, oldEntity) =>
                {
                    metrics.HitsTotal.Inc();
                    return Clone(entity);
                });

            return entity;

        }

        public void Upsert(IEnumerable<TValue> entities)
        {
            foreach (var entity in entities)
            {
                var id = entity.Metadata.Uid;

                logger?.LogDebugEx(() => $"Adding {typeof(TValue)}/{id} to cache.");

                cache.AddOrUpdate(
                    key: id,
                    addValueFactory: (id) =>
                    {
                        metrics.ItemsCount.Inc();
                        metrics.ItemsTotal.Inc();
                        return Clone(entity);
                    },
                    updateValueFactory: (key, oldEntity) =>
                    {
                        metrics.HitsTotal.Inc();
                        return Clone(entity);
                    });
            }
        }

        public void Upsert(TValue entity)
        {
            var id = entity.Metadata.Uid;

            logger?.LogDebugEx(() => $"Adding {typeof(TValue)}/{id} to cache.");

            cache.AddOrUpdate(
                key: id,
                addValueFactory: (id) =>
                {
                    metrics.ItemsCount.Inc();
                    metrics.ItemsTotal.Inc();
                    return Clone(entity);
                },
                updateValueFactory: (key, oldEntity) =>
                {
                    metrics.HitsTotal.Inc(); 
                    return Clone(entity);
                });
        }

        public bool IsFinalizing(TValue entity)
        {
            var id = entity.Metadata.Uid;

            return finalizingCache.ContainsKey(id);
        }

        public void AddFinalizer(TValue entity)
        {
            var id = entity.Metadata.Uid;

            finalizingCache.AddOrUpdate(
                    key: id,
                    addValueFactory: (id) => Clone(entity),
                    updateValueFactory: (key, oldEntity) => Clone(entity));
        }

        public void RemoveFinalizer(TValue entity)
        {
            finalizingCache.TryRemove(entity.Metadata.Uid, out _);
        }

        private TValue Clone(TValue entity)
        {
            return KubernetesJson.Deserialize<TValue>(KubernetesJson.Serialize(entity));
        }

        public ModifiedEventType CompareEntity(TValue entity)
        {
            var id = entity.Metadata.Uid;
            
            if (!cache.ContainsKey(id))
            {
                if (entity.DeletionTimestamp() != null
                    && entity.Finalizers().Count > 0)
                {
                    logger?.LogDebugEx(() => "Resource is being finalized.");

                    return ModifiedEventType.Finalizing;
                }

                return ModifiedEventType.Other;
            }

            var cachedEntity = Get(id);
            var comparison = comparelogLogic.Compare(entity, cachedEntity);

            if (comparison.AreEqual)
            {
                logger?.LogDebugEx(() => "No changes detected.");
                return ModifiedEventType.NoChanges;
            }

            if (comparison.Differences.All(d => d.PropertyName.Split('.')[0] == "Status"))
            {
                logger?.LogDebugEx(() => "Status update detected.");

                return ModifiedEventType.StatusUpdate;
            }

            if (comparison.Differences.All(d => d.ParentPropertyName == "Metadata.Finalizers" || d.PropertyName == "Metadata.Finalizers"))
            {
                logger?.LogDebugEx(() => "Finalizer update detected.");

                return ModifiedEventType.FinalizerUpdate;
            }

            if (entity.DeletionTimestamp() != null
                && entity.Finalizers().Count > 0)
            {
                logger?.LogDebugEx(() => "Resource is being finalized.");

                return ModifiedEventType.Finalizing;
            }

            logger?.LogDebugEx(() => "'other' change detected.");

            return ModifiedEventType.Other;
        }
    }
}
