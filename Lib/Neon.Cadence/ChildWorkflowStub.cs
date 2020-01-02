//-----------------------------------------------------------------------------
// FILE:	    ChildWorkflowStub.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
    /// Used to execute a child workflow in parallel with other child workflows or activities.
    /// Instances are created via <see cref="Workflow.NewChildWorkflowFutureStub{TWorkflowInterface}(string, ChildWorkflowOptions)"/>.
    /// </summary>
    /// <typeparam name="TWorkflowInterface">Specifies the workflow interface.</typeparam>
    public class ChildWorkflowStub<TWorkflowInterface>
        where TWorkflowInterface : class
    {
        private Workflow                parentWorkflow;
        private MethodInfo              targetMethod;
        private ChildWorkflowOptions    options;
        private string                  workflowTypeName;
        private bool                    hasStarted;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="parentWorkflow">The associated parent workflow.</param>
        /// <param name="methodName">Identifies the target workflow method or <c>null</c> or empty.</param>
        /// <param name="options">The child workflow options or <c>null</c>.</param>
        internal ChildWorkflowStub(Workflow parentWorkflow, string methodName, ChildWorkflowOptions options)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));

            var workflowInterface = typeof(TWorkflowInterface);

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            this.parentWorkflow   = parentWorkflow;
            this.options          = ChildWorkflowOptions.Normalize(parentWorkflow.Client, options);
            this.hasStarted       = false;

            var workflowTarget    = CadenceHelper.GetWorkflowTarget(workflowInterface, methodName);

            this.workflowTypeName = workflowTarget.WorkflowTypeName;
            this.targetMethod     = workflowTarget.TargetMethod;
        }

        /// <summary>
        /// Starts the target workflow that returns <typeparamref name="TResult"/>, passing any specified arguments.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <param name="args">The arguments to be passed to the workflow.</param>
        /// <returns>The <see cref="ChildWorkflowFuture{T}"/> with the <see cref="ChildWorkflowFuture{T}.GetAsync"/> than can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a future stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target workflow parameters.
        /// These are checked at runtime but not while compiling.
        /// </para>
        /// <note>
        /// Any given <see cref="ChildWorkflowStub{TWorkflowInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<ChildWorkflowFuture<TResult>> StartAsync<TResult>(params object[] args)
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
                args[i] = TypeDescriptor.GetConverter(parameters[i].ParameterType).ConvertTo(args[i], parameters[i].ParameterType);
            }

            // Start the child workflow and then construct and return the future.

            var client    = parentWorkflow.Client;
            var execution = await client.StartChildWorkflowAsync(parentWorkflow, workflowTypeName, client.DataConverter.ToData(args), options);

            // Initialize the type-safe stub property such that developers can call
            // any query or signal methods.

            Stub = StubManager.NewChildWorkflowStub<TWorkflowInterface>(client, parentWorkflow, workflowTypeName, execution);

            // Create and return the future.

            var resultType = targetMethod.ReturnType;

            if (resultType == typeof(Task))
            {
                throw new ArgumentException($"Workflow method [{nameof(TWorkflowInterface)}.{targetMethod.Name}()] does not return [void].", nameof(TWorkflowInterface));
            }
            
            resultType = resultType.GenericTypeArguments.First();
            
            if (!resultType.IsAssignableFrom(typeof(TResult)))
            {
                throw new ArgumentException($"Workflow method [{nameof(TWorkflowInterface)}.{targetMethod.Name}()] returns [{resultType.FullName}] which is not compatible with [{nameof(TResult)}].", nameof(TWorkflowInterface));
            }

            return new ChildWorkflowFuture<TResult>(parentWorkflow, execution);
        }

        /// <summary>
        /// Starts the target workflow that returns <c>void</c>, passing any specified arguments.
        /// </summary>
        /// <param name="args">The arguments to be passed to the workflow.</param>
        /// <returns>The <see cref="ChildWorkflowFuture{T}"/> with the <see cref="ChildWorkflowFuture{T}.GetAsync"/> than can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a future stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target workflow parameters.
        /// These are checked at runtime but not while compiling.
        /// </para>
        /// <note>
        /// Any given <see cref="ChildWorkflowStub{TWorkflowInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<ChildWorkflowFuture> StartAsync(params object[] args)
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
                args[i] = TypeDescriptor.GetConverter(parameters[i].ParameterType).ConvertTo(args[i], parameters[i].ParameterType);
            }

            // Start the child workflow and then construct the future.

            var client    = parentWorkflow.Client;
            var execution = await client.StartChildWorkflowAsync(parentWorkflow, workflowTypeName, client.DataConverter.ToData(args), options);

            // Initialize the type-safe stub property such that developers can call
            // any query or signal methods.

            Stub = StubManager.NewChildWorkflowStub<TWorkflowInterface>(client, parentWorkflow, workflowTypeName, execution);

            // Create and return the future.

            return new ChildWorkflowFuture(parentWorkflow, execution);
        }

        /// <summary>
        /// <para>
        /// Returns the underlying <typeparamref name="TWorkflowInterface"/> stub for the child workflow.
        /// This includes all the workflow entrypoint, query and signal methods.
        /// </para>
        /// <note>
        /// The entrypoint methods won't work because the workflow will already be running but you can
        /// interact with the child workflow using any query and signal methods.       
        /// </note>
        /// </summary>
        public TWorkflowInterface Stub { get; private set; }
    }
}
