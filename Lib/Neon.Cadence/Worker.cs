//-----------------------------------------------------------------------------
// FILE:	    Worker.cs
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
using System.Threading;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Identifies a worker registered with Cadence.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cadence doesn't appear to support starting, stopping, and then restarting the same
    /// worker within an individual Cadence client so this class will prevent this.
    /// </para>
    /// </remarks>
    public sealed class Worker : IDisposable
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The parent client.</param>
        /// <param name="mode">Identifies whether the worker will process activities, workflows, or both.</param>
        /// <param name="workerId">The ID of the worker as tracked by the <b>cadence-proxy</b>.</param>
        /// <param name="domain">The Cadence domain where the worker is registered.</param>
        /// <param name="taskList">The Cadence task list.</param>
        internal Worker(CadenceClient client, WorkerMode mode, long workerId, string domain, string taskList)
        {
            this.Client     = client;
            this.Mode       = mode;
            this.WorkerId   = workerId;
            this.Domain     = domain;
            this.Tasklist   = taskList;
            this.RefCount   = 1;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (IsDisposed)
            {
                return;
            }

            if (Interlocked.Decrement(ref RefCount) == 0)
            {
                Client.StopWorkerAsync(this).Wait();
                Client = null;

                GC.SuppressFinalize(this);
            }
        }

        /// <summary>
        /// Returns the parent Cadence client.
        /// </summary>
        internal CadenceClient Client { get; private set; }

        /// <summary>
        /// Indicates whether the worker has been fully disposed.
        /// </summary>
        internal bool IsDisposed => RefCount == 0;

        /// <summary>
        /// Identifies whether the worker will process activities, workflows, or both.
        /// </summary>
        internal WorkerMode Mode { get; private set; }

        /// <summary>
        /// Returns the ID of the worker as tracked by the <b>cadence-proxy</b>.
        /// </summary>
        internal long WorkerId { get; private set; }

        /// <summary>
        /// Returns the Cadence domain where the worker is registered.
        /// </summary>
        internal string Domain { get; private set; }

        /// <summary>
        /// Returns the Cadence task list.
        /// </summary>
        internal string Tasklist { get; private set; }

        /// <summary>
        /// Returns the current worker reference count.  This will be set to
        /// <b>1</b> the first time the worker is registered.
        /// </summary>
        internal int RefCount;
    }
}