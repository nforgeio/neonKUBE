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
        /// Signals Cadence that it can begin invoking workflows and activities from the
        /// specified domain and task list on the current connection.
        /// </summary>
        /// <param name="domain">The target Cadence domain.</param>
        /// <param name="taskList">The target task list.</param>
        /// <param name="options">Optionally specifies additional worker options.</param>
        /// <returns>A <see cref="Worker"/> identifying the worker instance.</returns>
        public async Task<Worker> StartWorkerAsync(string domain, string taskList, WorkerOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskList));

            options = options ?? new WorkerOptions();

            var reply = (NewWorkerReply)(await CallProxyAsync(
                new NewWorkerRequest()
                {
                    Domain   = domain,
                    TaskList = taskList,
                    Options  = options.ToInternal()
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
        /// <see cref="StartWorkerAsync(string, string, WorkerOptions)"/>).
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// This method does nothing if the worker is already stopped.
        /// </remarks>
        public async Task StopWorkerAsync(Worker worker)
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
