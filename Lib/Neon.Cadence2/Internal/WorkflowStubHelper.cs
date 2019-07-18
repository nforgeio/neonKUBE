//-----------------------------------------------------------------------------
// FILE:	    WorkflowStubHelper.cs
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

namespace Neon.Cadence
{
    /// <summary>
    /// Used to simplify the generation and implementation of typed
    /// workflow stubs.
    /// </summary>
    internal class WorkflowStubHelper
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Describes what a workflow interface method does.
        /// </summary>
        private enum WorkflowMethodType
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
            /// The method is a workflow entrypoint.
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
            public WorkflowMethodType Type { get; set; }

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
        }

        //---------------------------------------------------------------------
        // Implementation

        private CadenceClient                                   client;
        private Type                                            workflowInterface;
        private string                                          taskList;
        private WorkflowOptions                                 options;
        private string                                          domain;
        private WorkflowStub                                    untypedStub;
        private bool                                            hasStarted;
        private Dictionary<MethodBase, WorkflowMethodDetails>   methodToDetails;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="workflowInterface">The workflow interface definition.</param>
        /// <param name="execution">Optionally specifies the existing workflow execution.</param>
        /// <param name="taskList">Optionally specifies the target task list.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="domain">Optionally specifies the target domain.</param>
        public WorkflowStubHelper(CadenceClient client, Type workflowInterface, WorkflowExecution execution = null, string taskList = null, WorkflowOptions options = null, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Contract.Requires<ArgumentNullException>(workflowInterface != null);
            Contract.Requires<ArgumentException>(!workflowInterface.IsInterface, $"The [{workflowInterface.Name}] is not an interface.");
            Contract.Requires<ArgumentException>(!workflowInterface.IsGenericType, $"The [{workflowInterface.Name}] interface is generic.  This is not allowed.");

            if (string.IsNullOrEmpty(domain))
            {
                domain = null;
            }

            this.client            = client;
            this.workflowInterface = workflowInterface;
            this.taskList          = taskList;
            this.options           = options;
            this.domain            = domain ?? client.Settings.DefaultDomain;
            this.untypedStub       = new WorkflowStub() { Execution = execution, Options = options };
            this.hasStarted        = execution != null;
            this.methodToDetails   = new Dictionary<MethodBase, WorkflowMethodDetails>();

            // Scan the interface methods to identify those tagged to indicate 
            // that they are query, signal, or workflow methods and build a table
            // that maps these methods to the method type and also holds any options
            // specified by the tags.
            //
            // We're also going to ensure that all interface methods are tagged
            // as being a signal, query, or workflow method and that no method
            // is tagged more than once and that the interface has at least one
            // workflow method.
            //
            // Note this code will also ensure that all workflow interface methods
            // implement a task/async signature by returning a Task and also that
            // all signal and query methods have unique names.

            var signalNames   = new HashSet<string>();
            var queryNames    = new HashSet<string>();
            var methodDetails = new WorkflowMethodDetails();

            this.methodToDetails = new Dictionary<MethodBase, WorkflowMethodDetails>();

            foreach (var method in workflowInterface.GetMethods())
            {
                if (method.ReturnType.IsGenericType)
                {
                    if (method.ReturnType.BaseType != typeof(Task))
                    {
                        throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must return a Task.");
                    }
                }
                else
                {
                    if (method.ReturnType != typeof(Task))
                    {
                        throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] must return a Task.");
                    }
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

                    methodDetails.Type            = WorkflowMethodType.Signal;
                    methodDetails.SignalAttribute = signalAttribute;

                    methodToDetails.Add(method, methodDetails);
                }
                else if (queryAttributes.Length > 0)
                {
                    var queryAttribute = queryAttributes.First();

                    if (queryNames.Contains(queryAttribute.Name))
                    {
                        throw new ArgumentException($"Workflow interface method [{workflowInterface.FullName}.{method.Name}()] specifies [QueryMethod(Name = {queryAttribute.Name})] which conflicts with another signal method.");
                    }

                    queryNames.Add(queryAttribute.Name);

                    methodDetails.Type           = WorkflowMethodType.Query;
                    methodDetails.QueryAttribute = queryAttribute;

                    methodToDetails.Add(method, methodDetails);
                }
                else if (workflowAttributes.Length > 0)
                {
                    var workflowAttribute = workflowAttributes.First();

                    methodDetails.Type              = WorkflowMethodType.Workflow;
                    methodDetails.WorkflowAttribute = workflowAttribute;

                    methodToDetails.Add(method, methodDetails);
                }
                else
                {
                    Covenant.Assert(false); // We should never get here.
                }
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
    }
}
