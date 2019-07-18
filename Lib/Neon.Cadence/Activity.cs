//-----------------------------------------------------------------------------
// FILE:	    Activity.cs
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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all application Cadence activity implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Cadence activities are intended to perform most of the work related to
    /// actually implementing a workflow.  This includes interacting with the
    /// outside world by obtaining and updating external state as well as performing
    /// long running computations and operations.  Activities are where you'll 
    /// interact with databases and external services.
    /// </para>
    /// <para>
    /// Workflows are generally intended to invoke one or more activities
    /// and then use their results to decide which other activities need to
    /// run and then combine these activity results into the workflow result,
    /// as required.  Workflows can be considered to handle the decisions
    /// and activities are responsible for doing things.
    /// </para>
    /// <para>
    /// Activities are very easy to implement, simply derive your custom
    /// activity type from <see cref="Activity"/> and then implement a
    /// <see cref="RunAsync(byte[])"/> method with your custom code.
    /// This accepts a byte array with your custom activity arguments 
    /// as a parameter and returns a byte array as your activity result.
    /// Both of these values may be <c>null</c>.  Activities report failures
    /// by throwing an exception from their <see cref="RunAsync(byte[])"/>
    /// methods.
    /// </para>
    /// <para>
    /// Unlike the <see cref="Workflow.RunAsync(byte[])"/> method, the 
    /// <see cref="Activity.RunAsync(byte[])"/> method implementations has
    /// few limitations.  This method can use threads, can reference global
    /// state like time, environment variables and perform non-itempotent 
    /// operations like generating random numbers, UUIDs, etc.
    /// </para>
    /// <note>
    /// Although activities are not required to be idempotent from a Cadence
    /// perspective, this may be required for some workflows.  You'll need
    /// to carefully code your activities for these situations.
    /// </note>
    /// <para>
    /// The only real requirement for most activties is that your <see cref="RunAsync(byte[])"/>
    /// needs to call <see cref="SendHeartbeatAsync(byte[])"/> periodically at
    /// an interval no greater than <see cref="ActivityTask"/>.<see cref="ActivityTask.HeartbeatTimeout"/>.
    /// This proves to Cadence that the activity is still healthy and running and
    /// also provides an opportunity for long running and computationally expecsive
    /// activities to checkpoint their current state so they won't need to start
    /// completely over when the activity is rescheduled.
    /// </para>
    /// <para>
    /// Cadence supports two kinds of activities: <b>normal</b> and <b>local</b>.
    /// <b>normal</b> activities are registered via <see cref="CadenceClient.RegisterActivityAsync{TActivity}(string)"/>
    /// and are scheduled by the Cadence cluster to be executed on workers.  Workflows
    /// invoke theses using <see cref="Workflow.CallActivityAsync(string, byte[], ActivityOptions, CancellationToken)"/>.
    /// </para>
    /// <para>
    /// <b>local</b> activities simply run on the local worker without needing to
    /// be registered or scheduled by the Cadence cluster.  These are very low overhead
    /// and intended for for simple short running activities (a few seconds).
    /// Workflows invoke local activities using <see cref="Workflow.CallLocalActivityAsync{TActivity}(byte[], LocalActivityOptions, CancellationToken)"/>.
    /// <b>Local activities do not support heartbeats.</b>
    /// </para>
    /// <note>
    /// You can distinguish between normal and local activities via <see cref="IsLocal"/>.
    /// </note>
    /// <para>
    /// Non-local activities may be cancelled explicitly and all activities will be
    /// cancelled if the parent workflow or the local worker is stopped.  Well behaved
    /// activities will monitor their <see cref="CancellationToken"/> for cancellation
    /// by registering a handler, periodically calling <see cref="CancellationToken.IsCancellationRequested"/>
    /// or <see cref="CancellationToken.ThrowIfCancellationRequested"/>.  Non-local activities 
    /// that implement checkpoints can use this as an opportunity to call <see cref="SendHeartbeatAsync(byte[])"/>
    /// and persist checkpoint state and all activities should then throw or rethrow a 
    /// <see cref="TaskCanceledException"/> from their <see cref="RunAsync(byte[])"/>
    /// method.
    /// </para>
    /// <para><b>External Activity Completion</b></para>
    /// <para>
    /// Normally, activities are self-contained and will finish whatever they're doing and then
    /// simply return.  It's often useful though to be able to have an activity kickoff operations
    /// on an external system, exit the activity indicating that it's still pending, and then
    /// have the external system manage the activity heartbeats and report the activity completion.
    /// </para>
    /// <para>
    /// To take advantage of this, you'll need to obtain the opaque activity identifier from
    /// <see cref="Activity.ActivityTask"/> via its <see cref="ActivityTask.TaskToken"/> property.
    /// This is a byte array including enough information for Cadence to identify the specific
    /// activity.  Your activity should start the external action, passing the task token and
    /// then call <see cref="Activity.CompleteExternallyAsync()"/> which will thrown a
    /// <see cref="CadenceActivityExternalCompletionException"/> that will exit the activity 
    /// and then be handled internally by informing Cadence that the activity will continue
    /// running.
    /// </para>
    /// <note>
    /// You should not depend on the structure or contents of the task token since this
    /// may change for future Cadence releases and you must allow the <see cref="CadenceActivityExternalCompletionException"/>
    /// to be caught by the calling <see cref="CadenceClient"/> so <see cref="Activity.CompleteExternallyAsync()"/>
    /// will work properly.
    /// </note>
    /// </remarks>
    public abstract class Activity : IActivity, INeonLogger
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Used to map a Cadence client ID and workflow context ID into a
        /// key that can be used to dereference <see cref="idToActivity"/>.
        /// </summary>
        private struct ActivityKey
        {
            private long clientId;
            private long contextId;

            public ActivityKey(CadenceClient client, long contextId)
            {
                this.clientId = client.ClientId;
                this.contextId = contextId;
            }

            public override int GetHashCode()
            {
                return clientId.GetHashCode() ^ contextId.GetHashCode();
            }

            public override bool Equals(object obj)
            {
                if (obj == null || !(obj is ActivityKey))
                {
                    return false;
                }

                var other = (ActivityKey)obj;

                return this.clientId == other.clientId &&
                       this.contextId == other.contextId;
            }

            public override string ToString()
            {
                return $"clientID={clientId}, contextId={contextId}";
            }
        }

        private struct ConstructInfo
        {
            /// <summary>
            /// The activity type.
            /// </summary>
            public Type Type { get; set; }
            
            /// <summary>
            /// The activity constructor.
            /// </summary>
            public ConstructorInfo Constructor { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        private static object                               syncLock            = new object();
        private static INeonLogger                          log                 = LogManager.Default.GetLogger<Activity>();
        private static Type[]                               noTypeArgs          = new Type[0];
        private static object[]                             noArgs              = new object[0];
        private static Dictionary<ActivityKey, Activity>    idToActivity        = new Dictionary<ActivityKey, Activity>();
        private static Dictionary<Type, ConstructorInfo>    typeToConstructor   = new Dictionary<Type, ConstructorInfo>();

        // This dictionary is used to map activity type names to the target activity
        // type.  Note that these mappings are scoped to specific cadence client
        // instances by prefixing the type name with:
        //
        //      CLIENT-ID::
        //
        // where CLIENT-ID is the locally unique ID of the client.  This is important,
        // because we'll need to remove the entries for clients when they're disposed.

        private static Dictionary<string, ConstructInfo>        nameToConstructInfo = new Dictionary<string, ConstructInfo>();

        /// <summary>
        /// Prepends the Cadence client ID to the workflow type name to generate the
        /// key used to dereference the <see cref="nameToConstructInfo"/> dictionary.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="activityTypeName">The activity type name.</param>
        /// <returns>The prepended type name.</returns>
        private static string GetActivityTypeKey(CadenceClient client, string activityTypeName)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(activityTypeName != null);

            return $"{client.ClientId}::{activityTypeName}";
        }

        /// <summary>
        /// Registers an activity type.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="activityType">The activity type.</param>
        /// <param name="activityTypeName">The name used to identify the implementation.</param>
        /// <returns><c>true</c> if the activity was already registered.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a different activity class has already been registered for <paramref name="activityTypeName"/>.</exception>
        internal static bool Register(CadenceClient client, Type activityType, string activityTypeName)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(activityType != null);
            Covenant.Requires<ArgumentException>(activityType.IsSubclassOf(typeof(Activity)), $"Type [{activityType.FullName}] does not derive from [{nameof(Activity)}].");
            Covenant.Requires<ArgumentException>(activityType != typeof(Activity), $"The base [{nameof(Activity)}] class cannot be registered.");

            activityTypeName = GetActivityTypeKey(client, activityTypeName);

            var constructInfo = new ConstructInfo();

            constructInfo.Type        = activityType;
            constructInfo.Constructor = constructInfo.Type.GetConstructor(noTypeArgs);

            if (constructInfo.Constructor == null)
            {
                throw new ArgumentException($"Activity type [{constructInfo.Type.FullName}] does not have a default constructor.");
            }

            lock (syncLock)
            {
                if (nameToConstructInfo.TryGetValue(activityTypeName, out var existingEntry))
                {
                    if (!object.ReferenceEquals(existingEntry.Type, constructInfo.Type))
                    {
                        throw new InvalidOperationException($"Conflicting activity type registration: Activity type [{activityType.FullName}] is already registered for workflow type name [{activityTypeName}].");
                    }

                    return true;
                }
                else
                {
                    nameToConstructInfo[activityTypeName] = constructInfo;

                    return false;
                }
            }
        }

        /// <summary>
        /// Removes all type activity type registrations for a Cadence client (when it's being disposed).
        /// </summary>
        /// <param name="client">The client being disposed.</param>
        internal static void UnregisterClient(CadenceClient client)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            var prefix = $"{client.ClientId}::";

            lock (syncLock)
            {
                foreach (var key in nameToConstructInfo.Keys.Where(key => key.StartsWith(prefix)).ToList())
                {
                    nameToConstructInfo.Remove(key);
                }
            }
        }

        /// <summary>
        /// Constructs an activity instance with the specified type.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="activityType">The activity type.</param>
        /// <param name="contextId">The activity context ID or <c>null</c> for local activities.</param>
        /// <returns>The constructed activity.</returns>
        internal static Activity Create( CadenceClient client, Type activityType,long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null);

            ConstructorInfo constructor;

            lock (syncLock)
            {
                if (!typeToConstructor.TryGetValue(activityType, out constructor))
                {
                    constructor = activityType.GetConstructor(noTypeArgs);

                    if (constructor == null)
                    {
                        throw new ArgumentException($"Activity type [{activityType.FullName}] does not have a default constructor.");
                    }

                    typeToConstructor.Add(activityType, constructor);
                }
            }

            var activity = (Activity)constructor.Invoke(noArgs);

            activity.Initialize(client, contextId);

            return activity;
        }

        /// <summary>
        /// Constructs an activity instance with the specified activity type name.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="activityTypeName">The activity type name.</param>
        /// <param name="contextId">The activity context ID or <c>null</c> for local activities.</param>
        /// <returns>The constructed activity.</returns>
        internal static Activity Create(CadenceClient client, string activityTypeName, long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(activityTypeName != null);
            Covenant.Requires<ArgumentNullException>(client != null);

            ConstructInfo constructInfo;

            lock (syncLock)
            {
                if (!nameToConstructInfo.TryGetValue(GetActivityTypeKey(client, activityTypeName), out constructInfo))
                {
                    throw new ArgumentException($"No activty type is registered for [{activityTypeName}].");
                }
            }

            var activity = (Activity)constructInfo.Constructor.Invoke(noArgs);

            activity.Initialize(client, contextId);

            return activity;
        }

        /// <summary>
        /// Called to handle a workflow related request message received from the cadence-proxy.
        /// </summary>
        /// <param name="client">The client that received the request.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal static async Task OnProxyRequestAsync(CadenceClient client, ProxyRequest request)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(request != null);

            ProxyReply reply;

            switch (request.Type)
            {
                case InternalMessageTypes.ActivityInvokeRequest:

                    reply = await OnActivityInvokeRequest(client, (ActivityInvokeRequest)request);
                    break;

                case InternalMessageTypes.ActivityStoppingRequest:

                    reply = await ActivityStoppingRequest(client, (ActivityStoppingRequest)request);
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await client.ProxyReplyAsync(request, reply);
        }

        /// <summary>
        /// Handles received <see cref="ActivityInvokeRequest"/> messages.
        /// </summary>
        /// <param name="client">The receiving Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        private static async Task<ActivityInvokeReply> OnActivityInvokeRequest(CadenceClient client, ActivityInvokeRequest request)
        {
            var activity = Create(client, request.Activity, request.ContextId);

            try
            {
                var result = await activity.OnRunAsync(client, request.Args);

                return new ActivityInvokeReply()
                {
                    Result = result
                };
            }
            catch (CadenceException e)
            {
                return new ActivityInvokeReply()
                {
                    Error = e.ToCadenceError()
                };
            }
            catch (TaskCanceledException e)
            {
                return new ActivityInvokeReply()
                {
                    Error = new CadenceCancelledException(e.Message).ToCadenceError()
                };
            }
            catch (CadenceActivityExternalCompletionException)
            {
                return new ActivityInvokeReply()
                {
                    Pending = true
                };
            }
            catch (Exception e)
            {
                return new ActivityInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        /// <summary>
        /// Handles received <see cref="ActivityStoppingRequest"/> messages.
        /// </summary>
        /// <param name="client">The receiving Cadence client.</param>
        /// <param name="request">The request message.</param>
        /// <returns>The reply message.</returns>
        private static async Task<ActivityStoppingReply> ActivityStoppingRequest(CadenceClient client, ActivityStoppingRequest request)
        {
            lock (syncLock)
            {
                if (idToActivity.TryGetValue(new ActivityKey(client, request.ContextId), out var activity))
                {
                    activity.CancellationTokenSource.Cancel();
                }
            }

            return await Task.FromResult(new ActivityStoppingReply());
        }

        //---------------------------------------------------------------------
        // Instance members

        private long?           contextId;      // Will be NULL for local activities.
        private ActivityTask    cachedInfo;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public Activity()
        {
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this activity.
        /// </summary>
        public CadenceClient Client { get; private set; }

        /// <summary>
        /// Returns <c>true</c> for a local activity execution.
        /// </summary>
        public bool IsLocal => !contextId.HasValue;

        /// <summary>
        /// The internal cancellation token source.
        /// </summary>
        internal CancellationTokenSource CancellationTokenSource { get; private set; }

        /// <summary>
        /// Returns the activity's cancellation token.  Activities can monitor this
        /// to gracefully handle activity cancellation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// We recommend that all non-local activities that run for relatively long periods,
        /// monitor <see cref="CancellationToken"/> for activity cancellation so that they
        /// can gracefully ternminate including potentially calling <see cref="SendHeartbeatAsync(byte[])"/>
        /// to checkpoint the current activity state.
        /// </para>
        /// <para>
        /// Cancelled activities should throw a <see cref="TaskCanceledException"/> from
        /// their <see cref="OnRunAsync(CadenceClient, byte[])"/> method rather than returning 
        /// a result so that Cadence will reschedule the activity if possible.
        /// </para>
        /// </remarks>
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Returns the additional information about the activity and the workflow
        /// that invoked it.  Note that this doesn't work for local activities.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown for local activities.</exception>
        public ActivityTask ActivityTask
        {
            get
            {
                EnsureNotLocal();

                return this.cachedInfo;
            }
        }

        /// <summary>
        /// Called internally to initialize the activity.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The activity's context ID or <c>null</c> for local activities.</param>
        internal void Initialize(CadenceClient client, long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            this.Client                  = client;
            this.contextId               = contextId;
            this.CancellationTokenSource = new CancellationTokenSource();
            this.CancellationToken       = CancellationTokenSource.Token;
        }

        /// <summary>
        /// Called by Cadence to execute an activity.  Derived classes will need to implement
        /// their activity logic here.
        /// </summary>
        /// <param name="args">The activity arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The activity result encoded as a byte array or <c>null</c>.</returns>
        protected abstract Task<byte[]> RunAsync(byte[] args);

        /// <summary>
        /// Called internally to run the activity.
        /// </summary>
        /// <param name="client">The Cadence client.</param>
        /// <param name="args">The activity arguments.</param>
        /// <returns>Thye activity results.</returns>
        internal async Task<byte[]> OnRunAsync(CadenceClient client, byte[] args)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            if (IsLocal)
            {
                return await RunAsync(args);
            }
            else
            {
                // Capture the activity information.

                var reply = (ActivityGetInfoReply)(await Client.CallProxyAsync(
                    new ActivityGetInfoRequest()
                    {
                        ContextId = this.contextId.Value,
                    }));

                reply.ThrowOnError();

                cachedInfo = reply.Info.ToPublic();

                // Track the activity.

                var activityKey = new ActivityKey(client, contextId.Value);

                try
                {
                    lock (syncLock)
                    {
                        idToActivity[activityKey] = this;
                    }

                    return await RunAsync(args);
                }
                finally
                {
                    lock (syncLock)
                    {
                        idToActivity.Remove(activityKey);
                    }
                }
            }
        }

        /// <summary>
        /// Ensures that the activity has an associated Cadence context and thus
        /// is not a local actvity.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown for local activities.</exception>
        private void EnsureNotLocal()
        {
            if (IsLocal)
            {
                throw new InvalidOperationException("This operation is not supported for local activity executions.");
            }
        }

        /// <summary>
        /// <para>
        /// Sends a heartbeat with optional details to Cadence.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> Heartbeats are not supported for local activities.
        /// </note>
        /// </summary>
        /// <param name="details">Optional heartbeart details.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for local activity executions.</exception>
        /// <remarks>
        /// <para>
        /// Long running activities need to send periodic heartbeats back to
        /// Cadence to prove that the activity is still alive.  This can also
        /// be used by activities to implement checkpoints or record other
        /// details.  This method sends a heartbeat with optional details
        /// encoded as a byte array.
        /// </para>
        /// <note>
        /// The maximum allowed time period between heartbeats is specified in 
        /// <see cref="ActivityOptions"/> when activities are executed and it's
        /// also possible to enable automatic heartbeats sent by the Cadence client.
        /// </note>
        /// </remarks>
        public async Task SendHeartbeatAsync(byte[] details = null)
        {
            EnsureNotLocal();

            var reply = (ActivityRecordHeartbeatReply)await Client.CallProxyAsync(
                new ActivityRecordHeartbeatRequest()
                {
                    ContextId = this.contextId.Value,
                    Details   = details
                });

            reply.ThrowOnError();
        }

        /// <summary>
        /// <para>
        /// Determines whether the details from the last recorded heartbeat last
        /// failed attempt exist.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> Heartbeats are not supported for local activities.
        /// </note>
        /// </summary>
        /// <returns>The details from the last heartbeat or <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for local activity executions.</exception>
        public async Task<bool> HasLastHeartbeatDetailsAsync()
        {
            EnsureNotLocal();

            var reply = (ActivityHasHeartbeatDetailsReply)await Client.CallProxyAsync(
                new ActivityHasHeartbeatDetailsRequest()
                {
                    ContextId = this.contextId.Value
                });

            reply.ThrowOnError();

            return reply.HasDetails;
        }

        /// <summary>
        /// <para>
        /// Returns the details from the last recorded heartbeat last failed attempt
        /// at running the activity.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> Heartbeats are not supported for local activities.
        /// </note>
        /// </summary>
        /// <returns>The details from the last heartbeat or <c>null</c>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for local activity executions.</exception>
        public async Task<byte[]> GetLastHeartbeatDetailsAsync()
        {
            EnsureNotLocal();

            var reply = (ActivityGetHeartbeatDetailsReply)await Client.CallProxyAsync(
                new ActivityGetHeartbeatDetailsRequest()
                {
                    ContextId = this.contextId.Value
                });

            reply.ThrowOnError();

            return reply.Details;
        }

        /// <summary>
        /// This method may be called within <see cref="RunAsync(byte[])"/> to indicate that the
        /// activity will be completed externally.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="InvalidOperationException">Thrown for local activities.</exception>
        /// <remarks>
        /// <para>
        /// This method works by throwing an <see cref="CadenceActivityExternalCompletionException"/> which
        /// will be caught and handled by the base <see cref="Activity"/> class.  You'll need to allow
        /// this exception to exit your <see cref="RunAsync(byte[])"/> method for this to work.
        /// </para>
        /// <note>
        /// This method doesn't work for local activities.
        /// </note>
        /// </remarks>
        public async Task CompleteExternallyAsync()
        {
            EnsureNotLocal();

            await Task.CompletedTask;
            throw new CadenceActivityExternalCompletionException();
        }

        //---------------------------------------------------------------------
        // Logging implementation

        // $todo(jeff.lill): Implement these.
        //
        // Note that these calls are all synchronous.  Perhaps we should consider dumping
        // the [INeonLogger] implementations in favor of simpler async methods?

        /// <inheritdoc/>
        public bool IsLogDebugEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogSInfoEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogInfoEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogWarnEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogErrorEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogSErrorEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogCriticalEnabled => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsLogLevelEnabled(LogLevel logLevel)
        {
            return false;
        }

        /// <inheritdoc/>
        public void LogDebug(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogInfo(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogWarn(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSError(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogError(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogCritical(object message, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogDebug(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSInfo(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogInfo(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogWarn(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogError(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogSError(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogCritical(object message, Exception e, string activityId = null)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, IEnumerable<string> textFields, IEnumerable<double> numFields)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params string[] textFields)
        {
        }

        /// <inheritdoc/>
        public void LogMetrics(LogLevel level, params double[] numFields)
        {
        }
    }
}
