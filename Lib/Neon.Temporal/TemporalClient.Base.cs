//-----------------------------------------------------------------------------
// FILE:	    TemporalClient.Base.cs
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
    public partial class TemporalClient
    {
        //---------------------------------------------------------------------
        // Temporal basic client related operations.

        /// <summary>
        /// Pings the <b>temporal-proxy</b> and waits for the reply.  This is used 
        /// mainly for low-level performance and load testing but can also be used
        /// to explicitly verify that the <b>temporal-proxy</b> is still alive.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task PingAsync()
        {
            await SyncContext.ClearAsync;
            EnsureNotDisposed();

            await CallProxyAsync(new PingRequest());
        }

        /// <summary>
        /// Creates a new Temporal <see cref="Worker"/> attached to the current client.  You'll
        /// use this to register your workflow and/or activity implementations with Temporal and
        /// the start the worker to signal Temporal that the worker is ready for business.
        /// </summary>
        /// <param name="options">Optionally specifies additional worker options.</param>
        /// <returns>A <see cref="Worker"/> identifying the worker instance.</returns>
        /// <remarks>
        /// <para>
        /// Each worker instance will be responsible for actually executing Temporal workflows and
        /// activities.  Workers are registered within a Temporal namespace and are assigned to a
        /// task list which identifies the virtual queue Temporal uses to schedule work on workers.
        /// Workers implementing the same workflows and activities will generally be assigned to
        /// the same task list (which is just an identifying string).
        /// </para>
        /// <para>
        /// After you have a new worker, you'll need to register workflow and/or activity implementations
        /// via <see cref="Worker.RegisterActivityAsync{TActivity}(string, string)"/>,
        /// <see cref="Worker.RegisterAssemblyActivitiesAsync(Assembly, string)"/>,
        /// <see cref="Worker.RegisterAssemblyAsync(Assembly, string)"/>, or
        /// <see cref="Worker.RegisterAssemblyWorkflowsAsync(Assembly, string)"/>.
        /// </para>
        /// <para>
        /// Then after completing the registrations, you'll call <see cref="Worker.StartAsync"/>
        /// to start the worker, signalling to Temporal that the worker is ready to execute
        /// workflows and activities.
        /// </para>
        /// <para>
        /// You may call <see cref="Worker.Dispose"/> to explicitly stop a worker or just
        /// dispose the <see cref="TemporalClient"/> which automatically disposes any
        /// related workers.
        /// </para>
        /// </remarks>
        public async Task<Worker> NewWorkerAsync(WorkerOptions options = null)
        {
            await SyncContext.ClearAsync;
            EnsureNotDisposed();

            options = options ?? new WorkerOptions();

            if (string.IsNullOrEmpty(options.Namespace))
            {
                options.Namespace = Settings.Namespace;
            }

            if (string.IsNullOrEmpty(options.TaskList))
            {
                options.TaskList = Settings.DefaultTaskList;

                if (string.IsNullOrEmpty(options.TaskList))
                {
                    throw new ArgumentException("Worker cannot be started without a task list.  Please specify this via [WorkerOptions.TaskList] or [TemporalSettings.DefaultTaskList].");
                }
            }

            var reply = (NewWorkerReply)(await CallProxyAsync(
                new NewWorkerRequest()
                {
                    TaskList = options.TaskList,
                    Options  = options.ToInternal()
                }));

            reply.ThrowOnError();

            var worker = new Worker(this, reply.WorkerId, options);

            lock (syncLock)
            {
                idToWorker.Add(reply.WorkerId, worker);
            }

            return worker;
        }

        /// <summary>
        /// Returns information about pollers (AKA workers) that have communicated 
        /// with the Temporal cluster in the last few minutes.
        /// </summary>
        /// <param name="taskList">Identifies the tasklist.</param>
        /// <param name="taskListType">
        /// Indicates whether to return information for decision (AKA workflow pollers)
        /// or activity pollers.
        /// </param>
        /// <param name="namespace">Optionally specifies the Temporal namespace.</param>
        /// <param name="includeStatus">Optionally specifies that the result should include information about the task list status.</param>
        /// <returns>The <see cref="TaskListDescription"/> for the pollers.</returns>
        public async Task<TaskListDescription> DescribeTaskListAsync(string taskList, TaskListType taskListType, string @namespace = null, bool includeStatus = false)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskList));
            EnsureNotDisposed();

            @namespace = ResolveNamespace(@namespace);

            var reply = (DescribeTaskListReply)await CallProxyAsync(
                new DescribeTaskListRequest()
                {
                    Name          = taskList,
                    TaskListType  = taskListType,
                    Namespace     = @namespace,
                    IncludeStatus = includeStatus
                });

            reply.ThrowOnError();

            return reply.Result.ToPublic();
        }

        //---------------------------------------------------------------------
        // Internal utilities

        /// <summary>
        /// Signals Temporal that it should stop invoking activities and workflows 
        /// for the specified <see cref="Worker"/> (returned by a previous call to
        /// <see cref="NewWorkerAsync(WorkerOptions)"/>).
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// This method does nothing if the worker is already stopped.
        /// </remarks>
        internal async Task StopWorkerAsync(Worker worker)
        {
            Covenant.Requires<ArgumentNullException>(worker != null, nameof(worker));
            EnsureNotDisposed(noClosingCheck: true);

            if (!object.ReferenceEquals(worker.Client, this))
            {
                throw new InvalidOperationException("The worker passed does not belong to this client connection.");
            }

            lock (syncLock)
            {
                if (!idToWorker.ContainsKey(worker.WorkerId))
                {
                    // The worker does not exist.  We're going to ignore this.

                    return;
                }
            }

            var reply = (StopWorkerReply)(await CallProxyAsync(new StopWorkerRequest() { WorkerId = worker.WorkerId }));
            
            reply.ThrowOnError();
        }
    }
}
