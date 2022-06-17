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

// $hack(jefflill):
//
// Define [IGNORABLE] to ensure that an "ignorable" resource exists on the API
// server.  This was useful when [KubernetesClient] had trouble watching empty
// resource list responses (fixed for v7.2.19)
//
// This should be removed at some point in the future when we're sure we won't
// need it again.

#undef IGNORABLE_RESOURCE

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
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
using Neon.Kube;
using Neon.Tasks;

using KubeOps.Operator;
using KubeOps.Operator.Builder;
using KubeOps.Operator.Controller;
using KubeOps.Operator.Controller.Results;
using KubeOps.Operator.Entities;

using k8s;
using k8s.Autorest;
using k8s.Models;

using Prometheus;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Used by custom <b>KubeOps</b> based operators to manage a collection of custom resources.
    /// </summary>
    /// <typeparam name="TEntity">Specifies the custom Kubernetes entity type.</typeparam>
    /// <typeparam name="TController">Specifies the entity controller type.</typeparam>
    /// <remarks>
    /// <para>
    /// This class helps makes it easier to manage custom cluster resources.  Simply construct an
    /// instance with <see cref="ResourceManager{TResource, TController}"/> in your controller 
    /// (passing any custom settings as parameters) and then call <see cref="StartAsync(string)"/>.
    /// </para>
    /// <para>
    /// After the resource manager starts, your controller's <see cref="ReconciledAsync(TEntity, ResourceManager{TEntity, TController}.ReconcileHandlerAsync)"/>, 
    /// <see cref="DeletedAsync(TEntity, ResourceManager{TEntity, TController}.NoResultHandlerAsync)"/>, and
    /// <see cref="StatusModifiedAsync(TEntity, ResourceManager{TEntity, TController}.NoResultHandlerAsync)"/> 
    /// methods will be called as related resource related events are received.
    /// </para>
    /// <para><b>KUBEOPS INTEGRATION</b></para>
    /// <para>
    /// This class is designed to integrate cleanly with operators based on the [KubeOps](https://github.com/buehler/dotnet-operator-sdk)
    /// Kubernetes Operator SDK for .NET.  You'll instantiate a <see cref="ResourceManager{TResource, IController}"/>
    /// instance for each controller, passing the custom resource type as the type parameter and then set this
    /// as a static field in your controller.  Then you'll need to add a call to 
    /// <see cref="ReconciledAsync(TEntity, ResourceManager{TEntity, TController}.ReconcileHandlerAsync)"/>
    /// in your controller's <b>ReconcileAsync()</b> method, a call to 
    /// <see cref="DeletedAsync(TEntity, ResourceManager{TEntity, TController}.NoResultHandlerAsync)"/>
    /// in your controller's <b>DeletedAsync()</b> method and a call to 
    /// <see cref="StatusModifiedAsync(TEntity, ResourceManager{TEntity, TController}.NoResultHandlerAsync)"/>
    /// on your controller <b>StatusModifiedAsync()</b> method.
    /// </para>
    /// <para>
    /// You'll also need to pass a callback to each method to handle any resource changes for that operation.
    /// The callback signature for your handler is <see cref="ResourceManager{TResource, TController}.ReconcileHandlerAsync"/>,
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
    /// <item>
    /// By default, <see cref="ResourceManager{TResource, TController}"/> does nothing special to enforce
    /// processing exclusivity; it just relies on the the <b>KubeOps</b> SDK leader lease when enabled.
    /// This means that the <see cref="ReconciledAsync(TEntity, ResourceManager{TEntity, TController}.ReconcileHandlerAsync)"/>,
    /// <see cref="DeletedAsync(TEntity, NoResultHandlerAsync)"/>, and
    /// <see cref="StatusModifiedAsync(TEntity, ResourceManager{TEntity, TController}.NoResultHandlerAsync)"/>
    /// methods will only return managed resources when <b>KubeOps</b> is the leader for the current pod.
    /// </item>
    /// </list>
    /// <para>
    /// By default, <see cref="ResourceManager{TResource, TController}"/> does nothing special to enforce
    /// processing exclusivity; it just relies on the the <b>KubeOps</b> SDK leader lease when enabled.
    /// This means that the <see cref="ReconciledAsync(TEntity, ResourceManager{TEntity, TController}.ReconcileHandlerAsync)"/>,
    /// <see cref="DeletedAsync(TEntity, ResourceManager{TEntity, TController}.NoResultHandlerAsync)"/>, and
    /// <see cref="StatusModifiedAsync(TEntity, ResourceManager{TEntity, TController}.NoResultHandlerAsync)"/>
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
    /// <see cref="ReconciledAsync(TEntity, ResourceManager{TEntity, TController}.ReconcileHandlerAsync)"/> to
    /// return only when the instance holds the lease and all <see cref="ResourceManager{TResource, TController}"/> 
    /// instances without a leader config will continue returning changes.
    /// </para>
    /// <para><b>RESOURCE MANAGER MODES</b></para>
    /// <para>
    /// The resource manager operates in one of two modes, specified by the <see cref="ResourceManagerOptions.Mode"/>
    /// which defaults to <see cref="ResourceManagerMode.Normal"/>.  These modes control when your handler sees resource
    /// reconcile events:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><see cref="ResourceManagerMode.Normal"/></term>
    ///     <description>
    ///     <para>
    ///     This mode is the default and is intended for scenarios where individual resources
    ///     can be processed completely independently.
    ///     </para>
    ///     <para>
    ///     The resource manager operates similarily most other operator SDKs in this mode, where
    ///     reconcile events are passed to your handler immediately when detected.
    ///     </para>
    ///     <para>
    ///     Differences from other SDKs include:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///         The resource manager maintains a dictionary of resources it's seen, keyed
    ///         by name and passes this to the operator's handlers.
    ///         </item>
    ///         <item>
    ///         The resource also calls the operator's reconcile handler periodically, passing a 
    ///         <c>null</c> resource as well as the resource dictionary.  We refer to this as 
    ///         and <b>IDLE reconcile</b> event.  This is a good opportunity for your operator to
    ///         perform operations across a number of resources.  For example, your operator may 
    ///         implement job resources that coordinate database backups and the operator may need 
    ///         to periodically check the status of each backup and update and/or delete related 
    ///         resources when backups complete.
    ///         </item>
    ///     </list>
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><see cref="ResourceManagerMode.Collection"/></term>
    ///     <description>
    ///     <para>
    ///     This mode is intended for scenarios where resources need to be processed together
    ///     for example, when individual load balancer ingress rules need to be applied to
    ///     the load balancer together.
    ///     </para>
    ///     <para>
    ///     The resource manager needs to wait for all existing resources to be detected by
    ///     the underlying KubeOps Operator SDK and add them to an internal cache before 
    ///     relaying <b>reconcile</b> and <b>status-modified</b> event to your handler.
    ///     The resource manager collects resources for <see cref="ResourceManagerOptions.CollectInterval"/>
    ///     after receiving the last reconcile event from the SDK.
    ///     </para>
    ///     <para>
    ///     Once all existing resources have been collected, your handler will be called as
    ///     resources are reconciled, passing the name of the resource along with the 
    ///     collection of known resources.  This mode also generated <b>IDLE reconcile</b> events
    ///     where your handler is called periodically  <see cref="ResourceManagerOptions.IdleInterval"/>
    ///     passing <b>resource</b><c>=null</c>, giving your handler the chance to perform periodic 
    ///     maintenance operations across all known resources.
    ///     </para>
    ///     </description>
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class ResourceManager<TEntity, TController> : IDisposable
        where TEntity : CustomKubernetesEntity, new()
        where TController : IResourceController<TEntity>
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Defines the event handler you'll need to implement to handle <b>RECONCILE</b> events.
        /// </summary>
        /// <param name="resource">Passed as impacted resource or <c>null</c> for IDLE events.</param>
        /// <param name="resources">Passed a dictionary holding the currently known resources.  This is keyed by resource name.</param>
        /// <returns>
        /// Returns a <see cref="ResourceControllerResult"/> controlling how events may be requeued or
        /// <c>null</c> such that nothing will be explicitly requeued.
        /// </returns>
        /// <remarks>
        /// <paramref name="resource"/> will never be passed as <c>null</c> for <see cref="ResourceManagerMode.Normal"/>
        /// mode.  <paramref name="resources"/> will always be passed.
        /// </remarks>
        public delegate Task<ResourceControllerResult> ReconcileHandlerAsync(TEntity resource, IReadOnlyDictionary<string, TEntity> resources);

        /// <summary>
        /// Defines the event handler you'll need to implement to handle <b>DELETE</b> and <b>STATUS-MODIFIED</b> events.
        /// </summary>
        /// <param name="resource">Passed as impacted resource.</param>
        /// <param name="resources">Passed a dictionary holding the currently known resources.  This is keyed by resource name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <paramref name="resource"/> will never be passed as <c>null</c> for <see cref="ResourceManagerMode.Normal"/>
        /// mode.  <paramref name="resources"/> will always be passed.
        /// </remarks>
        public delegate Task NoResultHandlerAsync(TEntity resource, IReadOnlyDictionary<string, TEntity> resources);

        //---------------------------------------------------------------------
        // Implementation

        private bool                            isDisposed            = false;
        private AsyncReentrantMutex             mutex                 = new AsyncReentrantMutex();
        private Dictionary<string, TEntity>     resources             = new Dictionary<string, TEntity>(StringComparer.InvariantCultureIgnoreCase);
        private bool                            started               = false;
        private bool                            reconcileReceived     = false;
        private bool                            discovering           = false;
        private bool                            skipChangeDetection   = false;
        private TimeSpan                        notStartedRequeDelay = TimeSpan.FromSeconds(10);
        private ResourceManagerOptions          options;
        private IKubernetes                     k8s;
        private string                          resourceNamespace;
        private ConstructorInfo                 controllerConstructor;
        private Func<TEntity, bool>             filter;
        private INeonLogger                     log;
        private DateTime                        nextIdleReconcileUtc;
        private TimeSpan                        reconciledErrorBackoff;
        private LeaderElectionConfig            leaderConfig;
        private LeaderElector                   leaderElector;
        private Task                            leaderTask;

        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="k8s">The <see cref="IKubernetes"/> client used by the controller.</param>
        /// <param name="options">
        /// Optionally specifies options that customize the resource manager's behavior.  This
        /// defaults to <see cref="ResourceManagerMode.Normal"/> mode with reasonable timing settings.
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
        public ResourceManager(
            IKubernetes             k8s,
            ResourceManagerOptions  options      = null,
            Func<TEntity, bool>     filter       = null,
            INeonLogger             logger       = null,
            LeaderElectionConfig    leaderConfig = null)
        {
            Covenant.Requires<ArgumentNullException>(k8s != null, nameof(k8s));

            this.k8s                    = k8s;  // $todo(jefflill): Can we obtain this from KubeOps or the [IServiceProvider] somehow?
            this.options                = options ?? new ResourceManagerOptions();
            this.filter                 = filter ?? new Func<TEntity, bool>(resource => true);
            this.log                    = logger ?? LogManager.Default.GetLogger($"Neon.Kube.Operator.ResourceManager({typeof(TEntity).Name})");
            this.discovering            = options.Mode == ResourceManagerMode.Collection;
            this.reconciledErrorBackoff = TimeSpan.Zero;
            this.leaderConfig           = leaderConfig;

            options.Validate();

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

            if (started)
            {
                throw new InvalidOperationException($"[{nameof(ResourceManager<TEntity, TController>)}] is already running.");
            }

            //-----------------------------------------------------------------
            // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1599
            //
            // For controllers that implement [IExtendedController], determine whether the
            // controller wants an ignorable resource to always exist.  This works around
            // watch problems.

            await EnsureIgnorableResource();
            await Task.Delay(TimeSpan.FromSeconds(1));

            //-----------------------------------------------------------------
            // Start the leader elector if enabled.

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

            // Start the IDLE reconcile loop.

            _ = IdleLoopAsync();

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
        /// Determines whether a resource is ignorable.
        /// </summary>
        /// <param name="resource">The resource being tested (may be <c>null</c>).</param>
        /// <returns><c>true</c> if the resource is ignorable, <c>false</c> if not ignorable or <c>null</c>.</returns>
        private bool IsIgnorable(TEntity resource)
        {
#if IGNORABLE_RESOURCE
            return resource?.Name() == KubeHelper.IgnorableResourceName;
#else
            return false;
#endif
        }

        /// <summary>
        /// Ensures that an ignorable resource exists for controllers that need that.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task EnsureIgnorableResource()
        {
#if IGNORABLE_RESOURCE
            // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1599
            //
            // For controllers that implement [IExtendedController], determine whether the
            // controller wants an ignorable resource to always exist.  This works around
            // watch problems.

            var controller = (IResourceController<TEntity>)controllerConstructor.Invoke(new object[] { k8s });

            if (controller is IExtendedController<TEntity> extendedController)
            {
                var entity = extendedController.CreateIgnorable();

                if (entity != null)
                {
                    // The controller needs us to ensure that at least one ignorable
                    // resource exists.  We'll do that here.  Note that ignorable
                    // resources will always have the same name.

                    if (string.IsNullOrEmpty(resourceNamespace))
                    {
                        try
                        {
                            await k8s.GetNamespacedCustomObjectAsync<TEntity>(resourceNamespace, KubeHelper.IgnorableResourceName);
                        }
                        catch (HttpOperationException e)
                        {
                            if (e.Response.StatusCode == HttpStatusCode.NotFound)
                            {
                                try
                                {
                                    await k8s.CreateNamespacedCustomObjectAsync(entity, resourceNamespace, KubeHelper.IgnorableResourceName);
                                }
                                catch (HttpOperationException e2)
                                {
                                    // Any errors here will probably be due to other controller instances
                                    // creating an ignorable between the time we checked above and the
                                    // time Kubernetes actually handled the create.
                                    //
                                    // We're going to ignore these and take the watcher bug performance hit.

                                    log.LogWarn("Cannot create ignorable resource.", e2);
                                }
                            }
                        }
                    }
                    else
                    {
                        try
                        {
                            await k8s.GetClusterCustomObjectAsync<TEntity>(KubeHelper.IgnorableResourceName);
                        }
                        catch (HttpOperationException e)
                        {
                            if (e.Response.StatusCode == HttpStatusCode.NotFound)
                            {
                                try
                                {
                                    await k8s.CreateClusterCustomObjectAsync(entity, KubeHelper.IgnorableResourceName);
                                }
                                catch (HttpOperationException e2)
                                {
                                    // Any errors here will probably be due to other controller instances
                                    // creating an ignorable between the time we checked above and the
                                    // time Kubernetes actually handled the create.
                                    //
                                    // We're going to ignore these and take the watcher bug performance hit.

                                    log.LogWarn("Cannot create ignorable resource.", e2);
                                }
                            }
                        }
                    }
                }
            }
#endif
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
        /// Computes the backoff timeout for exceptions caught by the event handlers.
        /// </summary>
        /// <param name="errorBackoff">
        /// Passed as the current error backoff time being tracked for the event.  This with
        /// be increased honoring <see cref="ResourceManagerOptions.ErrorMinRequeueInterval"/> 
        /// and <see cref="ResourceManagerOptions.ErrorMaxRetryInterval"/> constraints and will
        /// also be returned as the backoff.
        /// </param>
        /// <returns>The backoff <see cref="TimeSpan"/>.</returns>
        private TimeSpan ComputeErrorBackoff(ref TimeSpan errorBackoff)
        {
            if (reconciledErrorBackoff <= TimeSpan.Zero)
            {
                return errorBackoff = options.ErrorMinRequeueInterval;
            }
            else
            {
                return errorBackoff = NeonHelper.Min(TimeSpan.FromTicks(reconciledErrorBackoff.Ticks * 2), options.ErrorMaxRetryInterval);
            }
        }

        /// <summary>
        /// Adds or updates a non-ignorable resource to the <see cref="resources"/> dictionary.
        /// Ignorable resources are excluded.
        /// </summary>
        /// <param name="resource">The resource being added.</param>
        private void AddResource(TEntity resource)
        {
            if (resource != null && !IsIgnorable(resource))
            {
                resources[resource.Name()] = resource;
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
        /// <param name="handlerAsync">Your custom event handler.</param>
        /// <returns>The <see cref="ResourceControllerResult"/> returned by your handler.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when KubeOps is running before <see cref="StartAsync(string)"/> is called for this controller.
        /// </exception>
        /// <remarks>
        /// <para>
        /// By default, the resource manager will hold off calling your handler until all
        /// existing resources have been receieved.  You can disable this behavior by passing
        /// <c>discover: false</c> to the constructor.
        /// </para>
        /// <note>
        /// The reconciled resource will be already added to the resource dictionary before
        /// your handler is called.
        /// </note>
        /// </remarks>
        public async Task<ResourceControllerResult> ReconciledAsync(TEntity resource, ReconcileHandlerAsync handlerAsync)
        {
            await SyncContext.Clear;
            Covenant.Requires<InvalidOperationException>(started, $"You must call [{nameof(TController)}.{nameof(StartAsync)}()] before starting KubeOps.");

            //-----------------------------------------------------------------
            // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1599
            //
            // Ignore "ignorable" resources.

            if (IsIgnorable(resource))
            {
                return null;
            }

            //-----------------------------------------------------------------
            // Handle the resource.

            EnsureNotDisposed();
            EnsureStarted();

            // Filter out undesired resources.

            if (resource != null && !filter(resource))
            {
                return null;
            }

            //-----------------------------------------------------------------
            // NORMAL MODE: We're just going to pass the resource directly to the handler
            // in a new dictionary in this case.  Note that the [IdleLoopAsync()] loop
            // isn't running, so we don't need to worry about the mutex.

            if (options.Mode == ResourceManagerMode.Normal)
            {
                try
                {
                    options.ReconcileCounter?.Inc();
                    AddResource(resource);

                    var result = await handlerAsync(resource, resources);

                    reconciledErrorBackoff = TimeSpan.Zero;   // Reset after a success

                    return result;
                }
                catch (Exception e)
                {
                    log.LogError(e);
                    options.ReconcileErrorCounter?.Inc();

                    return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff(ref reconciledErrorBackoff));
                }
            }

            //-----------------------------------------------------------------
            // COLLECTION MODE:

            Covenant.Assert(options.Mode == ResourceManagerMode.Collection);

            reconcileReceived = true;

            try
            {
                Covenant.Requires<ArgumentNullException>(handlerAsync != null, nameof(handlerAsync));

                return await mutex.ExecuteFuncAsync(
                    async () =>
                    {
                        options.ReconcileCounter?.Inc();

                        var name    = resource?.Metadata.Name;
                        var changed = false;
                        var utcNow  = DateTime.UtcNow;

                        if (resource == null)
                        {
                            // The [NoChangeAsync] loop below is sending these now so we're
                            // going always treat this as a change to pass this through to
                            // the user's handler.

                            changed = true;
                        }
                        else
                        {
                            // Determine whether the object has actually changed unless we're
                            // still discovering resources and change detection is disabled.

                            if (resources.TryGetValue(resource.Name(), out var existing))
                            {
                                changed = skipChangeDetection || resource.Metadata.Generation != existing.Metadata.Generation;
                            }
                            else
                            {
                                changed = true;
                            }

                            AddResource(resource);
                        }

                        if (discovering)
                        {
                            // We're still receiving known resources.

                            log.LogInfo($"RECONCILED: {name} (discovering resources)");
                            return null;
                        }

                        if (!changed)
                        {
                            return null;
                        }

                        var result = await handlerAsync(changed ? resource : null, resources);

                        reconciledErrorBackoff = TimeSpan.Zero;   // Reset after a success

                        return result;
                    });
            }
            catch (Exception e)
            {
                log.LogError(e);
                options.ReconcileErrorCounter?.Inc();

                return ResourceControllerResult.RequeueEvent(ComputeErrorBackoff(ref reconciledErrorBackoff));
            }
        }

        /// <summary>
        /// Call this when your controller receives a <b>deleted</b> event, passing the resource
        /// receievd.  If the resource exists in the collection, this method will remove it and call 
        /// your handler.  The handler is not called when the resource does not exist the collection
        /// or while we're still discovering existing resources.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handlerAsync">Your custom event handler.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when KubeOps is running before <see cref="StartAsync(string)"/> is called for this controller.
        /// </exception>
        /// <remarks>
        /// <para>
        /// By default, the resource manager will hold off calling your handler until all
        /// existing resources have been receieved.  You can disable this behavior by passing
        /// <c>discover: false</c> to the constructor.
        /// </para>
        /// <note>
        /// The reconciled resource will be already fromved from the resource dictionary before
        /// your handler is called.
        /// </note>
        /// </remarks>
        public async Task DeletedAsync(TEntity resource, NoResultHandlerAsync handlerAsync)
        {
            await SyncContext.Clear;
            EnsureNotDisposed();
            EnsureStarted();

            //-----------------------------------------------------------------
            // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1599
            //
            // Ignore "ignorable" resources.

            if (IsIgnorable(resource))
            {
                return;
            }

            //-----------------------------------------------------------------
            // Filter desired resources.

            if (resource != null && !filter(resource))
            {
                return;
            }

            //-----------------------------------------------------------------
            // NORMAL MODE: We're just going to pass the resource directly to the handler
            // in a new dictionary in this case.  Note that the [IdleLoopAsync()] loop
            // isn't running, so we don't need to worry about the mutex.

            if (options.Mode == ResourceManagerMode.Normal)
            {
                try
                {
                    options.DeleteCounter?.Inc();
                    resources.Remove(resource.Name());
                    await handlerAsync(resource, resources);
                }
                catch (Exception e)
                {
                    log.LogError(e);
                    options.DeleteErrorCounter?.Inc();
                }

                return;
            }

            //-----------------------------------------------------------------
            // COLLECTION MODE:

            Covenant.Assert(options.Mode == ResourceManagerMode.Collection);

            try
            {
                Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));
                Covenant.Requires<ArgumentNullException>(handlerAsync != null, nameof(handlerAsync));

                await mutex.ExecuteActionAsync(
                    async () =>
                    {
                        options.DeleteCounter?.Inc();

                        var name = resource.Name();

                        if (!resources.ContainsKey(name))
                        {
                            return;
                        }

                        resources.Remove(name);

                        // Wait until after we've finished discovering resources before calling
                        // the user's handler.

                        if (!discovering)
                        {
                            await handlerAsync(resource, resources);
                        }
                        else
                        {
                            log.LogInfo($"DELETED: {resource.Name()} (discovering resources)");
                        }
                    });
            }
            catch (Exception e)
            {
                log.LogError(e);
                options.DeleteErrorCounter?.Inc();
            }
        }

        /// <summary>
        /// Call this when a <b>status-modified</b> event was received, passing the resource
        /// reeceived.  This method replaces any existing resource with the same name in the 
        /// collection.  The handler is not called when the resource does not exist in the
        /// collection or while we're still discovering existing resources.
        /// </summary>
        /// <param name="resource">The custom resource received.</param>
        /// <param name="handlerAsync">Your custom event handler.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the instance has been disposed.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when KubeOps is running before <see cref="StartAsync(string)"/> is called for this controller.
        /// </exception>
        /// <remarks>
        /// <para>
        /// By default, the resource manager will hold off calling your handler until all
        /// existing resources have been receieved.  You can disable this behavior by passing
        /// <c>discover: false</c> to the constructor.
        /// </para>
        /// <note>
        /// The reconciled resource will be already updated in the resource dictionary before
        /// your handler is called.
        /// </note>
        /// </remarks>
        public async Task StatusModifiedAsync(TEntity resource, NoResultHandlerAsync handlerAsync)
        {
            await SyncContext.Clear;
            EnsureNotDisposed();
            EnsureStarted();

            //-----------------------------------------------------------------
            // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1599
            //
            // Ignore "ignorable" resources.

            if (IsIgnorable(resource))
            {
                return;
            }

            //-----------------------------------------------------------------
            // Filter desired resources.

            if (resource != null && !filter(resource))
            {
                return;
            }

            //-----------------------------------------------------------------
            // NORMAL MODE: We're just going to pass the resource directly to the handler
            // in a new dictionary in this case.  Note that the [IdleLoopAsync()] loop
            // isn't running, so we don't need to worry about the mutex.

            if (options.Mode == ResourceManagerMode.Normal)
            {
                try
                {
                    options.StatusModifiedCounter?.Inc();
                    AddResource(resource);

                    await handlerAsync(resource, resources);
                }
                catch (Exception e)
                {
                    log.LogError(e);
                    options.StatusModifiedErrorCounter?.Inc();
                }

                return;
            }

            //-----------------------------------------------------------------
            // COLLECTION MODE:

            Covenant.Assert(options.Mode == ResourceManagerMode.Collection);

            try
            {
                Covenant.Requires<ArgumentNullException>(resource != null, nameof(resource));
                Covenant.Requires<ArgumentNullException>(handlerAsync != null, nameof(handlerAsync));

                await mutex.ExecuteActionAsync(
                    async () =>
                    {
                        options.StatusModifiedCounter?.Inc();

                        var name = resource.Name();

                        if (!resources.ContainsKey(name))
                        {
                            return;
                        }

                        AddResource(resource);

                        // Wait until after we've finished discovering resources before calling
                        // the user's handler.

                        if (!discovering)
                        {
                            await handlerAsync(resource, resources);
                        }
                        else
                        {
                            log.LogInfo($"STATUS-MODIFIED: {resource.Name()} (discovering resources)");
                        }
                    });
            }
            catch (Exception e)
            {
                log.LogError(e);
                options.StatusModifiedErrorCounter?.Inc();
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
        /// This loop handles raising of <see cref="ReconciledAsync(TEntity, ReconcileHandlerAsync)"/> 
        /// events when there's been no changes to any of the monitored resources.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task IdleLoopAsync()
        {
            var loopDelay = TimeSpan.FromSeconds(1);

            while (!isDisposed)
            {
                await Task.Delay(loopDelay);

                var reconcileDiscovered = false;

                if (DateTime.UtcNow >= nextIdleReconcileUtc)
                {
                    nextIdleReconcileUtc = DateTime.UtcNow + options.IdleInterval;

                    //-----------------------------------------------------------------
                    // $hack(jefflill): https://github.com/nforgeio/neonKUBE/issues/1599
                    //
                    // For controllers that implement [IExtendedController], determine whether the
                    // controller wants an ignorable resource to always exist.  This works around
                    // watch problems.

                    await EnsureIgnorableResource();

                    if (reconcileReceived)
                    {
                        // It's been [reconciledNoChangeInterval] since we saw the last 
                        // resource from KubeOps, so we're going to assume that we have
                        // all of them.  So we're ready to send RECONCILE events for all
                        // discovered resources to the operator's handler.

                        reconcileDiscovered = discovering;
                        discovering         = false;

                        log.LogInfo($"No resources discovered.");
                    }
                    else
                    {
                        // If we're going to trigger the first IDLE RECONCILE and we haven't
                        // seen any resources from KubeOps, we need to that we have connectivity
                        // to Kubernetes and that there really aren't any known resources for
                        // the operator.
                        //
                        // We'll continue the loop for resource listing falures and also
                        // when resources do exist.  We do the latter with the expection
                        // that KubeOps will discover and report the existing resources in
                        // the near future.
                        //
                        // This is a bit risky because we're assuming that KubeOps is
                        // seeing the same resources from Kubernetes that we are here.
                        // I believe that waiting a minute for KubeOps to stablize and
                        // the other mitigations will be pretty safe though.

                        try
                        {
                            IList<TEntity> items;

                            if (resourceNamespace != null)
                            {
                                items = (await k8s.ListNamespacedCustomObjectAsync<TEntity>(resourceNamespace)).Items;
                            }
                            else
                            {
                                items = (await k8s.ListClusterCustomObjectAsync<TEntity>()).Items;
                            }

                            if (items
                                .Where(item => item.Name() != KubeHelper.IgnorableResourceName)
                                .Any(filter))
                            {
                                if (discovering)
                                {
                                    log.LogWarn($"Undiscovered resources.");
                                    continue;
                                }
                            }

                            reconcileDiscovered = discovering;
                            discovering         = false;

                            if (reconcileDiscovered)
                            {
                                log.LogInfo($"All resources discovered.");
                            }
                        }
                        catch (HttpOperationException e)
                        {
                            log.LogWarn(e);
                        }
                        catch (Exception e)
                        {
                            log.LogWarn(e);
                            continue;
                        }
                    }

                    // Don't send an IDLE RECONCILE while we're still discovering resources.

                    if (discovering)
                    {
                        continue;
                    }

                    // We're going to log and otherwise ignore any exceptions thrown by the 
                    // the operator's controller or from any members above called by the controller.

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

                                var controller = (IResourceController<TEntity>)controllerConstructor.Invoke(new object[] { k8s });

                                // Reconcile all of the resources when we just finished discovering them,
                                // otherwise send an IDLE RECONCILE.
                                //
                                // We're going to set [skipChangeDetection=true] while we're doing this so
                                // that all off the discovered resources will be considered as new when
                                // we handle them.  We're also going to catch and log any exceptions here,
                                // to ensure that all existing resources get reconciled.

                                if (reconcileDiscovered)
                                {
                                    try
                                    {
                                        skipChangeDetection = true;

                                        foreach (var resource in resources.Values.Where(resource => !IsIgnorable(resource)))
                                        {
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
                                }

                                await controller.ReconcileAsync(null);
                            }
                            catch (OperationCanceledException)
                            {
                                // Exit the loop when the [mutex] is disposed which happens
                                // when the resource manager is disposed.

                                return;
                            }
                            catch (Exception e)
                            {
                                log.LogError(e);
                            }
                        });

                    nextIdleReconcileUtc = DateTime.UtcNow + options.IdleInterval;
                }
            }
        }
    }
}
