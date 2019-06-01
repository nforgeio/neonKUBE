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
    public abstract class Activity
    {
        //---------------------------------------------------------------------
        // Static members

        private static object                       syncLock           = new object();
        private static INeonLogger                  log                = LogManager.Default.GetLogger<Activity>();
        private static Dictionary<string, Type>     nameToActivityType = new Dictionary<string, Type>();

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

            lock (activityTypeName)
            {
                nameToActivityType[activityTypeName] = typeof(TActivity);
            }
        }

        /// <summary>
        /// Constructs an activity instance of the specified type.
        /// </summary>
        /// <param name="activityType">The activity type.</param>
        /// <param name="args">The low-level worker initialization arguments.</param>
        /// <param name="cancellationToken">The activity stop cancellation token.</param>
        /// <returns>The constructed activity.</returns>
        internal static Activity Create(Type activityType, WorkerArgs args, CancellationToken cancellationToken)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null);

            var constructor = activityType.GetConstructor(new Type[] { typeof(WorkerArgs), typeof(CancellationToken) });

            if (constructor == null)
            {
                throw new InvalidOperationException($"Activity type [{activityType.FullName}] does not have a constructor with [{nameof(WorkerArgs)}, {nameof(CancellationToken)}] parameters.");
            }

            return (Activity)constructor.Invoke(new object[] { args, cancellationToken });
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

                    throw new NotImplementedException();
                    break;

                case InternalMessageTypes.ActivityStoppingRequest:

                    throw new NotImplementedException();
                    break;

                default:

                    throw new InvalidOperationException($"Unexpected message type [{request.Type}].");
            }

            await client.ProxyReplyAsync(request, reply);
        }

        //---------------------------------------------------------------------
        // Instance members

        private long contextId;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="args">The low-level worker initialization arguments.</param>
        /// <param name="cancellationToken">The activity stop cancellation token.</param>
        internal Activity(WorkerArgs args, CancellationToken cancellationToken)
        {
            Covenant.Requires<ArgumentNullException>(args != null);
            Covenant.Requires<ArgumentNullException>(cancellationToken != null);

            this.Client            = args.Client;
            this.contextId         = args.ContextId;
            this.CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this activity.
        /// </summary>
        public CadenceClient Client { get; private set; }

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> that will be cancelled when the activity
        /// is being stopped.  This can be monitored by activity implementations that would like
        /// to handle this gracefully by recording a heartbeat with progress information or
        /// by doing other cleanup.
        /// </summary>
        public CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Handles the invocation of the activity.
        /// </summary>
        /// <param name="args">The activity arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The activity result encoded as a byte array or <c>null</c>.</returns>
        internal async Task<byte[]> OnRunAsync(byte[] args)
        {
            return await RunAsync(args);
        }

        /// <summary>
        /// Called by Cadence to execute an activity.  Derived classes will need to implement
        /// their activity logic here.
        /// </summary>
        /// <param name="args">The activity arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The activity result encoded as a byte array or <c>null</c>.</returns>
        protected abstract Task<byte[]> RunAsync(byte[] args);
    }
}
