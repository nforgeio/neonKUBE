//-----------------------------------------------------------------------------
// FILE:	    DynamicWorkflowStub.cs
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
using System.IO;
using System.Reflection;
using System.Runtime;
using System.Runtime.Loader;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Manages a dynamically generated type safe workflow stub class for a workflow interface.
    /// </summary>
    internal class DynamicWorkflowStub
    {
        private Type                workflowInterface;
        private string              className;
        private Assembly            assembly;
        private Type                stubType;
        private ConstructorInfo     startConstructor;
        private ConstructorInfo     childConstructor;
        private MethodInfo          toUntyped;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="workflowInterface">Specifies the workflow interface.</param>
        /// <param name="assembly">The assembly holding the generated stub class.</param>
        /// <param name="className">The fully qualified stub class name.</param>
        public DynamicWorkflowStub(Type workflowInterface, Assembly assembly, string className)
        {
            this.workflowInterface = workflowInterface;
            this.assembly          = assembly;
            this.className         = className;

            // Fetch the stub type and reflect the required constructors and methods.

            this.stubType         = assembly.GetType(className);
            this.startConstructor = NeonHelper.GetConstructor(stubType, typeof(CadenceClient), typeof(IDataConverter), typeof(string), typeof(WorkflowOptions));
            this.childConstructor = NeonHelper.GetConstructor(stubType, typeof(CadenceClient), typeof(IDataConverter), typeof(Workflow), typeof(string), typeof(ChildWorkflowOptions));
            this.toUntyped        = NeonHelper.GetMethod(stubType, "ToUntyped", Type.EmptyTypes);
        }

        /// <summary>
        /// Creates a workflow stub instance suitable for starting a new external workflow.
        /// </summary>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="workflowTypeName">Specifies the workflow type name.</param>
        /// <param name="options">Specifies the <see cref="WorkflowOptions"/>.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(CadenceClient client, IDataConverter dataConverter, string workflowTypeName, WorkflowOptions options)
        {
            return startConstructor.Invoke(new object[] { client, dataConverter, workflowTypeName, options });
        }

        /// <summary>
        /// Creates a workflow stub instance suitable for starting a new child workflow.
        /// </summary>
        /// <param name="client">The associated <see cref="CadenceClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowTypeName">Specifies the workflow type name.</param>
        /// <param name="options">Specifies the child workflow options.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(CadenceClient client, IDataConverter dataConverter, Workflow parentWorkflow, string workflowTypeName, ChildWorkflowOptions options)
        {
            return childConstructor.Invoke(new object[] { client, dataConverter, parentWorkflow, workflowTypeName, options });
        }

        /// <summary>
        /// Creates a new untyped <see cref="WorkflowStub"/> from the dynamic stub.
        /// </summary>
        /// <returns>The new <see cref="WorkflowStub"/>.</returns>
        public WorkflowStub ToUntyped()
        {
            return (WorkflowStub)toUntyped.Invoke(this, Type.EmptyTypes);
        }
    }
}
