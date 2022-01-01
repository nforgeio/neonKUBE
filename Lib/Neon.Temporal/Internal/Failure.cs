//-----------------------------------------------------------------------------
// FILE:	    Failure.cs
// CONTRIBUTOR: John C. Burns
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

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Defines a workflow execution failure.
    /// </summary>
    public class Failure
    {
        /// <summary>
        /// The failure message.
        /// </summary>
        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        /// <summary>
        /// The source of failure.
        /// </summary>
        [JsonProperty(PropertyName = "source")]
        public string Source { get; set; }

        /// <summary>
        /// The failure stack trace.
        /// </summary>
        [JsonProperty(PropertyName = "stack_trace")]
        public string StackTrace { get; set; }

        /// <summary>
        /// The cause of failure.
        /// </summary>
        [JsonProperty(PropertyName = "cause")]
        public Failure Cause { get; set; }
    }
}
