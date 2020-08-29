//-----------------------------------------------------------------------------
// FILE:	    WorkflowBase.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Temporal.Internal;
using Neon.Time;

namespace Neon.Temporal
{
    /// <summary>
    /// Base class that must be inherited for all workflow implementations.
    /// </summary>
    public class WorkflowBase
    {
        //---------------------------------------------------------------------
        // Static members

        private static INeonLogger  log = LogManager.Default.GetLogger<WorkflowBase>();

        /// <summary>
        /// Holds ambient task state indicating whether the current task executing
        /// in the context of a workflow entry point, signal, or query.
        /// </summary>
        internal static AsyncLocal<WorkflowCallContext> CallContext { get; private set; } = new AsyncLocal<WorkflowCallContext>();

        /// <summary>
        /// Ensures that the current <see cref="Task"/> is running within the context of a workflow 
        /// entry point, signal, or query method and also that the context matches one of the parameters
        /// indicating which contexts are allowed.  This is used ensure that only workflow operations
        /// that are valid for a context are allowed.
        /// </summary>
        /// <param name="allowWorkflow">Optionally indicates that calls from workflow entry point contexts are allowed.</param>
        /// <param name="allowQuery">Optionally indicates that calls from workflow query contexts are allowed.</param>
        /// <param name="allowSignal">Optionally indicates that calls from workflow signal contexts are allowed.</param>
        /// <param name="allowActivity">Optionally indicates that calls from activity contexts are allowed.</param>
        /// <exception cref="NotSupportedException">Thrown when the operation is not supported in the current context.</exception>
        internal static void CheckCallContext(
            bool allowWorkflow = false, 
            bool allowQuery    = false, 
            bool allowSignal   = false, 
            bool allowActivity = false)
        {
            switch (CallContext.Value)
            {
                case WorkflowCallContext.None:

                    throw new NotSupportedException("This operation cannot be performed outside of a workflow.");

                case WorkflowCallContext.Entrypoint:

                    if (!allowWorkflow)
                    {
                        throw new NotSupportedException("This operation cannot be performed within a workflow entry point method.");
                    }
                    break;

                case WorkflowCallContext.Query:

                    if (!allowQuery)
                    {
                        throw new NotSupportedException("This operation cannot be performed within a workflow query method.");
                    }
                    break;

                case WorkflowCallContext.Signal:

                    if (!allowSignal)
                    {
                        throw new NotSupportedException("This operation cannot be performed within a workflow signal method.");
                    }
                    break;

                case WorkflowCallContext.Activity:

                    if (!allowActivity)
                    {
                        throw new NotSupportedException("This operation cannot be performed within an activity method.");
                    }
                    break;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly object                         syncLock                  = new object();
        private long                                    nextLocalActivityActionId = 0;
        private Dictionary<long, LocalActivityAction>   idToLocalActivityAction   = new Dictionary<long, LocalActivityAction>();
        private Dictionary<string, SyncSignalStatus>    signalIdToStatus          = new Dictionary<string, SyncSignalStatus>();

        /// <summary>
        /// This field holds the stack trace for the most recent decision related 
        /// <see cref="Workflow"/> method calls.  This will be returned for internal
        /// workflow <b>"__stack_trace"</b> queries.
        /// </summary>
        internal StackTrace StackTrace { get; set; } = null;

        /// <summary>
        /// Indicates that the workflow implements one or more synchronous signals.
        /// </summary>
        internal bool HasSynchronousSignals { get; set; } = false;

        /// <summary>
        /// Returns a <see cref="Workflow"/> instance with utilty methods you'll use
        /// for implementing your workflows.
        /// </summary>
        public Workflow Workflow { get; set; }

        /// <summary>
        /// Waits for any pending workflow operations (like outstanding synchronous signals) to 
        /// complete.  This is called before returning from a workflow method.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal async Task WaitForPendingWorkflowOperationsAsync()
        {
            // Right now, the only pending operations can be completed outstanding 
            // synchronous signals that haven't returned their results to the
            // calling client via polled queries.

            if (!HasSynchronousSignals)
            {
                // The workflow doesn't implement any synchronous signals, so we can
                // return immediately.

                return;
            }

            // Wait for a period of time for any signals to be acknowledged.  We're simply going
            // to loop until all of the signals have been acknowledged, sleeping for 1 second
            // between checks.
            //
            // I originally tried using [MutableSideEffectAsync()] for the polling and using
            // [Task.Delay()] for the poll delay, but that didn't work because it
            // appears that Temporal doesn't process queries when MutableSideEffectAsync() 
            // is running (perhaps this doesn't count as a real decision task).
            //
            // The down side of doing it this way is that each of the sleeps will be
            // recorded to the workflow history.  We'll have to live with that.  I 
            // expect that we'll only have to poll for a second or two in most 
            // circumstances anyway.

            var sysDeadline = SysTime.Now + Workflow.Client.Settings.MaxWorkflowKeepAlive;
            var signalCount = 0;

            while (SysTime.Now < sysDeadline)
            {
                // Break when all signals have been acknowledged.

                lock (signalIdToStatus)
                {
                    signalCount = signalIdToStatus.Count;

                    if (signalCount == 0)
                    {
                        break; // No synchronous signals were called.
                    }
                    else if (signalIdToStatus.Values.All(status => status.Acknowledged))
                    {
                        break; // All signals have been acknowledged
                    }
                }

                await Workflow.SleepAsync(TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Registers a local activity type and method with the workflow and returns 
        /// its local activity action ID.
        /// </summary>
        /// <param name="activityType">The activity type.</param>
        /// <param name="activityConstructor">The activity constructor.</param>
        /// <param name="activityMethod">The target local activity method.</param>
        /// <returns>The new local activity action ID.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Temporal client is disposed.</exception>
        internal long RegisterActivityAction(Type activityType, ConstructorInfo activityConstructor, MethodInfo activityMethod)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));
            Covenant.Requires<ArgumentNullException>(activityConstructor != null, nameof(activityConstructor));
            Covenant.Requires<ArgumentException>(activityType.BaseType == typeof(ActivityBase), nameof(activityType));
            Covenant.Requires<ArgumentNullException>(activityMethod != null, nameof(activityMethod));

            var activityActionId = Interlocked.Increment(ref nextLocalActivityActionId);

            lock (syncLock)
            {
                idToLocalActivityAction.Add(activityActionId, new LocalActivityAction(activityType, activityConstructor, activityMethod));
            }

            return activityActionId;
        }

        /// <summary>
        /// Retrieves the <see cref="LocalActivityAction"/> for the specified activity type ID.
        /// </summary>
        /// <param name="activityTypeId">The activity type ID.</param>
        /// <returns>The corresponding <see cref="LocalActivityAction"/> or <c>null</c>.</returns>
        internal LocalActivityAction GetActivityAction(long activityTypeId)
        {
            lock (syncLock)
            {
                if (idToLocalActivityAction.TryGetValue(activityTypeId, out var activityAction))
                {
                    return activityAction;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Returns the <see cref="SyncSignalStatus"/> for the specified workflow and signal.
        /// </summary>
        /// <param name="signalId">The target signal ID.</param>
        /// <returns>The <see cref="SyncSignalStatus"/> for the signal.</returns>
        internal SyncSignalStatus GetSignalStatus(string signalId)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalId), nameof(signalId));

            // Lookup the status for the signal.

            lock (syncLock)
            {
                if (!signalIdToStatus.TryGetValue(signalId, out var signalStatus))
                {
                    signalStatus = new SyncSignalStatus() { Completed = false };

                    signalIdToStatus.Add(signalId, signalStatus);
                }

                return signalStatus;
            }
        }

        /// <summary>
        /// Creates a new signal status record for the specified signal ID if no signal
        /// status already exists.
        /// </summary>
        /// <param name="signalId">The signal ID.</param>
        /// <param name="args">The signal arguments.</param>
        /// <param name="newSignal">Returns as <c>true</c> if the signal status was created by the method.</param>
        /// <returns>The new <see cref="SyncSignalStatus"/> instance or the preexisting one.</returns>
        internal SyncSignalStatus SetSignalStatus(string signalId, Dictionary<string, object> args, out bool newSignal)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalId), nameof(args));
            Covenant.Requires<ArgumentNullException>(args != null, nameof(args));

            newSignal = false;

            lock (syncLock)
            {
                if (!signalIdToStatus.TryGetValue(signalId, out var signalStatus))
                {
                    newSignal    = true;
                    signalStatus = new SyncSignalStatus();

                    signalIdToStatus.Add(signalId, signalStatus);
                }

                signalStatus.Args = args;

                return signalStatus;
            }
        }
    }
}
