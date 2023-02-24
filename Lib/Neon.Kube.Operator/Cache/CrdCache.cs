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
using IdentityModel;
using IdentityModel.OidcClient;
using System.Diagnostics.Contracts;

namespace Neon.Kube.Operator.Cache
{
    /// <summary>
    /// Used to cache CRDs for improved performance.
    /// </summary>
    internal class CrdCache : ICrdCache
    {
        private readonly ILogger<CrdCache>                                        logger;
        private readonly ConcurrentDictionary<string, V1CustomResourceDefinition> cache;
        private readonly ResourceCacheMetrics<V1CustomResourceDefinition>         metrics;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="metrics">Specifies the resource metrics.</param>
        /// <param name="loggerFactory">Optionally specifies a logger factory.</param>
        public CrdCache(ResourceCacheMetrics<V1CustomResourceDefinition> metrics, ILoggerFactory loggerFactory = null) 
        {
            Covenant.Requires<ArgumentNullException>(metrics != null, nameof(metrics));

            this.cache   = new ConcurrentDictionary<string, V1CustomResourceDefinition>();
            this.metrics = metrics;
            this.logger  = loggerFactory?.CreateLogger<CrdCache>();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            cache.Clear();
            metrics.ItemsCount.DecTo(0);
        }

        /// <inheritdoc/>
        public V1CustomResourceDefinition Get(string id)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id), nameof(id));

            var result = cache.GetValueOrDefault(id);

            if (result == null)
            {
                metrics.HitsTotal.Inc();
            }

            return result;
        }

        /// <inheritdoc/>
        public V1CustomResourceDefinition Get<TEntity>()
            where TEntity : IKubernetesObject<V1ObjectMeta>
        {
            var result = cache.GetValueOrDefault(typeof(TEntity).GetKubernetesCrdName());

            if (result == null)
            {
                metrics.HitsTotal.Inc();
            }

            return result;
        }

        /// <inheritdoc/>
        public void Remove(V1CustomResourceDefinition entity)
        {
            Covenant.Requires<ArgumentNullException>(entity != null, nameof(entity));

            if (cache.TryRemove(entity.Metadata.Name, out _))
            {
                metrics.ItemsCount.Dec();
            }
        }

        /// <inheritdoc/>
        public void Upsert(V1CustomResourceDefinition entity)
        {
            Covenant.Requires<ArgumentNullException>(entity != null, nameof(entity));

            var id = entity.Metadata.Name;

            logger?.LogDebugEx(() => $"Adding {id} to cache.");

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

        /// <inheritdoc/>
        private V1CustomResourceDefinition Clone(V1CustomResourceDefinition entity)
        {
            Covenant.Requires<ArgumentNullException>(entity != null, nameof(entity));

            return KubernetesJson.Deserialize<V1CustomResourceDefinition>(KubernetesJson.Serialize(entity));
        }
    }
}
