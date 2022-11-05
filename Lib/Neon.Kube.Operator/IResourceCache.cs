//-----------------------------------------------------------------------------
// FILE:	    IResourceCache.cs
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Kube;

using k8s.Models;
using k8s;

namespace Neon.Kube.Operator
{
    internal interface IResourceCache<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>
    {
        TEntity Get(string id);

        void Compare(TEntity resource, out ModifiedEventType result);

        TEntity Upsert(TEntity resource, out ModifiedEventType result);

        void Upsert(IEnumerable<TEntity> resources);

        void Remove(TEntity resource);

        void Clear();

        bool IsFinalizing(TEntity resource);

        void AddFinalizer(TEntity resource);

        void RemoveFinalizer(TEntity resource);
    }
}
