//-----------------------------------------------------------------------------
// FILE:	    StartChildWorkflowStub.cs
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
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Used to execute a child workflow in parallel with other child workflows or activities.
    /// Instances can be created via <see cref="Workflow.NewStartChildWorkflowStub{TWorkflowInterface}(string, ChildWorkflowOptions)"/>.
    /// </summary>
    public class StartChildWorkflowStub
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Implements the child workflow future.
        /// </summary>
        private class AsyncFuture : IAsyncFuture<object>
        {
            private bool            valueReturned = false;
            private Workflow        parentWorkflow;
            private ChildExecution  execution;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="parentWorkflow">Identifies the parent workflow context.</param>
            /// <param name="execution">The child execution.</param>
            public AsyncFuture(Workflow parentWorkflow, ChildExecution execution)
            {
                this.parentWorkflow = parentWorkflow;
                this.execution      = execution;
            }

            /// <inheritdoc/>
            public async Task<object> GetAsync()
            {
                if (valueReturned)
                {
                    throw new InvalidOperationException($"[{nameof(IAsyncFuture<object>)}.{nameof(IAsyncFuture<object>.GetAsync)}] may only be called once.");
                }

                valueReturned = true;

                return await parentWorkflow.Client.GetChildWorkflowResultAsync(parentWorkflow, execution);
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private MethodInfo              targetMethod;
        private ChildWorkflowOptions    options;
        private string                  workflowTypeName;
        private bool                    hasStarted;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="client">The associated Cadence client.</param>
        /// <param name="workflowInterface">The target workflow interface.</param>
        /// <param name="methodName">Identifies the target workflow method.</param>
        /// <param name="options">The child workflow options or <c>null</c>.</param>
        internal StartChildWorkflowStub(CadenceClient client, Type workflowInterface, string methodName, ChildWorkflowOptions options)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(workflowInterface != null);
            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();
            var methodAttribute   = (WorkflowMethodAttribute)null;

            this.options    = options;
            this.hasStarted = false;

            if (string.IsNullOrEmpty(methodName))
            {
                // Look for the method with a null or empty method name.

                foreach (var method in workflowInterface.GetMethods())
                {
                    methodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

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
                // Look for the method with the matching method name.

                foreach (var method in workflowInterface.GetMethods())
                {
                    methodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

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
                throw new ArgumentException($"Workflow interface [{workflowInterface.FullName}] does not have a method tagged by [WorkflowMethod(Name = {methodName})].");
            }

            workflowTypeName = CadenceHelper.GetWorkflowTypeName(workflowInterface, workflowAttribute);

            // $hack(jeff.lill):
            //
            // It would be nicer if [CadenceHelper.GetWorkflowTypeName()] accepted an optional
            // [WorkflowMethodAttribute] that would be used to append the method name so that
            // we won't need to hardcode that behavior here.

            if (!string.IsNullOrEmpty(methodAttribute.Name))
            {
                workflowTypeName += $"::{methodAttribute.Name}";
            }
        }

        /// <summary>
        /// Starts the target workflow, passing the arguments.
        /// </summary>
        /// <param name="parentWorkflow">The parent workflow context.</param>
        /// <param name="args">The arguments to be passed to the workflow.</param>
        /// <returns>The <see cref="IAsyncFuture{T}"/> with the <see cref="IAsyncFuture{T}.GetAsync"/> than can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target workflow parameters.
        /// These are checked at runtime but not while compiling.  The <see cref="IAsyncFuture{T}.GetAsync"/>
        /// returns always returns an <c>object</c> regardless of the actual type returned by the target
        /// workflow method.  You'll also need to cast the result to the required type when necessary.
        /// </para>
        /// <note>
        /// Any given <see cref="StartChildWorkflowStub"/> may only be executed once.
        /// </note>
        /// </remarks>
        internal async Task<IAsyncFuture<object>> StartAsync(Workflow parentWorkflow, params object[] args)
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
            // cast things types integers into longs, floats into doubles, etc.

            for (int i = 0; i < args.Length; i++)
            {
                args[i] = TypeDescriptor.GetConverter(parameters[i].ParameterType).ConvertTo(args[i], parameters[i].ParameterType);
            }

            // Start the child workflow and then construct and return the future.

            var client    = parentWorkflow.Client;
            var execution = await client.StartChildWorkflowAsync(parentWorkflow, workflowTypeName, client.DataConverter.ToData(args), options);

            return new AsyncFuture(parentWorkflow, execution);
        }
    }
}
