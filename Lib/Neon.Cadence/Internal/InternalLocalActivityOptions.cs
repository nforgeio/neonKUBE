//-----------------------------------------------------------------------------
// FILE:	    InternalLocalActivityOptions.cs
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

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Specifies local activity execution options.
    /// </summary>
    internal class InternalLocalActivityOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public InternalLocalActivityOptions()
        {
        }

        /// <summary>
        /// Specifies the maximum time the activity can run.
        /// </summary>
        [JsonProperty(PropertyName = "ScheduleToCloseTimeoutSeconds", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long ScheduleToCloseTimeoutSeconds { get; set; }

        /// <summary>
        /// The activity retry policy.
        /// </summary>
        [JsonProperty(PropertyName = "RetryPolicy", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public InternalRetryPolicy RetryPolicy { get; set; } = null;
    }
}
