//-----------------------------------------------------------------------------
// FILE:	    ActivityMethodAttribute.cs
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
using System.Threading;

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Used to customize activity interface method options.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class ActivityMethodAttribute : Attribute
    {
        private string      name;
        private string      taskList;
        private string      @namespace;
        private int         heartbeatTimeoutSeconds;
        private int         scheduleToCloseTimeoutSeconds;
        private int         scheduleToStartTimeoutSeconds;
        private int         startToCloseTimeoutSeconds;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ActivityMethodAttribute()
        {
        }

        /// <summary>
        /// Specifies the name to be used to identify a specific activity method.  This is optional
        /// for activity interfaces that have only one method but is required for interfaces with
        /// multiple entry points.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When specified, this name will be combined with the activity type name when registering
        /// and executing an activity via the method.  This will look like:
        /// </para>
        /// <code>
        /// ACTIVITY_TYPENAME::METHODNAME
        /// </code>
        /// <para>
        /// where <b>ACTIVITY_TYPENAME</b> is either the activity interface's fully qualified 
        /// name or the name specified by <see cref="ActivityAttribute.Name"/> and 
        /// <b>METHOD_NAME</b> is from <see cref="ActivityMethodAttribute.Name"/>.  This
        /// is the same convention implemented by the Java client.
        /// </para>
        /// <para>
        /// Sometimes it's useful to be able to specify a workflow type name that doesn't
        /// follow the convention above, for example to interoperate with workflows written
        /// in another language..  You can do this by setting <see cref="Name"/> to the
        /// required workflow type name and then setting <see cref="IsFullName"/><c>=true</c>.
        /// </para>
        /// </remarks>
        public string Name
        {
            get => name;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    name = null;
                }
                else
                {
                    TemporalHelper.ValidateActivityTypeName(value);

                    name = value;
                }
            }
        }

        /// <summary>
        /// <para>
        /// Optionally indicates that <see cref="Name"/> holds the fully qualified type name for
        /// the workflow and that the .NET client will not add a prefix to <see cref="Name"/>
        /// when registering the workflow.
        /// </para>
        /// <para>
        /// This is useful when interoperating with workflows written in another language by
        /// providing a way to specify a specific workflow type name. 
        /// </para>
        /// <note>
        /// <see cref="Name"/> cannot be <c>null</c> or empty when this is <c>true</c>.
        /// </note>
        /// </summary>
        public bool IsFullName { get; set; } = false;

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum time can wait between recording
        /// a heartbeat before Temporal will consider the activity to have 
        /// timed out.
        /// </para>
        /// </summary>
        public int HeartbeatTimeoutSeconds
        {
            get => heartbeatTimeoutSeconds;
            set => heartbeatTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum total time allowed for the activity to
        /// complete from the time it is scheduled.  This includes the time the 
        /// activity is waiting to start executing on the worker, the time it takes
        /// for the activity to execute on the worker, as well as any time scheduling
        /// and performing retries.
        /// </para>
        /// </note>
        /// </summary>
        public int ScheduleToCloseTimeoutSeconds
        {
            get => scheduleToCloseTimeoutSeconds;
            set => scheduleToCloseTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum time the activity may remain 
        /// in the task list before being assigned to a worker.
        /// </para>
        /// </summary>
        public int ScheduleToStartTimeoutSeconds
        {
            get => scheduleToStartTimeoutSeconds;
            set => scheduleToStartTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the maximum execution time for
        /// an individual workflow task once it has been assigned
        /// to a worker.
        /// </para>
        /// </summary>
        public int StartToCloseTimeoutSeconds
        {
            get => startToCloseTimeoutSeconds;
            set => startToCloseTimeoutSeconds = Math.Max(value, 0);
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the target task list.
        /// </para>
        /// </summary>
        public string TaskList
        {
            get => taskList;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    taskList = null;
                }
                else
                {
                    taskList = value;
                }
            }
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the target namespace.
        /// </para>
        /// </summary>
        public string Namespace
        {
            get => @namespace;

            set
            {
                if (string.IsNullOrEmpty(value))
                {
                    @namespace = null;
                }
                else
                {
                    @namespace = value;
                }
            }
        }
    }
}
