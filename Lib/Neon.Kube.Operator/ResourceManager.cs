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
using Neon.Tasks;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;

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
    /// <typeparam name="TCustomResource">The custom Kubernetes entity type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class helps makes it easier to manage custom cluster resources.  Simply call
    /// <see cref="ReconciledAsync(TCustomResource, ResourceManager{TCustomResource}.EventHandlerAsync, Counter)"/>, 
    /// <see cref="DeletedAsync(TCustomResource, ResourceManager{TCustomResource}.EventHandlerAsync, Counter)"/>, and
    /// <see cref="StatusModifiedAsync(TCustomResource, ResourceManager{TCustomResource}.EventHandlerAsync, Counter)"/> 
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
    /// <see cref="ReconciledAsync(TCustomResource, ResourceManager{TCustomResource}.EventHandlerAsync, Counter)"/>
    /// in your controller's <b>ReconcileAsync()</b> method, a call to 
    /// <see cref="DeletedAsync(TCustomResource, ResourceManager{TCustomResource}.EventHandlerAsync, Counter)"/>
    /// in your controller's <b>DeletedAsync()</b> method and a call to 
    /// <see cref="StatusModifiedAsync(TCustomResource, ResourceManager{TCustomResource}.EventHandlerAsync, Counter)"/>
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
    /// starts watching the resource specified by <typeparamref name="TCustomResource"/> and raises the
    /// controller events as they are received, handling any failures seamlessly.  The <see cref="ResourceManager{TCustomResource}"/> 
    /// class helps keep track of the known resources as well reducing the complexity of determining why
    /// an event was raised.  KubeOps also periodically raises reconciled events even when nothing has 
    /// changed.  This appears to happen once a minute.
    /// </para>
    /// <para>
    /// When your operator first starts, a reconciled event will be raised for each custom resource of 
    /// type <typeparamref name="TCustomResource"/> in the cluster and the resource manager will add
    /// these resources to its internal dictionary.  By default, the resource manager will not call 
    /// your handler until all known resources have been added to this dictionary.  Then after the 
    /// resource manager has determined that it has collected all of the known resources, it will call 
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
    /// been reconciled yet, which could easily cause a lot of trouble, especially if your operator
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
    /// </remarks>
    public class ResourceManager<TCustomResource>
        where TCustomResource : CustomKubernetesEntity
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
        public delegate Task<ResourceControllerResult> EventHandlerAsync(string name, IReadOnlyDictionary<string, TCustomResource> resources);

        //---------------------------------------------------------------------
        // Implementation

        private AsyncMutex                          mutex     = new AsyncMutex();
        private Dictionary<string, TCustomResource> resources = new Dictionary<string, TCustomResource>(StringComparer.InvariantCultureIgnoreCase);
        private INeonLogger                         log;
        private bool                                waitForAll;
        private DateTime                            nextNoChangeReconcileUtc;
        private TimeSpan                            reconcileRequeueInterval;
        private TimeSpan                            errorBackoff;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="logger">Optionally specifies the logger to be used by the instance.</param>
        /// <param name="waitForAll">
        /// <para>
        /// Controls whether the resource manager will absorb all reconciled events until an
        /// event is raised that indicates that nothing has changed.  This happens when the 
        /// manager has all of the resources.  This means that your handler can depend
        /// on all of the resources being present when it is called for the first time.
        /// </para>
        /// <para>
        /// This defaults to <c>true</c> which will work for most scenarios.
        /// </para>
        /// </param>
        public ResourceManager(INeonLogger logger = null, bool waitForAll = true)
        {
            this.log                      = logger ?? LogManager.Default.GetLogger("Neon.Kube.Operator.ResourceManager");
            this.waitForAll               = waitForAll;
            this.nextNoChangeReconcileUtc = DateTime.UtcNow;
            this.reconcileRequeueInterval = TimeSpan.FromMinutes(5);
            this.errorBackoff             = TimeSpan.Zero;
        }

        /// <summary>
        /// Specifies the amount of time after processing a reconcile event before processing
        /// a new event that does not change any resources.  This defaults to <b>1 minute</b>.
        /// Set <see cref="TimeSpan.Zero"/> to disable reconcile event processing when
        /// there are no changes.  This defaults to <b>5 minutes</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is useful as a fallback to ensure that current custom resource state actually
        /// matches the corresponding cluster or physical state.  For example, if you have 
        /// custom resources that map to running pods and one of the pods was manually deleted,
        /// after <see cref="ReconcileRequeueInterval"/> and up to minute or so more, your 
        /// operator will receive a no-change reconciled event which your handler can take as
        /// an oppertunity to ensure that all of the expected pods actually exist.
        /// </para>
        /// <note>
        /// The actual resolution of this property is rougly <b>1 minute</b> at this time due
        /// to how the KubeOps SDK works.
        /// </note>
        /// </remarks>
        public TimeSpan ReconcileRequeueInterval
        {
            get => this.reconcileRequeueInterval;

            set
            {
                this.reconcileRequeueInterval = value;

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
        /// Computes the backoff timeout for exceptions.
        /// </summary>
        /// <returns>The backoff <see cref="TimeSpan"/>.</returns>
        private TimeSpan ComputeErrorBackoff()
        {
            if (errorBackoff <= TimeSpan.Zero)
            {
                return errorBackoff = ErrorMaxRequeueInterval;
            }
            else
            {
                return errorBackoff = NeonHelper.Min(TimeSpan.FromTicks(errorBackoff.Ticks * 2), ErrorMaxRequeueInterval);
            }
        }

        /// <summary>
        /// Call this when your controller receives a <b>reconciled</b> event, passing the
        /// resource.  This method adds the resource to the collection if it  doesn't already 
        /// exist and then calls your handler with the resource name and a dictionary of the
        /// known resources when a change is detected.  The resource name will be passed as
        /// <c>null</c> when no change is detected or when all known resources have been
        /// collected.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <remarks>
        /// By default, the resource manager will hold off calling your handler until all
        /// known resources have been receieved.  You can disable this behavior by passing
        /// <c>false</c> to the constructor.
        /// </remarks>
        public async Task<ResourceControllerResult> ReconciledAsync(TCustomResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
            try
            {
                Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));
                Covenant.Requires<ArgumentNullException>(handler != null, nameof(handler));

                using (await mutex.AcquireAsync())
                {
                    var name    = resource.Metadata.Name;
                    var changed = false;

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
                        // Looks like we're tracking all of the known resources now.

                        waitForAll = false;

                        log.LogInfo($"All known resources loaded.");
                    }

                    if (waitForAll)
                    {
                        // We're still receiving known resources.

                        log.LogInfo($"RECONCILED: {name} (waiting for known resources)");
                        return null;
                    }

                    var utcNow = DateTime.UtcNow;

                    if (!changed && utcNow < nextNoChangeReconcileUtc)
                    {
                        // It's not time yet for another no-change handler call.

                        return null;
                    }

                    if (reconcileRequeueInterval > TimeSpan.Zero)
                    {
                        nextNoChangeReconcileUtc = utcNow + ReconcileRequeueInterval;
                    }

                    var result = await handler(changed ? name : null, resources);

                    errorBackoff = TimeSpan.Zero;   // Reset after a success

                    return result;
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff());
            }
        }

        /// <summary>
        /// Call this when your controller receives a <b>deleted</b> event, passing the resource.
        /// If the resource exists in the collection, this method will remove it and call your
        /// handler.  The handler is not called when the resource does not exist the collection
        /// or while we're still waiting to receive all known resources.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <exception cref="KeyNotFoundException">Thrown if the named resource is not currently present.</exception>
        public async Task<ResourceControllerResult> DeletedAsync(TCustomResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
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

                        errorBackoff = TimeSpan.Zero;   // Reset after a success

                        return result;
                    }
                    else
                    {
                        log.LogInfo($"DELETED: {resource.Metadata.Name} (waiting for known resources)");
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff());
            }
        }

        /// <summary>
        /// Call this when a <b>status-modified</b> event was received, passing the resource.
        /// This method replaces any existing resource with the same name in the collection.
        /// The handler is not called when the resource does not exist in the collection or
        /// while we're still waiting to receive all known resources.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handler">Your custom event handler.</param>
        /// <param name="errorCounter">Optionally specifies the counter to be incremented for caught exceptions.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        public async Task<ResourceControllerResult> StatusModifiedAsync(TCustomResource resource, EventHandlerAsync handler, Counter errorCounter = null)
        {
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

                        errorBackoff = TimeSpan.Zero;   // Reset after a success

                        return result;
                    }
                    else
                    {
                        log.LogInfo($"STATUS-MODIFIED: {resource.Metadata.Name} (waiting for known resources)");
                        return null;
                    }
                }
            }
            catch (Exception e)
            {
                log.LogError(e);

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff());
            }
        }

        /// <summary>
        /// Determines whether a custom resource with the specific name exists.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <returns><c>true</c> when the name exists.</returns>
        public async Task<bool> ContainsAsync(string name)
        {
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
        public async Task<TCustomResource> GetResourceAsync(string name)
        {
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

        /// <summary>
        /// Returns the current set of resources being managed.  This may be use for
        /// complex that cannot be performed via a handler callback.
        /// </summary>
        /// <returns>A copy of the current set of managed resources.</returns>
        /// <remarks>
        /// <para>
        /// You'll need to take care to ensure that any new events raised don't result in
        /// unexpected resource changes to the handled while you're working with the collection
        /// returned.  Consider locking the <see cref="ResourceManager{TCustomResource}"/> instance
        /// while you're processing the collection returned.
        /// </para>
        /// <note>
        /// We recommend that you avoid call this and use handler callbacks whenerver possible
        /// to keep things safe and simple.
        /// </note>
        /// </remarks>
        public async Task<IEnumerable<TCustomResource>> CloneResourcesAsync()
        {
            using (await mutex.AcquireAsync())
            {
                return new List<TCustomResource>(resources.Values);
            }
        }
    }
}
