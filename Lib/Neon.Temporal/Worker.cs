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
    public sealed class Worker : IDisposable
    {
        //---------------------------------------------------------------------
        // Activity related private types

        /// <summary>
        /// Used for mapping an activity type name to its underlying type
        /// and entry point method.
        /// </summary>
        private struct ActivityRegistration
        {
            /// <summary>
            /// The activity type.
            /// </summary>
            public Type ActivityType { get; set; }

            /// <summary>
            /// The activity entry point method.
            /// </summary>
            public MethodInfo ActivityMethod { get; set; }

            /// <summary>
            /// The activity method parameter types.
            /// </summary>
            public Type[] ActivityMethodParameterTypes { get; set; }
        }

        //---------------------------------------------------------------------
        // Workflow related private types

        /// <summary>
        /// Enumerates the possible contexts workflow code may be executing within.
        /// This is used to limit what code can do (i.e. query methods shouldn't be
        /// allowed to execute activities).  This is also used in some situations to
        /// modify how workflow code behaves.
        /// </summary>
        internal enum WorkflowCallContext
        {
            /// <summary>
            /// The current task is not executing within the context
            /// of any workflow method.
            /// </summary>
            None = 0,

            /// <summary>
            /// The current task is executing within the context of
            /// a workflow entrypoint.
            /// </summary>
            Entrypoint,

            /// <summary>
            /// The current task is executing within the context of a
            /// workflow signal method.
            /// </summary>
            Signal,

            /// <summary>
            /// The current task is executing within the context of a
            /// workflow query method.
            /// </summary>
            Query,

            /// <summary>
            /// The current task is executing within the context of a
            /// normal or local activity.
            /// </summary>
            Activity
        }

        /// <summary>
        /// Describes the workflow implementation type, entry point method, and 
        /// signal/query methods for registered workflow.
        /// </summary>
        private class WorkflowRegistration
        {
            /// <summary>
            /// The workflow implemention type.
            /// </summary>
            public Type WorkflowType { get; set; }

            /// <summary>
            /// The workflow entry point method.
            /// </summary>
            public MethodInfo WorkflowMethod { get; set; }

            /// <summary>
            /// The workflow entry point parameter types.
            /// </summary>
            public Type[] WorkflowMethodParameterTypes { get; set; }

            /// <summary>
            /// Maps workflow signal and query names to the corresponding
            /// method implementations.
            /// </summary>
            public WorkflowMethodMap MethodMap { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private AsyncMutex      workerMutex = new AsyncMutex();
        private bool            isRunning   = false;
        private INeonLogger     log         = LogManager.Default.GetLogger<WorkflowBase>();
        private WorkerOptions   options;

        // Maps a workflow context ID to the workflow's internal state.
        private Dictionary<long, WorkflowBase> idToWorkflow = new Dictionary<long, WorkflowBase>();

        // Maps an activity context ID to the activity's internal state.
        private static Dictionary<long, ActivityBase> idToActivity = new Dictionary<long, ActivityBase>();

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
            using (workerMutex.AcquireAsync().GetAwaiter().GetResult())
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
        /// Ensures that the worker is not running during a registration operation. 
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when the worker has already started.</exception>
        private void EnsureCanRegister()
        {
            if (isRunning)
            {
                throw new InvalidOperationException("Cannot register workflow or activity implementations after a worker has started.");
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
            EnsureCanRegister();

            var activityType = typeof(TActivity);

            if (string.IsNullOrEmpty(activityTypeName))
            {
                activityTypeName = TemporalHelper.GetActivityTypeName(activityType, activityType.GetCustomAttribute<ActivityAttribute>());
            }

            await ActivityBase.RegisterAsync(this, activityType, activityTypeName, Client.ResolveNamespace(@namespace));

            lock (await workerMutex.AcquireAsync())
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
            EnsureCanRegister();

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass))
            {
                var activityAttribute = type.GetCustomAttribute<ActivityAttribute>();

                if (activityAttribute != null && activityAttribute.AutoRegister)
                {
                    var activityTypeName = TemporalHelper.GetActivityTypeName(type, activityAttribute);

                    await ActivityBase.RegisterAsync(this, type, activityTypeName, Client.ResolveNamespace(@namespace));

                    using (await workerMutex.AcquireAsync())
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
            EnsureCanRegister();

            await RegisterAssemblyWorkflowsAsync(assembly, @namespace);
            await RegisterAssemblyActivitiesAsync(assembly, @namespace);
        }

        /// <summary>
        /// Registers a workflow implementation with Temporal.
        /// </summary>
        /// <typeparam name="TWorkflow">The <see cref="WorkflowBase"/> derived class implementing the workflow.</typeparam>
        /// <param name="namespace">Optionally overrides the default client namespace.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the worker has already been started.  You must register workflow 
        /// and activity implementations before starting workers.
        /// </exception>
        /// <remarks>
        /// <note>
        /// Be sure to register all of your workflow implementations before starting workers.
        /// </note>
        /// </remarks>
        public async Task RegisterWorkflowAsync<TWorkflow>(string @namespace = null)
            where TWorkflow : WorkflowBase
        {
            await SyncContext.ClearAsync;
            TemporalHelper.ValidateWorkflowImplementation(typeof(TWorkflow));
            EnsureNotDisposed();
            EnsureCanRegister();

            var workflowType = typeof(TWorkflow);

            using (await workerMutex.AcquireAsync())
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
            EnsureCanRegister();

            foreach (var type in assembly.GetTypes().Where(t => t.IsClass))
            {
                var workflowAttribute = type.GetCustomAttribute<WorkflowAttribute>();

                if (workflowAttribute != null && workflowAttribute.AutoRegister)
                {
                    using (await workerMutex.AcquireAsync())
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

            using (await workerMutex.AcquireAsync())
            {
                // Fetch the stub for each registered workflow and activity type so that
                // they'll be precompiled so compilation won't impact workflow and activity
                // performance, potentially intruducing enough delay to cause decision tasks
                // or activity heartbeats to fail (in probably rare situations).
                //
                // Note that the compiled stubs are cached, so we don't need to worry
                // about compiling stubs for types more than once causing a problem.

                // $todo(jefflill): Performance optimization:
                //
                //      https://github.com/nforgeio/neonKUBE/issues/796

                foreach (var workflowInterface in registeredWorkflowTypes)
                {
                    // Workflows, we're going to compile both the external and child
                    // versions of the stubs.

                    StubManager.GetWorkflowStub(workflowInterface, isChild: false);
                    StubManager.GetWorkflowStub(workflowInterface, isChild: true);
                }

                foreach (var activityInterface in registeredActivityTypes)
                {
                    StubManager.GetActivityStub(activityInterface);
                }

                // Register workflow implementations.

                foreach (var workflowType in registeredWorkflowTypes)
                {
                    await RegisterWorkflowImplementationAsync(workflowType);
                }
            }

            isRunning = true;
        }

        //---------------------------------------------------------------------
        // Workflow runtime implementation

        private List<Type>                                  registeredWorkflowTypes = new List<Type>();
        private Dictionary<string, WorkflowRegistration>    nameToRegistration      = new Dictionary<string, WorkflowRegistration>();

        /// <summary>
        /// Registers a workflow implementation.
        /// </summary>
        /// <param name="workflowType">The workflow implementation type.</param>
        private async Task RegisterWorkflowImplementationAsync(Type workflowType)
        {
            TemporalHelper.ValidateWorkflowImplementation(workflowType);

            var methodMap = WorkflowMethodMap.Create(workflowType);

            // We need to register each workflow method that implements a workflow interface method
            // with the same signature that that was tagged by [WorkflowMethod].
            //
            // First, we'll create a dictionary that maps method signatures from any inherited
            // interfaces that are tagged by [WorkflowMethod] to the attribute.

            var methodSignatureToAttribute = new Dictionary<string, WorkflowMethodAttribute>();

            foreach (var interfaceType in workflowType.GetInterfaces())
            {
                foreach (var method in interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var workflowMethodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

                    if (workflowMethodAttribute == null)
                    {
                        continue;
                    }

                    var signature = method.ToString();

                    if (methodSignatureToAttribute.ContainsKey(signature))
                    {
                        throw new NotSupportedException($"Workflow type [{workflowType.FullName}] cannot implement the [{signature}] method from two different interfaces.");
                    }

                    methodSignatureToAttribute.Add(signature, workflowMethodAttribute);
                }
            }

            // Next, we need to register the workflow methods that implement the
            // workflow interface.

            foreach (var method in workflowType.GetMethods())
            {
                if (!methodSignatureToAttribute.TryGetValue(method.ToString(), out var workflowMethodAttribute))
                {
                    continue;
                }

                var workflowTypeName = TemporalHelper.GetWorkflowTypeName(workflowType, workflowMethodAttribute);

                if (nameToRegistration.TryGetValue(workflowTypeName, out var existingRegistration))
                {
                    if (!object.ReferenceEquals(existingRegistration.WorkflowType, workflowType))
                    {
                        throw new InvalidOperationException($"Conflicting workflow interface registration: Workflow interface [{workflowType.FullName}] is already registered for workflow type name [{workflowTypeName}].");
                    }
                }
                else
                {
                    nameToRegistration[workflowTypeName] =
                        new WorkflowRegistration()
                        {
                            WorkflowType                 = workflowType,
                            WorkflowMethod               = method,
                            WorkflowMethodParameterTypes = method.GetParameterTypes(),
                            MethodMap                    = methodMap
                        };
                }

                var reply = (WorkflowRegisterReply)await Client.CallProxyAsync(
                    new WorkflowRegisterRequest()
                    {
                        Name     = workflowTypeName,
                        WorkerId = WorkerId
                    });

                reply.ThrowOnError();
            }
        }

        //---------------------------------------------------------------------
        // Activity runtime implementation

        private List<Type> registeredActivityTypes = new List<Type>();
    }
}
