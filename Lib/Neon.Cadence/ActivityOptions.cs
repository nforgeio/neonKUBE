//-----------------------------------------------------------------------------
// FILE:	    ActivityOptions.cs
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
using System.Reflection;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
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
        /// <param name="client">The associated Cadence client.</param>
        /// <param name="options">The input options or <c>null</c>.</param>
        /// <param name="activityInterface">Optionally specifies the activity interface definition.</param>
        /// <param name="method">Optionally specifies the target workflow method.</param>
        /// <returns>The normalized options.</returns>
        /// <exception cref="ArgumentNullException">Thrown if a valid task list is not specified.</exception>
        internal static ActivityOptions Normalize(CadenceClient client, ActivityOptions options, Type activityInterface = null, MethodInfo method = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            ActivityInterfaceAttribute  interfaceAttribute = null;
            ActivityMethodAttribute     methodAttribute    = null;

            if (options == null)
            {
                options = new ActivityOptions();
            }
            else
            {
                options = options.Clone();
            }

            if (activityInterface != null)
            {
                CadenceHelper.ValidateActivityInterface(activityInterface);

                interfaceAttribute = activityInterface.GetCustomAttribute<ActivityInterfaceAttribute>();
            }

            if (method != null)
            {
                methodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();
            }

            if (string.IsNullOrEmpty(options.Domain))
            {
                if (!string.IsNullOrEmpty(methodAttribute?.Domain))
                {
                    options.Domain = methodAttribute.Domain;
                }

                if (string.IsNullOrEmpty(options.Domain) && !string.IsNullOrEmpty(interfaceAttribute?.Domain))
                {
                    options.Domain = interfaceAttribute.Domain;
                }
            }

            if (string.IsNullOrEmpty(options.TaskList))
            {
                if (!string.IsNullOrEmpty(methodAttribute?.TaskList))
                {
                    options.TaskList = methodAttribute.TaskList;
                }

                if (string.IsNullOrEmpty(options.TaskList) && !string.IsNullOrEmpty(interfaceAttribute?.TaskList))
                {
                    options.TaskList = interfaceAttribute.TaskList;
                }
            }

            if (options.ScheduleToCloseTimeout <= TimeSpan.Zero)
            {
                if (methodAttribute != null && methodAttribute.ScheduleToCloseTimeoutSeconds > 0)
                {
                    options.ScheduleToCloseTimeout = TimeSpan.FromSeconds(methodAttribute.ScheduleToCloseTimeoutSeconds);
                }

                if (options.ScheduleToCloseTimeout <= TimeSpan.Zero)
                {
                    options.ScheduleToCloseTimeout = client.Settings.ActivityScheduleToCloseTimeout;
                }
            }

            if (options.ScheduleToStartTimeout <= TimeSpan.Zero)
            {
                if (methodAttribute != null && methodAttribute.ScheduleToStartTimeoutSeconds > 0)
                {
                    options.ScheduleToStartTimeout = TimeSpan.FromSeconds(methodAttribute.ScheduleToStartTimeoutSeconds);
                }

                if (options.ScheduleToStartTimeout <= TimeSpan.Zero)
                {
                    options.ScheduleToStartTimeout = client.Settings.ActivityScheduleToStartTimeout;
                }
            }

            if (options.StartToCloseTimeout <= TimeSpan.Zero)
            {
                if (methodAttribute != null && methodAttribute.StartToCloseTimeoutSeconds > 0)
                {
                    options.StartToCloseTimeout = TimeSpan.FromSeconds(methodAttribute.StartToCloseTimeoutSeconds);
                }

                if (options.StartToCloseTimeout <= TimeSpan.Zero)
                {
                    options.StartToCloseTimeout = client.Settings.ActivityStartToCloseTimeout;
                }
            }

            return options;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Optionally specifies the target Cadence task list.  This defaults to the task list
        /// specified by <see cref="ActivityMethodAttribute.TaskList"/>,
        /// <see cref="ActivityInterfaceAttribute.TaskList"/>, or the parent workflow's
        /// task list, in that order of precedence.
        /// </summary>
        public string TaskList { get; set; } = null;

        /// <summary>
        /// Optionally specifies the target Cadence domain.  This defaults to the domain
        /// specified by <see cref="ActivityMethodAttribute.Domain"/>, 
        /// <see cref="ActivityInterfaceAttribute.Domain"/>, or 
        /// to the parent workflow's domain, in that order of precedence.
        /// </summary>
        public string Domain { get; set; } = null;

        /// <summary>
        /// Optionally specifies the end-to-end timeout for the activity.  The 
        /// default <see cref="TimeSpan.Zero"/> value uses the sum of 
        /// <see cref="ScheduleToStartTimeout"/> and <see cref="StartToCloseTimeout"/>.
        /// </summary>
        public TimeSpan ScheduleToCloseTimeout { get; set; }

        /// <summary>
        /// Specifies the maximum time the activity be queued, waiting to be scheduled
        /// on a worker.  This defaults to <see cref="CadenceSettings.ActivityScheduleToStartTimeoutSeconds"/>.
        /// </summary>
        public TimeSpan ScheduleToStartTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Specifies the maximum time the activity may take to run.  This defaults to
        /// <see cref="CadenceSettings.ActivityStartToCloseTimeoutSeconds"/>.
        /// </summary>
        public TimeSpan StartToCloseTimeout { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Optionally specifies the maximum time the activity has to send a heartbeat
        /// back to Cadence.  This defaults to <see cref="TimeSpan.Zero"/> which indicates
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
        /// mark the task as failed and create a new task and put back in the queue waiting worker to pick again. Cadence
        /// server also make sure the <see cref="ScheduleToStartTimeout"/> will not be larger than the workflow's timeout.
        /// Same apply to <see cref="StartToCloseTimeout"/>.
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
                HeartbeatTimeout       = CadenceHelper.ToCadence(this.HeartbeatTimeout),
                RetryPolicy            = RetryOptions?.ToInternal(),
                ScheduleToCloseTimeout = CadenceHelper.ToCadence(this.ScheduleToCloseTimeout),
                ScheduleToStartTimeout = CadenceHelper.ToCadence(this.ScheduleToStartTimeout),
                StartToCloseTimeout    = CadenceHelper.ToCadence(this.StartToCloseTimeout),
                TaskList               = this.TaskList,
                WaitForCancellation    = WaitForCancellation,
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
                Domain                 = this.Domain,
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
