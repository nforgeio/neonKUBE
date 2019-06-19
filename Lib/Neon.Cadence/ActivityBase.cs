//-----------------------------------------------------------------------------
// FILE:	    ActivityBase.cs
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
    /// activity type from <see cref="ActivityBase"/> and then implement a
    /// <see cref="RunAsync(byte[])"/> method with your custom code.
    /// This accepts a byte array with your custom activity arguments 
    /// as a parameter and returns a byte array as your activity result.
    /// Both of these values may be <c>null</c>.  Activities report failures
    /// by throwing an exception from their <see cref="RunAsync(byte[])"/>
    /// methods.
    /// </para>
    /// <para>
    /// Unlike the <see cref="WorkflowBase.RunAsync(byte[])"/> method, the 
    /// <see cref="ActivityBase.RunAsync(byte[])"/> method implementations has
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
    /// an interval no greater than <see cref="Info"/>.<see cref="ActivityInfo.HeartbeatTimeout"/>.
    /// This proves to Cadence that the activity is still healthy and running and
    /// also provides an opportunity for long running and computationally expecsive
    /// activities to checkpoint their current state so they won't need to start
    /// completely over when the activity is rescheduled.
    /// </para>
    /// <para>
    /// Cadence supports two kinds of activities: <b>normal</b> and <b>local</b>.
    /// <b>normal</b> activities are registered via <see cref="CadenceClient.RegisterActivityAsync{TActivity}(string)"/>
    /// and are scheduled by the Cadence cluster to be executed on workers.  Workflows
    /// invoke theses using <see cref="WorkflowBase.CallActivityAsync(string, byte[], ActivityOptions, CancellationToken?)"/>.
    /// </para>
    /// <para>
    /// <b>local</b> activities simply run on the local worker without needing to
    /// be registered or scheduled by the Cadence cluster.  These are very low overhead
    /// and intended for for simple short running activities (a few seconds).
    /// Workflows invoke local activities using <see cref="WorkflowBase.CallLocalActivityAsync{TActivity}(byte[], LocalActivityOptions, CancellationToken?)"/>.
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
    /// <para>
    /// It is often necessary for an activity to be considered complete after an
    /// external event occurs.  For example, an order processing system may may have
    /// an activity that records a potential order in a database and then notifies
    /// an actual person to review and then manually confirm the order.
    /// </para>
    /// <para>
    /// The activity could potentially poll the database for the confirmation but this
    /// is messy and probably won't scale well.  Cadence supports an alternative 
    /// mechanism where the activity gives its <b>task token</b> (an opaque byte array
    /// from <see cref="Info"/>.<see cref="ActivityInfo.TaskToken"/>) to the external 
    /// system and then calls <see cref="CompleteExternallyAsync()"/> which exits
    /// <see cref="RunAsync(byte[])"/> and tells Cadence to wait for an external
    /// completion.
    /// </para>
    /// <para>
    /// Then the external system can complete the activity by connecting a <see cref="CadenceClient"/>
    /// to the cluster and calling <see cref="CadenceClient.CompleteActivityAsync(byte[], byte[], Exception)"/>
    /// passing a result or exception.
    /// </para>
    /// </remarks>
    public abstract class ActivityBase : INeonLogger
    {
        //---------------------------------------------------------------------
        // Private types

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
        private static INeonLogger                          log                 = LogManager.Default.GetLogger<ActivityBase>();
        private static Type[]                               noTypeArgs          = new Type[0];
        private static object[]                             noArgs              = new object[0];
        private static Dictionary<long, ActivityBase>       contextIdToActivity = new Dictionary<long, ActivityBase>();

        // These dictionaries are used to cache reflected activity
        // constructors for better performance.

        private static Dictionary<string, ConstructInfo>    nameToConstructInfo = new Dictionary<string, ConstructInfo>();
        private static Dictionary<Type, ConstructorInfo>    typeToConstructor   = new Dictionary<Type, ConstructorInfo>();

        /// <summary>
        /// Registers an activity type.
        /// </summary>
        /// <param name="activityType">The activity type.</param>
        /// <param name="activityTypeName">The name used to identify the implementation.</param>
        internal static void Register(Type activityType, string activityTypeName)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null);
            Covenant.Requires<ArgumentException>(activityType.IsSubclassOf(typeof(ActivityBase)), $"Type [{activityType.FullName}] does not derive from [{nameof(ActivityBase)}].");
            Covenant.Requires<ArgumentException>(activityType != typeof(ActivityBase), $"The base [{nameof(ActivityBase)}] class cannot be registered.");

            var constructInfo = new ConstructInfo();

            constructInfo.Type        = activityType;
            constructInfo.Constructor = constructInfo.Type.GetConstructor(noTypeArgs);

            if (constructInfo.Constructor == null)
            {
                throw new ArgumentException($"Activity type [{constructInfo.Type.FullName}] does not have a default constructor.");
            }

            lock (syncLock)
            {
                nameToConstructInfo[activityTypeName] = constructInfo;
            }
        }

        /// <summary>
        /// Constructs an activity instance with the specified type.
        /// </summary>
        /// <param name="activityType">The activity type.</param>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The activity context ID or <c>null</c> for local activities.</param>
        /// <returns>The constructed activity.</returns>
        internal static ActivityBase Create(Type activityType, CadenceClient client, long? contextId)
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
                }
            }

            var activity = (ActivityBase)constructor.Invoke(noArgs);

            activity.Initialize(client, contextId);

            return activity;
        }

        /// <summary>
        /// Constructs an activity instance with the specified activity type name.
        /// </summary>
        /// <param name="activityTypeName">The activity type name.</param>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The activity context ID or <c>null</c> for local activities.</param>
        /// <returns>The constructed activity.</returns>
        internal static ActivityBase Create(string activityTypeName, CadenceClient client, long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(activityTypeName != null);
            Covenant.Requires<ArgumentNullException>(client != null);

            ConstructInfo constructInfo;

            lock (syncLock)
            {
                if (!nameToConstructInfo.TryGetValue(activityTypeName, out constructInfo))
                {
                    throw new ArgumentException($"No activty type is registered for [{activityTypeName}].");
                }
            }

            var activity = (ActivityBase)constructInfo.Constructor.Invoke(noArgs);

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
            var activity = Create(request.Activity, client, request.ContextId);

            try
            {
                var result = await activity.OnRunAsync(request.Args);

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
                if (contextIdToActivity.TryGetValue(request.ContextId, out var activity))
                {
                    activity.CancellationTokenSource.Cancel();
                }
            }

            return await Task.FromResult(new ActivityStoppingReply());
        }

        //---------------------------------------------------------------------
        // Instance members

        private long?           contextId;      // Will be NULL for local activities.
        private ActivityInfo    cachedInfo;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActivityBase()
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
        /// their <see cref="OnRunAsync(byte[])"/> method rather than returning a result
        /// so that Cadence will reschedule the activity if possible.
        /// </para>
        /// </remarks>
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Returns the additional information about the activity and the workflow
        /// that invoked it.
        /// </summary>
        public ActivityInfo Info
        {
            get
            {
                // Return the cached value if there is one otherwise query
                // Cadence for the info and cache it.
                
                // $note(jeff.lill):
                //
                // I could have used a lock here to prevent an app from
                // calling this simultaniously on two different threads,
                // resulting in multiple queries to the cadence-proxy,
                // but that's probably very unlikely to happen in the
                // real world and since the info returned is invariant,
                // having this happen would be harmless anyway.
                // 
                // So, that wasn't worth the trouble.

                if (cachedInfo != null)
                {
                    return cachedInfo;
                }

                var reply = (ActivityGetInfoReply)Client.CallProxyAsync(
                    new ActivityGetInfoRequest()
                    {
                        ContextId = this.contextId.Value,

                    }).Result;

                reply.ThrowOnError();

                return this.cachedInfo = reply.Info.ToPublic();
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
        /// <param name="args">The activity arguments.</param>
        /// <returns>Thye activity results.</returns>
        internal async Task<byte[]> OnRunAsync(byte[] args)
        {
            try
            {
                if (!IsLocal)
                {
                    lock (syncLock)
                    {
                        contextIdToActivity[contextId.Value] = this;
                    }
                }

                return await RunAsync(args);
            }
            finally
            {
                if (!IsLocal)
                {
                    lock (syncLock)
                    {
                        contextIdToActivity.Remove(contextId.Value);
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
            if (!IsLocal)
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
        /// <param name="details">The optional heartbeart details.</param>
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
        /// <remarks>
        /// This method works by throwing an <see cref="CadenceActivityExternalCompletionException"/> which
        /// will be caught and handled by the base <see cref="ActivityBase"/> class.  You'll need to allow
        /// this exception to exit your <see cref="RunAsync(byte[])"/> method for this to work.
        /// </remarks>
        public async Task CompleteExternallyAsync()
        {
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
