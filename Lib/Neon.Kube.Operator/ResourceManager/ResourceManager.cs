//-----------------------------------------------------------------------------
// FILE:	    ResourceManager.cs
// CONTRIBUTOR: Jeff Lill
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
using Neon.Kube.Operator.Cache;
using Neon.Kube.Operator.Controller;
using Neon.Kube.Operator.Entities;
using Neon.Kube.Operator.Finalizer;
using Neon.Kube.Operator.ResourceManager;
using Neon.Kube.Operator.Util;
using Neon.Tasks;

using AsyncKeyedLock;
using k8s;
using k8s.Autorest;
using k8s.LeaderElection;
using k8s.Models;

using Prometheus;

// $todo(jefflill):
//
// We don't currently do anything with non-null [ResourceControllerResult] returned by [ReconcileAsync()].
// I'm not entirely sure what the semantics for this are.  I assume that:
//
//      1. a subsequent DELETE will cancel a pending RECONCILE
//      2. a subsequent ADD/UPDATE will cancel (or replace?) a pending RECONILE
//      3. a subsequent MODIFY will cancel a pending RECONCILE
//
// Note also that DeletedAsync() and StatusModified() should also return an optional requeue result.
//
// I need to do some more research.  neonKUBE isn't currently depending on any of this.

namespace Neon.Kube.Operator.ResourceManager
{
    /// <summary>
    /// Used by custom Kubernetes operators to manage a collection of custom resources.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the custom Kubernetes entity type being managed.</typeparam>
    /// <typeparam name="TController">Specifies the entity controller type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class helps makes it easier to manage custom cluster resources.  Simply construct an
    /// instance with <see cref="ResourceManager{TResource, TController}"/> in your controller 
    /// (passing any custom settings as parameters) and then call <see cref="StartAsync()"/>.
    /// </para>
    /// <para>
    /// After the resource manager starts, your controller's <see cref="IResourceController{TEntity}.ReconcileAsync(TEntity)"/>, 
    /// <see cref="IResourceController{TEntity}.DeletedAsync(TEntity)"/>, and <see cref="IResourceController{TEntity}.StatusModifiedAsync(TEntity)"/> 
    /// methods will be called as related resource related events are received.
    /// </para>
    /// <para>
    /// Your handlers should perform any necessary operations to converge the actual state with set
    /// of resources passed and then return a <see cref="ResourceControllerResult"/> to control event 
    /// requeuing or <c>null</c>.
    /// </para>
    /// <note>
    /// For most operators, we recommend that all of your handlers execute shared code that handles
    /// all reconcilation by comparing the desired state represented by the custom resources passed to
    /// your handler in the dictionary passed with the current state and then performing any required 
    /// converge operations as opposed to handling just resource add/edits for reconciled events or
    /// just resource deletions for deletred events.  This is often cleaner by keeping all of your
    /// reconcilation logic in one place.
    /// </note>
    /// <para><b>OPERATOR LIFECYCLE</b></para>
    /// <para>
    /// Kubernetes operators work by watching cluster resources via the API server.  The Operator SDK
    /// starts watching the resource specified by <typeparamref name="TEntity"/> and raises the
    /// controller events as they are received, handling any failures seamlessly.  The <see cref="ResourceManager{TResource, TController}"/> 
    /// class helps keep track of the existing resources as well reducing the complexity of determining why
    /// an event was raised. Operator SDK also periodically raises reconciled events even when nothing has 
    /// changed.  This appears to happen once a minute.
    /// </para>
    /// <para>
    /// When your operator first starts, a reconciled event will be raised for each custom resource of 
    /// type <typeparamref name="TEntity"/> in the cluster and the resource manager will add
    /// these resources to its internal dictionary.  By default, the resource manager will not call 
    /// your handler until all existing resources have been added to this dictionary.  Then after the 
    /// resource manager has determined that it has collected all of the existing resources, it will call 
    /// your handler for the first time, passing a <c>null</c> resource name and your handler can start
    /// doing it's thing.
    /// </para>
    /// <note>
    /// <para>
    /// Holding back calls to your reconciled handler is important in many situations by ensuring
    /// that the entire set of resources is known before the first handler call.  Without this,
    /// your handler may perform delete actions on resources that exist in the cluster but haven't
    /// been reconciled yet which could easily cause a lot of trouble, especially if your operator
    /// gets scheduled and needs to start from scratch.
    /// </para>
    /// </note>
    /// <para>
    /// After the resource manager has all of the resources, it will start calling your reconciled
    /// handler for every event raised by the operator and start calling your deleted and status modified
    /// handlers for changes.
    /// </para>
    /// <para>
    /// Your handlers are called <b>after</b> the internal resource dictionary is updated with
    /// changes implied by the event.  This means that a new resource received with a reconcile
    /// event will be added to the dictionary before your handler is called and a resource from
    /// a deleted event will be removed before the handler is called.
    /// </para>
    /// <para>
    /// The name of the new, deleted, or changed resource will be passed to your handler.  This
    /// will be passed as <c>null</c> when nothing changed.
    /// </para>
    /// <para><b>LEADER LEADER ELECTION</b></para>
    /// <para>
    /// It's often necessary to ensure that only one entity (typically a pod) is managing a specific
    /// resource kind at a time.  For example, let's say you're writing an operator that manages the
    /// deployment of other applications based on custom resources.  In this case, it'll be important
    /// that only a single operator instance be managing the application at a time to avoid having the 
    /// operators step on each other's toes when the operator has multiple replicas running.
    /// </para>
    /// <para>
    /// The Operator SDK and other operator SDKs allow operators to indicate that only a single
    /// replica in the cluster should be allowed to process changes to custom resources.  This uses
    /// Kubernetes leases and works well for simple operators that manage only a single resource or 
    /// perhaps a handful of resources that are not also managed by other operators.
    /// </para>
    /// <para>
    /// It's often handy to be able to have an operator application manage multiple resources, with
    /// each resource kind having their own lease enforcing this exclusivity:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// Allow multiple replicas of an operator be able to load balance processing of different 
    /// resource kinds.
    /// </item>
    /// <item>
    /// Allow operators to consolidate processing of different resource kinds, some that need
    /// exclusivity and others that don't.  This can help reduce the number of operator applications
    /// that need to be created, deployed, and managed and can also reduce the number of system
    /// processes required along with their associated overhead.
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class ResourceManager<TEntity, TController> : IDisposable
        where TEntity : IKubernetesObject<V1ObjectMeta>, new()
        where TController : IResourceController<TEntity>
    {
        private bool                                           isDisposed   = false;
        private bool                                           stopIdleLoop = false;
        private AsyncReentrantMutex                            mutex        = new AsyncReentrantMutex();
        private bool                                           started      = false;
        private ResourceManagerOptions                         options;
        private ResourceManagerMetrics<TEntity, TController>   metrics;
        private IKubernetes                                    k8s;
        private IServiceProvider                               serviceProvider;
        private IResourceCache<TEntity>                        resourceCache;
        private IFinalizerManager<TEntity>                     finalizerManager;
        private AsyncKeyedLocker<string>                       lockProvider;
        private string                                         resourceNamespace;
        private Type                                           controllerType;
        private ILogger<ResourceManager<TEntity, TController>> logger;
        private DateTime                                       nextIdleReconcileUtc;
        private LeaderElectionConfig                           leaderConfig;
        private bool                                           leaderElectionDisabled;
        private LeaderElector                                  leaderElector;
        private Task                                           leaderTask;
        private Task                                           idleLoopTask;
        private Task                                           watcherTask;
        private CancellationTokenSource                        watcherTcs;
        private EventQueue<TEntity>                            eventQueue;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="namespace"></param>
        /// <param name="options">
        /// Optionally specifies options that customize the resource manager's behavior.  Reasonable
        /// defaults will be used when this isn't specified.
        /// </param>
        /// <param name="leaderConfig">
        /// Optionally specifies the <see cref="LeaderElectionConfig"/> to be used to control
        /// whether only a single entity is managing a specific resource kind at a time.  See
        /// the <b>LEADER ELECTION SECTION</b> in the <see cref="ResourceManager{TResource, TController}"/>
        /// remarks for more information.
        /// </param>
        /// <param name="leaderElectionDisabled"></param>
        /// <param name="serviceProvider"></param>
        public ResourceManager(
            IServiceProvider        serviceProvider,
            string                  @namespace             = null,
            ResourceManagerOptions  options                = null,
            LeaderElectionConfig    leaderConfig           = null,
            bool                    leaderElectionDisabled = false)
        {
            Covenant.Requires<ArgumentException>(@namespace == null || @namespace != string.Empty, nameof(@namespace));
            Covenant.Requires<ArgumentException>(!(leaderConfig != null && leaderElectionDisabled == true), nameof(leaderElectionDisabled));
            
            this.serviceProvider        = serviceProvider;
            this.resourceNamespace      = @namespace;
            this.options                = options ?? serviceProvider.GetRequiredService<ResourceManagerOptions>();
            this.leaderConfig           = leaderConfig;
            this.leaderElectionDisabled = leaderElectionDisabled;
            this.metrics                = new ResourceManagerMetrics<TEntity, TController>();
            this.logger                 = serviceProvider.GetService<ILoggerFactory>()?.CreateLogger<ResourceManager<TEntity, TController>>();
            
            this.options.Validate();

            this.controllerType = typeof(TController);
            var entityType      = typeof(TEntity);
        }

        /// <summary>
        /// Starts the resource manager.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the resource manager has already been started.</exception>
        public async Task StartAsync()
        {
            Covenant.Requires<ArgumentNullException>(serviceProvider != null, nameof(serviceProvider));

            this.k8s              = serviceProvider.GetRequiredService<IKubernetes>();
            this.resourceCache    = serviceProvider.GetRequiredService<IResourceCache<TEntity>>();
            this.finalizerManager = serviceProvider.GetRequiredService<IFinalizerManager<TEntity>>();
            this.lockProvider     = serviceProvider.GetRequiredService<AsyncKeyedLocker<string>>();

            if (leaderConfig != null && string.IsNullOrEmpty(leaderConfig.MetricsPrefix))
            {
                leaderConfig.SetCounters($"{typeof(TController).Name}_{typeof(TEntity).Name}".ToLower());
            }

            if (leaderConfig == null && !leaderElectionDisabled)
            {
                this.leaderConfig =
                    new LeaderElectionConfig(
                        this.k8s,
                        @namespace: Pod.Namespace,
                        leaseName: $"{typeof(TController).Name}.{typeof(TEntity).GetKubernetesTypeMetadata().PluralName}".ToLower(),
                        identity: Pod.Name,
                        metricsPrefix: $"{typeof(TController).Name}_{typeof(TEntity).Name}".ToLower());
            }

            if (started)
            {
                throw new InvalidOperationException($"[{nameof(ResourceManager<TEntity, TController>)}] is already running.");
            }

            //-----------------------------------------------------------------
            // Start the leader elector if enabled.

            started           = true;

            // Start the leader elector when enabled.

            if (leaderConfig != null)
            {
                leaderElector = new LeaderElector(
                    leaderConfig, 
                    onStartedLeading: OnPromotion, 
                    onStoppedLeading: OnDemotion, 
                    onNewLeader:      OnNewLeader);

                leaderTask = leaderElector.RunAsync();
            }

            await Task.CompletedTask;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (isDisposed)
            {
                return;
            }

            isDisposed = true;

            if (leaderElector != null)
            {
                leaderElector.Dispose();

                try
                {
                    leaderTask.WaitWithoutAggregate();
                }
                catch (OperationCanceledException)
                {
                    // We're expecting this.
                }

                leaderElector = null;
                leaderTask    = null;
            }

            mutex.Dispose();

            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Ensures that the instance has not been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
        private void EnsureNotDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException($"ResourceManager[{typeof(TEntity).FullName}]");
            }
        }

        /// <summary>
        /// Ensures that the controller has been started before the operator.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when Operator is running before <see cref="StartAsync()"/> is called for this controller.
        /// </exception>
        private void EnsureStarted()
        {
            if (!started)
            {
                throw new InvalidOperationException($"You must call [{nameof(TController)}.{nameof(StartAsync)}()] before starting Operator.");
            }
        }

        private async Task EnsurePermissionsAsync()
        {
            HttpOperationResponse<object> resp;
            try
            {
                if (string.IsNullOrEmpty(resourceNamespace))
                {
                    resp = await k8s.CustomObjects.ListClusterCustomObjectWithHttpMessagesAsync<TEntity>(
                        allowWatchBookmarks: true,
                        watch: true);
                }
                else
                {
                    resp = await k8s.CustomObjects.ListNamespacedCustomObjectWithHttpMessagesAsync<TEntity>(
                    resourceNamespace,
                    allowWatchBookmarks: true,
                    watch: true);
                }
            }
            catch (HttpOperationException e)
            {
                if (e.Response.StatusCode == HttpStatusCode.Forbidden)
                {
                    logger?.LogErrorEx($"Cannot watch type {typeof(TEntity)}, please check RBAC rules for the controller.");

                    throw;
                }
            }
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> and this instance has
        /// assumed leadership.
        /// </summary>
        private void OnPromotion()
        {
            logger?.LogInformationEx("PROMOTED");

            IsLeader = true;

            Task.Run(
                async () =>
                {
                    if (options.ManageCustomResourceDefinitions)
                    {
                        await CreateOrReplaceCustomResourceDefinitionAsync();
                    }

                    await EnsurePermissionsAsync();

                    // Start the IDLE reconcile loop.

                    stopIdleLoop         = false;
                    nextIdleReconcileUtc = DateTime.UtcNow + options.IdleInterval;
                    idleLoopTask         = IdleLoopAsync();

                    // Start the watcher.

                    watcherTcs  = new CancellationTokenSource();
                    watcherTask = WatchAsync(watcherTcs.Token);

                    // Inform the controller.

                    using (var scope = serviceProvider.CreateScope())
                    {
                        await CreateController(scope.ServiceProvider).OnPromotionAsync();
                    }

                }).Wait();
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> this instance has
        /// been demoted.
        /// </summary>
        private void OnDemotion()
        {
            logger?.LogInformationEx("DEMOTED");

            IsLeader = false;

            try
            {
                Task.Run(
                    async () =>
                    {
                        // Stop the IDLE loop.

                        stopIdleLoop = true;
                        await idleLoopTask;

                        // Stop the watcher.

                        watcherTcs.Cancel();
                        await watcherTask;

                        // Inform the controller.

                        using (var scope = serviceProvider.CreateScope())
                        {
                            await CreateController(scope.ServiceProvider).OnDemotionAsync();
                        }

                    }).Wait();
            }
            finally
            {
                // Reset operator state.

                stopIdleLoop = false;
                idleLoopTask = null;
                watcherTask  = null;
            }
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> and a new leader has
        /// been elected.
        /// </summary>
        /// <param name="identity">Identifies the new leader.</param>
        private void OnNewLeader(string identity)
        {
            LeaderIdentity = identity;

            Task.Run(
                async () =>
                {
                    logger?.LogInformationEx(() => $"LEADER-IS: {identity}");

                    // Inform the controller.

                    using (var scope = serviceProvider.CreateScope())
                    {
                        await CreateController(scope.ServiceProvider).OnNewLeaderAsync(identity);
                    }

                }).Wait();
        }

        private async Task CreateOrReplaceCustomResourceDefinitionAsync()
        {
            await SyncContext.Clear;

            using (var activity = TelemetryHub.ActivitySource?.StartActivity())
            {
                try
                {
                    var generator = new CustomResourceGenerator();

                    var crd = await generator.GenerateCustomResourceDefinitionAsync(typeof(TEntity));

                    var existingList = await k8s.ApiextensionsV1.ListCustomResourceDefinitionAsync(
                       fieldSelector: $"metadata.name={crd.Name()}");

                    var existingCustomResourceDefinition = existingList?.Items?.SingleOrDefault();

                    if (existingCustomResourceDefinition != null)
                    {
                        crd.Metadata.ResourceVersion = existingCustomResourceDefinition.ResourceVersion();
                        await k8s.ApiextensionsV1.ReplaceCustomResourceDefinitionAsync(crd, crd.Name());
                    }
                    else
                    {
                        await k8s.ApiextensionsV1.CreateCustomResourceDefinitionAsync(crd);
                    }

                    await k8s.WaitForCustomResourceDefinitionAsync<TEntity>();
                }
                catch (Exception e)
                {
                    logger?.LogErrorEx(e);
                }
            }
        }

        /// <summary>
        /// Returns <c>true</c> when this instance is currently the leader for the resource type.
        /// </summary>
        public bool IsLeader { get; private set; }

        /// <summary>
        /// Returns the identity of the current leader for the resource type or <c>null</c>
        /// when there is no leader.
        /// </summary>
        public string LeaderIdentity { get; private set; }

        /// <summary>
        /// Creates a controller instance.
        /// </summary>
        /// <returns>The controller.</returns>
        private IResourceController<TEntity> CreateController(IServiceProvider serviceProvider)
        {
            return (IResourceController<TEntity>)ActivatorUtilities.CreateInstance(serviceProvider, controllerType);
        }

        /// <summary>
        /// This loop handles raising of <see cref="IResourceController{TEntity}.IdleAsync()"/> 
        /// events when there's been no changes to any of the monitored resources.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task IdleLoopAsync()
        {
            await SyncContext.Clear;
            
            var loopDelay = TimeSpan.FromSeconds(1);

            while (!isDisposed && !stopIdleLoop)
            {
                await Task.Delay(loopDelay);

                if (DateTime.UtcNow >= nextIdleReconcileUtc)
                {
                    // Don't send an IDLE RECONCILE while we're when we're not the leader.

                    if (IsLeader)
                    {
                        // We're going to log and otherwise ignore any exceptions thrown by the 
                        // operator's controller or from any members above called by the controller.

                        await mutex.ExecuteActionAsync(
                            async () =>
                            {
                                try
                                {
                                    // $todo(jefflill):
                                    //
                                    // We're currently assuming that operator controllers all have a constructor
                                    // that accepts a single [IKubernetes] parameter.  We should change this to
                                    // doing actual dependency injection when we have the time.
                                    //
                                    //       https://github.com/nforgeio/neonKUBE/issues/1589

                                    using (var scope = serviceProvider.CreateScope())
                                    {
                                        await CreateController(scope.ServiceProvider).IdleAsync();
                                    }
                                }
                                catch (OperationCanceledException)
                                {
                                    // Exit the loop when the [mutex] is disposed which happens
                                    // when the resource manager is disposed.

                                    return;
                                }
                                catch (Exception e)
                                {
                                    metrics.IdleErrorCounter?.Inc();
                                    logger?.LogErrorEx(e);
                                }
                            });
                    }

                    nextIdleReconcileUtc = DateTime.UtcNow + options.IdleInterval;
                }
            }
        }

        /// <summary>
        /// Temporarily implements our own resource watcher.
        /// </summary>
        /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to stop the watcher when the operator is demoted.</param>
        /// <returns></returns>
        private async Task WatchAsync(CancellationToken cancellationToken)
        {
            await SyncContext.Clear;

            //-----------------------------------------------------------------
            // We're going to use this dictionary to keep track of the [Status]
            // property of the resources we're watching so we can distinguish
            // between changes to the status vs. changes to anything else in
            // the resource.
            //
            // The dictionary simply holds the status property serialized to
            // JSON, with these keyed by resource name.  Note that the resource
            // entities might not have a [Status] property.

            var entityType      = typeof(TEntity);
            var statusGetter    = entityType.GetProperty("Status")?.GetMethod;

            //-----------------------------------------------------------------
            // Our watcher handler action.

            var actionAsync =
                async (WatchEvent<TEntity> @event) =>
                {
                    await SyncContext.Clear;

                    ResourceControllerResult result            = null;
                    ModifiedEventType        modifiedEventType = ModifiedEventType.Other;

                    var resource     = @event.Value;
                    var resourceName = resource.Metadata.Name;
                    
                    using (await lockProvider.LockAsync(@event.Value.Uid(), cancellationToken).ConfigureAwait(false))
                    {
                        try
                        {
                            using (var scope = serviceProvider.CreateScope())
                            {
                                var cachedEntity = resourceCache.Upsert(resource, out modifiedEventType);

                                if (modifiedEventType == ModifiedEventType.Finalizing)
                                {
                                    @event.Type = WatchEventType.Modified;
                                }

                                switch (@event.Type)
                                {
                                    case WatchEventType.Added:

                                        try
                                        {
                                            metrics.ReconcileCounter?.Inc();

                                            result = await CreateController(scope.ServiceProvider).ReconcileAsync(resource);
                                        }
                                        catch (Exception e)
                                        {
                                            metrics.ReconcileErrorCounter.Inc();
                                            logger?.LogErrorEx(() => $"Event type [{@event.Type}] on resource [{resource.Kind}/{resourceName}] threw a [{e.GetType()}] error. Retrying... Attempt [{@event.Attempt}]");

                                            if (@event.Attempt < options.ErrorMaxRetryCount)
                                            {
                                                @event.Attempt += 1;
                                                resourceCache.Remove(resource);
                                                await eventQueue.RequeueAsync(@event, watchEventType: WatchEventType.Modified);
                                                return;
                                            }
                                        }

                                        break;

                                    case WatchEventType.Deleted:

                                        try
                                        {
                                            metrics.DeleteCounter?.Inc();
                                            await CreateController(scope.ServiceProvider).DeletedAsync(resource);

                                            resourceCache.Remove(resource);
                                        }
                                        catch (Exception e)
                                        {
                                            metrics.DeleteErrorCounter?.Inc();
                                            logger?.LogErrorEx(e);
                                        }

                                        break;

                                    case WatchEventType.Modified:

                                        switch (modifiedEventType)
                                        {
                                            case ModifiedEventType.Other:

                                                try
                                                {
                                                    metrics.ReconcileCounter?.Inc();

                                                    result = await CreateController(scope.ServiceProvider).ReconcileAsync(resource);
                                                }
                                                catch (Exception e)
                                                {
                                                    metrics.ReconcileErrorCounter?.Inc();
                                                    logger?.LogErrorEx(e);

                                                    if (@event.Attempt < options.ErrorMaxRetryCount)
                                                    {
                                                        logger?.LogErrorEx(() => $"Event type [{modifiedEventType}] on resource [{resource.Kind}/{resourceName}] threw a [{e.GetType()}] error. Retrying... Attempt [{@event.Attempt}]");

                                                        @event.Attempt += 1;
                                                        resourceCache.Remove(resource);
                                                        await eventQueue.RequeueAsync(@event, watchEventType: WatchEventType.Modified);
                                                        return;
                                                    }
                                                }
                                                break;

                                            case ModifiedEventType.Finalizing:

                                                try
                                                {
                                                    metrics.FinalizeCounter?.Inc();

                                                    if (!resourceCache.IsFinalizing(resource))
                                                    {
                                                        resourceCache.AddFinalizer(resource);

                                                        await finalizerManager.FinalizeAsync(resource, scope);
                                                    }

                                                    resourceCache.RemoveFinalizer(resource);
                                                }
                                                catch (Exception e)
                                                {
                                                    metrics.FinalizeErrorCounter?.Inc();
                                                    logger?.LogErrorEx(e);

                                                    resourceCache.RemoveFinalizer(resource);

                                                    if (@event.Attempt < options.ErrorMaxRetryCount)
                                                    {
                                                        logger?.LogErrorEx(() => $"Event type [{modifiedEventType}] on resource [{resource.Kind}/{resourceName}] threw a [{e.GetType()}] error. Retrying... Attempt [{@event.Attempt}]");

                                                        @event.Attempt += 1;
                                                        resourceCache.Remove(resource);
                                                        await eventQueue.RequeueAsync(@event, watchEventType: WatchEventType.Modified);
                                                        return;
                                                    }
                                                }
                                                break;

                                            case ModifiedEventType.StatusUpdate:

                                                if (statusGetter == null)
                                                {
                                                    return;
                                                }

                                                var newStatus = statusGetter.Invoke(resource, Array.Empty<object>());
                                                var newStatusJson = newStatus == null ? null : JsonSerializer.Serialize(newStatus);

                                                var oldStatus = statusGetter.Invoke(cachedEntity, Array.Empty<object>());
                                                var oldStatusJson = oldStatus == null ? null : JsonSerializer.Serialize(oldStatus);

                                                if (newStatusJson != oldStatusJson)
                                                {
                                                    try
                                                    {
                                                        metrics.StatusModifyCounter?.Inc();
                                                        await CreateController(scope.ServiceProvider).StatusModifiedAsync(resource);
                                                    }
                                                    catch (Exception e)
                                                    {
                                                        metrics.StatusModifyErrorCounter?.Inc();
                                                        logger?.LogErrorEx(e);
                                                    }
                                                }

                                                break;

                                            case ModifiedEventType.FinalizerUpdate:
                                            default:

                                                break;
                                        }

                                        break;
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            logger?.LogCriticalEx(e);
                            logger?.LogCriticalEx("Cannot recover from exception within watch loop.  Terminating process.");
                            Environment.Exit(1);
                        }
                    }

                    if (@event.Type < WatchEventType.Deleted
                        && modifiedEventType == ModifiedEventType.Other)
                    {
                        switch (result)
                        {
                            case null:
                                logger?.LogInformationEx(() =>
                                    $@"Event type [{@event.Type}] on resource [{resource.Kind}/{resourceName}] successfully reconciled. Requeue not requested.");
                                return;
                            case RequeueEventResult requeue:
                                var specificQueueTypeRequested = requeue.EventType.HasValue;
                                var requestedQueueType = requeue.EventType ?? WatchEventType.Modified;

                                if (specificQueueTypeRequested)
                                {
                                    logger?.LogInformationEx(() =>
                                            $@"Event type [{@event.Type}] on resource [{resource.Kind}/{resourceName}] successfully reconciled. Requeue requested as type [{requestedQueueType}] with delay [{requeue}].");
                                }
                                else
                                {
                                    logger?.LogInformationEx(() =>
                                        $@"Event type [{@event.Type}] on resource [{resource.Kind}/{resourceName}] successfully reconciled. Requeue requested with delay [{requeue}].");
                                }

                                resourceCache.Remove(resource);
                                await eventQueue.RequeueAsync(@event, requeue.RequeueDelay, requestedQueueType);
                                break;
                        }
                    }
                };

            var enqueueAsync =
                async (WatchEvent<TEntity> @event) =>
                {
                    await SyncContext.Clear;

                    var resource = @event.Value;
                    var resourceName = resource.Metadata.Name;

                    resourceCache.Compare(resource, out var modifiedEventType);

                    using (var scope = serviceProvider.CreateScope())
                    {
                        var controller = CreateController(scope.ServiceProvider);

                        if (!controller.Filter(resource))
                        {
                            return;
                        }
                    }

                    switch (@event.Type)
                    {
                        case WatchEventType.Added:
                        case WatchEventType.Deleted:

                            await eventQueue.DequeueAsync(@event);
                            await eventQueue.EnqueueAsync(@event);

                            break;

                        case WatchEventType.Modified:

                            if (modifiedEventType == ModifiedEventType.NoChanges)
                            {
                                return;
                            }

                            await eventQueue.DequeueAsync(@event);
                            await eventQueue.EnqueueAsync(@event);

                            break;

                        case WatchEventType.Bookmark:

                            break;  // We don't care about these.

                        case WatchEventType.Error:

                            // I believe we're only going to see this for extreme scenarios, like:
                            //
                            //      1. The CRD we're watching was deleted and recreated.
                            //      2. The watcher is so far behind that part of the
                            //         history is no longer available.
                            //
                            // We're going to log this and terminate the application, expecting
                            // that Kubernetes will reschedule it so we can start over.

                            var stub = new TEntity();

                            if (!string.IsNullOrEmpty(resourceNamespace))
                            {
                                logger?.LogCriticalEx(() => $"Critical error watching: [namespace={resourceNamespace}] {stub.ApiGroupAndVersion}/{stub.Kind}");
                            }
                            else
                            {
                                logger?.LogCriticalEx(() => $"Critical error watching: {stub.ApiGroupAndVersion}/{stub.Kind}");
                            }

                            logger?.LogCriticalEx("Terminating the pod so Kubernetes can reschedule it and we can restart the watch.");
                            Environment.Exit(1);
                            break;

                        default:
                            break;
                    }
                };


            this.eventQueue = new EventQueue<TEntity>(k8s, options, actionAsync);

            //-----------------------------------------------------------------
            // Start the watcher.

            try
            {
                await k8s.WatchAsync<TEntity>(enqueueAsync, namespaceParameter: resourceNamespace, cancellationToken: cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // This is thrown when the watcher is stopped due the operator being demoted.

                return;
            }
            catch (HttpOperationException)
            {
                return;
            }
        }
    }
}
