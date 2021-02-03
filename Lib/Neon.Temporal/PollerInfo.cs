//-----------------------------------------------------------------------------
// FILE:	    PollerInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;
using Newtonsoft.Json;

namespace Neon.Temporal
{
    /// <summary>
    /// Describes the status of a poller (AKA worker) listening to a task queue.
    /// </summary>
    public class PollerInfo
    {
        /// <summary>
        /// The last time the poller accessed Temporal.
        /// </summary>
        [JsonProperty(PropertyName = "last_access_time")]
        public DateTime? LastAccessTime { get; set; }

        /// <summary>
        /// Identifies the poller.
        /// </summary>
        [JsonProperty(PropertyName = "identity")]
        public string Identity { get; set; }

        /// <summary>
        /// Operations per second from the poller.
        /// </summary>
        [JsonProperty(PropertyName = "rate_per_second")]
        public double RatePerSecond { get; set; }
    }
}
