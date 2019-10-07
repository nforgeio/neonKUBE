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
    /// Instances are created via <see cref="Workflow.NewStartChildWorkflowStub{TWorkflowInterface}(string, ChildWorkflowOptions)"/>.
    /// </summary>
    /// <typeparam name="TWorkflowInterface">Specifies the workflow interface.</typeparam>
    public class StartChildWorkflowStub<TWorkflowInterface>
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
        internal StartChildWorkflowStub(Workflow parentWorkflow, string methodName, ChildWorkflowOptions options)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));

            var workflowInterface = typeof(TWorkflowInterface);

            CadenceHelper.ValidateWorkflowInterface(workflowInterface);

            options = ChildWorkflowOptions.Normalize(parentWorkflow.Client, options);

            var workflowAttribute = workflowInterface.GetCustomAttribute<WorkflowAttribute>();
            var methodAttribute   = (WorkflowMethodAttribute)null;

            this.parentWorkflow = parentWorkflow;
            this.options        = options;
            this.hasStarted     = false;

            if (string.IsNullOrEmpty(methodName))
            {
                // Look for the entrypoint method with a null or empty method name.

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
                // Look for the entrypoint method with the matching method name.

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
                throw new ArgumentException($"Workflow interface [{workflowInterface.FullName}] does not have a method tagged by [WorkflowMethod(Name = {methodName})].", nameof(workflowInterface));
            }

            workflowTypeName = CadenceHelper.GetWorkflowTypeName(workflowInterface, workflowAttribute);

            // $hack(jefflill):
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
        /// Starts the target workflow that returns <typeparamref name="TResult"/>, passing any specified arguments.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <param name="args">The arguments to be passed to the workflow.</param>
        /// <returns>The <see cref="IAsyncFuture{T}"/> with the <see cref="IAsyncFuture{T}.GetAsync"/> than can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target workflow parameters.
        /// These are checked at runtime but not while compiling.
        /// </para>
        /// <note>
        /// Any given <see cref="StartChildWorkflowStub{TWorkflowInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture<TResult>> StartAsync<TResult>(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            parentWorkflow.SetStackTrace();

            if (hasStarted)
            {
                throw new InvalidOperationException("Cannot start a stub more than once.");
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

            return new AsyncChildFuture<TResult>(parentWorkflow, execution, resultType);
        }

        /// <summary>
        /// Starts the target workflow that returns <c>void</c>, passing any specified arguments.
        /// </summary>
        /// <param name="args">The arguments to be passed to the workflow.</param>
        /// <returns>The <see cref="IAsyncFuture{T}"/> with the <see cref="IAsyncFuture{T}.GetAsync"/> than can be used to retrieve the workfow result.</returns>
        /// <exception cref="InvalidOperationException">Thrown when attempting to start a stub more than once.</exception>
        /// <remarks>
        /// <para>
        /// You must take care to pass parameters that are compatible with the target workflow parameters.
        /// These are checked at runtime but not while compiling.
        /// </para>
        /// <note>
        /// Any given <see cref="StartChildWorkflowStub{TWorkflowInterface}"/> may only be executed once.
        /// </note>
        /// </remarks>
        public async Task<IAsyncFuture> StartAsync(params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            parentWorkflow.SetStackTrace();

            if (hasStarted)
            {
                throw new InvalidOperationException("Cannot start a stub more than once.");
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

            return new AsyncChildFuture(parentWorkflow, execution);
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
