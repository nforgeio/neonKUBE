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
using System.Diagnostics.Contracts;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Specifies options used when running a local workflow activity.
    /// </summary>
    public class LocalActivityOptions
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Normalizes the options passed by creating or cloning a new instance as
        /// required and filling unset properties using default client settings.
        /// </summary>
        /// <param name="client">The associated Cadence client.</param>
        /// <param name="options">The input options or <c>null</c>.</param>
        /// <returns>The normalized options.</returns>
        internal static LocalActivityOptions Normalize(CadenceClient client, LocalActivityOptions options)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            if (options == null)
            {
                options = new LocalActivityOptions();
            }
            else
            {
                options = options.Clone();
            }

            if (options.ScheduleToCloseTimeout <= TimeSpan.Zero)
            {
                options.ScheduleToCloseTimeout = client.Settings.ActivityScheduleToCloseTimeout;
            }

            return options;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LocalActivityOptions()
        {
        }

        /// <summary>
        /// Optionally specifies the maximum time the activity can run.
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
                ScheduleToCloseTimeout = CadenceHelper.ToCadence(this.ScheduleToCloseTimeout),
                RetryPolicy            = RetryOptions?.ToInternal()
            };
        }

        /// <summary>
        /// Returns a shallow copy of the instance.
        /// </summary>
        /// <returns>The cloned <see cref="LocalActivityOptions"/>.</returns>
        public LocalActivityOptions Clone()
        {
            return new LocalActivityOptions()
            {
                ScheduleToCloseTimeout = this.ScheduleToCloseTimeout,
                RetryOptions           = this.RetryOptions
            };
        }
    }
}
