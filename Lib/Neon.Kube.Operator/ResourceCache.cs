//-----------------------------------------------------------------------------
// FILE:	    ResourceCache.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

using KubeOps.Operator.Entities;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Holds the cache of resources currently known by the resource manager in a form
    /// that will be presented to operator implementations as resource related events
    /// are raised.  Operator implementations should generally call <see cref="Update(TEntity)"/>
    /// whenever an existing resource is updated.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the custom Kubernetes entity type being managed.</typeparam>
    public class ResourceCache<TEntity> : IReadOnlyDictionary<string, TEntity>
        where TEntity : CustomKubernetesEntity, new()
    {
        private Dictionary<string, TEntity> resources;

        /// <summary>
        /// Internal constyructor.
        /// </summary>
        /// <param name="resources">The resource manager's internal dictionary of cached resources.</param>
        internal ResourceCache(Dictionary<string, TEntity> resources)
        {
            Covenant.Requires<ArgumentNullException>(resources != null, nameof(resources));

            this.resources = resources;
        }

        /// <inheritdoc/>
        public TEntity this[string key] => resources[key];

        /// <inheritdoc/>
        public IEnumerable<string> Keys => resources.Keys;

        /// <inheritdoc/>
        public IEnumerable<TEntity> Values => resources.Values;

        /// <inheritdoc/>
        public int Count => resources.Count;

        /// <inheritdoc/>
        public bool ContainsKey(string key) => resources.ContainsKey(key);

        /// <inheritdoc/>
        public IEnumerator<KeyValuePair<string, TEntity>> GetEnumerator() => resources.GetEnumerator();

        /// <inheritdoc/>
        public bool TryGetValue(string key, [MaybeNullWhen(false)] out TEntity value) => resources.TryGetValue(key, out value);

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator() => resources.GetEnumerator();

        /// <summary>
        /// Persists updates made by an operator controller to the resource cache.  Operator implementations
        /// should call this after modifying a resource to avoid seeing RECONCILE or STATUS-MODIFIED events
        /// for changes the operator itself made.
        /// </summary>
        /// <param name="resource">Specifies the resource being updated.</param>
        public void Update(TEntity resource)
        {
            Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));

            var name = resource.Metadata?.Name;

            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentException>(resources.TryGetValue(name, out var existing), $"Resource [{name}] is not present in the cache.  You may only update existing resources.");
            Covenant.Requires<ArgumentException>(resource.Metadata.Generation >= existing.Metadata.Generation, $"Resource [{name}]: Updated resource's [generation] cannot be less than the existing resource.");

            resources[name] = resource;
        }
    }
}
