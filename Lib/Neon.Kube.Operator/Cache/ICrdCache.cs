//-----------------------------------------------------------------------------
// FILE:        ICrdCache.cs
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

using Neon.Kube;
using Neon.Kube.Operator.ResourceManager;

using k8s.Models;
using k8s;

namespace Neon.Kube.Operator.Cache
{
    /// <summary>
    /// Describes a CRD cache.
    /// </summary>
    internal interface ICrdCache
    {
        /// <summary>
        /// Attempts to retrieve cached entity by ID.
        /// </summary>
        /// <param name="id">Specifies the CRD ID.</param>
        /// <returns>The retrieved CRD or <c>null</c> when it's not cached.</returns>
        V1CustomResourceDefinition Get(string id);

        /// <summary>
        /// Attempts to retrieve a cached entity by type.
        /// </summary>
        /// <typeparam name="TEntity">Specifies the CRD type.</typeparam>
        /// <returns>The retrieved CRD or <c>null</c> when it's not cached.</returns>
        V1CustomResourceDefinition Get<TEntity>()
            where TEntity : IKubernetesObject<V1ObjectMeta>;

        /// <summary>
        /// Adds or replaces an entity in the cache.
        /// </summary>
        /// <param name="resource">Specifies the new or updated CRD.</param>
        void Upsert(V1CustomResourceDefinition resource);

        /// <summary>
        /// Removes an entity from the cache, if present.
        /// </summary>
        /// <param name="resource">Specifies the CRD to be removed.</param>
        void Remove(V1CustomResourceDefinition resource);

        /// <summary>
        /// Clears the cache.
        /// </summary>
        void Clear();
    }
}
