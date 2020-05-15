//-----------------------------------------------------------------------------
// FILE:	    Worker.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Manages a Temporal activity/workflow worker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Temporal doesn't appear to support starting, stopping, and then restarting the same
    /// worker within an individual Temporal client so this class will prevent this.
    /// </para>
    /// </remarks>
    public sealed class Worker : IDisposable
    {
        private object          syncLock                = new object();
        private bool            isRunning               = false;
        private List<Type>      registeredActivityTypes = new List<Type>();
        private List<Type>      registeredWorkflowTypes = new List<Type>();
        private WorkerOptions   options;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The parent client.</param>
        /// <param name="workerId">The ID of the worker as tracked by the <b>temporal-proxy</b>.</param>
        /// <param name="options">Specifies the worker options or <c>null</c>.</param>
        internal Worker(TemporalClient client, long workerId, WorkerOptions options)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(options != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(options.Namespace));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(options.TaskList));

            this.Client   = client;
            this.WorkerId = workerId;
            this.options  = options;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            lock (syncLock)
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;

                Client.StopWorkerAsync(this).Wait();
                Client = null;

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns the parent Temporal client.
        /// </summary>
        internal TemporalClient Client { get; private set; }

        /// <summary>
        /// Indicates whether the worker has been fully disposed.
        /// </summary>
        internal bool IsDisposed { get; private set; } = false;

        /// <summary>
        /// Identifies whether the worker will process activities, workflows, or both.
        /// </summary>
        internal WorkerMode Mode { get; private set; }

        /// <summary>
        /// Returns the ID of the worker as tracked by the <b>temporal-proxy</b>.
        /// </summary>
        internal long WorkerId { get; private set; }

        /// <summary>
        /// Returns the Temporal namespace where the worker is registered.
        /// </summary>
        internal string Namespace { get; private set; }

        /// <summary>
        /// Returns the Temporal task list.
        /// </summary>
        internal string Tasklist { get; private set; }

        /// <summary>
        /// Ensures that the worker instances is not disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the worker is disposed.</exception>
        private void EnsureNotDisposed()
        {
            if (IsDisposed)
            {
                throw new ObjectDisposedException(nameof(Worker));
            }
        }

        /// <summary>
        /// Registers an activity implementation with Temporal.
        /// </summary>
        /// <typeparam name="TActivity">The <see cref="ActivityBase"/> derived class implementing the activity.</typeparam>
        /// <param name="activityTypeName">
        /// Optionally specifies a custom activity type name that will be used 
        /// for identifying the activity implementation in Temporal.  This defaults
        /// to the fully qualified <typeparamref name="TActivity"/> type name.
        /// </param>
        /// <param name="namespace">Optionally overrides the default client namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a different activity class has already been registered for <paramref name="activityTypeName"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all services you will be injecting into activities via
        /// <see cref="NeonHelper.ServiceContainer"/> before you call this as well as 
        /// registering of your activity implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterActivityAsync<TActivity>(string activityTypeName = null, string @namespace = null)
            where TActivity : ActivityBase
        {
            await SyncContext.ClearAsync;
            TemporalHelper.ValidateActivityImplementation(typeof(TActivity));
            TemporalHelper.ValidateActivityTypeName(activityTypeName);
            EnsureNotDisposed();

            if (isRunning)
            {
                throw new InvalidOperationException("Cannot register workflow or activity implementations after a worker has started.");
            }

            var activityType = typeof(TActivity);

            if (string.IsNullOrEmpty(activityTypeName))
            {
                activityTypeName = TemporalHelper.GetActivityTypeName(activityType, activityType.GetCustomAttribute<ActivityAttribute>());
            }

            await ActivityBase.RegisterAsync(this, activityType, activityTypeName, Client.ResolveNamespace(@namespace));

            lock (registeredActivityTypes)
            {
                registeredActivityTypes.Add(TemporalHelper.GetActivityInterface(typeof(TActivity)));
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for activity implementations derived from
        /// <see cref="ActivityBase"/> and tagged by <see cref="ActivityAttribute"/> and
        /// registers them with Temporal.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="namespace">Optionally overrides the default client namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="ActivityAttribute"/> that are not 
        /// derived from <see cref="ActivityBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all services you will be injecting into activities via
        /// <see cref="NeonHelper.ServiceContainer"/> before you call this as well as 
        /// registering of your activity implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyActivitiesAsync(Assembly assembly, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(assembly != null, nameof(assembly));
            EnsureNotDisposed();

            if (isRunning)
            {
                throw new InvalidOperationException("Cannot register workflow or activity implementations after a worker has started.");
            }

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass))
            {
                var activityAttribute = type.GetCustomAttribute<ActivityAttribute>();

                if (activityAttribute != null && activityAttribute.AutoRegister)
                {
                    var activityTypeName = TemporalHelper.GetActivityTypeName(type, activityAttribute);

                    await ActivityBase.RegisterAsync(this, type, activityTypeName, Client.ResolveNamespace(@namespace));

                    lock (registeredActivityTypes)
                    {
                        registeredActivityTypes.Add(TemporalHelper.GetActivityInterface(type));
                    }
                }
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for workflow and activity implementations 
        /// derived from and registers them with Temporal.  This is equivalent to calling
        /// <see cref="RegisterAssemblyWorkflowsAsync(Assembly, string)"/> and
        /// <see cref="RegisterAssemblyActivitiesAsync(Assembly, string)"/>,
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="namespace">Optionally overrides the default client namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="WorkflowAttribute"/> that are not 
        /// derived from <see cref="WorkflowBase"/> or for types tagged by <see cref="ActivityAttribute"/>
        /// that are now derived from <see cref="ActivityBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all services you will be injecting into activities via
        /// <see cref="NeonHelper.ServiceContainer"/> before you call this as well as 
        /// registering of your activity and workflow implementations before starting 
        /// workers.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyAsync(Assembly assembly, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            EnsureNotDisposed();

            await RegisterAssemblyWorkflowsAsync(assembly, @namespace);
            await RegisterAssemblyActivitiesAsync(assembly, @namespace);
        }

        /// <summary>
        /// <summary>
        /// Registers a workflow implementation with Temporal.
        /// </summary>
        /// <typeparam name="TWorkflow">The <see cref="WorkflowBase"/> derived class implementing the workflow.</typeparam>
        /// <param name="workflowTypeName">
        /// Optionally specifies a custom workflow type name that will be used 
        /// for identifying the workflow implementation in Temporal.  This defaults
        /// to the fully qualified <typeparamref name="TWorkflow"/> type name.
        /// </param>
        /// <param name="namespace">Optionally overrides the default client namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown if another workflow class has already been registered for <paramref name="workflowTypeName"/>.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterWorkflowAsync<TWorkflow>(string workflowTypeName = null, string @namespace = null)
            where TWorkflow : WorkflowBase
        {
            await SyncContext.ClearAsync;
            TemporalHelper.ValidateWorkflowImplementation(typeof(TWorkflow));
            TemporalHelper.ValidateWorkflowTypeName(workflowTypeName);
            EnsureNotDisposed();

            if (isRunning)
            {
                throw new InvalidOperationException("Cannot register workflow or activity implementations after a worker has started.");
            }

            var workflowType = typeof(TWorkflow);

            if (string.IsNullOrEmpty(workflowTypeName))
            {
                workflowTypeName = TemporalHelper.GetWorkflowTypeName(workflowType, workflowType.GetCustomAttribute<WorkflowAttribute>());
            }

            await WorkflowBase.RegisterAsync(this, workflowType, workflowTypeName, Client.ResolveNamespace(@namespace));

            lock (registeredWorkflowTypes)
            {
                registeredWorkflowTypes.Add(TemporalHelper.GetWorkflowInterface(typeof(TWorkflow)));
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for workflow implementations derived from
        /// <see cref="WorkflowBase"/> and tagged by <see cref="WorkflowAttribute"/> with
        /// <see cref="WorkflowAttribute.AutoRegister"/> set to <c>true</c> and registers 
        /// them with Temporal.
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="namespace">Optionally overrides the default client namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="TypeLoadException">
        /// Thrown for types tagged by <see cref="WorkflowAttribute"/> that are not 
        /// derived from <see cref="WorkflowBase"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">Thrown if one of the tagged classes conflict with an existing registration.</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyWorkflowsAsync(Assembly assembly, string @namespace = null)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(assembly != null, nameof(assembly));
            EnsureNotDisposed();

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass))
            {
                var workflowAttribute = type.GetCustomAttribute<WorkflowAttribute>();

                if (workflowAttribute != null && workflowAttribute.AutoRegister)
                {
                    var workflowTypeName = TemporalHelper.GetWorkflowTypeName(type, workflowAttribute);

                    await WorkflowBase.RegisterAsync(this, type, workflowTypeName, Client.ResolveNamespace(@namespace));

                    lock (registeredWorkflowTypes)
                    {
                        registeredWorkflowTypes.Add(TemporalHelper.GetWorkflowInterface(type));
                    }
                }
            }
        }

        /// <summary>
        /// Starts the worker if it has not already been started.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the worker is disposed.</exception>
        public async Task StartAsync()
        {
            EnsureNotDisposed();

            if (isRunning)
            {
                return;
            }

            // Fetch the stub for each registered workflow and activity type so that
            // they'll be precompiled so compilation won't impact workflow and activity
            // performance including potentially intruducing enough delay to cause
            // decision tasks or activity heartbeats to fail (in very rare situations).
            //
            // Note that the compiled stubs are cached, so we don't need to worry
            // about compiling stubs for types more than once causing a problem.

            // $todo(jefflill): Performance optimization:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/796

            lock (registeredWorkflowTypes)
            {
                foreach (var workflowInterface in registeredWorkflowTypes)
                {
                    // Workflows, we're going to compile both the external and child
                    // versions of the stubs.

                    StubManager.GetWorkflowStub(workflowInterface, isChild: false);
                    StubManager.GetWorkflowStub(workflowInterface, isChild: true);
                }
            }

            lock (registeredActivityTypes)
            {
                foreach (var activityInterface in registeredActivityTypes)
                {
                    StubManager.GetActivityStub(activityInterface);
                }
            }

            isRunning = true;
        }
    }
}
