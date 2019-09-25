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
        /// Implements the activity future.
        /// </summary>
        private class AsyncFuture : IAsyncFuture<object>
        {
            private bool            valueReturned = false;
            private Workflow        parentWorkflow;
            private long            activityId;
            private Type            resultType;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="parentWorkflow">Identifies the parent workflow context.</param>
            /// <param name="activityId">The workflow local activity ID.</param>
            /// <param name="resultType">The workflow result type or <c>null</c> for <c>void</c>.</param>
            public AsyncFuture(Workflow parentWorkflow, long activityId, Type resultType)
            {
                this.parentWorkflow = parentWorkflow;
                this.activityId     = activityId;
                this.resultType     = resultType;
            }

            /// <inheritdoc/>
            public async Task<object> GetAsync()
            {
                var client = parentWorkflow.Client;

                if (valueReturned)
                {
                    throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.{nameof(IAsyncFuture<object>.GetAsync)}] may only be called once per stub instance.");
                }

                valueReturned = true;

                var reply = (ActivityGetLocalResultReply)await client.CallProxyAsync(
                    new ActivityGetLocalResultRequest()
                    {
                        ContextId  = parentWorkflow.ContextId,
                        ActivityId = activityId,
                    });

                reply.ThrowOnError();

                if (resultType != null && reply.Result != null)
                {
                    return client.DataConverter.FromData(resultType, reply.Result);
                }
                else
                {
                    return null;
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private Workflow                parentWorkflow;
        private MethodInfo              targetMethod;
        private LocalActivityOptions    options;
        private string                  activityTypeName;
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

            activityTypeName = CadenceHelper.GetActivityTypeName(activityInterface, activityAttribute);

            // $hack(jeff.lill):
            //
            // It would be nicer if [CadenceHelper.GetActivityTypeName()] accepted an optional
            // [ActivityMethodAttribute] that would be used to append the method name so that
            // we won't need to hardcode that behavior here.

            if (!string.IsNullOrEmpty(methodAttribute.Name))
            {
                activityTypeName += $"::{methodAttribute.Name}";
            }

            // Normalize the options.

            if (options == null)
            {
                options = new LocalActivityOptions();
            }
            else
            {
                options = options.Clone();
            }

            if (options.ScheduleToCloseTimeout <= TimeSpan.Zero)
            {
                options.ScheduleToCloseTimeout = TimeSpan.FromSeconds(methodAttribute.ScheduleToCloseTimeoutSeconds);
            }

            this.options = options;
        }

        /// <summary>
        /// Starts the target activity, passing the specified arguments.
        /// </summary>
        /// <param name="args">The arguments to be passed to the activity.</param>
        /// <returns>The <see cref="IAsyncFuture{T}"/> with the <see cref="IAsyncFuture{T}.GetAsync"/> than can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target activity parameters.
        /// These are checked at runtime but not while compiling.  The <see cref="IAsyncFuture{T}.GetAsync"/>
        /// returns always returns an <c>object</c> regardless of the actual type returned by the target
        /// activity method.  You'll also need to cast the result to the required type as necessary.
        /// </para>
        /// <note>
        /// Any given <see cref="StartActivityStub{TActivityInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture<object>> StartAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null);

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
            // cast things likes integers into longs, floats into doubles, etc.

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = TypeDescriptor.GetConverter(parameters[i].ParameterType).ConvertTo(args[i], parameters[i].ParameterType);
            }

            // Start the activity.

            var client        = parentWorkflow.Client;
            var dataConverter = client.DataConverter;
            var activityId    = parentWorkflow.GetNextActivityId();

            var reply = (ActivityStartLocalReply)await client.CallProxyAsync(
                new ActivityStartLocalRequest()
                {
                    ContextId = parentWorkflow.ContextId,
                    Activity  = activityTypeName,
                    Args      = dataConverter.ToData(args),
                    Options   = options.ToInternal()
                });

            reply.ThrowOnError();

            // Create and return the future.

            var resultType = targetMethod.ReturnType;

            if (resultType == typeof(Task))
            {
                resultType = null;
            }
            else
            {
                resultType = resultType.GenericTypeArguments.First();
            }

            return new AsyncFuture(parentWorkflow, activityId, resultType);
        }
    }
}
