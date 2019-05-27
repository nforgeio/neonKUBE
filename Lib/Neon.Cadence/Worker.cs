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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Identifies a worker registered with Cadence.
    /// </summary>
    public class Worker
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The parent client.</param>
        /// <param name="isWorkflow">Used to distinguish between workflow and activity workers.</param>
        /// <param name="workerId">The ID of the worker as tracked by the <b>cadence-proxy</b>.</param>
        /// <param name="domain">The Cadence domain where the worker is registered.</param>
        /// <param name="taskList">The Cadence tasklist.</param>
        /// <param name="typeName">The registered workflow or activity type name.</param>
        internal Worker(CadenceClient client, bool isWorkflow, long workerId, string domain, string taskList, string typeName)
        {
            this.Client     = client;
            this.IsWorkflow = isWorkflow;
            this.WorkerId   = workerId;
            this.Domain     = domain;
            this.Tasklist   = taskList;
            this.TypeName   = typeName;
        }

        /// <summary>
        /// Returns the parent Cadence client.
        /// </summary>
        internal CadenceClient Client { get; private set; }

        /// <summary>
        /// Used to distinguish between workflow and activity workers.
        /// </summary>
        internal bool IsWorkflow { get; private set;}

        /// <summary>
        /// Returns the ID of the worker as tracked by the <b>cadence-proxy</b>.
        /// </summary>
        internal long WorkerId { get; private set; }

        /// <summary>
        /// Returns the Cadence domain where the worker is registered.
        /// </summary>
        internal string Domain { get; private set; }

        /// <summary>
        /// Returns the Cadence tasklist.
        /// </summary>
        internal string Tasklist { get; private set; }

        /// <summary>
        /// Returns the registered workflow or activity type name.
        /// </summary>
        internal string TypeName { get; private set; }
    }
}
