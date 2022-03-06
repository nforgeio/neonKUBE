//-----------------------------------------------------------------------------
// FILE:	    Worker.cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
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
    public sealed partial class Worker : IDisposable
    {
        private bool            isRunning          = false;
        private bool            allowRegistrations = true;
        private INeonLogger     log                = LogManager.Default.GetLogger<WorkflowBase>();
        private WorkerOptions   options;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The parent client.</param>
        /// <param name="workerId">The ID of the worker as tracked by the <b>temporal-proxy</b>.</param>
        /// <param name="options">Specifies the worker options or <c>null</c>.</param>
        internal Worker(TemporalClient client, long workerId, WorkerOptions options)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(options != null, nameof(options));

            this.Client   = client;
            this.WorkerId = workerId;
            this.options  = options;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            IsDisposed = true;

            if (isRunning)
            {
                Client.StopWorkerAsync(this).Wait();
                Client = null;
            }

            GC.SuppressFinalize(this);
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
        /// Returns the Temporal task queue.
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
        /// Ensures that the worker is not running during a registration operation. 
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the worker has already started.</exception>
        private void EnsureCanRegister()
        {
            if (!allowRegistrations)
            {
                throw new InvalidOperationException("Cannot register workflow or activity implementations after a worker has started.");
            }
        }

        /// <summary>
        /// Scans the assembly passed looking for workflow and activity implementations 
        /// derived from and registers them with Temporal.  This is equivalent to calling
        /// <see cref="RegisterAssemblyWorkflowsAsync(Assembly, bool)"/> and
        /// <see cref="RegisterAssemblyActivitiesAsync(Assembly, bool)"/>,
        /// </summary>
        /// <param name="assembly">The target assembly.</param>
        /// <param name="disableDuplicateCheck">Disable checks for duplicate workflow and activity registrations.</param>
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
        /// <exception cref="RegistrationException">Thrown when there's a problem with the registration.</exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all services you will be injecting into activities via
        /// <see cref="NeonHelper.ServiceContainer"/> before you call this as well as 
        /// registering of your activity and workflow implementations before starting 
        /// a worker.
        /// </note>
        /// </remarks>
        public async Task RegisterAssemblyAsync(Assembly assembly, bool disableDuplicateCheck = false)
        {
            await SyncContext.ClearAsync;
            EnsureNotDisposed();
            EnsureCanRegister();

            await RegisterAssemblyWorkflowsAsync(assembly, disableDuplicateCheck);
            await RegisterAssemblyActivitiesAsync(assembly, disableDuplicateCheck);
        }

        /// <summary>
        /// Starts the worker if it has not already been started.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the worker is disposed.</exception>
        public async Task StartAsync()
        {
            await SyncContext.ClearAsync;
            EnsureNotDisposed();

            if (isRunning)
            {
                return;
            }

            // Register activities and workflows with the proxy.

            List<Type>      clonedActivityRegistrations;
            List<Type>      clonedWorkflowRegistrations;

            allowRegistrations = false;

            lock (registeredWorkflowTypes)
            {
                clonedWorkflowRegistrations = registeredWorkflowTypes.ToList();
            }

            lock (registeredActivityTypes)
            {
                clonedActivityRegistrations = registeredActivityTypes.ToList();
            }

            foreach (var activityType in clonedActivityRegistrations)
            {
                await RegisterActivityImplementationAsync(activityType);
            }

            foreach (var workflowType in registeredWorkflowTypes)
            {
                await RegisterWorkflowImplementationAsync(workflowType);
            }

            // Start the worker.

            await Client.StartWorkerAsync(this);

            isRunning = true;
        }
    }
}
