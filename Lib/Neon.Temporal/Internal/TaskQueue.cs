//-----------------------------------------------------------------------------
// FILE:	    TaskQueue.cs
// CONTRIBUTOR: John C Burns
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Text;

using Newtonsoft.Json;

using Neon.Data;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Represents a Temporal task queue with a <see cref="string"/> Name
    /// and <see cref="TaskQueueKind"/> kind.
    /// </summary>
    public class TaskQueue
    {
        /// <summary>
        /// Identifies the name of the task queue.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Identifies the kind of task queue (normal/sticky).
        /// </summary>
        [JsonConverter(typeof(IntegerEnumConverter<TaskQueueKind>))]
        public TaskQueueKind Kind { get; set; }
    }
}
