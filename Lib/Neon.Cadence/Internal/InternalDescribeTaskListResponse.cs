//-----------------------------------------------------------------------------
// FILE:	    InternalDescribeTaskListResponse.cs
// CONTRIBUTOR: Jeff Lill
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
using System.ComponentModel;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Cadence task list details.
    /// </summary>
    internal class InternalDescribeTaskListResponse
    {
        /// <summary>
        /// Lists the pollers (AKA workers) that have communicated with the Cadence cluster over
        /// the past few minutes.
        /// </summary>
        [JsonProperty(PropertyName = "pollers", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalPollerInfo[] Pollers { get; set; }

        /// <summary>
        /// Converts the instance into a public <see cref="TaskListDescription"/>.
        /// </summary>
        /// <returns>The converted <see cref="TaskListDescription"/>.</returns>
        public TaskListDescription ToPublic()
        {
            var description = new TaskListDescription();

            if (Pollers != null)
            {
                foreach (var poller in Pollers)
                {
                    description.Pollers.Add(poller.ToPublic());
                }
            }

            return description;
        }
    }
}
