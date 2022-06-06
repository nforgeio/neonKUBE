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
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Entities;

using Prometheus;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Used by custom <b>KubeOps</b> based operators to manage a collection of custom resources.
    /// </summary>
    /// <typeparam name="TResource">The custom Kubernetes entity type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class helps makes it easier to manage custom cluster resources.  Simply call
    /// <see cref="ReconciledAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>, 
    /// <see cref="DeletedAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>, and
    /// <see cref="StatusModifiedAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/> 
    /// when your operator receives related events from the operator, passing a handler callback that
    /// handles changes to the cluster resources being watched, requested requeue events, as well
    /// as periodic reconciled events raised when nothing has changed.
    /// </para>
    /// <para><b>KUBEOPS INTEGRATION</b></para>
    /// <para>
    /// This class is designed to integrate cleanly with operators based on the [KubeOps](https://github.com/buehler/dotnet-operator-sdk)
    /// Kubernetes Operator SDK for .NET.  You'll instantiate a <see cref="ResourceManager{TCustomResource}"/>
    /// instance for each controller, passing the custom resource type as the type parameter and then set this
    /// as a static field in your controller.  Then you'll need to add a call to 
    /// <see cref="ReconciledAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>
    /// in your controller's <b>ReconcileAsync()</b> method, a call to 
    /// <see cref="DeletedAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>
    /// in your controller's <b>DeletedAsync()</b> method and a call to 
    /// <see cref="StatusModifiedAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>
    /// on your controller <b>StatusModifiedAsync()</b> method.
    /// </para>
    /// <para>
    /// You'll also need to pass a callback to each method to handle any resource changes for that operation.
    /// The callback signature for your handler is <see cref="ResourceManager{TCustomResource}.EventHandlerAsync"/>,
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
    /// controller events as they are received, handling any failures seamlessly.  The <see cref="ResourceManager{TCustomResource}"/> 
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
    /// We currently use the first no-change reconciled event raised by KubeOps to determine that
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
    /// By default, <see cref="ResourceManager{TCustomResource}"/> does nothing special to enforce
    /// processing exclusivity; it just relies on the the <b>KubeOps</b> SDK leader lease when enabled.
    /// This means that the <see cref="ReconciledAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>,
    /// <see cref="DeletedAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>, and
    /// <see cref="StatusModifiedAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>
    /// methods will only return managed resources when <b>KubeOps</b> is the leader for the current pod.
    /// </item>
    /// </list>
    /// <para>
    /// By default, <see cref="ResourceManager{TCustomResource}"/> does nothing special to enforce
    /// processing exclusivity; it just relies on the the <b>KubeOps</b> SDK leader lease when enabled.
    /// This means that the <see cref="ReconciledAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>,
    /// <see cref="DeletedAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>, and
    /// <see cref="StatusModifiedAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/>
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
    /// Then you'll need to pass a <see cref="LeaderElectionConfig"/> to the <see cref="ResourceManager{TCustomResource}"/>
    /// constructor when resource processing needs to be restricted to a single operator instance (the leader).  Then 
    /// <see cref="ResourceManager{TCustomResource}"/> instances with this config will allow methods like 
    /// <see cref="ReconciledAsync(TResource, ResourceManager{TResource}.EventHandlerAsync, Counter)"/> to
    /// return only when the instance holds the lease and all <see cref="ResourceManager{TCustomResource}"/> instances
    /// without a leader config will continue returning changes.
    /// </para>
    /// </remarks>
    public sealed class ResourceManager<TResource> : IDisposable
        where TResource : CustomKubernetesEntity
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

        private bool                            isDisposed = false;
        private AsyncMutex                      mutex      = new AsyncMutex();
        private Dictionary<string, TResource>   resources  = new Dictionary<string, TResource>(StringComparer.InvariantCultureIgnoreCase);
        private Func<TResource, bool>           filter;
        private INeonLogger                     log;
        private bool                            waitForAll;
        private DateTime                        nextNoChangeReconciledUtc;
        private TimeSpan                        reconciledNoChangeInterval;
        private TimeSpan                        reconciledErrorBackoff;
        private TimeSpan                        deletedErrorBackoff;
        private TimeSpan                        statusModifiedErrorBackoff;
        private LeaderElector                   leaderElector;
        private Task                            leaderTask;

        /// <summary>
        /// Default constructor.
        /// </summary>
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
        /// the <b>LEADER ELECTION SECTION</b> in the <see cref="ResourceManager{TCustomResource}"/>
        /// remarks for more information.
        /// </param>
        /// <param name="waitForAll">
        /// <para>
        /// Controls whether the resource manager will absorb all reconciled events until an
        /// event is raised that indicates that nothing has changed.  This happens when the 
        /// manager has received all of the resources.  Doing this means that your handler can
        /// depend on all of the resources being present when <see cref="ReconciledAsync(TResource, EventHandlerAsync, Counter)"/> 
        /// returns resources the for the first time.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c> which will work for most scenarios.
        /// </para>
        /// </param>
        public ResourceManager(
            Func<TResource, bool>   filter       = null,
            INeonLogger             logger       = null,
            LeaderElectionConfig    leaderConfig = null,
            bool                    waitForAll   = true)
        {
            this.filter                     = filter ?? new Func<TResource, bool>(resource => true);
            this.log                        = logger ?? LogManager.Default.GetLogger($"Neon.Kube.Operator.ResourceManager({typeof(TResource).Name})");
            this.waitForAll                 = waitForAll;
            this.nextNoChangeReconciledUtc  = DateTime.UtcNow;
            this.reconciledNoChangeInterval = TimeSpan.FromMinutes(5);
            this.reconciledErrorBackoff     = TimeSpan.Zero;
            this.deletedErrorBackoff        = TimeSpan.Zero;
            this.statusModifiedErrorBackoff = TimeSpan.Zero;

            // Start the leader elector when enabled.

            if (leaderConfig != null)
            {
                this.leaderElector = new LeaderElector(leaderConfig);
                this.leaderTask    = leaderElector.RunAsync();
            }
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
                    // We're expoecting this.
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
            // $todo(jefflill)
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> this instance has
        /// been demoted.
        /// </summary>
        private void OnStoppedLeading()
        {
            // $todo(jefflill)
        }

        /// <summary>
        /// Called when the instance has a <see cref="LeaderElector"/> and a new leader has
        /// been elected.
        /// </summary>
        /// <param name="identity">Identifies the new leader.</param>
        private void OnNewLeader(string identity)
        {
            // $todo(jefflill)
        }

        /// <summary>
        /// Specifies the amount of time after processing a reconcile event before processing
        /// a new event that does not change any resources.  This defaults to <b>1 minute</b>.
        /// Set <see cref="TimeSpan.Zero"/> to disable reconcile event processing when there 
        /// are no changes.  This defaults to <b>5 minutes</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is useful as a fallback to ensure that current custom resource state actually
        /// matches the corresponding cluster or physical state.  For example, if you have 
        /// custom resources that map to running pods and one of the pods was manually deleted,
        /// after <see cref="ReconcileNoChangeInterval"/> and up to minute or so more, your 
        /// operator will receive a no-change reconciled event which your handler can take as
        /// an oppertunity to ensure that all of the expected pods actually exist.
        /// </para>
        /// <note>
        /// The actual resolution of this property is rougly <b>1 minute</b> at this time due
        /// to how the KubeOps SDK works.
        /// </note>
        /// </remarks>
        public TimeSpan ReconcileNoChangeInterval
        {
            get => this.reconciledNoChangeInterval;

            set
            {
                this.reconciledNoChangeInterval = value;

                if (value >= TimeSpan.Zero)
                {
                    this.nextNoChangeReconciledUtc = DateTime.UtcNow + value;
                }
                else
                {
                    this.nextNoChangeReconciledUtc = DateTime.MinValue;
                }
            }
        }

        /// <summary>
        /// We capture and log any exceptions thrown by your event handlers and also schedule 
        /// the event to be retried in the future using an exponential backoff.  This property 
        /// specifies  the initial backoff time which will be doubled for every successive error
        /// until the backoff maxes out at <see cref="ErrorMaxRequeueInterval"/>.
        /// </summary>
        public TimeSpan ErrorMinRequeueInterval { get; set; } = TimeSpan.FromSeconds(15);

        /// <summary>
        /// We capture and log any exceptions thrown by your event handlers and also schedule the event
        /// to be retried in the future using an exponential backoff.  The <see cref="ErrorMinRequeueInterval"/>
        /// property specifies the initial backoff time which will be doubled for every successive error until
        /// the backoff maxes out at <see cref="ErrorMaxRequeueInterval"/>.
        /// </summary>
        public TimeSpan ErrorMaxRequeueInterval { get; set; } = TimeSpan.FromMinutes(10);

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
        /// resource.  This method adds the resource to the collection if it  doesn't already 
        /// exist and then calls your handler with the resource name and a dictionary of the
        /// existing resources when a change is detected.  The resource name will be passed as
        /// <c>null</c> when no change is detected or when all existing resources have been
        /// collected.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <remarks>
        /// <para>
        /// This method honors the global KubeOps SDK leader elector when enabled or a local
        /// elector when a <see cref="LeaderElectionConfig"/> was passed to the constructor
        /// by returning only when leadership for the resource is attained.
        /// </para>
        /// <para>
        /// By default, the resource manager will hold off calling your handler until all
        /// existing resources have been receieved.  You can disable this behavior by passing
        /// <c>false</c> to the constructor.
        /// </para>
        /// </remarks>
        public async Task<ResourceControllerResult> ReconciledAsync(TResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
            await SyncContext.Clear;

            EnsureNotDisposed();

            if (resource != null && !filter(resource))
            {
                return null;
            }

            try
            {
                Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));
                Covenant.Requires<ArgumentNullException>(handler != null, nameof(handler));

                using (await mutex.AcquireAsync())
                {
                    var name    = resource.Metadata.Name;
                    var changed = false;
                    var utcNow  = DateTime.UtcNow;

                    if (resources.TryGetValue(resource.Metadata.Name, out var existing))
                    {
                        changed = resource.Metadata.Generation != existing.Metadata.Generation;
                    }
                    else
                    {
                        changed = true;
                    }

                    if (resource != null)
                    {
                        resources[name] = resource;
                    }


                    if (waitForAll && !changed)
                    {
                        // Looks like we're tracking all of the existing resources now, so stop
                        // waiting for resources and configure to raise an immediate NO-CHANGE
                        // event below.

                        log.LogInfo($"All existing resources loaded.");

                        waitForAll                = false;
                        changed                   = true;
                        nextNoChangeReconciledUtc = utcNow;
                    }

                    if (waitForAll)
                    {
                        // We're still receiving known resources.

                        log.LogInfo($"RECONCILED: {name} (waiting for existing resources)");
                        return null;
                    }

                    if (!changed && utcNow < nextNoChangeReconciledUtc)
                    {
                        // It's not time yet for another no-change handler call.

                        return null;
                    }

                    if (reconciledNoChangeInterval > TimeSpan.Zero)
                    {
                        nextNoChangeReconciledUtc = utcNow + ReconcileNoChangeInterval;
                    }

                    var result = await handler(changed ? name : null, resources);

                    reconciledErrorBackoff = TimeSpan.Zero;   // Reset after a success

                    return result;
                }
            }
            catch (Exception e)
            {
                log.LogError(e);
                errorCounter?.Inc();

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff(ref reconciledErrorBackoff));
            }
        }

        /// <summary>
        /// Call this when your controller receives a <b>deleted</b> event, passing the resource.
        /// If the resource exists in the collection, this method will remove it and call your
        /// handler.  The handler is not called when the resource does not exist the collection
        /// or while we're still waiting to receive all existing resources.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named resource is not currently present.</exception>
        /// <remarks>
        /// <para>
        /// This method honors the global KubeOps SDK leader elector when enabled or a local
        /// elector when a <see cref="LeaderElectionConfig"/> was passed to the constructor
        /// by returning only when leadership for the resource is attained.
        /// </para>
        /// </remarks>
        public async Task<ResourceControllerResult> DeletedAsync(TResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
            await SyncContext.Clear;

            EnsureNotDisposed();

            if (resource != null && !filter(resource))
            {
                return null;
            }

            try
            {
                Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));
                Covenant.Requires<ArgumentNullException>(handler != null, nameof(handler));

                using (await mutex.AcquireAsync())
                {
                    var name = resource.Metadata.Name;

                    if (!resources.ContainsKey(name))
                    {
                        return null;
                    }

                    resources.Remove(name);

                    if (!waitForAll)
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
                }
            }
            catch (Exception e)
            {
                log.LogError(e);
                errorCounter?.Inc();

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff(ref deletedErrorBackoff));
            }
        }

        /// <summary>
        /// Call this when a <b>status-modified</b> event was received, passing the resource.
        /// This method replaces any existing resource with the same name in the collection.
        /// The handler is not called when the resource does not exist in the collection or
        /// while we're still waiting to receive all existing resources.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <remarks>
        /// <para>
        /// This method honors the global KubeOps SDK leader elector when enabled or a local
        /// elector when a <see cref="LeaderElectionConfig"/> was passed to the constructor
        /// by returning only when leadership for the resource is attained.
        /// </para>
        /// </remarks>
        public async Task<ResourceControllerResult> StatusModifiedAsync(TResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
            await SyncContext.Clear;

            EnsureNotDisposed();

            if (resource != null && !filter(resource))
            {
                return null;
            }

            try
            {
                Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));
                Covenant.Requires<ArgumentNullException>(handler != null, nameof(handler));

                using (await mutex.AcquireAsync())
                {
                    var name = resource.Metadata.Name;

                    if (!resources.ContainsKey(name))
                    {
                        return null;
                    }

                    resources[name] = resource;

                    if (!waitForAll)
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
                }
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

            using (await mutex.AcquireAsync())
            {
                return resources.ContainsKey(name);
            }
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

            using (await mutex.AcquireAsync())
            {
                if (resources.TryGetValue(name, out var resource))
                {
                    return resource;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>d
        /// <para>
        /// Returns a deep clone the current set of resources being managed or of the specific dictionary passed.
        /// </para>
        /// <note>
        /// This can be an expensive operation when you're tracking a lot of resources.
        /// </note>
        /// </summary>
        /// <param name="resources">Optionally specifies the resource dictionary to be copied.</param>
        /// <returns>A deep clone of the current set of resources being managed or the dictionary passed..</returns>
        public async Task<IReadOnlyDictionary<string, TResource>> CloneResourcesAsync(IReadOnlyDictionary<string, TResource> resources = null)
        {
            await SyncContext.Clear;

            if (resources == null)
            {
                using (await mutex.AcquireAsync())
                {
                    return DeepClone(this.resources);
                }
            }
            else
            {
                return DeepClone(resources);
            }
        }

        /// <summary>
        /// Returns a deep clone of the resource dictionary passed.
        /// </summary>
        /// <param name="source">The source dictionary.</param>
        /// <returns></returns>
        private IReadOnlyDictionary<string, TResource> DeepClone(IReadOnlyDictionary<string, TResource> source)
        {
            Covenant.Requires<ArgumentNullException>(source != null, nameof(source));

            var target = new Dictionary<string, TResource>(StringComparer.InvariantCultureIgnoreCase);

            foreach (var item in source)
            {
                // $note(jefflill): 
                //
                // NeonHelper.JsonClone() is going to serialize and deserialize each 
                // item value which will be somewhat expensive.

                target.Add(item.Key, NeonHelper.JsonClone(item.Value));
            }

            return (IReadOnlyDictionary<string, TResource>)target;
        }
    }
}
