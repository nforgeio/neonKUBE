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
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence basic client related operations.

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
        /// Signals Cadence that the application is capable of executing workflows for
        /// a specific domain and tasklist.
        /// </summary>
        /// <param name="domain">The target Cadence domain.</param>
        /// <param name="tasklist">Optionally specifies the target tasklist (defaults to <b>"default"</b>).</param>
        /// <param name="options">Optionally specifies additional worker options.</param>
        /// <returns>A <see cref="Worker"/> identifying the worker instance.</returns>
        /// <remarks>
        /// <para>
        /// Your workflow application will need to call this method so that Cadence will know
        /// that it can schedule workflows to run within the current process.  You'll need
        /// to specify the target Cadence domain and tasklist.
        /// </para>
        /// <para>
        /// You may also specify an optional <see cref="WorkerOptions"/> parameter as well
        /// as customize the workflow typ name used to register the workflow, which defaults 
        /// to the fully qualified name of the workflow type.
        /// </para>
        /// <para>
        /// This method returns a <see cref="Worker"/> which may be used by advanced applications
        /// to explicitly stop the worker by calling <see cref="StopWorkerAsync(Worker)"/>.  Doing
        /// this is entirely optional.
        /// </para>
        /// </remarks>
        public async Task<Worker> StartWorkflowWorkerAsync(string domain, string tasklist = "default", WorkerOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tasklist));

            Worker worker;

            options = options ?? new WorkerOptions();

            lock (syncLock)
            {
                // Ensure that we haven't already registered a worker for the
                // specified workflow, domain, and tasklist.  We'll just ignore
                // the call if we already have this registration.
                //
                // I know that this is a linear search but the number of workflow
                // registrations per service will generally be very small and 
                // registrations will happen infrequently (typically just once
                // per service, when it starts).

                worker = workers.Values.SingleOrDefault(wf => wf.IsWorkflow && wf.Domain == domain && wf.Tasklist == tasklist);

                if (worker != null)
                {
                    return worker;
                }
            }

            options = options ?? new WorkerOptions();

            var reply = (NewWorkerReply)(await CallProxyAsync(
                new NewWorkerRequest()
                {
                    IsWorkflow = true,
                    Domain     = domain,
                    TaskList   = tasklist,
                    Options    = options.ToInternal()
                }));

            reply.ThrowOnError();

            worker = new Worker(this, true, reply.WorkerId, domain, tasklist);

            lock (syncLock)
            {
                workers.Add(reply.WorkerId, worker);
            }

            return worker;
        }

        /// <summary>
        /// Signals Cadence that the application is capable of executing activities for a specific
        /// domain and tasklist.
        /// </summary>
        /// <param name="domain">The target Cadence domain.</param>
        /// <param name="tasklist">Optionally specifies the target tasklist (defaults to <b>"default"</b>).</param>
        /// <param name="options">Optionally specifies additional worker options.</param>
        /// <returns>A <see cref="Worker"/> identifying the worker instance.</returns>
        /// <remarks>
        /// <para>
        /// Your workflow application will need to call this method so that Cadence will know
        /// that it can schedule activities to run within the current process.  You'll need
        /// to specify the target Cadence domain and tasklist.
        /// </para>
        /// <para>
        /// You may also specify an optional <see cref="WorkerOptions"/> parameter as well
        /// as customize the name used to register the activity, which defaults to the
        /// fully qualified name of the activity type.
        /// </para>
        /// <para>
        /// This method returns a <see cref="Worker"/> which may be used by advanced applications
        /// to explicitly stop the worker by calling <see cref="StopWorkerAsync(Worker)"/>.  Doing
        /// this is entirely optional.
        /// </para>
        /// </remarks>
        public async Task<Worker> StartActivityWorkerAsync(string domain, string tasklist = "default", WorkerOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tasklist));

            Worker worker;

            options = options ?? new WorkerOptions();

            lock (syncLock)
            {
                // Ensure that we haven't already registered a worker for the
                // specified activity, domain, and tasklist.  We'll just ignore
                // the call if we already have this registration.
                //
                // I know that this is a linear search but the number of activity
                // registrations per service will generally be very small and 
                // registrations will happen infrequently (typically just once
                // per service, when it starts).

                worker = workers.Values.SingleOrDefault(wf => !wf.IsWorkflow && wf.Domain == domain && wf.Tasklist == tasklist);

                if (worker != null)
                {
                    return worker;
                }
            }

            options = options ?? new WorkerOptions();

            var reply = (NewWorkerReply)(await CallProxyAsync(
                new NewWorkerRequest()
                {
                    IsWorkflow = false,
                    Domain     = domain,
                    TaskList   = tasklist,
                    Options    = options.ToInternal()
                }));

            reply.ThrowOnError();

            worker = new Worker(this, false, reply.WorkerId, domain, tasklist);

            lock (syncLock)
            {
                workers.Add(reply.WorkerId, worker);
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
        public async Task StopWorkerAsync(Worker worker)
        {
            Covenant.Requires<ArgumentNullException>(worker != null);

            lock (syncLock)
            {
                if (!object.ReferenceEquals(worker.Client, this))
                {
                    throw new InvalidOperationException("The worker passed does not belong to this client connection.");
                }

                if (!workers.ContainsKey(worker.WorkerId))
                {
                    // The worker has already been stopped.

                    return;
                }

                workers.Remove(worker.WorkerId);
            }

            var reply = (StopWorkerReply)(await CallProxyAsync(new StopWorkerRequest() { WorkerId = worker.WorkerId }));
            
            reply.ThrowOnError();
        }
    }
}
