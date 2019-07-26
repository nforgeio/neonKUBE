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
    public abstract class ActivityBase : IActivityBase
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

        private static object                                   syncLock            = new object();
        private static INeonLogger                              log                 = LogManager.Default.GetLogger<ActivityBase>();
        private static Type[]                                   noTypeArgs          = new Type[0];
        private static object[]                                 noArgs              = new object[0];
        private static Dictionary<ActivityKey, ActivityBase>    idToActivity        = new Dictionary<ActivityKey, ActivityBase>();
        private static Dictionary<Type, ConstructorInfo>        typeToConstructor   = new Dictionary<Type, ConstructorInfo>();

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
            CadenceHelper.ValidateActivityImplementation(activityType);

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
        internal static ActivityBase Create( CadenceClient client, Type activityType,long? contextId)
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

            var activity = (ActivityBase)constructor.Invoke(noArgs);

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
        internal static ActivityBase Create(CadenceClient client, string activityTypeName, long? contextId)
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

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ActivityBase()
        {
            this.Activity = new Activity(this);
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
            this.ContextId               = contextId;
            this.CancellationTokenSource = new CancellationTokenSource();
            this.CancellationToken       = CancellationTokenSource.Token;
        }

        /// <inheritdoc/>
        public Activity Activity { get; set;  }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this activity invocation.
        /// </summary>
        internal CadenceClient Client { get; private set; }

        /// <summary>
        /// Returns the <see cref="CancellationTokenSource"/> for the activity invocation.
        /// </summary>
        internal CancellationTokenSource CancellationTokenSource { get; private set; }

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> for thge activity invocation.
        /// </summary>
        internal CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Returns the context ID for the activity invocation or <c>null</c> for
        /// local activities.
        /// </summary>
        internal long? ContextId { get; private set; }

        /// <summary>
        /// Indicates whether the activity was executed locally.
        /// </summary>
        internal bool IsLocal => !ContextId.HasValue;

        /// <summary>
        /// Returns additional information about the activity and thr workflow that executed it.
        /// </summary>
        internal ActivityTask ActivityTask { get; private set; }

        /// <summary>
        /// Called by Cadence to execute an activity.  Derived classes will need to implement
        /// their activity logic here.
        /// </summary>
        /// <param name="args">The activity arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The activity result encoded as a byte array or <c>null</c>.</returns>
        protected abstract Task<byte[]> RunAsync(byte[] args);

        /// <summary>
        /// Called internally to execute the activity.
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
                        ContextId = ContextId.Value,
                    }));

                reply.ThrowOnError();

                ActivityTask = reply.Info.ToPublic();

                // Track the activity.

                var activityKey = new ActivityKey(client, ContextId.Value);

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
        internal void EnsureNotLocal()
        {
            if (IsLocal)
            {
                throw new InvalidOperationException("This operation is not supported for local activity executions.");
            }
        }
    }
}
