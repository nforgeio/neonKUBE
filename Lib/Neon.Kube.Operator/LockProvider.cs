//-----------------------------------------------------------------------------
// FILE:	    LockProvider.cs
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

using k8s;
using k8s.Models;

using KellermanSoftware.CompareNetObjects;
using Neon.Common;
using OpenTelemetry.Resources;

namespace Neon.Kube.Operator
{
    internal class LockProvider<TEntity> : ILockProvider<TEntity>
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
    {
        static readonly ConcurrentDictionary<string, SemaphoreSlim> lockDictionary = new ConcurrentDictionary<string, SemaphoreSlim>();

        /// <inheritdoc/>
        public void Release(string entityId)
        {
            SemaphoreSlim semaphore;

            if (lockDictionary.TryGetValue(entityId, out semaphore))
            {
                semaphore.Release();
                lockDictionary.TryRemove(entityId, out _);
            }
        }

        /// <inheritdoc/>
        public async Task WaitAsync(string entityId)
        {
            var semaphore = lockDictionary.GetOrAdd(entityId, new SemaphoreSlim(1, 1));

            await semaphore.WaitAsync();
        }
    }
}
