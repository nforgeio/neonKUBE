//-----------------------------------------------------------------------------
// FILE:	    ActivityFutureStubT.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Used to execute an untyped activity in parallel with other activities, child
    /// workflows or other operations.  Instances are created via 
    /// <see cref="Workflow.NewActivityFutureStub(string, ActivityOptions)"/>.
    /// </summary>
    public class ActivityFutureStub
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Implements an activity future that returns <c>void</c>.
        /// </summary>
        private class AsyncFuture : IAsyncFuture
        {
            private bool            completed = false;
            private Workflow        parentWorkflow;
            private long            activityId;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="parentWorkflow">Identifies the parent workflow context.</param>
            /// <param name="activityId">The workflow local activity ID.</param>
            public AsyncFuture(Workflow parentWorkflow, long activityId)
            {
                this.parentWorkflow = parentWorkflow;
                this.activityId     = activityId;
            }

            /// <inheritdoc/>
            public async Task GetAsync()
            {
                await SyncContext.Clear;

                var client = parentWorkflow.Client;

                if (completed)
                {
                    throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.GetAsync()] may only be called once per stub instance.");
                }

                completed = true;

                var reply = (ActivityGetResultReply)await client.CallProxyAsync(
                    new ActivityGetResultRequest()
                    {
                        ContextId  = parentWorkflow.ContextId,
                        ActivityId = activityId,
                    });

                reply.ThrowOnError();
                parentWorkflow.UpdateReplay(reply);
            }
        }

        /// <summary>
        /// Implements an activity future that returns a value.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        private class AsyncFuture<TResult> : IAsyncFuture<TResult>
        {
            private bool            completed = false;
            private Workflow        parentWorkflow;
            private long            activityId;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="parentWorkflow">Identifies the parent workflow context.</param>
            /// <param name="activityId">The workflow local activity ID.</param>
            public AsyncFuture(Workflow parentWorkflow, long activityId)
            {
                this.parentWorkflow = parentWorkflow;
                this.activityId     = activityId;
            }

            /// <inheritdoc/>
            public async Task<TResult> GetAsync()
            {
                await SyncContext.Clear;

                var client = parentWorkflow.Client;

                if (completed)
                {
                    throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.{nameof(IAsyncFuture<object>.GetAsync)}] may only be called once per stub instance.");
                }

                completed = true;

                var reply = (ActivityGetResultReply)await client.CallProxyAsync(
                    new ActivityGetResultRequest()
                    {
                        ContextId  = parentWorkflow.ContextId,
                        ActivityId = activityId,
                    });

                reply.ThrowOnError();
                parentWorkflow.UpdateReplay(reply);

                return client.DataConverter.FromData<TResult>(reply.Result);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private Workflow            parentWorkflow;
        CadenceClient               client;
        private string              activityTypeName;
        private ActivityOptions     options;
        private bool                hasStarted;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The associated parent workflow.</param>
        /// <param name="activityTypeName">
        /// Specifies the target activity type name.
        /// </param>
        /// <param name="options">The activity options or <c>null</c>.</param>
        internal ActivityFutureStub(Workflow parentWorkflow, string activityTypeName, ActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));

            this.parentWorkflow   = parentWorkflow;
            this.client           = parentWorkflow.Client;
            this.activityTypeName = activityTypeName;
            this.hasStarted       = false;
            this.options          = ActivityOptions.Normalize(client, options);
        }

        /// <summary>
        /// Starts the target activity that returns <typeparamref name="TResult"/>, passing the specified arguments.
        /// </summary>
        /// <typeparam name="TResult">The activity result type.</typeparam>
        /// <param name="args">The arguments to be passed to the activity.</param>
        /// <returns>The <see cref="IAsyncFuture{T}"/> with the <see cref="IAsyncFuture{T}.GetAsync"/> that can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a future stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target activity parameters.
        /// These are checked at runtime but not while compiling.
        /// </para>
        /// <note>
        /// Any given <see cref="ActivityFutureStub{TActivityInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture<TResult>> StartAsync<TResult>(params object[] args)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            parentWorkflow.SetStackTrace();

            if (hasStarted)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            hasStarted = true;

            // Start the activity.

            var client        = parentWorkflow.Client;
            var dataConverter = client.DataConverter;
            var activityId    = parentWorkflow.GetNextActivityId();

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (ActivityStartReply)await client.CallProxyAsync(
                        new ActivityStartRequest()
                        {
                            ContextId  = parentWorkflow.ContextId,
                            ActivityId = activityId,
                            Activity   = activityTypeName,
                            Args       = CadenceHelper.ArgsToBytes(dataConverter, args),
                            Options    = options.ToInternal(),
                            Domain     = options.Domain
                        });
                });

            reply.ThrowOnError();
            parentWorkflow.UpdateReplay(reply);

            // Create and return the future.

            return new AsyncFuture<TResult>(parentWorkflow, activityId);
        }

        /// <summary>
        /// Starts the target activity that returns <c>void</c>, passing the specified arguments.
        /// </summary>
        /// <param name="args">The arguments to be passed to the activity.</param>
        /// <returns>The <see cref="IAsyncFuture{T}"/> with the <see cref="IAsyncFuture{T}.GetAsync"/> that can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a future stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target activity parameters.
        /// These are checked at runtime but not while compiling.
        /// </para>
        /// <note>
        /// Any given <see cref="ActivityFutureStub{TActivityInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture> StartAsync(params object[] args)
        {
            await SyncContext.Clear;
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            parentWorkflow.SetStackTrace();

            if (hasStarted)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            hasStarted = true;

            // Start the activity.

            var client        = parentWorkflow.Client;
            var dataConverter = client.DataConverter;
            var activityId    = parentWorkflow.GetNextActivityId();

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (ActivityStartReply)await client.CallProxyAsync(
                        new ActivityStartRequest()
                        {
                            ContextId  = parentWorkflow.ContextId,
                            ActivityId = activityId,
                            Activity   = activityTypeName,
                            Args       = CadenceHelper.ArgsToBytes(dataConverter, args),
                            Options    = options.ToInternal(),
                            Domain     = options.Domain,
                        });
                });

            reply.ThrowOnError();
            parentWorkflow.UpdateReplay(reply);

            // Create and return the future.

            return new AsyncFuture(parentWorkflow, activityId);
        }
    }
}
