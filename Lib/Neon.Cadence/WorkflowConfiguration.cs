//-----------------------------------------------------------------------------
// FILE:	    WorkflowConfiguration.cs
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
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Common;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Describes a workflow's configuration.
    /// </summary>
    public class WorkflowConfiguration
    {
        /// <summary>
        /// Identifies the tasklist where the workflow was scheduled.
        /// </summary>
        public string TaskList { get; internal set; }

        /// <summary>
        /// Maximum time the entire workflow may take to complete end-to-end.
        /// </summary>
        public TimeSpan ExecutionStartToCloseTimeout { get; internal set; }

        /// <summary>
        /// Maximum time a workflow task/decision may take to complete.
        /// </summary>
        public TimeSpan TaskStartToCloseTimeoutSeconds { get; internal set; }

        /// <summary>
        /// The child execution policy.
        /// </summary>
        public ChildWorkflowPolicy ChildPolicy { get; internal set; }
    }
}
