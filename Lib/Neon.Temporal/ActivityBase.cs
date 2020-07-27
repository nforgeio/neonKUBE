//-----------------------------------------------------------------------------
// FILE:	    ActivityBase.cs
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Base class that must be inherited by all implementations.
    /// </summary>
    public abstract class ActivityBase
    {
        private Worker          worker;
        private Type            activityType;
        private MethodInfo      activityMethod;
        private IDataConverter  dataConverter;
        private INeonLogger     logger;

        /// <summary>
        /// Default protected constructor.
        /// </summary>
        protected ActivityBase()
        {
        }

        /// <summary>
        /// Called internally to initialize the activity.
        /// </summary>
        /// <param name="worker">The worker hosting the activity.</param>
        /// <param name="activityType">Specifies the target activity type.</param>
        /// <param name="activityMethod">Specifies the target activity method.</param>
        /// <param name="dataConverter">Specifies the data converter to be used for parameter and result serilization.</param>
        /// <param name="contextId">The activity's context ID.</param>
        internal void Initialize(Worker worker, Type activityType, MethodInfo activityMethod, IDataConverter dataConverter, long contextId)
        {
            Covenant.Requires<ArgumentNullException>(worker != null, nameof(worker));
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));
            Covenant.Requires<ArgumentNullException>(activityMethod != null, nameof(activityMethod));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            TemporalHelper.ValidateActivityImplementation(activityType);

            this.worker                  = worker;
            this.Client                  = worker.Client;
            this.Activity                = new Activity(this);
            this.activityType            = activityType;
            this.activityMethod          = activityMethod;
            this.dataConverter           = dataConverter;
            this.ContextId               = contextId;
            this.CancellationTokenSource = new CancellationTokenSource();
            this.CancellationToken       = CancellationTokenSource.Token;
            this.logger                  = LogManager.Default.GetLogger(module: activityType.FullName);
        }

        /// <inheritdoc/>
        public Activity Activity { get; set;  }

        /// <summary>
        /// Returns the <see cref="TemporalClient"/> managing this activity invocation.
        /// </summary>
        internal TemporalClient Client { get; private set; }

        /// <summary>
        /// Returns the <see cref="CancellationTokenSource"/> for the activity invocation.
        /// </summary>
        internal CancellationTokenSource CancellationTokenSource { get; private set; }

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> for thge activity invocation.
        /// </summary>
        internal CancellationToken CancellationToken { get; private set; }

        /// <summary>
        /// Returns the context ID for the activity invocation.
        /// </summary>
        internal long ContextId { get; private set; }

        /// <summary>
        /// Indicates whether the activity was executed locally.
        /// </summary>
        internal bool IsLocal { get; set; }

        /// <summary>
        /// Returns additional information about the activity and the workflow that executed it.
        /// </summary>
        internal ActivityTask ActivityTask { get; private set; }

        /// <summary>
        /// Indicates that the activity will be completed externally.
        /// </summary>
        internal bool CompleteExternally { get; set; } = false;

        /// <summary>
        /// Executes the target activity method.
        /// </summary>
        /// <param name="client">The associated Temporal client.</param>
        /// <param name="argBytes">The encoded activity arguments.</param>
        /// <returns>The encoded activity results.</returns>
        private async Task<byte[]> InvokeAsync(TemporalClient client, byte[] argBytes)
        {
            var parameters     = activityMethod.GetParameters();
            var parameterTypes = new Type[parameters.Length];

            for (int i = 0; i < parameters.Length; i++)
            {
                parameterTypes[i] = parameters[i].ParameterType;
            }

            var resultType       = activityMethod.ReturnType;
            var args             = TemporalHelper.BytesToArgs(dataConverter, argBytes, parameterTypes);
            var serializedResult = Array.Empty<byte>();

            if (resultType.IsGenericType)
            {
                // Activity method returns: Task<T>

                var result = await NeonHelper.GetTaskResultAsObjectAsync((Task)activityMethod.Invoke(this, args));

                serializedResult = client.DataConverter.ToData(result);
            }
            else
            {
                // Activity method returns: Task

                await (Task)activityMethod.Invoke(this, args);
            }

            return serializedResult;
        }

        /// <summary>
        /// Called internally to execute the activity.
        /// </summary>
        /// <param name="args">The encoded activity arguments.</param>
        /// <returns>Thye activity results.</returns>
        internal async Task<byte[]> OnInvokeAsync(byte[] args)
        {
            // Capture the activity context details.

            var reply = (ActivityGetInfoReply)(await Client.CallProxyAsync(
                new ActivityGetInfoRequest()
                {
                    ContextId = ContextId,
                }));

            reply.ThrowOnError();

            ActivityTask = reply.Info;

            // Invoke the activity.

            if (IsLocal)
            {
                // This doesn't make sense for local activities.

                ActivityTask.ActivityTypeName = null;

                return await InvokeAsync(Client, args);
            }
            else
            {
                try
                {
                    return await InvokeAsync(worker.Client, args);
                }
                catch (Exception e)
                {
                    logger.LogError(e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Ensures that the activity has an associated Temporal context and thus
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
