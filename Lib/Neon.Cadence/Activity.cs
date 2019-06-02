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
    public abstract class Activity : INeonLogger
    {
        //---------------------------------------------------------------------
        // Private types

        private struct ActivityInfo
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

        private static object                               syncLock           = new object();
        private static INeonLogger                          log                = LogManager.Default.GetLogger<Activity>();
        private static Type[]                               noTypeArgs         = new Type[0];
        private static object[]                             noArgs             = new object[0];

        // These dictionaries are used to cache reflected activity
        // constructors for better performance.

        private static Dictionary<string, ActivityInfo>     nameToActivityInfo = new Dictionary<string, ActivityInfo>();
        private static Dictionary<Type, ConstructorInfo>    typeToConstructor  = new Dictionary<Type, ConstructorInfo>();

        /// <summary>
        /// Registers an activity type.
        /// </summary>
        /// <typeparam name="TActivity">The activity implementation type.</typeparam>
        /// <param name="activityTypeName">The name used to identify the implementation.</param>
        internal static void Register<TActivity>(string activityTypeName)
            where TActivity : Activity
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName));
            Covenant.Requires<ArgumentException>(typeof(TActivity) != typeof(Activity), $"The base [{nameof(Activity)}] class cannot be registered.");

            var activityInfo = new ActivityInfo();

            activityInfo.Type        = typeof(TActivity);
            activityInfo.Constructor = activityInfo.Type.GetConstructor(noTypeArgs);

            if (activityInfo.Constructor == null)
            {
                throw new ArgumentException($"Activity type [{activityInfo.Type.FullName}] does not have a default constructor.");
            }

            lock (syncLock)
            {
                nameToActivityInfo[activityTypeName] = activityInfo;
            }
        }

        /// <summary>
        /// Constructs an activity instance with the specified type.
        /// </summary>
        /// <param name="activityType">The activity type.</param>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The activity context ID or <c>null</c> for local activities.</param>
        /// <returns>The constructed activity.</returns>
        internal static Activity Create(Type activityType, CadenceClient client, long? contextId)
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

            var activity = (Activity)constructor.Invoke(noArgs);

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
        internal static Activity Create(string activityTypeName, CadenceClient client, long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(activityTypeName != null);
            Covenant.Requires<ArgumentNullException>(client != null);

            ActivityInfo activityInfo;

            lock (syncLock)
            {
                if (!nameToActivityInfo.TryGetValue(activityTypeName, out activityInfo))
                {
                    throw new ArgumentException($"No activty type is registered for [{activityTypeName}].");
                }
            }

            var activity = (Activity)activityInfo.Constructor.Invoke(noArgs);

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

                    throw new NotImplementedException();
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
            catch (Exception e)
            {
                return new ActivityInvokeReply()
                {
                    Error = new CadenceError(e)
                };
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private long? contextId;

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
        /// The cancallation token source (linked to <see cref="CancellationToken"/>).
        /// </summary>
        private CancellationTokenSource CancellationTokenSource { get; set; }

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> that will be cancelled when the activity
        /// is being stopped due to the local activity working being stopped.  
        /// </summary>
        /// <remarks>
        /// <para>
        /// Activities have a limited time to terminate gracefully when the worker is being stopped.
        /// This is defaults to 10 seconds but may be customized when the worker is started
        /// by setting the <see cref="WorkerOptions.WorkerStopTimeout"/> property.  Activities
        /// that don't return from their <see cref="RunAsync(byte[])"/> method within this
        /// limit will unceremoniously terminated.
        /// </para>
        /// <para>
        /// For longer running activities, it's a best practice to monitor the 
        /// <see cref="CancellationToken"/> to try to handle worker stopping by recording
        /// a heartbeat with progress information and/or by doing other cleanup 
        /// and then promptly returning from its <see cref="RunAsync(byte[])"/> method.
        /// </para>
        /// </remarks>
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Called internally to initialize the activity.
        /// </summary>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The activity's context ID or <c>null</c> for local activities.</param>
        internal void Initialize(CadenceClient client, long? contextId)
        {
            Covenant.Requires<ArgumentNullException>(client != null);

            this.Client                  = client;
            this.CancellationTokenSource = new CancellationTokenSource();
            this.CancellationToken       = this.CancellationTokenSource.Token;
            this.contextId               = contextId;
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
            return await RunAsync(args);
        }

        /// <summary>
        /// Ensures that the activity has an associated Cadence context and thus
        /// is not a local actvity.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown for local activities.</exception>
        private void EnsureNotLocal()
        {
            if (!contextId.HasValue)
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
        public async Task<bool> HasLastHeartbeatDetails()
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
        public async Task<byte[]> GetLastHeartbeatDetails()
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
