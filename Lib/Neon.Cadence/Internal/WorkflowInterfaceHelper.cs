//-----------------------------------------------------------------------------
// FILE:	    WorkflowInterfaceHelper.cs
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
using System.Linq;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Used to simplify the generation and implementation of typed
    /// workflow stubs for a workflow interface.
    /// </summary>
    internal class WorkflowInterfaceHelper
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Describes what a workflow interface method does.
        /// </summary>
        private enum WorkflowMethodKind
        {
            /// <summary>
            /// The method implements a query.
            /// </summary>
            Query,

            /// <summary>
            /// The method implements a signal.
            /// </summary>
            Signal,

            /// <summary>
            /// The method is a workflow entry point.
            /// </summary>
            Workflow
        }

        /// <summary>
        /// Holds additional information about a workflow interface method.
        /// </summary>
        private class WorkflowMethodDetails
        {
            /// <summary>
            /// The workflow method type.
            /// </summary>
            public WorkflowMethodKind Kind { get; set; }

            /// <summary>
            /// The signal attributes for signal methods.
            /// </summary>
            public SignalMethodAttribute SignalAttribute { get; set; }

            /// <summary>
            /// The query attributes for query methods.
            /// </summary>
            public QueryMethodAttribute QueryAttribute { get; set; }

            /// <summary>
            /// The workflow attributes for workflow methods.
            /// </summary>
            public WorkflowMethodAttribute WorkflowAttribute { get; set; }

            /// <summary>
            /// The workflow result type, not including the wrapping <see cref="Task"/>.
            /// This will be <see cref="void"/> for methods that don't return a value.
            /// </summary>
            public Type ReturnType { get; set; }

            /// <summary>
            /// The low-level method information.
            /// </summary>
            public MethodInfo Method { get; set; }
        }

        //---------------------------------------------------------------------
        // Static members

        // This dictionary maps workflow interfaces to their dynamically generated
        // assemblies.

        private static Dictionary<Type, Assembly> workflowInterfaceToStub = new Dictionary<Type, Assembly>();

        //---------------------------------------------------------------------
        // Instance members

        private CadenceClient       client;
        private Type                workflowInterface;
        private string              taskList;
        private WorkflowOptions     options;
        private string              domain;
        private WorkflowStub        untypedStub;
        private bool                hasStarted;

        // Maps the workflow entry point, signal, or query methods to 
        // implementation details using the method's [MethodBase.ToString()]
        // value as the key.

        private Dictionary<string, WorkflowMethodDetails>   methodToDetails;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="workflowInterface">The workflow interface definition.</param>
        /// <param name="execution">Optionally specifies the existing workflow execution.</param>
        /// <param name="taskList">Optionally specifies the target task list.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="domain">Optionally specifies the target domain.</param>
        public WorkflowInterfaceHelper(CadenceClient client, Type workflowInterface, WorkflowExecution execution = null, string taskList = null, WorkflowOptions options = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Contract.Requires<ArgumentNullException>(workflowInterface != null);
            Contract.Requires<ArgumentException>(!workflowInterface.IsInterface, $"The [{workflowInterface.Name}] is not an interface.");
            Contract.Requires<ArgumentException>(!workflowInterface.IsGenericType, $"The [{workflowInterface.Name}] interface is generic.  This is not allowed.");

            options = options ?? new WorkflowOptions();

            if (string.IsNullOrEmpty(domain))
            {
                domain = null;
            }

            this.client            = client;
            this.workflowInterface = workflowInterface;
            this.options           = options;
            this.taskList          = taskList ?? client.Settings.DefaultTaskList;
            this.domain            = domain ?? client.Settings.DefaultDomain;
            this.untypedStub       = new WorkflowStub(client) { Execution = execution, Options = options };
            this.hasStarted        = execution != null;
            this.methodToDetails   = new Dictionary<string, WorkflowMethodDetails>();

            if (string.IsNullOrEmpty(this.taskList))
            {
                throw new ArgumentException("No Cadence task list was specified either explicitly or as the default in the client settings.");
            }

            if (string.IsNullOrEmpty(this.domain))
            {
                throw new ArgumentException("No Cadence domain was specified either explicitly or as the default in the client settings.");
            }

            // Scan the interface methods to identify those tagged to indicate 
            // that they are query, signal, or workflow methods and build a table
            // that maps these methods to the method type and also holds any options
            // specified by the tags.
            //
            // We're also going to ensure that all interface methods are tagged
            // as being a signal, query, or workflow method and that no method
            // is tagged more than once and finally, that the interface has at
            // least one workflow method.
            //
            // Note this code will also ensure that all workflow interface methods
            // implement a task/async signature by returning a Task and also that
            // all signal and query methods have unique names.

            var signalNames = new HashSet<string>();
            var queryNames  = new HashSet<string>();

            foreach (var method in workflowInterface.GetMethods())
            {
                var details = new WorkflowMethodDetails() { Method = method };

                if (method.ReturnType.IsGenericType)
                {
                    if (method.ReturnType.BaseType != typeof(Task))
                    {
                        throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must return a Task.");
                    }

                    details.ReturnType = typeof(void);
                }
                else
                {
                    if (method.ReturnType != typeof(Task))
                    {
                        throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must return a Task.");
                    }

                    details.ReturnType = method.ReturnType.GetGenericArguments().First();
                }

                var signalAttributes   = method.GetCustomAttributes<SignalMethodAttribute>().ToArray();
                var queryAttributes    = method.GetCustomAttributes<QueryMethodAttribute>().ToArray();
                var workflowAttributes = method.GetCustomAttributes<WorkflowMethodAttribute>().ToArray();
                var attributeCount     = signalAttributes.Length + queryAttributes.Length + workflowAttributes.Length;

                if (attributeCount == 0)
                {
                    throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must have one of these attributes: SignalMethod, QueryMethod, or WorkflowMethod");
                }
                else if (attributeCount > 1)
                {
                    throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] can have only one of these attributes: SignalMethod, QueryMethod, or WorkflowMethod");
                }

                if (signalAttributes.Length > 0)
                {
                    var signalAttribute = signalAttributes.First();

                    if (signalNames.Contains(signalAttribute.Name))
                    {
                        throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] specifies [SignalMethod(Name = {signalAttribute.Name})] which conflicts with another signal method.");
                    }

                    signalNames.Add(signalAttribute.Name);

                    details.Kind            = WorkflowMethodKind.Signal;
                    details.SignalAttribute = signalAttribute;
                }
                else if (queryAttributes.Length > 0)
                {
                    var queryAttribute = queryAttributes.First();

                    if (queryNames.Contains(queryAttribute.Name))
                    {
                        throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] specifies [QueryMethod(Name = {queryAttribute.Name})] which conflicts with another signal method.");
                    }

                    queryNames.Add(queryAttribute.Name);

                    details.Kind           = WorkflowMethodKind.Query;
                    details.QueryAttribute = queryAttribute;
                }
                else if (workflowAttributes.Length > 0)
                {
                    var workflowAttribute = workflowAttributes.First();

                    details.Kind              = WorkflowMethodKind.Workflow;
                    details.WorkflowAttribute = workflowAttribute;
                }
                else
                {
                    Covenant.Assert(false); // We should never get here.
                }

                methodToDetails.Add(method.ToString(), details);
            }
        }

        /// <summary>
        /// Indicates whether the workflow has already been started.
        /// </summary>
        /// <returns><c>true</c> if the workflow has been started.</returns>
        public bool HasStarted()
        {
            return hasStarted;
        }

        /// <summary>
        /// Returns the associated untyped <see cref="WorkflowStub"/>.
        /// </summary>
        /// <returns>The <see cref="WorkflowStub"/>.</returns>
        public WorkflowStub GetUntypedStub()
        {
            return untypedStub;
        }

        /// <summary>
        /// Executes the workflow method with the specified method signature, passing 
        /// the arguments.  This method will be used for executing workflows that 
        /// return <see cref="void"/>.
        /// </summary>
        /// <param name="methodSignature">The workflow method signature.</param>
        /// <param name="args">The method arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ExecuteAsync(string methodSignature, object[] args)
        {
            if (!methodToDetails.TryGetValue(methodSignature, out var details))
            {
                throw new KeyNotFoundException($"Cannot locate a workflow method with signature: {methodSignature}");
            }

            if (details.ReturnType != typeof(void))
            {
                throw new InvalidOperationException($"Cannot execute workflow method because the method returns a result when VOID is expected: {methodSignature}");
            }

            var execution = await untypedStub.StartAsync(args);

            await untypedStub.GetResultAsync(typeof(void));
        }

        /// <summary>
        /// Executes the workflow method with the specified method signature,
        /// passing the arguments and returning the workflow result.
        /// </summary>
        /// <param name="methodSignature">The workflow method signature.</param>
        /// <param name="args">The method arguments.</param>
        /// <returns>The method result.</returns>
        public async Task<object> ExecuteWithResultAsync(string methodSignature, object[] args)
        {
            if (!methodToDetails.TryGetValue(methodSignature, out var details))
            {
                throw new KeyNotFoundException($"Cannot locate a workflow method with signature: {methodSignature}");
            }

            if (details.ReturnType == typeof(void))
            {
                throw new InvalidOperationException($"Cannot execute workflow method because the method returns VOID when a result is expected: {methodSignature}");
            }

            var execution = await untypedStub.StartAsync(args);

            return await untypedStub.GetResultAsync(details.ReturnType);
        }
    }
}
