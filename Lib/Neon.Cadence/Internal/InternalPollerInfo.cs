//-----------------------------------------------------------------------------
// FILE:	    InternalPollerInfo.cs
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
    /// Describes a workflow or activity poller.
    /// </summary>
    public class InternalPollerInfo
    {
        /// <summary>
        /// The last time the poller accessed Cadence (Unix Nano UTC).
        /// </summary>
        [JsonProperty(PropertyName = "lastAccessTime", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public long LastAccessTime { get; set; }

        /// <summary>
        /// Identifies the poller.
        /// </summary>
        [JsonProperty(PropertyName = "identity", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Identity { get; set; }

        /// <summary>
        /// Operations per second from the poller.
        /// </summary>
        [JsonProperty(PropertyName = "ratePerSecond", DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0.0)]
        public double RatePerSecond { get; set; }

        /// <summary>
        /// Converts the instance to a <see cref="PollerInfo"/>.
        /// </summary>
        /// <returns>The converted <see cref="PollerInfo"/>.</returns>
        public PollerInfo ToPublic()
        {
            return new PollerInfo()
            {
                LastAccessTime = CadenceHelper.UnixNanoToDateTimeUtc(this.LastAccessTime),
                Identity       = this.Identity,
                RatePerSecond  = this.RatePerSecond
            };
        }
    }
}
