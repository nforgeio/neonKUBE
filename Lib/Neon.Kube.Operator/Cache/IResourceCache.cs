//-----------------------------------------------------------------------------
// FILE:	    IResourceCache.cs
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
using System.Text;
using System.Threading.Tasks;

using Neon.Kube;
using Neon.Kube.Operator.ResourceManager;

using k8s.Models;
using k8s;

namespace Neon.Kube.Operator.Cache
{
    /// <summary>
    /// Describes a resource cache.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the entity type.</typeparam>
    /// <typeparam name="TValue">Specifies the entity Kubernetes object type.</typeparam>
    internal interface IResourceCache<TEntity, TValue>
        where TValue : IKubernetesObject<V1ObjectMeta>
    {
        // $todo(marcusbooyah):
        //
        // Seems like Get(id) should throw a [KeyNotFoundExceptipon] or something
        // when the resource is not present to be consistent with how Get() methods
        // normally work.
        //
        // We have the TryGet() method to handle the resource missing case.

        /// <summary>
        /// Attempts to retrieve cached resource by ID.
        /// </summary>
        /// <param name="id">Specifies the resouce ID.</param>
        /// <returns>The resource <typeparamref name="TValue"/> when present, <c>null</c> otherwise.</returns>
        TValue Get(string id);

        /// <summary>
        /// Attempts to retrieve a cached resource by ID.
        /// </summary>
        /// <param name="id">Specifies the resource ID.</param>
        /// <param name="result">Returns the the resource if found.</param>
        /// <returns><c>true</c> when the resource exists and was return, <c>false</c> otherwise.</returns>
        bool TryGet(string id, out TValue result);

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="result"></param>
        void Compare(TValue resource, out ModifiedEventType result);

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="result"></param>
        /// <returns></returns>
        TValue Upsert(TValue resource, out ModifiedEventType result);

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        /// <param name="resource"></param>
        void Upsert(TValue resource);

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        /// <param name="resources"></param>
        void Upsert(IEnumerable<TValue> resources);

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        /// <param name="resource"></param>
        void Remove(TValue resource);

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        void Clear();

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        /// <param name="resource"></param>
        /// <returns></returns>
        bool IsFinalizing(TValue resource);

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        /// <param name="resource"></param>
        void AddFinalizer(TValue resource);

        /// <summary>
        /// $todo(marcusbooyah): Documentation
        /// </summary>
        /// <param name="resource"></param>
        void RemoveFinalizer(TValue resource);
    }
}
