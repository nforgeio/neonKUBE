//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.Base.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence
{
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence basic client related operations.

        private AsyncMutex workerRegistrationMutex = new AsyncMutex();

        /// <summary>
        /// Pings the <b>cadence-proxy</b> and waits for the reply.  This is used 
        /// mainly for low-level performance and load testing but can also be used
        /// to explicitly verify that the <b>cadence-proxy</b> is still alive.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task PingAsync()
        {
            await CallProxyAsync(new PingRequest());
        }

        /// <summary>
        /// Signals Cadence that the application is capable of executing activities for a specific
        /// domain and task list.
        /// </summary>
        /// <param name="domain">Optionally specifies the target Cadence domain.  This defaults to the domain configured for the client.</param>
        /// <param name="taskList">Optionally specifies the target task list (defaults to <b>"default"</b>).</param>
        /// <param name="options">Optionally specifies additional worker options.</param>
        /// <returns>A <see cref="Worker"/> identifying the worker instance.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an attempt is made to recreate a worker with the
        /// same properties on a given client.  See the note in the remarks.
        /// </exception>
        /// <remarks>
        /// <para>
        /// Your workflow application will need to call this method so that Cadence will know
        /// that it can schedule activities to run within the current process.  You'll need
        /// to specify the target Cadence domain and task list.
        /// </para>
        /// <para>
        /// You may also specify an optional <see cref="WorkerOptions"/> parameter as well
        /// as customize the name used to register the activity, which defaults to the
        /// fully qualified name of the activity type.
        /// </para>
        /// <para>
        /// This method returns a <see cref="Worker"/> which implements <see cref="IDisposable"/>.
        /// It's a best practice to call <see cref="Dispose()"/> just before the a worker process
        /// terminates, but this is optional.  Advanced worker implementation that need to change
        /// their configuration over time can also call <see cref="Dispose()"/> to stop workers
        /// for specific domains and task lists.
        /// </para>
        /// <note>
        /// The Cadence GOLANG client does not appear to support starting a worker with a given
        /// set of parameters, stopping that workflow, and then restarting another worker
        /// with the same parameters on the same client.  This method detects this situation
        /// and throws an <see cref="InvalidOperationException"/> when these restart attempts
        /// are made.
        /// </note>
        /// </remarks>
        public async Task<Worker> StartWorkerAsync(string domain, string taskList = "default", WorkerOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskList));

            Worker worker;

            options = options ?? new WorkerOptions();

            using (await workerRegistrationMutex.AcquireAsync())
            {
                // Ensure that we haven't already registered a worker for the
                // specified activity, domain, and task list.  We'll just increment
                // the reference count for the existing worker and return it 
                // in this case.
                //
                // I know that this is a linear search but the number of activity
                // registrations per service will generally be very small and 
                // registrations will happen infrequently (typically just once
                // per service, when it starts).

                // $note(jeff.lill):
                //
                // If the worker exists but its [refcount==0], then we're going to
                // throw an exception because Cadence doesn't support recreating
                // a worker with the same parameters on the same client.

                worker = workers.Values.SingleOrDefault(wf => 
                    wf.Options.DisableActivityWorker == options.DisableActivityWorker && 
                    wf.Options.DisableWorkflowWorker == options.DisableWorkflowWorker && 
                    wf.Domain == domain && wf.Tasklist == taskList);

                if (worker != null)
                {
                    if (worker.RefCount == 0)
                    {
                        throw new InvalidOperationException("A worker with these same parameters has already been started and stopped on this Cadence client.  Cadence does not support recreating workers for a given client instance.");
                    }

                    Interlocked.Increment(ref worker.RefCount);
                    return worker;
                }

                options = options ?? new WorkerOptions();

                var reply = (NewWorkerReply)(await CallProxyAsync(
                    new NewWorkerRequest()
                    {
                        Domain   = domain,
                        TaskList = taskList,
                        Options  = options.ToInternal()
                    }));

                reply.ThrowOnError();

                worker = new Worker(this, reply.WorkerId, domain, taskList, options);
                workers.Add(reply.WorkerId, worker);

                activityWorkerStarted = !options.DisableActivityWorker || activityWorkerStarted;
                workflowWorkerStarted = !options.DisableWorkflowWorker || workflowWorkerStarted;
            }

            return worker;
        }

        /// <summary>
        /// Signals Cadence that it should stop invoking activities and workflows 
        /// for the specified <see cref="Worker"/> (returned by a previous call to
        /// <see cref="StartWorkflowWorkerAsync(string, string, WorkerOptions)"/>)
        /// or <see cref="StartActivityWorkerAsync(string, string, WorkerOptions)"/>.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// This method does nothing if the worker is already stopped.
        /// </remarks>
        internal async Task StopWorkerAsync(Worker worker)
        {
            Covenant.Requires<ArgumentNullException>(worker != null);

            using (await workerRegistrationMutex.AcquireAsync())
            {
                if (!object.ReferenceEquals(worker.Client, this))
                {
                    throw new InvalidOperationException("The worker passed does not belong to this client connection.");
                }

                if (!workers.ContainsKey(worker.WorkerId))
                {
                    // The worker does not exist.  We're going to ignore this.

                    return;
                }

                // $note(jeff.lill):
                //
                // If Cadence was able to restart a given worker, we'd uncomment
                // this line.

                // workers.Remove(worker.WorkerId);
            }

            var reply = (StopWorkerReply)(await CallProxyAsync(new StopWorkerRequest() { WorkerId = worker.WorkerId }));
            
            reply.ThrowOnError();
        }
    }
}
