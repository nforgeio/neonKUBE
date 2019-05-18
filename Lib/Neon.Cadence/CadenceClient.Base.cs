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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;
using Neon.Tasks;

using Neon.Cadence.Internal;

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
        /// Signals Cadence that the application is capable of executing workflows
        /// of type <typeparamref name="TWorkflow"/>.
        /// </summary>
        /// <typeparam name="TWorkflow">Identifies the workflow implementation.</typeparam>
        /// <param name="domain">The target Cadence domain.</param>
        /// <param name="tasklist">The target task list.</param>
        /// <param name="options">Optionally specifies additional worker options.</param>
        /// <param name="name">
        /// Optionally overrides the fully qualified <typeparamref name="TWorkflow"/> name 
        /// used to register the worker.
        /// </param>
        /// <returns>A <see cref="Worker"/> identifying the worker instance.</returns>
        /// <remarks>
        /// <para>
        /// Your workflow application will need to call this method so that Cadence will know
        /// that it can schedule workflows to run within the current process.  You'll need
        /// to specify the target Cadence domain and tasklist.
        /// </para>
        /// <para>
        /// You may also specify an optional <see cref="WorkerOptions"/> parameter as well
        /// as customize the name used to register the workflow, which defaults to the
        /// fully qualified name of the workflow type.
        /// </para>
        /// <para>
        /// This method returns a <see cref="Worker"/> which may be used by advanced applications
        /// to explicitly stop the worker by calling <see cref="StopWorkerAsync(Worker)"/>.  Doing
        /// this is entirely optional.
        /// </para>
        /// </remarks>
        internal async Task<Worker> StartWorkflowWorkerAsync<TWorkflow>(string domain, string tasklist, WorkerOptions options = null, string name = null)
            where TWorkflow : Workflow
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tasklist));

            options = options ?? new WorkerOptions();

            var reply = (NewWorkerReply)(await CallProxyAsync(
                new NewWorkerRequest()
                {
                    Name       = name ?? typeof(TWorkflow).FullName,
                    IsWorkflow = true,
                    Domain     = domain,
                    TaskList   = tasklist,
                    Options    = options.ToInternal()
                }));

            reply.ThrowOnError();

            var worker = new Worker(reply.WorkerId);

            lock (syncLock)
            {
                workers.Add(reply.WorkerId, worker);
            }

            return worker;
        }

        /// <summary>
        /// Signals Cadence that the application is capable of executing activities
        /// of type <typeparamref name="TActivity"/>.
        /// </summary>
        /// <typeparam name="TActivity">Identifies the activity implementation.</typeparam>
        /// <param name="domain">The target Cadence domain.</param>
        /// <param name="tasklist">The target task list.</param>
        /// <param name="options">Optionally specifies additional worker options.</param>
        /// <param name="name">
        /// Optionally overrides the fully qualified <typeparamref name="TActivity"/> name 
        /// used to register the worker.
        /// </param>
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
        internal async Task<Worker> StartActivityWorkerAsync<TActivity>(string domain, string tasklist, WorkerOptions options = null, string name = null)
            where TActivity : Activity
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(tasklist));

            options = options ?? new WorkerOptions();

            var reply = (NewWorkerReply)(await CallProxyAsync(
                new NewWorkerRequest()
                {
                    Name       = name ?? typeof(TActivity).FullName,
                    IsWorkflow = false,
                    Domain     = domain,
                    TaskList   = tasklist,
                    Options    = options.ToInternal()
                }));

            reply.ThrowOnError();

            var worker = new Worker(reply.WorkerId);

            lock (syncLock)
            {
                workers.Add(reply.WorkerId, worker);
            }

            return worker;
        }

        /// <summary>
        /// Signals Cadence that it should stop invoking activities and workflows 
        /// for the specified <see cref="Worker"/> (returned by a previous call to
        /// <see cref="StartWorkflowWorkerAsync(string, string, WorkerOptions, string)"/>)
        /// or <see cref="StartActivityWorkerAsync(string, string, WorkerOptions, string)"/>.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// This method does nothing if the worker is already stopped.
        /// </remarks>
        internal async Task StopWorkerAsync(Worker worker)
        {
            lock (syncLock)
            {
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
