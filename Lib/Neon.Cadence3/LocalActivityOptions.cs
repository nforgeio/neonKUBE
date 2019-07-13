//-----------------------------------------------------------------------------
// FILE:	    LocalActivityOptions.cs
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

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Specifies options used when running a local workflow activity.
    /// </summary>
    public class LocalActivityOptions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public LocalActivityOptions()
        {
        }

        /// <summary>
        /// Specifies the maximum time the activity can run.
        /// </summary>
        public TimeSpan ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// The activity retry options.
        /// </summary>
        public RetryOptions RetryOptions { get; set; } = null;

        /// <summary>
        /// Converts this instance into the corresponding internal object.
        /// </summary>
        /// <returns>The equivalent <see cref="InternalLocalActivityOptions"/>.</returns>
        internal InternalLocalActivityOptions ToInternal()
        {
            return new InternalLocalActivityOptions()
            {
                ScheduleToCloseTimeoutSeconds = CadenceHelper.ToCadence(this.ScheduleToCloseTimeout),
                RetryPolicy                   = RetryOptions?.ToInternal()
            };
        }
    }
}
