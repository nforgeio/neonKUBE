//-----------------------------------------------------------------------------
// FILE:	    NatsSubscriptionManager.cs
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Protocol;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

using NATS;
using NATS.Client;

namespace Neon.SignalR
{
    internal sealed class NatsSubscriptionManager
    {
        private readonly ConcurrentDictionary<string, HubConnectionStore> subscriptions = new ConcurrentDictionary<string, HubConnectionStore>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, IAsyncSubscription> natsSubscriptions = new ConcurrentDictionary<string, IAsyncSubscription>(StringComparer.Ordinal);
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public async Task AddSubscriptionAsync(string id, HubConnectionContext connection, Func<string, HubConnectionStore, Task<IAsyncSubscription>> subscribeMethod)
        {
            await _lock.WaitAsync();

            try
            {
                // Avoid adding subscription if connection is closing/closed
                // We're in a lock and ConnectionAborted is triggered before OnDisconnectedAsync is called so this is guaranteed to be safe when adding while connection is closing and removing items
                if (connection.ConnectionAborted.IsCancellationRequested)
                {
                    return;
                }

                var subscription = subscriptions.GetOrAdd(id, _ => new HubConnectionStore());

                subscription.Add(connection);

                // Subscribe once
                if (subscription.Count == 1)
                {
                    var sAsync = await subscribeMethod(id, subscription);
                    sAsync.Start();
                    natsSubscriptions.GetOrAdd(id, _ => sAsync);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task RemoveSubscriptionAsync(string id, HubConnectionContext connection, object state)
        {
            await _lock.WaitAsync();

            try
            {
                if (!subscriptions.TryGetValue(id, out var subscription))
                {
                    return;
                }

                subscription.Remove(connection);

                if (subscription.Count == 0)
                {
                    subscriptions.TryRemove(id, out _);

                    if (natsSubscriptions.TryGetValue(id, out var sAsync))
                    {
                        await sAsync.DrainAsync();
                    }

                    natsSubscriptions.TryRemove(id, out _);
                }
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
