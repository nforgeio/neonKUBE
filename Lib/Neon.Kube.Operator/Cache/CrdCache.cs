//-----------------------------------------------------------------------------
// FILE:	    CrdCache.cs
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

using Prometheus;

namespace Neon.Kube.Operator.Cache
{
    internal class CrdCache : ICrdCache
    {
        /// <summary>
        /// The number of items currently in the CRD cache.
        /// </summary>
        public static readonly Gauge CacheSize = Metrics.CreateGauge(
            $"neonkubeoperator_cache_crds_items_current",
            "The number of items currently in the CRD cache."
            );

        private readonly ILogger<CrdCache>                                        logger;
        private readonly ConcurrentDictionary<string, V1CustomResourceDefinition> cache;

        public CrdCache(ILoggerFactory loggerFactory = null) 
        {
            cache = new ConcurrentDictionary<string, V1CustomResourceDefinition>();

            this.logger = loggerFactory?.CreateLogger<CrdCache>();
        }

        public void Clear()
        {
            cache.Clear();
            CacheSize?.DecTo(0);
        }

        public V1CustomResourceDefinition Get(string id)
        {
            return cache.GetValueOrDefault(id);
        }

        public V1CustomResourceDefinition Get<TEntity>()
            where TEntity : IKubernetesObject<V1ObjectMeta>
        {
            return cache.GetValueOrDefault(typeof(TEntity).GetKubernetesCrdName());
        }

        public void Remove(V1CustomResourceDefinition entity)
        {
            if (cache.TryRemove(entity.Metadata.Name, out _))
            {
                CacheSize?.Dec();
            }
        }

        public void Upsert(V1CustomResourceDefinition entity)
        {
            var id = entity.Metadata.Name;

            logger?.LogDebugEx(() => $"Adding {id} to cache.");

            cache.AddOrUpdate(
                key: id,
                addValueFactory: (id) => 
                {
                    CacheSize?.Inc();
                    return Clone(entity);
                },
                updateValueFactory: (key, oldEntity) =>
                {
                    return Clone(entity);
                });
        }
        private V1CustomResourceDefinition Clone(V1CustomResourceDefinition entity)
        {
            return KubernetesJson.Deserialize<V1CustomResourceDefinition>(KubernetesJson.Serialize(entity));
        }
    }
}
