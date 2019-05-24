//-----------------------------------------------------------------------------
// FILE:	    WorkflowRun.cs
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Describes the state of an executed workflow.
    /// </summary>
    public class WorkflowRun
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="id">The current ID for the workflow.</param>
        /// <param name="runId">The original ID the workflow.</param>
        internal WorkflowRun(string runId, string id)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));

            this.RunId = id;
            this.Id    = id;
        }

        /// <summary>
        /// The original ID assigned to the workflow when it was started.
        /// </summary>
        public string RunId { get; private set; }

        /// <summary>
        /// Returns the current ID for workflow execution.  This will be different
        /// than <see cref="RunId"/> when the workflow has been continued as new
        /// or potentially restarted.
        /// </summary>
        public string Id { get; private set; }
    }
}
