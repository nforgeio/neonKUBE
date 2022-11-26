//-----------------------------------------------------------------------------
// FILE:	    ResourceManager.cs
// CONTRIBUTOR: Jeff Lill
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
using KubeOps.Operator.Events;
using KubeOps.Operator.Kubernetes;
using OpenTelemetry.Resources;
using Microsoft.AspNetCore.Mvc;
using KellermanSoftware.CompareNetObjects;

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

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Used by custom <b>KubeOps</b> based operators to manage a collection of custom resources.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the custom Kubernetes entity type being managed.</typeparam>
    /// <typeparam name="TController">Specifies the entity controller type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class helps makes it easier to manage custom cluster resources.  Simply construct an
    /// instance with <see cref="ResourceManager{TResource, TController}"/> in your controller 
    /// (passing any custom settings as parameters) and then call <see cref="StartAsync(string)"/>.
    /// </para>
    /// <para>
    /// After the resource manager starts, your controller's <see cref="IOperatorController{TEntity}.ReconcileAsync(TEntity)"/>, 
    /// <see cref="IOperatorController{TEntity}.DeletedAsync(TEntity)"/>, and <see cref="IOperatorController{TEntity}.StatusModifiedAsync(TEntity)"/> 
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
    /// Kubernetes operators work by watching cluster resources via the API server.  The KubeOps Operator SDK
    /// starts watching the resource specified by <typeparamref name="TEntity"/> and raises the
    /// controller events as they are received, handling any failures seamlessly.  The <see cref="ResourceManager{TResource, TController}"/> 
    /// class helps keep track of the existing resources as well reducing the complexity of determining why
    /// an event was raised.  KubeOps also periodically raises reconciled events even when nothing has 
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
    /// handler for every event raised by KUbeOps and start calling your deleted and status modified
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
    /// The <b>KubeOps</b> SDK and other operator SDKs allow operators to indicate that only a single
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
        where TEntity : class, IKubernetesObject<V1ObjectMeta>, new()
        where TController : IOperatorController<TEntity>
    {
        private bool                            isDisposed   = false;
        private bool                            stopIdleLoop = false;
        private AsyncReentrantMutex             mutex        = new AsyncReentrantMutex();
        private bool                            started      = false;
        private ResourceManagerOptions          options;
        private IKubernetes                     k8s;
        private IServiceProvider                serviceProvider;
        private IResourceCache<TEntity>         resourceCache;
        private ILockProvider<TEntity>          lockProvider;
        private string                          resourceNamespace;
        private ConstructorInfo                 controllerConstructor;
        private Type                            controllerType;
        private Func<TEntity, bool>             filter;
        private ILogger                         logger;
        private DateTime                        nextIdleReconcileUtc;
        private LeaderElectionConfig            leaderConfig;
        private LeaderElector                   leaderElector;
        private Task                            leaderTask;
        private Task                            idleLoopTask;
        private Task                            watcherTask;
        private CancellationTokenSource         watcherTcs;
        private EventQueue<TEntity>             eventQueue;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client used by the controller.</param>
        /// <param name="options">
        /// Optionally specifies options that customize the resource manager's behavior.  Reasonable
        /// defaults will be used when this isn't specified.
        /// </param>
        /// <param name="filter">
        /// <para>
        /// Optionally specifies a predicate to be use for filtering the resources to be managed.
        /// This can be useful for situations where multiple operator instances will partition
        /// and handle the resources amongst themselves.  A good example is a node based operator
        /// that handles only the resources associated with the node.
        /// </para>
        /// <para>
        /// Your filter should examine the resource passed and return <c>true</c> when the resource
        /// should be managed by this resource manager.  The default filter always returns <c>true</c>.
        /// </para>
        /// </param>
        /// <param name="logger">Optionally specifies the logger to be used by the instance.</param>
        /// <param name="leaderConfig">
        /// Optionally specifies the <see cref="LeaderElectionConfig"/> to be used to control
        /// whether only a single entity is managing a specific resource kind at a time.  See
        /// the <b>LEADER ELECTION SECTION</b> in the <see cref="ResourceManager{TResource, TController}"/>
        /// remarks for more information.
        /// </param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/>.</param>
        public ResourceManager(
            IKubernetes             k8s,
            ResourceManagerOptions  options         = null,
            Func<TEntity, bool>     filter          = null,
            ILogger                 logger          = null,
            LeaderElectionConfig    leaderConfig    = null,
            IServiceProvider        serviceProvider = null)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.k8s             = k8s;  // $todo(jefflill): Can we obtain this from KubeOps or the [IServiceProvider] somehow?
            this.serviceProvider = serviceProvider;
            this.options         = options ?? new ResourceManagerOptions();
            this.filter          = filter ?? new Func<TEntity, bool>(resource => true);
            this.logger          = logger ?? TelemetryHub.CreateLogger($"Neon.Kube.Operator.ResourceManager({typeof(TEntity).Name})");
            this.leaderConfig    = leaderConfig;
            this.resourceCache   = serviceProvider.GetRequiredService<IResourceCache<TEntity>>();
            this.lockProvider    = serviceProvider.GetRequiredService<ILockProvider<TEntity>>();

            options.Validate();

            // $todo(jefflill): https://github.com/nforgeio/neonKUBE/issues/1589
            //
            // Locate the controller's constructor that has a single [IKubernetes] parameter.

            this.controllerType = typeof(TController);
            var entityType      = typeof(TEntity);

            if (serviceProvider == null) 
            { 
                this.controllerConstructor = controllerType.GetConstructor(new Type[] { typeof(IKubernetes) });

                if (this.controllerConstructor == null)
                {
                    throw new NotSupportedException($"Controller type [{controllerType.FullName}] does not have a constructor accepting a single [{nameof(IKubernetes)}] parameter.  This is currently required.");
                }

            }
            var controller = CreateController();
        }

        /// <summary>
        /// Starts the resource manager.
        /// </summary>
        /// <param name="namespace">Optionally specifies the namespace for namespace scoped operators.</param>
        /// <exception cref="InvalidOperationException">Thrown when the resource manager has already been started.</exception>
        public async Task StartAsync(string @namespace = null)
        {
            Covenant.Requires<ArgumentException>(@namespace == null || @namespace != string.Empty, nameof(@namespace));

            if (started)
            {
                throw new InvalidOperationException($"[{nameof(ResourceManager<TEntity, TController>)}] is already running.");
            }

            //-----------------------------------------------------------------
            // Start the leader elector if enabled.

            resourceNamespace = @namespace;
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
        /// Ensures that the controller has been started before KubeOps.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when KubeOps is running before <see cref="StartAsync(string)"/> is called for this controller.
        /// </exception>
        private void EnsureStarted()
        {
            if (!started)
            {
                throw new InvalidOperationException($"You must call [{nameof(TController)}.{nameof(StartAsync)}()] before starting KubeOps.");
            }
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> and this instance has
        /// assumed leadership.
        /// </summary>
        private void OnPromotion()
        {
            logger.LogInformationEx("PROMOTED");

            IsLeader = true;

            Task.Run(
                async () =>
                {
                    // Start the IDLE reconcile loop.

                    stopIdleLoop         = false;
                    nextIdleReconcileUtc = DateTime.UtcNow + options.IdleInterval;
                    idleLoopTask         = IdleLoopAsync();

                    // Start the watcher.

                    watcherTcs  = new CancellationTokenSource();
                    watcherTask = WatchAsync(watcherTcs.Token);

                    // Inform the controller.

                    await CreateController().OnPromotionAsync();

                }).Wait();
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> this instance has
        /// been demoted.
        /// </summary>
        private void OnDemotion()
        {
            logger.LogInformationEx("DEMOTED");

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

                        await CreateController().OnDemotionAsync();

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
                    logger.LogInformationEx(() => $"LEADER-IS: {identity}");

                    // Inform the controller.

                    await CreateController().OnNewLeaderAsync(identity);

                }).Wait();
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
        private IOperatorController<TEntity> CreateController()
        {
            if (this.serviceProvider== null)
            {
                return (IOperatorController<TEntity>)controllerConstructor.Invoke(new object[] { k8s });
            }
            else
            {
                using var scope = serviceProvider.CreateScope();

                return (IOperatorController<TEntity>)ActivatorUtilities.CreateInstance(scope.ServiceProvider, controllerType);
            }
        }

        //---------------------------------------------------------------------
        // $todo(jefflill): At least support dependency injection when constructing the controller.
        //
        //      https://github.com/nforgeio/neonKUBE/issues/1589
        //
        // For some reason, KubeOps does not seem to send RECONCILE events when no changes
        // have been detected, even though we return a [ResourceControllerResult] with a
        // delay.  We're also not seeing any RECONCILE event when the operator starts and
        // there are no resources.  This used to work before we upgraded to KubeOps v7.0.0-preview2.
        //
        // NOTE: It's very possible that the old KubeOps behavior was invalid and the current
        //       behavior actually is correct.
        //
        // This completely breaks our logic where we expect to see an IDLE event after
        // all of the existing resources have been discovered or when no resources were
        // discovered.
        //
        // We're going to work around this with a pretty horrible hack for the time being:
        //
        //      1. We're going to use the [nextNoChangeReconcileUtc] field to track
        //         when the next IDLE event should be raised.  This will default
        //         to the current time plus 1 minute when the resource manager is 
        //         constructed.  This gives KubeOps a chance to discover existing
        //         resources before we start raising IDLE events.
        //
        //      2. After RECONCILE events are handled by the operator's controller,
        //         we'll reset the [nextNoChangeReconcileUtc] property to be the current
        //         time plus the [reconciledNoChangeInterval].
        //
        //      3. The [NoChangeLoop()] method below loops watching for when [nextNoChangeReconcileUtc]
        //         indicates that an IDLE RECONCILE event should be raised.  The loop
        //         will instantiate an instance of the controller, hardcoding the [IKubernetes]
        //         constructor parameter for now, rather than supporting real dependency
        //         injection.  We'll then call [ReconcileAsync()] ourselves.
        //
        //         The loop uses [mutex] to ensure that only controller event handler is
        //         called at a time, so this should be thread/task safe.
        //
        //      4. We're only going to do this for RECONCILE events right now: our
        //         operators aren't currently monitoring DELETED or STATUS-MODIFIED
        //         events and I suspect that KubeOps is probably doing the correct
        //         thing for these anyway.
        //
        // PROBLEM:
        //
        // This hack can result in a problem when KubeOps is not able to watch the resource
        // for some reason.  The problem is that if this continutes for the first 1 minute
        // delay, then the loop below will tragger an IDLE RECONCILE event with no including
        // no items, and then the operator could react by deleting any existing related physical
        // resources, which would be REALLY BAD.
        //
        // To mitigate this, I'm going to special case the first IDLE reconcile to query the
        // custom resources and only trigger the IDLE reconcile when the query succeeded and
        // no items were returned.  Otherwise KubeOps may be having trouble communicating with 
        // Kubernetes or when there are items, we should expect KubeOps to reconcile those for us.

        /// <summary>
        /// This loop handles raising of <see cref="IOperatorController{TEntity}.IdleAsync()"/> 
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

                                    var controller = CreateController();

                                    await controller.IdleAsync();
                                }
                                catch (OperationCanceledException)
                                {
                                // Exit the loop when the [mutex] is disposed which happens
                                // when the resource manager is disposed.

                                    return;
                                }
                                catch (Exception e)
                                {
                                    options.IdleErrorCounter?.Inc();
                                    logger.LogErrorEx(e);
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

                    await lockProvider.WaitAsync(@event.Value.Uid());

                    ResourceControllerResult result = null;

                    var resource = @event.Value;
                    var resourceName = resource.Metadata.Name;

                    try
                    {
                        var cachedEntity = resourceCache.Upsert(resource, out var modifiedEventType);

                        if (modifiedEventType == ModifiedEventType.Finalizing)
                        {
                            @event.Type = WatchEventType.Modified;
                        }

                        switch (@event.Type)
                        {
                            case WatchEventType.Added:

                                try
                                {
                                    options.ReconcileCounter?.Inc();

                                    result = await CreateController().ReconcileAsync(resource);

                                }
                                catch (Exception e)
                                {
                                    options.ReconcileErrorCounter.Inc();
                                    logger.LogErrorEx(() => $"Event type [{@event.Type}] on resource [{resource.Kind}/{resourceName}] " +
                                                                $"threw a [{e.GetType()}] error. Retrying... Attempt [{@event.Attempt}]");

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
                                    options.DeleteCounter?.Inc();
                                    await CreateController().DeletedAsync(resource);

                                    resourceCache.Remove(resource);
                                }
                                catch (Exception e)
                                {
                                    options.DeleteErrorCounter?.Inc();
                                    logger.LogErrorEx(e);
                                }

                                break;

                            case WatchEventType.Modified:

                                switch (modifiedEventType)
                                {
                                    case ModifiedEventType.Other:

                                        try
                                        {
                                            options.ReconcileCounter?.Inc();

                                            result = await CreateController().ReconcileAsync(resource);
                                        }
                                        catch (Exception e)
                                        {
                                            options.ReconcileErrorCounter?.Inc();
                                            logger.LogErrorEx(e);

                                            if (@event.Attempt < options.ErrorMaxRetryCount)
                                            {
                                                logger.LogErrorEx(() => $"Event type [{modifiedEventType}] on resource [{resource.Kind}/{resourceName}] " +
                                                                        $"threw a [{e.GetType()}] error. Retrying... Attempt [{@event.Attempt}]");

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
                                            options.FinalizeCounter?.Inc();

                                            if (!resourceCache.IsFinalizing(resource))
                                            {
                                                using var scope = serviceProvider.CreateScope();

                                                resourceCache.AddFinalizer(resource);

                                                await scope.ServiceProvider.GetRequiredService<IFinalizerManager<TEntity>>()
                                                    .FinalizeAsync(resource);
                                            }

                                            resourceCache.RemoveFinalizer(resource);
                                        }
                                        catch (Exception e)
                                        {
                                            options.FinalizeErrorCounter?.Inc();
                                            logger.LogErrorEx(e);

                                            resourceCache.RemoveFinalizer(resource);

                                            if (@event.Attempt < options.ErrorMaxRetryCount)
                                            {
                                                logger.LogErrorEx(() => $"Event type [{modifiedEventType}] on resource [{resource.Kind}/{resourceName}] " +
                                                                        $"threw a [{e.GetType()}] error. Retrying... Attempt [{@event.Attempt}]");

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
                                                options.StatusModifyCounter?.Inc();
                                                await CreateController().StatusModifiedAsync(resource);
                                            }
                                            catch (Exception e)
                                            {
                                                options.StatusModifyErrorCounter?.Inc();
                                                logger.LogErrorEx(e);
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
                    catch (Exception e)
                    {
                        logger.LogCriticalEx(e);
                        logger.LogCriticalEx("Cannot recover from exception within watch loop.  Terminating process.");
                        Environment.Exit(1);
                    }
                    finally
                    {
                        lockProvider.Release(resource.Uid());
                    }


                    switch (result)
                    {
                        case null:
                            logger.LogInformationEx(() =>
                                $@"Event type [{@event.Type}] on resource [{resource.Kind}/{resourceName}] successfully reconciled. Requeue not requested.");
                            return;
                        case RequeueEventResult requeue:
                            var specificQueueTypeRequested = requeue.EventType.HasValue;
                            var requestedQueueType = requeue.EventType ?? WatchEventType.Modified;

                            if (specificQueueTypeRequested)
                            {
                                logger.LogInformationEx(() =>
                                        $@"Event type [{@event.Type}] on resource [{resource.Kind}/{resourceName}] successfully reconciled. Requeue requested as type [{requestedQueueType}] with delay [{requeue}].");
                            }
                            else
                            {
                                logger.LogInformationEx(() =>
                                    $@"Event type [{@event.Type}] on resource [{resource.Kind}/{resourceName}] successfully reconciled. Requeue requested with delay [{requeue}].");
                            }

                            resourceCache.Remove(resource);
                            await eventQueue.RequeueAsync(@event, requeue.RequeueDelay, requestedQueueType);
                            break;
                    }
                };

            var enqueueAsync =
                async (WatchEvent<TEntity> @event) =>
                {
                    await SyncContext.Clear;

                    var resource = @event.Value;
                    var resourceName = resource.Metadata.Name;

                    resourceCache.Compare(resource, out var modifiedEventType);

                    if (!filter(resource))
                    {
                        return;
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
                                logger.LogCriticalEx(() => $"Critical error watching: [namespace={resourceNamespace}] {stub.ApiGroupAndVersion}/{stub.Kind}");
                            }
                            else
                            {
                                logger.LogCriticalEx(() => $"Critical error watching: {stub.ApiGroupAndVersion}/{stub.Kind}");
                            }

                            logger.LogCriticalEx("Terminating the pod so Kubernetes can reschedule it and we can restart the watch.");
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
        }
    }
}
