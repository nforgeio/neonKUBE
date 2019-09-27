//-----------------------------------------------------------------------------
// FILE:	    StartLocalActivityStub.cs
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
    /// Used to execute a local activity in parallel with other activities or child workflows.
    /// Instances are created via <see cref="Workflow.NewLocalActivityStub{TActivityInterface, TActivityImplementation}(LocalActivityOptions)"/>.
    /// </summary>
    /// <typeparam name="TActivityInterface">Specifies the activity interface.</typeparam>
    /// <typeparam name="TActivityImplementation">Specifies the local activity implementation class.</typeparam> 
    public class StartLocalActivityStub<TActivityInterface, TActivityImplementation>
        where TActivityInterface : class
        where TActivityImplementation : TActivityInterface
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Implements the activity future that returns <c>void</c>.
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
                var client = parentWorkflow.Client;

                if (completed)
                {
                    throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.GetAsync()] may only be called once per stub instance.");
                }

                completed = true;

                var reply = (ActivityGetLocalResultReply)await client.CallProxyAsync(
                    new ActivityGetLocalResultRequest()
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
                var client = parentWorkflow.Client;

                if (completed)
                {
                    throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.{nameof(IAsyncFuture<object>.GetAsync)}] may only be called once per stub instance.");
                }

                completed = true;

                var reply = (ActivityGetLocalResultReply)await client.CallProxyAsync(
                    new ActivityGetLocalResultRequest()
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

        private Workflow                parentWorkflow;
        private MethodInfo              targetMethod;
        private LocalActivityOptions    options;
        private bool                    hasStarted;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The associated parent workflow.</param>
        /// <param name="methodName">Identifies the target activity method or <c>null</c> or empty.</param>
        /// <param name="options">The activity options or <c>null</c>.</param>
        internal StartLocalActivityStub(Workflow parentWorkflow, string methodName, LocalActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null);

            var activityInterface = typeof(TActivityInterface);

            CadenceHelper.ValidateActivityInterface(activityInterface);

            var activityAttribute = activityInterface.GetCustomAttribute<ActivityAttribute>();
            var methodAttribute   = (ActivityMethodAttribute)null;

            this.parentWorkflow = parentWorkflow;
            this.hasStarted     = false;

            if (string.IsNullOrEmpty(methodName))
            {
                // Look for the entrypoint method with a null or empty method name.

                foreach (var method in activityInterface.GetMethods())
                {
                    methodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();

                    if (methodAttribute != null)
                    {
                        if (string.IsNullOrEmpty(methodAttribute.Name))
                        {
                            this.targetMethod = method;
                            break;
                        }
                    }
                }
            }
            else
            {
                // Look for the entrypoint method with the matching method name.

                foreach (var method in activityInterface.GetMethods())
                {
                    methodAttribute = method.GetCustomAttribute<ActivityMethodAttribute>();

                    if (methodAttribute != null)
                    {
                        if (methodName == methodAttribute.Name)
                        {
                            this.targetMethod = method;
                            break;
                        }
                    }
                }
            }

            if (this.targetMethod == null)
            {
                throw new ArgumentException($"Activity interface [{activityInterface.FullName}] does not have a method tagged by [ActivityMethod(Name = {methodName})].");
            }

            this.options = LocalActivityOptions.Normalize(parentWorkflow.Client, options);
        }


        /// <summary>
        /// Starts the target activity that returns <c>void</c>, passing the specified arguments.
        /// </summary>
        /// <typeparam name="TResult">The local activity result type.</typeparam>
        /// <param name="args">The arguments to be passed to the activity.</param>
        /// <returns>The <see cref="IAsyncFuture{T}"/> with the <see cref="IAsyncFuture{T}.GetAsync"/> than can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target activity parameters.
        /// These are checked at runtime but not while compiling.
        /// </para>
        /// <note>
        /// Any given <see cref="StartActivityStub{TActivityInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture<TResult>> StartAsync<TResult>(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null);
            parentWorkflow.SetStackTrace();

            if (hasStarted)
            {
                throw new InvalidOperationException("Cannot start a stub more than once.");
            }

            var parameters = targetMethod.GetParameters();

            if (parameters.Length != args.Length)
            {
                throw new ArgumentException($"Invalid number of parameters: [{parameters.Length}] expected but [{args.Length}] were passed.");
            }

            hasStarted = true;

            // Cast the input parameters to the target types so that developers won't need to expicitly
            // cast things like integers into longs, floats into doubles, etc.

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = TypeDescriptor.GetConverter(parameters[i].ParameterType).ConvertTo(args[i], parameters[i].ParameterType);
            }

            // Validate the return type.

            var resultType = targetMethod.ReturnType;

            if (resultType == typeof(Task))
            {
                throw new ArgumentException($"Activity method [{nameof(TActivityInterface)}.{targetMethod.Name}()] does not return [Task<{resultType.FullName}>].");
            }

            resultType = resultType.GenericTypeArguments.First();

            if (!resultType.IsAssignableFrom(typeof(TResult)))
            {
                throw new ArgumentException($"Activity method [{nameof(TActivityInterface)}.{targetMethod.Name}()] returns [{resultType.FullName}] which is not compatible with [{nameof(TResult)}].");
            }

            // Start the activity.

            var client              = parentWorkflow.Client;
            var dataConverter       = client.DataConverter;
            var activityConstructor = typeof(TActivityImplementation).GetConstructor(Type.EmptyTypes);
            var activityActionId    = parentWorkflow.RegisterActivityAction(typeof(TActivityImplementation), activityConstructor, targetMethod);
            var activityId          = parentWorkflow.GetNextActivityId();

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (ActivityStartLocalReply)await client.CallProxyAsync(
                        new ActivityStartLocalRequest()
                        {
                            ContextId      = parentWorkflow.ContextId,
                            ActivityId     = activityId,
                            ActivityTypeId = activityActionId,
                            Args           = dataConverter.ToData(args),
                            Options        = options.ToInternal()
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
        /// <returns>The <see cref="IAsyncFuture{T}"/> with the <see cref="IAsyncFuture{T}.GetAsync"/> than can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target activity parameters.
        /// These are checked at runtime but not while compiling.
        /// </para>
        /// <note>
        /// Any given <see cref="StartActivityStub{TActivityInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture> StartAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null);
            parentWorkflow.SetStackTrace();

            if (hasStarted)
            {
                throw new InvalidOperationException("Cannot start a stub more than once.");
            }

            var parameters = targetMethod.GetParameters();

            if (parameters.Length != args.Length)
            {
                throw new ArgumentException($"Invalid number of parameters: [{parameters.Length}] expected but [{args.Length}] were passed.");
            }

            hasStarted = true;

            // Cast the input parameters to the target types so that developers won't need to expicitly
            // cast things like integers into longs, floats into doubles, etc.

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = TypeDescriptor.GetConverter(parameters[i].ParameterType).ConvertTo(args[i], parameters[i].ParameterType);
            }

            // Start the activity.

            var client              = parentWorkflow.Client;
            var dataConverter       = client.DataConverter;
            var activityConstructor = typeof(TActivityImplementation).GetConstructor(Type.EmptyTypes);
            var activityId          = parentWorkflow.GetNextActivityId();
            var activityActionId    = parentWorkflow.RegisterActivityAction(typeof(TActivityImplementation), activityConstructor, targetMethod);

            var reply = await parentWorkflow.ExecuteNonParallel(
                async () =>
                {
                    return (ActivityStartLocalReply)await client.CallProxyAsync(
                        new ActivityStartLocalRequest()
                        {
                            ContextId      = parentWorkflow.ContextId,
                            ActivityId     = activityId,
                            ActivityTypeId = activityActionId,
                            Args           = dataConverter.ToData(args),
                            Options        = options.ToInternal()
                        });
                });

            reply.ThrowOnError();
            parentWorkflow.UpdateReplay(reply);

            // Create and return the future.

            return new AsyncFuture(parentWorkflow, activityId);
        }
    }
}
