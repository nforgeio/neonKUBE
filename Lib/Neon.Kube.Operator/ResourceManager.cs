//-----------------------------------------------------------------------------
// FILE:	    ResourceManager.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Tasks;

using KubeOps.Operator;
using KubeOps.Operator.Builder;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Entities;

using k8s;
using Prometheus;
using k8s.Models;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Used by custom <b>KubeOps</b> based operators to manage a collection of custom resources.
    /// </summary>
    /// <typeparam name="TResource">Specifies the custom Kubernetes entity type.</typeparam>
    /// <typeparam name="TController">Specifies the entity controller type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class helps makes it easier to manage custom cluster resources.  Simply construct an
    /// instance with <see cref="ResourceManager{TResource, TController}"/> in your controller 
    /// (passing any custom settings as parameters) and then call <see cref="StartAsync(string)"/>.
    /// </para>
    /// <para>
    /// After the resource manager starts, your controller's <see cref="ReconciledAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>, 
    /// <see cref="DeletedAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>, and
    /// <see cref="StatusModifiedAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/> 
    /// methods will be called as related resource related events are received.
    /// </para>
    /// <para><b>KUBEOPS INTEGRATION</b></para>
    /// <para>
    /// This class is designed to integrate cleanly with operators based on the [KubeOps](https://github.com/buehler/dotnet-operator-sdk)
    /// Kubernetes Operator SDK for .NET.  You'll instantiate a <see cref="ResourceManager{TResource, IController}"/>
    /// instance for each controller, passing the custom resource type as the type parameter and then set this
    /// as a static field in your controller.  Then you'll need to add a call to 
    /// <see cref="ReconciledAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>
    /// in your controller's <b>ReconcileAsync()</b> method, a call to 
    /// <see cref="DeletedAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>
    /// in your controller's <b>DeletedAsync()</b> method and a call to 
    /// <see cref="StatusModifiedAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>
    /// on your controller <b>StatusModifiedAsync()</b> method.
    /// </para>
    /// <para>
    /// You'll also need to pass a callback to each method to handle any resource changes for that operation.
    /// The callback signature for your handler is <see cref="ResourceManager{TResource, TController}.EventHandlerAsync"/>,
    /// where the <c>name</c> parameter will be passed as the name of the changed resource or <c>null</c> when
    /// the event was raised when nothing changed.  The <b>resources</b> parameter will be passed as a read-only
    /// dictionary holding the current set of resources keyed by name.
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
    /// starts watching the resource specified by <typeparamref name="TResource"/> and raises the
    /// controller events as they are received, handling any failures seamlessly.  The <see cref="ResourceManager{TResource, TController}"/> 
    /// class helps keep track of the existing resources as well reducing the complexity of determining why
    /// an event was raised.  KubeOps also periodically raises reconciled events even when nothing has 
    /// changed.  This appears to happen once a minute.
    /// </para>
    /// <para>
    /// When your operator first starts, a reconciled event will be raised for each custom resource of 
    /// type <typeparamref name="TResource"/> in the cluster and the resource manager will add
    /// these resources to its internal dictionary.  By default, the resource manager will not call 
    /// your handler until all existing resources have been added to this dictionary.  Then after the 
    /// resource manager has determined that it has collected all of the existing resources, it will call 
    /// your handler for the first time, passing a <c>null</c> resource name and your handler can start
    /// doing it's thing.
    /// </para>
    /// <note>
    /// <para>
    /// We currently use the first NO-CHANGE reconciled event raised by KubeOps to determine that
    /// all resources have been received.
    /// </para>
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
    /// <item>
    /// By default, <see cref="ResourceManager{TResource, TController}"/> does nothing special to enforce
    /// processing exclusivity; it just relies on the the <b>KubeOps</b> SDK leader lease when enabled.
    /// This means that the <see cref="ReconciledAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>,
    /// <see cref="DeletedAsync(TResource, EventHandlerAsync, Counter)"/>, and
    /// <see cref="StatusModifiedAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>
    /// methods will only return managed resources when <b>KubeOps</b> is the leader for the current pod.
    /// </item>
    /// </list>
    /// <para>
    /// By default, <see cref="ResourceManager{TResource, TController}"/> does nothing special to enforce
    /// processing exclusivity; it just relies on the the <b>KubeOps</b> SDK leader lease when enabled.
    /// This means that the <see cref="ReconciledAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>,
    /// <see cref="DeletedAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>, and
    /// <see cref="StatusModifiedAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/>
    /// methods will only return when <b>KubeOps</b> is the leader for the current pod.
    /// </para>
    /// <para>
    /// To control leader election based on resource kind, <b>YOU MUST DISABLE</b> <b>KubeOps</b> leader
    /// election like this:
    /// </para>
    /// <code language="C#">
    /// public class Startup
    /// {
    ///     public void ConfigureServices(IServiceCollection services)
    ///     {
    ///         var operatorBuilder = services
    ///             .AddKubernetesOperator(
    ///                 settings =>
    ///                 {
    ///                     settings.EnableLeaderElection = false;  // &lt;--- DISABLE LEADER ELECTION
    ///                 });
    ///     }
    ///
    ///     public void Configure(IApplicationBuilder app)
    ///     {
    ///         app.UseKubernetesOperator();
    ///     }
    /// }
    /// </code>
    /// <para>
    /// Then you'll need to pass a <see cref="LeaderElectionConfig"/> to the <see cref="ResourceManager{TResource, TController}"/>
    /// constructor when resource processing needs to be restricted to a single operator instance (the leader).  Then 
    /// <see cref="ResourceManager{TResource, TController}"/> instances with this config will allow methods like 
    /// <see cref="ReconciledAsync(TResource, ResourceManager{TResource, TController}.EventHandlerAsync, Counter)"/> to
    /// return only when the instance holds the lease and all <see cref="ResourceManager{TResource, TController}"/> 
    /// instances without a leader config will continue returning changes.
    /// </para>
    /// </remarks>
    public sealed class ResourceManager<TResource, TController> : IDisposable
        where TResource : CustomKubernetesEntity, new()
        where TController : IResourceController<TResource>
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Defines the event handler you'll need to implement to handle custom resource events.
        /// </summary>
        /// <param name="name">Passed as name of the the changed resource or <c>null</c> when nothing has changed.</param>
        /// <param name="resources">Passed a dictionary holding the current resources.  This is keyed by resource name.</param>
        /// <returns>
        /// Returns a <see cref="ResourceControllerResult"/> controlling how events may be requeued or
        /// <c>null</c> such that nothing will be explicitly requeued.
        /// </returns>
        public delegate Task<ResourceControllerResult> EventHandlerAsync(string name, IReadOnlyDictionary<string, TResource> resources);

        //---------------------------------------------------------------------
        // Implementation

        private bool                            isDisposed          = false;
        private AsyncReentrantMutex             mutex               = new AsyncReentrantMutex();
        private Dictionary<string, TResource>   resources           = new Dictionary<string, TResource>(StringComparer.InvariantCultureIgnoreCase);
        private bool                            started             = false;
        private bool                            reconcileReceived   = false;
        private bool                            discovering         = false;
        private bool                            skipChangeDetection = false;
        private IKubernetes                     k8s;
        private string                          resourceNamespace;
        private ConstructorInfo                 controllerConstructor;
        private Func<TResource, bool>           filter;
        private INeonLogger                     log;
        private DateTime                        nextNoChangeReconcileUtc;
        private TimeSpan                        reconciledNoChangeInterval;
        private TimeSpan                        reconciledErrorBackoff;
        private TimeSpan                        deletedErrorBackoff;
        private TimeSpan                        statusModifiedErrorBackoff;
        private LeaderElectionConfig            leaderConfig;
        private LeaderElector                   leaderElector;
        private Task                            leaderTask;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client used by the controller.</param>
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
        /// <param name="discover">
        /// <para>
        /// Controls whether the resource manager will absorb all reconciled events until we
        /// detect we've discovered all existing resources.  This means that your handler can
        /// depend on all of the resources being present when <see cref="ReconciledAsync(TResource, EventHandlerAsync, Counter)"/> 
        /// returns resources the for the first time.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c> which will work for most scenarios.
        /// </para>
        /// </param>
        /// <param name="reconcileNoChangeInterval">
        /// <para>
        /// Specifies the amount of time after processing a reconcile event before processing
        /// a new event that does not change any resources.  Set <see cref="TimeSpan.Zero"/> 
        /// to disable reconcile event processing when there are no changes.  This defaults 
        /// to <b>1 minute</b>.
        /// </para>
        /// <para>
        /// This is useful as a fallback to ensure that current custom resource state actually
        /// matches the corresponding cluster or physical state.  For example, if you have 
        /// custom resources that map to running pods and one of the pods was manually deleted,
        /// after <see cref="ReconcileNoChangeInterval"/> and up to minute or so more, your 
        /// operator will receive a NO-CHANGE reconciled event which your handler can take as
        /// an opportunity to ensure that all of the expected pods actually exist.
        /// </para>
        /// </param>
        /// <param name="errorMinRequeueInterval">
        /// We capture and log any exceptions thrown by your event handlers and also schedule 
        /// the event to be retried in the future using an exponential backoff.  This property 
        /// specifies  the initial backoff time which will be doubled for every successive error
        /// until the backoff maxes out at <see cref="ErrorMaxRequeueInterval"/>.  This defaults
        /// to <b>15 seconds</b>.
        /// </param>
        /// <param name="errorMaxRequeueInterval">
        /// We capture and log any exceptions thrown by your event handlers and also schedule the event
        /// to be retried in the future using an exponential backoff.  The <see cref="ErrorMinRequeueInterval"/>
        /// property specifies the initial backoff time which will be doubled for every successive error until
        /// the backoff maxes out at <see cref="ErrorMaxRequeueInterval"/>.  This defaults to <b>5 minutes</b>.
        /// </param>
        public ResourceManager(
            IKubernetes             k8s,
            Func<TResource, bool>   filter                    = null,
            INeonLogger             logger                    = null,
            LeaderElectionConfig    leaderConfig              = null,
            bool                    discover                  = true,
            TimeSpan?               reconcileNoChangeInterval = null,
            TimeSpan?               errorMinRequeueInterval   = null,
            TimeSpan?               errorMaxRequeueInterval   = null)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.k8s                        = k8s;  // $todo(jefflill): Can we obtain this from KubeOps or the [IServiceProvider] somehow?
            this.filter                     = filter ?? new Func<TResource, bool>(resource => true);
            this.log                        = logger ?? LogManager.Default.GetLogger($"Neon.Kube.Operator.ResourceManager({typeof(TResource).Name})");
            this.discovering                = discover;
            this.reconciledNoChangeInterval = reconcileNoChangeInterval ?? TimeSpan.FromMinutes(1);
            this.nextNoChangeReconcileUtc   = DateTime.UtcNow + reconciledNoChangeInterval;
            this.ErrorMinRequeueInterval    = errorMinRequeueInterval ?? TimeSpan.FromSeconds(15);
            this.ErrorMaxRequeueInterval    = errorMaxRequeueInterval ?? TimeSpan.FromMinutes(5);
            this.reconciledErrorBackoff     = TimeSpan.Zero;
            this.deletedErrorBackoff        = TimeSpan.Zero;
            this.statusModifiedErrorBackoff = TimeSpan.Zero;
            this.leaderConfig               = leaderConfig;

log.LogDebug($"MGR_CONSTRUCTOR: 0: reconcileNoChangeInterval = {reconcileNoChangeInterval}");
            // $todo(jefflill): https://github.com/nforgeio/neonKUBE/issues/1589
            //
            // Locate the controller's constructor that has a single [IKubernetes] parameter.

            var controllerType = typeof(TController);

            this.controllerConstructor = controllerType.GetConstructor(new Type[] { typeof(IKubernetes) });

            if (this.controllerConstructor == null)
            {
                throw new NotSupportedException($"Controller type [{controllerType.FullName}] does not have a constructor accepting a single [{nameof(IKubernetes)}] parameter.  This is currently required.");
            }
        }

        /// <summary>
        /// Starts the resource manager.
        /// </summary>
        /// <param name="namespace">Optionally specifies the namespace for namespace scoped operators.</param>
        /// <exception cref="InvalidOperationException">Thrown when the resource manager has already been started.</exception>
        public async Task StartAsync(string @namespace = null)
        {
            Covenant.Requires<ArgumentException>(@namespace == null || @namespace != string.Empty, nameof(@namespace));

log.LogDebug($"MGR_START: 0");
            if (started)
            {
log.LogDebug($"MGR_START: 1");
                throw new InvalidOperationException($"[{nameof(ResourceManager<TResource, TController>)}] is already running.");
            }

log.LogDebug($"MGR_START: 2");
            resourceNamespace = @namespace;
            started           = true;

            // Start the leader elector when enabled.

            if (leaderConfig != null)
            {
                leaderElector = new LeaderElector(
                    leaderConfig, 
                    onStartedLeading: OnStartedLeading, 
                    onStoppedLeading: OnStoppedLeading, 
                    onNewLeader:      OnNewLeader);

                leaderTask = leaderElector.RunAsync();
            }

log.LogDebug($"MGR_START: 3");
            // Start the NO-CHANGE reconcile loop.

            _ = NoChangeLoopAsync();
log.LogDebug($"MGR_START: 4");

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

            resources.Clear();
            resources = null;

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
                throw new ObjectDisposedException($"ResourceManager[{typeof(TResource).FullName}]");
            }
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> and this instance has
        /// assumed leadership.
        /// </summary>
        private void OnStartedLeading()
        {
            IsLeader = true;
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> this instance has
        /// been demoted.
        /// </summary>
        private void OnStoppedLeading()
        {
            IsLeader = false;
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> and a new leader has
        /// been elected.
        /// </summary>
        /// <param name="identity">Identifies the new leader.</param>
        private void OnNewLeader(string identity)
        {
            LeaderIdentity = identity;
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
        /// Specifies the amount of time after processing a reconcile event before processing
        /// a new event that does not change any resources.  Set <see cref="TimeSpan.Zero"/> 
        /// to disable reconcile event processing when there are no changes.  This defaults 
        /// to <b>1 minute</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is useful as a fallback to ensure that current custom resource state actually
        /// matches the corresponding cluster or physical state.  For example, if you have 
        /// custom resources that map to running pods and one of the pods was manually deleted,
        /// after <see cref="ReconcileNoChangeInterval"/> and up to minute or so more, your 
        /// operator will receive a NO-CHANGE reconciled event which your handler can take as
        /// an opportunity to ensure that all of the expected pods actually exist.
        /// </para>
        /// </remarks>
        public TimeSpan ReconcileNoChangeInterval
        {
            get => this.reconciledNoChangeInterval;

            private set
            {
                this.reconciledNoChangeInterval = value;

                if (value >= TimeSpan.Zero)
                {
                    this.nextNoChangeReconcileUtc = DateTime.UtcNow + value;
                }
                else
                {
                    this.nextNoChangeReconcileUtc = DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// We capture and log any exceptions thrown by your event handlers and also schedule 
        /// the event to be retried in the future using an exponential backoff.  This property 
        /// specifies  the initial backoff time which will be doubled for every successive error
        /// until the backoff maxes out at <see cref="ErrorMaxRequeueInterval"/>.  This defaults
        /// to <b>15 seconds</b>.
        /// </summary>
        public TimeSpan ErrorMinRequeueInterval { get; private set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// We capture and log any exceptions thrown by your event handlers and also schedule the event
        /// to be retried in the future using an exponential backoff.  The <see cref="ErrorMinRequeueInterval"/>
        /// property specifies the initial backoff time which will be doubled for every successive error until
        /// the backoff maxes out at <see cref="ErrorMaxRequeueInterval"/>.  This defaults to <b>5 minutes</b>.
        /// </summary>
        public TimeSpan ErrorMaxRequeueInterval { get; private set; } = TimeSpan.FromMinutes(5);

        /// <summary>
        /// Computes the backoff timeout for exceptions caught by the event handlers.
        /// </summary>
        /// <param name="errorBackoff">
        /// Passed as the current error backoff time being tracked for the event.  This
        /// will be increased honoring in the <see cref="ErrorMinRequeueInterval"/> and
        /// <see cref="ErrorMaxRequeueInterval"/> constraints and will also be returned
        /// as the backoff.
        /// </param>
        /// <returns>The backoff <see cref="TimeSpan"/>.</returns>
        private TimeSpan ComputeErrorBackoff(ref TimeSpan errorBackoff)
        {
            if (reconciledErrorBackoff <= TimeSpan.Zero)
            {
                return errorBackoff = ErrorMinRequeueInterval;
            }
            else
            {
                return errorBackoff = NeonHelper.Min(TimeSpan.FromTicks(reconciledErrorBackoff.Ticks * 2), ErrorMaxRequeueInterval);
            }
        }

        /// <summary>
        /// Call this when your controller receives a <b>reconciled</b> event, passing the
        /// resource received.  This method adds the resource to the collection if it  doesn't 
        /// already exist and then calls your handler with the resource name and a dictionary of 
        /// the existing resources when a change is detected.  The resource name will be passed as
        /// <c>null</c> when no change is detected or when all existing resources have been
        /// collected.
        /// </summary>
        /// <param name="resource">The custom resource received or <c>null</c> when nothing has changed.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <remarks>
        /// <para>
        /// By default, the resource manager will hold off calling your handler until all
        /// existing resources have been receieved.  You can disable this behavior by passing
        /// <c>discover: false</c> to the constructor.
        /// </para>
        /// </remarks>
        public async Task<ResourceControllerResult> ReconciledAsync(TResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
            await SyncContext.Clear;

log.LogDebug($"MGR_RECONCILE: 0:");
            EnsureNotDisposed();

            if (resource != null && !filter(resource))
            {
log.LogDebug($"MGR_RECONCILE: 1: EXIT");
                return null;
            }
log.LogDebug($"MGR_RECONCILE: 2: name = [{resource?.Metadata.Name}]");

            reconcileReceived = true;

            try
            {
                Covenant.Requires<ArgumentNullException>(handler != null, nameof(handler));

                return await mutex.ExecuteFuncAsync(
                    async () =>
                    {
log.LogDebug($"MGR_RECONCILE: 3:");
                        var name    = resource?.Metadata.Name;
                        var changed = false;
                        var utcNow  = DateTime.UtcNow;

                        if (resource == null)
                        {
log.LogDebug($"MGR_RECONCILE: 4:");
                            // The [NoChangeAsync] loop below is sending these now so we're
                            // going always treat this as a change such that the user's operator
                            // will see these.

                            changed = true;
                        }
                        else
                        {
                            // Determine whether the object has actually changed unless we're
                            // still discovering resources and change detection is disabled.

log.LogDebug($"MGR_RECONCILE: 4: generation = {resource.Metadata.Generation}");
                            if (resources.TryGetValue(resource.Metadata.Name, out var existing))
                            {
log.LogDebug($"MGR_RECONCILE: 5A: skipChangeDetection={skipChangeDetection}");
                                changed = skipChangeDetection || resource.Metadata.Generation != existing.Metadata.Generation;
log.LogDebug($"MGR_RECONCILE: 5B: changed={changed}");
                            }
                            else
                            {
log.LogDebug($"MGR_RECONCILE: 6: changed = NEW");
                                changed = true;
                            }

                            resources[name] = resource;
                        }

log.LogDebug($"MGR_RECONCILE: 7: discovering={discovering}");
                        if (discovering)
                        {
log.LogDebug($"MGR_RECONCILE: 8: EXIT");
                            // We're still receiving known resources.

                            log.LogInfo($"RECONCILED: {name} (discovering resources)");
log.LogDebug($"MGR_RECONCILE: 9: EXIT");

                            return null;
                        }
log.LogDebug($"MGR_RECONCILE: 10");

                        if (!changed)
                        {
log.LogDebug($"MGR_RECONCILE: 11: EXIT");
                            // It's not time yet for another NO-CHANGE handler call.

                            return null;
                        }
log.LogDebug($"MGR_RECONCILE: 12");

                        var result = await handler(changed ? name : null, resources);

log.LogDebug($"MGR_RECONCILE: 13A: result is null: {result == null}");

                        reconciledErrorBackoff = TimeSpan.Zero;   // Reset after a success

log.LogDebug($"MGR_RECONCILE: 13B: EXIT");
                        return result;
                    });
            }
            catch (Exception e)
            {
log.LogDebug($"MGR_RECONCILE: 14");
                log.LogError(e);
                errorCounter?.Inc();

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff(ref reconciledErrorBackoff));
            }
log.LogDebug($"MGR_RECONCILE: 15: EXIT");
        }

        /// <summary>
        /// Call this when your controller receives a <b>deleted</b> event, passing the resource
        /// receievd.  If the resource exists in the collection, this method will remove it and call 
        /// your handler.  The handler is not called when the resource does not exist the collection
        /// or while we're still discovering existing resources.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named resource is not currently present.</exception>
        /// <remarks>
        /// <para>
        /// By default, the resource manager will hold off calling your handler until all
        /// existing resources have been receieved.  You can disable this behavior by passing
        /// <c>discover: false</c> to the constructor.
        /// </para>
        /// </remarks>
        public async Task<ResourceControllerResult> DeletedAsync(TResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
            await SyncContext.Clear;

log.LogDebug($"MGR_DELETED: 0");
            EnsureNotDisposed();

            if (resource != null && !filter(resource))
            {
                return null;
            }

            try
            {
                Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));
                Covenant.Requires<ArgumentNullException>(handler != null, nameof(handler));

                return await mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        var name = resource.Metadata.Name;

                        if (!resources.ContainsKey(name))
                        {
                            return null;
                        }

                        resources.Remove(name);

                        if (!discovering)
                        {
                            var result = await handler(resource.Metadata.Name, resources);

                            deletedErrorBackoff = TimeSpan.Zero;   // Reset after a success

                            return result;
                        }
                        else
                        {
                            log.LogInfo($"DELETED: {resource.Metadata.Name} (discovering resources)");
                            return null;
                        }
                    });
            }
            catch (Exception e)
            {
                log.LogError(e);
                errorCounter?.Inc();

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff(ref deletedErrorBackoff));
            }
        }

        /// <summary>
        /// Call this when a <b>status-modified</b> event was received, passing the resource
        /// reeceived.  This method replaces any existing resource with the same name in the 
        /// collection.  The handler is not called when the resource does not exist in the
        /// collection or while we're still discovering existing resources.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <remarks>
        /// <para>
        /// By default, the resource manager will hold off calling your handler until all
        /// existing resources have been receieved.  You can disable this behavior by passing
        /// <c>discover: false</c> to the constructor.
        /// </para>
        /// </remarks>
        public async Task<ResourceControllerResult> StatusModifiedAsync(TResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
            await SyncContext.Clear;

log.LogDebug($"MGR_STATUS-MODIFIED: 0");
            EnsureNotDisposed();

            if (resource != null && !filter(resource))
            {
                return null;
            }

            try
            {
                Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));
                Covenant.Requires<ArgumentNullException>(handler != null, nameof(handler));

                return await mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        var name = resource.Metadata.Name;

                        if (!resources.ContainsKey(name))
                        {
                            return null;
                        }

                        resources[name] = resource;

                        if (!discovering)
                        {
                            var result = await handler(resource.Metadata.Name, resources);

                            statusModifiedErrorBackoff = TimeSpan.Zero;   // Reset after a success

                            return result;
                        }
                        else
                        {
                            log.LogInfo($"STATUS-MODIFIED: {resource.Metadata.Name} (discovering resources)");
                            return null;
                        }
                    });
            }
            catch (Exception e)
            {
                log.LogError(e);
                errorCounter?.Inc();

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff(ref statusModifiedErrorBackoff));
            }
        }

        /// <summary>
        /// Determines whether a custom resource with the specific name exists.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <returns><c>true</c> when the name exists.</returns>
        public async Task<bool> ContainsAsync(string name)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            return await mutex.ExecuteFuncAsync(async () => await Task.FromResult(resources.ContainsKey(name)));
        }

        /// <summary>
        /// Attempts to retrieve a custom resource by name.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <returns>Returns the resource if it exists or <c>null</c>.</returns>
        public async Task<TResource> GetResourceAsync(string name)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            return await mutex.ExecuteFuncAsync(
                async () =>
                {
                    if (resources.TryGetValue(name, out var resource))
                    {
                        return await Task.FromResult(resource);
                    }
                    else
                    {
                        return await Task.FromResult<TResource>(null);
                    }
                });
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
        // This completely breaks our logic where we expect to see a NO-CHANGE event after
        // all of the existing resources have been discovered or when no resources were
        // discovered.
        //
        // We're going to work around this with a pretty horrible hack for the time being:
        //
        //      1. We're going to use the [nextNoChangeReconcileUtc] field to track
        //         when the next NO-CHANGE event should be raised.  This will default
        //         to the current time plus 1 minute when the resource manager is 
        //         constructed.  This gives KubeOps a chance to discover existing
        //         resources before we start raising NO-CHANGE events.
        //
        //      2. After RECONCILE events are handled by the operator's controller,
        //         we'll reset the [nextNoChangeReconcileUtc] property to be the current
        //         time plus the [reconciledNoChangeInterval].
        //
        //      3. The [NoChangeLoop()] method below loops watching for when [nextNoChangeReconcileUtc]
        //         indicates that a NO-CHANGE RECONCILE event should be raised.  The loop
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
        // delay, then the loop below will tragger a NO-CHANGE RECONCILE event with no including
        // no items, and then the operator could react by deleting any existing related physical
        // resources, which would be REALLY BAD.
        //
        // To mitigate this, I'm going to special case the first NO-CHANGE reconcile to query the
        // custom resources and only trigger the NO-CHANGE reconcile when the query succeeded and
        // no items were returned.  Otherwise KubeOps may be having trouble communicating with 
        // Kubernetes or when there are items, we should expect KubeOps to reconcile those for us.
        //
        // This is somewhat FRAGILE!

        /// <summary>
        /// This loop handles raising of <see cref="ReconciledAsync(TResource, EventHandlerAsync, Counter)"/> 
        /// events when there's been no changes to any of the monitored resources.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task NoChangeLoopAsync()
        {
log.LogDebug($"MGR_CHANGE-LOOP: 0");
            var loopDelay = TimeSpan.FromSeconds(1);

            while (!isDisposed)
            {
                await Task.Delay(loopDelay);
//log.LogDebug($"MGR_CHANGE-LOOP: 1A: nextNoChangeReconcileUtc={nextNoChangeReconcileUtc} ({nextNoChangeReconcileUtc - DateTime.UtcNow})");

                var reconcileDiscovered = false;

                if (DateTime.UtcNow >= nextNoChangeReconcileUtc)
                {
                    nextNoChangeReconcileUtc = DateTime.UtcNow + reconciledNoChangeInterval;

log.LogDebug($"MGR_CHANGE-LOOP: 1B: RECONCILE_NOCHANGE!!!");
log.LogDebug($"MGR_CHANGE-LOOP: 1C: reconcileReceived={reconcileReceived} discovering={discovering}");
                    if (reconcileReceived)
                    {
                        // It's been [reconciledNoChangeInterval] since we saw the last 
                        // resource from KubeOps, so we're going to assume that we have
                        // all of them.  So we're ready to send RECONCILE events for all
                        // discovered resources to the operator's handler.

log.LogDebug($"MGR_CHANGE-LOOP: 1D: ITEMS EXIST");
                        reconcileDiscovered = discovering;
                        discovering         = false;
                    }
                    else
                    {
                        // If we're going to trigger the first NO-CHANGE RECONCILE, and
                        // we haven't seen an resources from KubeOps, we need to ensure
                        // that we have connectivity to Kubernetes and that there really
                        // aren't any known resources for the operator.
                        //
                        // We'll continue the loop for resource listing falures and also
                        // when resources do exist.  We do the latter with the expection
                        // that KubeOps will discover and report the existing resources in
                        // the near future.
                        //
                        // This is a bit risky because we're assuming that KubeOps is
                        // seeing the same resources from Kubernetes that we are here.
                        // I believe that waiting a minute for KubeOps to stablize and
                        // these other mitigations will be pretty safe though.

                        try
                        {
                            IList<TResource> items;

                            if (resourceNamespace != null)
                            {
                                items = (await k8s.ListNamespacedCustomObjectAsync<TResource>(resourceNamespace)).Items;
                            }
                            else
                            {
                                items = (await k8s.ListClusterCustomObjectAsync<TResource>()).Items;
                            }

                            if (items.Any(filter))
                            {
log.LogDebug($"MGR_CHANGE-LOOP: 1E: ITEMS EXIST");
                                continue;
                            }

log.LogDebug($"MGR_CHANGE-LOOP: 1F: discovering={discovering} reconcileDiscovered={reconcileDiscovered}");
                            reconcileDiscovered = discovering;
                            discovering         = false;

                            log.LogInfo($"All resources discovered.");
log.LogDebug($"MGR_CHANGE-LOOP: 1G: discovering={discovering} reconcileDiscovered={reconcileDiscovered}");
                        }
                        catch (Exception e)
                        {
                            log.LogWarn(e);
                            continue;
                        }
                    }

                    // Don't send a NO-CHANGE RECONCILE while we're still discovering resources.

log.LogDebug($"MGR_CHANGE-LOOP: 1H: discovering={discovering}");
                    if (discovering)
                    {
log.LogDebug($"MGR_CHANGE-LOOP: 1I:");
                        continue;
                    }

log.LogDebug($"MGR_CHANGE-LOOP: 2:");
                    // We're going to log and otherwise ignore any exceptions thrown by the 
                    // the operator's controller or any code above called by the controller.

                    await mutex.ExecuteActionAsync(
                        async () =>
                        {
log.LogDebug($"MGR_CHANGE-LOOP: 3");

                            try
                            {
                                // $todo(jefflill):
                                //
                                // We're currently assuming that operator controllers all have a constructor
                                // that accepts a single [IKubernetes] parameter.  We should change this to
                                // doing real dependency injection when we have the time.
                                //
                                //       https://github.com/nforgeio/neonKUBE/issues/1589

log.LogDebug($"MGR_CHANGE-LOOP: 4A");
                                var controller = (IResourceController<TResource>)controllerConstructor.Invoke(new object[] { k8s });
log.LogDebug($"MGR_CHANGE-LOOP: 4B");

                                // Reconcile all of the resources when we just finished discovering them
                                // otherwise send a NO-CHANGE RECONCILE.
                                //
                                // We're going to set [skipChangeDetection=true] while we're doing this so
                                // that all off the discovered resources will be considered as new when
                                // we handle them.  We're also going to catch and log any exceptions here,
                                // to ensure that all existing resources get reconciled.

                                if (reconcileDiscovered)
                                {
log.LogDebug($"MGR_CHANGE-LOOP: 4C: count={resources.Count}");
                                    try
                                    {
                                        skipChangeDetection = true;

                                        foreach (var resource in resources.Values)
                                        {
log.LogDebug("=======================================================================");
log.LogDebug($"MGR_CHANGE-LOOP: 4D: name={resource.Metadata.Name}");
log.LogDebug("=======================================================================");
                                            await controller.ReconcileAsync(resource);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        log.LogWarn(e);
                                    }
                                    finally
                                    {
                                        skipChangeDetection = false;
                                    }

log.LogDebug($"MGR_CHANGE-LOOP: 4D:");
                                }

log.LogDebug("=======================================================================");
log.LogDebug($"MGR_CHANGE-LOOP: 4E: NULL");
log.LogDebug("=======================================================================");
                                await controller.ReconcileAsync(null);
log.LogDebug($"MGR_CHANGE-LOOP: 4F");
                            }
                            catch (OperationCanceledException)
                            {
log.LogDebug($"MGR_CHANGE-LOOP: 4G: OPERATION CANCELLED");
                                // Exit the loop when the [mutex] is disposed which happens
                                // when the resource manager is disposed.

                                return;
                            }
                            catch (Exception e)
                            {
log.LogDebug($"MGR_CHANGE-LOOP: 5: {NeonHelper.ExceptionError(e)}");
                                log.LogError(e);
                            }
                        });

                    nextNoChangeReconcileUtc = DateTime.UtcNow + reconciledNoChangeInterval;
log.LogDebug($"MGR_CHANGE-LOOP: 6");
                }

//log.LogDebug($"MGR_CHANGE-LOOP: 7");
            }
        }
    }
}
