//-----------------------------------------------------------------------------
// FILE:	    EventQueue.cs
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
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Tasks;

using KubeOps.Operator;
using KubeOps.Operator.Builder;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Entities;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Prometheus;
using System.Reactive.Subjects;
using KubeOps.Operator.Kubernetes;
using OpenTelemetry.Resources;

namespace Neon.Kube.Operator
{
    internal class EventQueue<TEntity>
        where TEntity : class, IKubernetesObject<V1ObjectMeta>
    {
        private readonly IKubernetes                                                            k8s;
        private readonly ILogger                                                                logger;
        private readonly ResourceManagerOptions                                                 options;
        private readonly ConcurrentDictionary<WatchEvent<TEntity>, CancellationTokenSource>     queue;
        private readonly Func<WatchEvent<TEntity>, Task>                                        eventHandler;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="k8s"></param>
        /// <param name="options"></param>
        /// <param name="eventHandler"></param>
        public EventQueue(
            IKubernetes                         k8s,
            ResourceManagerOptions              options,
            Func<WatchEvent<TEntity>, Task>     eventHandler)
        {
            this.k8s = k8s;
            this.options = options;
            this.eventHandler = eventHandler;
            this.logger = TelemetryHub.CreateLogger($"Neon.Kube.Operator.EventQueue({typeof(TEntity).Name})");
            this.queue = new ConcurrentDictionary<WatchEvent<TEntity>, CancellationTokenSource>();
        }

        /// <summary>
        /// Used to notigfy the queue of a new reconcilliation request. This will make sure that any pending
        /// requeue requests are cancelled, since they are no longer valid.
        /// </summary>
        /// <param name="event"></param>
        /// <returns></returns>
        public async Task NotifyAsync(WatchEvent<TEntity> @event)
        {
            var queuedEvent = queue.Keys.Where(key => key.Value.Uid() == @event.Value.Uid()).FirstOrDefault();

            if (queuedEvent != null)
            {
                if (@event.Value.Generation() > queuedEvent.Value.Generation())
                {
                    await DequeueAsync(queuedEvent);
                }
            }
        }

        /// <summary>
        /// Requeue an event.
        /// </summary>
        /// <param name="event"></param>
        /// <param name="delay"></param>
        /// <param name="watchEventType"></param>
        /// <returns></returns>
        public async Task EnqueueAsync(
            WatchEvent<TEntity>  @event,
            TimeSpan?           delay          = null,
            WatchEventType?     watchEventType = null)
        {
            await SyncContext.Clear;

            var resource = @event.Value;

            logger.LogDebugEx(() => $"Event [{@event.Type}] queued for resource [{resource.Kind}/{resource.Name()}] ");

            if (queue.Keys.Any(key => key.Value.Uid() == @event.Value.Uid()))
            {
                logger.LogInformationEx(() => $"Event [{@event.Type}] already exists for resource [{resource.Kind}/{resource.Name()}], aborting");
                return;
            }

            if (watchEventType == null)
            {
                watchEventType = @event.Type;
            }

            if (delay == null)
            {
                delay = GetDelay(@event.Attempt);

                logger.LogDebugEx(() => $"Event [{@event.Type}] delay for resource [{resource.Kind}/{resource.Name()}]: {delay}");
            }

            @event.Type = watchEventType.Value;

            var cts = new CancellationTokenSource();

            queue.TryAdd(@event, cts);

            _ = QueueAsync(@event, delay.Value, cts.Token);
        }

        /// <summary>
        /// Queue an event, but dequeue existing event first.
        /// </summary>
        /// <param name="event"></param>
        /// <param name="delay"></param>
        /// <param name="watchEventType"></param>
        /// <returns></returns>
        public async Task RequeueAsync(
            WatchEvent<TEntity> @event,
            TimeSpan?           delay          = null, 
            WatchEventType?     watchEventType = null)
        {
            try
            {
                var old = queue.Keys.Where(key => key.Value.Uid() == @event.Value.Uid()).Single();

                await DequeueAsync(old);
            }
            catch (Exception e)
            {
                logger.LogErrorEx(e);
            }

            await EnqueueAsync(@event, delay, watchEventType);
        }

        /// <summary>
        /// Dequeue an event.
        /// </summary>
        /// <param name="event"></param>
        /// <returns></returns>
        public async Task DequeueAsync(WatchEvent<TEntity> @event)
        {
            await SyncContext.Clear;

            var queuedEvent = queue.Keys.Where(key => key.Value.Uid() == @event.Value.Uid()).FirstOrDefault();

            if (queuedEvent?.Value != null)
            {
                if (!queue[queuedEvent].IsCancellationRequested)
                {
                    queue[queuedEvent].Cancel();
                }

                queue.TryRemove(queuedEvent, out _);
            }
        }

        private async Task QueueAsync(WatchEvent<TEntity> @event, TimeSpan delay, CancellationToken cancellationToken)
        {
            await SyncContext.Clear;

            try
            {
                if (delay > TimeSpan.Zero)
                {
                    logger.LogDebugEx(() => $"Sleeping before executing event [{@event.Type}] for resource [{@event.Value.Kind}/{@event.Value.Name()}]");
                 
                    await Task.Delay(delay, cancellationToken);
                }

                logger.LogDebugEx(() => $"Executing event [{@event.Type}] for resource [{@event.Value.Kind}/{@event.Value.Name()}]");

                await eventHandler?.Invoke(@event);

                logger.LogDebugEx(() => $"Event [{@event.Type}] executed for resource [{@event.Value.Kind}/{@event.Value.Name()}]");

                queue.TryRemove(@event, out _);
            }
            catch (TaskCanceledException)
            {
                logger.LogDebugEx($"Canceling task for [{@event.Type}] event on resource [{@event.Value.Kind}/{@event.Value.Name()}]");

                var queuedEvent = queue.Keys.Where(key => key.Value.Uid() == @event.Value.Uid()).FirstOrDefault();

                if (queuedEvent != null)
                {
                    if (queue.TryRemove(queuedEvent, out _))
                    {
                        logger.LogDebugEx($"Sucessfully canceled task for [{@event.Type}] event on resource [{@event.Value.Kind}/{@event.Value.Name()}]");
                    }
                }
            }
        }

        private TimeSpan GetDelay(int attempts)
        {
            var delay = Math.Min(options.ErrorMinRequeueInterval.TotalMilliseconds * (attempts), options.ErrorMaxRequeueInterval.TotalMilliseconds);

            return TimeSpan.FromMilliseconds(delay);
        }
    }
}
