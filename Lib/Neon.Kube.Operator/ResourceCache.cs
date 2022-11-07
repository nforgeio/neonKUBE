//-----------------------------------------------------------------------------
// FILE:	    ResourceCache.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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

using k8s;
using k8s.Models;

using KellermanSoftware.CompareNetObjects;
using Neon.Common;
using OpenTelemetry.Resources;

namespace Neon.Kube.Operator
{
    internal class ResourceCache<TEntity> : IResourceCache<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        private readonly ConcurrentDictionary<string, TEntity> cache;
        private readonly ConcurrentDictionary<string, TEntity> finalizingCache;
        private readonly CompareLogic comparelogLogic;

        public ResourceCache() 
        {
            cache           = new ConcurrentDictionary<string, TEntity>();
            finalizingCache = new ConcurrentDictionary<string, TEntity>();

            comparelogLogic = new CompareLogic(new ComparisonConfig()
            {
                AutoClearCache = false,
                MembersToIgnore = new List<string>()
                {
                    "ResourceVersion",
                    "ManagedFields"
                }
            });
        }

        public void Clear()
        {
            cache.Clear();
        }

        public TEntity Get(string id)
        {
            return cache.GetValueOrDefault(id);
        }

        public void Compare(TEntity entity, out ModifiedEventType result)
        {
            result = CompareEntity(entity);
        }

        public void Remove(TEntity entity)
        {
            cache.TryRemove(entity.Metadata.Uid, out _);
        }

        public TEntity Upsert(TEntity entity, out ModifiedEventType result)
        {
            var id = entity.Metadata.Uid;

            result = CompareEntity(entity);

            cache.AddOrUpdate(
                key: id, 
                addValueFactory: (id) => Clone(entity), 
                updateValueFactory: (key, oldEntity) => Clone(entity));

            return entity;

        }

        public void Upsert(IEnumerable<TEntity> entities)
        {
            foreach (var entity in entities)
            {
                var id = entity.Metadata.Uid;

                cache.AddOrUpdate(
                    key: id,
                    addValueFactory: (id) => Clone(entity),
                    updateValueFactory: (key, oldEntity) => Clone(entity));
            }
        }

        public bool IsFinalizing(TEntity entity)
        {
            var id = entity.Metadata.Uid;

            return finalizingCache.ContainsKey(id);
        }

        public void AddFinalizer(TEntity entity)
        {
            var id = entity.Metadata.Uid;

            finalizingCache.AddOrUpdate(
                    key: id,
                    addValueFactory: (id) => Clone(entity),
                    updateValueFactory: (key, oldEntity) => Clone(entity));
        }

        public void RemoveFinalizer(TEntity entity)
        {
            finalizingCache.TryRemove(entity.Metadata.Uid, out _);
        }

        private TEntity Clone(TEntity entity)
        {
            return KubernetesJson.Deserialize<TEntity>(KubernetesJson.Serialize(entity));
        }

        public ModifiedEventType CompareEntity(TEntity entity)
        {
            var id = entity.Metadata.Uid;
            
            if (!cache.ContainsKey(id))
            {
                return ModifiedEventType.Other;
            }

            var cachedEntity = Get(id);
            var comparison = comparelogLogic.Compare(entity, cachedEntity);

            if (comparison.AreEqual)
            {
                return ModifiedEventType.NoChanges;
            }

            if (comparison.Differences.All(d => d.PropertyName.Split('.')[0] == "Status"))
            {
                return ModifiedEventType.StatusUpdate;
            }

            if (comparison.Differences.All(d => d.ParentPropertyName == "Metadata.Finalizers" || d.PropertyName == "Metadata.Finalizers"))
            {
                return ModifiedEventType.FinalizerUpdate;
            }

            if (entity.DeletionTimestamp() != null)
            {
                return ModifiedEventType.Finalizing;
            }

            return ModifiedEventType.Other;
        }
    }
}
