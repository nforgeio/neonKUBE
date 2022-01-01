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
    /// Used to execute a typed activity in parallel with other activities, child
    /// workflows or other operations.  Instances are created via 
    /// <see cref="Workflow.NewActivityFutureStub{TActivityInterface}(string, ActivityOptions)"/>.
    /// </summary>
    /// <typeparam name="TActivityInterface">Specifies the activity interface.</typeparam>
    public class ActivityFutureStub<TActivityInterface>
        where TActivityInterface : class
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
                await SyncContext.ClearAsync;

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
                await SyncContext.ClearAsync;

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
        private CadenceClient       client;
        private MethodInfo          targetMethod;
        private ActivityOptions     options;
        private string              activityTypeName;
        private bool                hasStarted;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The associated parent workflow.</param>
        /// <param name="methodName">
        /// Optionally identifies the target activity method by the name specified in
        /// the <c>[ActivityMethod]</c> attribute tagging the method.  Pass a <c>null</c>
        /// or empty string to target the default method.
        /// </param>
        /// <param name="options">The activity options or <c>null</c>.</param>
        internal ActivityFutureStub(Workflow parentWorkflow, string methodName = null, ActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));

            var activityInterface = typeof(TActivityInterface);

            CadenceHelper.ValidateActivityInterface(activityInterface);

            this.parentWorkflow = parentWorkflow;
            this.client         = parentWorkflow.Client;
            this.hasStarted     = false;

            var activityTarget  = CadenceHelper.GetActivityTarget(activityInterface, methodName);
            var methodAttribute = activityTarget.MethodAttribute;

            this.activityTypeName = activityTarget.ActivityTypeName;
            this.targetMethod     = activityTarget.TargetMethod;
            this.options          = ActivityOptions.Normalize(client, options, typeof(TActivityInterface), activityTarget.TargetMethod);
        }

        /// <summary>
        /// Starts the target activity that returns <typeparamref name="TActivityInterface"/>, passing the specified arguments.
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
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            parentWorkflow.SetStackTrace();

            if (hasStarted)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            var parameters = targetMethod.GetParameters();

            if (parameters.Length != args.Length)
            {
                throw new ArgumentException($"Invalid number of parameters: [{parameters.Length}] expected but [{args.Length}] were passed.", nameof(parameters));
            }

            hasStarted = true;

            // Cast the input parameters to the target types so that developers won't need to expicitly
            // cast things like integers into longs, floats into doubles, etc.

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = CadenceHelper.ConvertArg(parameters[i].ParameterType, args[i]);
            }

            // Validate the return type.

            var resultType = targetMethod.ReturnType;

            if (resultType == typeof(Task))
            {
                throw new ArgumentException($"Activity method [{nameof(TActivityInterface)}.{targetMethod.Name}()] does not return [void].", nameof(TActivityInterface));
            }

            resultType = resultType.GenericTypeArguments.First();

            if (!resultType.IsAssignableFrom(typeof(TResult)))
            {
                throw new ArgumentException($"Activity method [{nameof(TActivityInterface)}.{targetMethod.Name}()] returns [{resultType.FullName}] which is not compatible with [{nameof(TResult)}].", nameof(TActivityInterface));
            }

            // Start the activity.

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
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            parentWorkflow.SetStackTrace();

            if (hasStarted)
            {
                throw new InvalidOperationException("Cannot start a future stub more than once.");
            }

            var parameters = targetMethod.GetParameters();

            if (parameters.Length != args.Length)
            {
                throw new ArgumentException($"Invalid number of parameters: [{parameters.Length}] expected but [{args.Length}] were passed.", nameof(parameters));
            }

            hasStarted = true;

            // Cast the input parameters to the target types so that developers won't need to expicitly
            // cast things like integers into longs, floats into doubles, etc.

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = CadenceHelper.ConvertArg(parameters[i].ParameterType, args[i]);
            }

            // Start the activity.

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
