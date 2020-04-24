//-----------------------------------------------------------------------------
// FILE:	    ActivityOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Reflection;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Specifies the options used for executing an activity.
    /// </summary>
    public class ActivityOptions
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Normalizes the options passed by creating or cloning a new instance as
        /// required and filling unset properties using default client settings.
        /// </summary>
        /// <param name="client">The associated Temporal client.</param>
        /// <param name="options">The input options or <c>null</c>.</param>
        /// <param name="activityInterface">Optionally specifies the activity interface definition.</param>
        /// <returns>The normalized options.</returns>
        /// <exception cref="ArgumentNullException">Thrown if a valid task list is not specified.</exception>
        internal static ActivityOptions Normalize(TemporalClient client, ActivityOptions options, Type activityInterface = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            if (options == null)
            {
                options = new ActivityOptions();
            }
            else
            {
                options = options.Clone();
            }

            if (string.IsNullOrEmpty(options.Namespace))
            {
                options.Namespace = client.Settings.DefaulNamespace;
            }

            if (options.ScheduleToCloseTimeout <= TimeSpan.Zero)
            {
                options.ScheduleToCloseTimeout = client.Settings.ActivityScheduleToCloseTimeout;
            }

            if (options.ScheduleToStartTimeout <= TimeSpan.Zero)
            {
                options.ScheduleToStartTimeout = client.Settings.ActivityScheduleToStartTimeout;
            }

            if (options.StartToCloseTimeout <= TimeSpan.Zero)
            {
                options.StartToCloseTimeout = client.Settings.ActivityStartToCloseTimeout;
            }

            if (string.IsNullOrEmpty(options.TaskList))
            {
                if (activityInterface != null)
                {
                    TemporalHelper.ValidateActivityInterface(activityInterface);

                    var interfaceAttribute = activityInterface.GetCustomAttribute<ActivityInterfaceAttribute>();

                    if (interfaceAttribute != null && !string.IsNullOrEmpty(interfaceAttribute.TaskList))
                    {
                        options.TaskList = interfaceAttribute.TaskList;
                    }
                }
            }

            if (string.IsNullOrEmpty(options.TaskList))
            {
                throw new ArgumentNullException(nameof(options), "You must specify a valid task list explicitly or via an [ActivityInterface(TaskList = \"my-tasklist\")] attribute on the target activity interface.");
            }

            return options;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Specifies the task list where the activity will be scheduled.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A task list must be specified when executing an activity.  For activities
        /// started via a typed stub, this will default to the type list specified
        /// by the <c>[ActivityInterface(TaskList = "my-tasklist"]</c> tagging the
        /// interface (if any).
        /// </para>
        /// <para>
        /// For activity stubs created from an interface without a specified task list
        /// or activities created via untyped or external stubs, this will need to
        /// be explicitly set to a non-empty value.
        /// </para>
        /// </remarks>
        public string TaskList { get; set; } = null;

        /// <summary>
        /// Optionally specifies the target namespace.  This defaults to the parent 
        /// workflow's namespace.
        /// </summary>
        public string Namespace { get; set; } = null;

        /// <summary>
        /// Optionally specifies the end-to-end timeout for the activity.  The 
        /// default <see cref="TimeSpan.Zero"/> value uses the sum of 
        /// <see cref="ScheduleToStartTimeout"/> and <see cref="StartToCloseTimeout"/>.
        /// </summary>
        public TimeSpan ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Specifies the maximum time the activity be queued, waiting to be scheduled
        /// on a worker.  This defaults to <see cref="TemporalSettings.ActivityScheduleToStartTimeoutSeconds"/>.
        /// </summary>
        public TimeSpan ScheduleToStartTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Specifies the maximum time the activity may take to run.  This defaults to
        /// <see cref="TemporalSettings.ActivityStartToCloseTimeoutSeconds"/>.
        /// </summary>
        public TimeSpan StartToCloseTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Optionally specifies the maximum time the activity has to send a heartbeat
        /// back to Temporal.  This defaults to <see cref="TimeSpan.Zero"/> which indicates
        /// that no heartbeating is required.
        /// </summary>
        public TimeSpan HeartbeatTimeout { get; set; }

        /// <summary>
        /// Optionally specifies that the cancelled activities won't be considered to be
        /// finished until they actually complete.  This defaults to <c>false</c>.
        /// </summary>
        public bool WaitForCancellation { get; set; }

        /// <summary>
        /// Optionally specifies the activity retry policy.  The default value is <c>null</c> which indicates
        /// that there will be no retry attempts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When <see cref="RetryOptions.ExpirationInterval"/> is specified and it is larger than the activity's 
        /// <see cref="ScheduleToStartTimeout"/>, then the <see cref="RetryOptions.ExpirationInterval"/> will override 
        /// activity's <see cref="ScheduleToStartTimeout"/>. This is to avoid retrying on <see cref="ScheduleToStartTimeout"/>
        /// error which only happen when worker is not picking up the task within the timeout.
        /// </para>
        /// <para>
        /// Retrying <see cref="ScheduleToStartTimeout"/> does not make sense as it just
        /// mark the task as failed and create a new task and put back in the queue waiting worker to pick again. Temporal
        /// server also make sure the <see cref="ScheduleToStartTimeout"/> will not be larger than the workflow's timeout.
        /// Same apply to <see cref="ScheduleToCloseTimeout"/>.
        /// </para>
        /// </remarks>
        public RetryOptions RetryOptions { get; set; }

        /// <summary>
        /// Converts the instance to its internal representation.
        /// </summary>
        internal InternalActivityOptions ToInternal()
        {
            return new InternalActivityOptions()
            {
                TaskList               = this.TaskList,
                ScheduleToCloseTimeout = TemporalHelper.ToTemporal(this.ScheduleToCloseTimeout),
                ScheduleToStartTimeout = TemporalHelper.ToTemporal(this.ScheduleToStartTimeout),
                StartToCloseTimeout    = TemporalHelper.ToTemporal(this.StartToCloseTimeout),
                HeartbeatTimeout       = TemporalHelper.ToTemporal(this.HeartbeatTimeout),
                WaitForCancellation    = WaitForCancellation,
                RetryPolicy            = RetryOptions?.ToInternal()
            };
        }

        /// <summary>
        /// Returns a shallow clone of the current instance.
        /// </summary>
        /// <returns>The cloned <see cref="ActivityOptions"/>.</returns>
        public ActivityOptions Clone()
        {
            return new ActivityOptions()
            {
                Namespace              = this.Namespace,
                HeartbeatTimeout       = this.HeartbeatTimeout,
                RetryOptions           = this.RetryOptions,
                ScheduleToCloseTimeout = this.ScheduleToCloseTimeout,
                ScheduleToStartTimeout = this.ScheduleToStartTimeout,
                StartToCloseTimeout    = this.StartToCloseTimeout,
                TaskList               = this.TaskList,
                WaitForCancellation    = this.WaitForCancellation
            };
        }
    }
}
