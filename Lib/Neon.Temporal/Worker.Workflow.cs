//-----------------------------------------------------------------------------
// FILE:	    Worker.Workflow.cs
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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    public sealed partial class Worker : IDisposable
    {
        //---------------------------------------------------------------------
        // Workflow related private types

        /// <summary>
        /// Enumerates the possible contexts workflow code may be executing within.
        /// This is used to limit what code can do (i.e. query methods shouldn't be
        /// allowed to execute activities).  This is also used in some situations to
        /// modify how workflow code behaves.
        /// </summary>
        internal enum WorkflowCallContext
        {
            /// <summary>
            /// The current task is not executing within the context
            /// of any workflow method.
            /// </summary>
            None = 0,

            /// <summary>
            /// The current task is executing within the context of
            /// a workflow entrypoint.
            /// </summary>
            Entrypoint,

            /// <summary>
            /// The current task is executing within the context of a
            /// workflow signal method.
            /// </summary>
            Signal,

            /// <summary>
            /// The current task is executing within the context of a
            /// workflow query method.
            /// </summary>
            Query,

            /// <summary>
            /// The current task is executing within the context of a
            /// normal or local activity.
            /// </summary>
            Activity
        }

        /// <summary>
        /// Describes the workflow implementation type, entry point method, and 
        /// signal/query methods for registered workflow.
        /// </summary>
        private class WorkflowRegistration
        {
            /// <summary>
            /// The workflow implemention type.
            /// </summary>
            public Type WorkflowType { get; set; }

            /// <summary>
            /// The workflow entry point method.
            /// </summary>
            public MethodInfo WorkflowMethod { get; set; }

            /// <summary>
            /// The workflow entry point parameter types.
            /// </summary>
            public Type[] WorkflowMethodParameterTypes { get; set; }

            /// <summary>
            /// Maps workflow signal and query names to the corresponding
            /// method implementations.
            /// </summary>
            public WorkflowMethodMap MethodMap { get; set; }
        }

        //---------------------------------------------------------------------
        // Implementation

        private List<Type>                                  registeredWorkflowTypes    = new List<Type>();
        private Dictionary<string, WorkflowRegistration>    nameToWorkflowRegistration = new Dictionary<string, WorkflowRegistration>();
        private Dictionary<long, WorkflowBase>              idToWorkflow               = new Dictionary<long, WorkflowBase>();

        /// <summary>
        /// Registers a workflow implementation.
        /// </summary>
        /// <param name="workflowType">The workflow implementation type.</param>
        private async Task RegisterWorkflowImplementationAsync(Type workflowType)
        {
            TemporalHelper.ValidateWorkflowImplementation(workflowType);

            var methodMap = WorkflowMethodMap.Create(workflowType);

            // We need to register each workflow method that implements a workflow interface method
            // with the same signature that that was tagged by [WorkflowMethod].
            //
            // First, we'll create a dictionary that maps method signatures from any inherited
            // interfaces that are tagged by [WorkflowMethod] to the attribute.

            var methodSignatureToAttribute = new Dictionary<string, WorkflowMethodAttribute>();

            foreach (var interfaceType in workflowType.GetInterfaces())
            {
                foreach (var method in interfaceType.GetMethods(BindingFlags.Public | BindingFlags.Instance))
                {
                    var workflowMethodAttribute = method.GetCustomAttribute<WorkflowMethodAttribute>();

                    if (workflowMethodAttribute == null)
                    {
                        continue;
                    }

                    var signature = method.ToString();

                    if (methodSignatureToAttribute.ContainsKey(signature))
                    {
                        throw new NotSupportedException($"Workflow type [{workflowType.FullName}] cannot implement the [{signature}] method from two different interfaces.");
                    }

                    methodSignatureToAttribute.Add(signature, workflowMethodAttribute);
                }
            }

            // Next, we need to register the workflow methods that implement the
            // workflow interface.

            foreach (var method in workflowType.GetMethods())
            {
                if (!methodSignatureToAttribute.TryGetValue(method.ToString(), out var workflowMethodAttribute))
                {
                    continue;
                }

                var workflowTypeName = TemporalHelper.GetWorkflowTypeName(workflowType, workflowMethodAttribute);

                if (nameToWorkflowRegistration.TryGetValue(workflowTypeName, out var existingRegistration))
                {
                    if (!object.ReferenceEquals(existingRegistration.WorkflowType, workflowType))
                    {
                        throw new InvalidOperationException($"Conflicting workflow interface registration: Workflow interface [{workflowType.FullName}] is already registered for workflow type name [{workflowTypeName}].");
                    }
                }
                else
                {
                    nameToWorkflowRegistration[workflowTypeName] =
                        new WorkflowRegistration()
                        {
                            WorkflowType                 = workflowType,
                            WorkflowMethod               = method,
                            WorkflowMethodParameterTypes = method.GetParameterTypes(),
                            MethodMap                    = methodMap
                        };
                }

                var reply = (WorkflowRegisterReply)await Client.CallProxyAsync(
                    new WorkflowRegisterRequest()
                    {
                        Name     = workflowTypeName,
                        WorkerId = WorkerId
                    });

                reply.ThrowOnError();
            }
        }
    }
}
