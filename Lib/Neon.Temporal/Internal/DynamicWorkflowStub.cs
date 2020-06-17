//-----------------------------------------------------------------------------
// FILE:	    DynamicWorkflowStub.cs
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

using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Common;

namespace Neon.Temporal.Internal
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
        private ConstructorInfo     externalStartConstructor;
        private ConstructorInfo     externalConstructor;
        private ConstructorInfo     childConstructor;
        private ConstructorInfo     childExecutedConstructor;
        private ConstructorInfo     continueConstructor;
        private ConstructorInfo     childExternalConstructor;
        private ConstructorInfo     childWorkflowIdConstructor;
        private MethodInfo          toUntypedAsync;

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

            this.stubType                   = assembly.GetType(className);
            this.externalStartConstructor   = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(string), typeof(WorkflowOptions), typeof(System.Type));
            this.externalConstructor        = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(string), typeof(string), typeof(string));
            this.childConstructor           = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(Workflow), typeof(string), typeof(ChildWorkflowOptions), typeof(System.Type));
            this.childExecutedConstructor   = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(Workflow), typeof(string), typeof(ChildExecution));
            this.childExternalConstructor   = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(Workflow), typeof(WorkflowExecution));
            this.childWorkflowIdConstructor = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(Workflow), typeof(string), typeof(string));
            this.continueConstructor        = NeonHelper.GetConstructor(stubType, typeof(TemporalClient), typeof(IDataConverter), typeof(string), typeof(ContinueAsNewOptions));
            this.toUntypedAsync             = NeonHelper.GetMethod(stubType, "ToUntypedAsync", Type.EmptyTypes);
        }

        /// <summary>
        /// Creates a workflow stub instance suitable for starting a new external workflow.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="workflowTypeName">Specifies the workflow type name.</param>
        /// <param name="options">Specifies the <see cref="WorkflowOptions"/> or <c>null</c>.</param>
        /// <param name="workflowInterface">Specifies the workflow interface definition.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(TemporalClient client, IDataConverter dataConverter, string workflowTypeName, WorkflowOptions options, System.Type workflowInterface)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(workflowInterface != null, nameof(workflowInterface));

            options = options ?? new WorkflowOptions();

            return externalStartConstructor.Invoke(new object[] { client, dataConverter, workflowTypeName, options, workflowInterface });
        }

        /// <summary>
        /// Creates a workflow stub instance suitable for connecting to an existing external workflow.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="workflowId">Specifies the workflow ID.</param>
        /// <param name="runId">Optionally specifies the workflow run ID.</param>
        /// <param name="namespace">Optionally specifies the namespace.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(TemporalClient client, IDataConverter dataConverter, string workflowId, string runId = null, string @namespace = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId), nameof(workflowId));

            return externalConstructor.Invoke(new object[] { client, dataConverter, workflowId, runId, @namespace });
        }

        /// <summary>
        /// Creates a workflow stub instance suitable for starting a new child workflow.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowTypeName">Specifies the workflow type name.</param>
        /// <param name="options">Specifies the child workflow options or <c>null</c>.</param>
        /// <param name="workflowInterface">Specifies the workflow interface definition.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(TemporalClient client, IDataConverter dataConverter, Workflow parentWorkflow, string workflowTypeName, ChildWorkflowOptions options, System.Type workflowInterface)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(options != null, nameof(options));
            Covenant.Requires<ArgumentNullException>(workflowInterface != null, nameof(workflowInterface));

            options = options ?? new ChildWorkflowOptions();

            return childConstructor.Invoke(new object[] { client, dataConverter, parentWorkflow, workflowTypeName, options, workflowInterface });
        }

        /// <summary>
        /// Creates a workflow stub instance mapping to an already started child workflow.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowTypeName">Specifies the workflow type name.</param>
        /// <param name="childExecution">The child execution information.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(TemporalClient client, IDataConverter dataConverter, Workflow parentWorkflow, string workflowTypeName, ChildExecution childExecution)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(childExecution != null, nameof(childExecution));

            return childExecutedConstructor.Invoke(new object[] { client, dataConverter, parentWorkflow, workflowTypeName, childExecution });
        }

        /// <summary>
        /// Creates a stub for an existing child workflow specified by a <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="execution">The <see cref="WorkflowExecution"/>.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(TemporalClient client, IDataConverter dataConverter, Workflow parentWorkflow, WorkflowExecution execution)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));

            return childExternalConstructor.Invoke(new object[] { client, dataConverter, parentWorkflow, execution });
        }

        /// <summary>
        /// Creates a stub for an existing child workflow specified by its workflow ID and optional namespace.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="parentWorkflow">The parent workflow.</param>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="namespace">Optionally overrides the default client namespace.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(TemporalClient client, IDataConverter dataConverter,  Workflow parentWorkflow, string workflowId, string @namespace = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            Covenant.Requires<ArgumentNullException>(parentWorkflow != null, nameof(parentWorkflow));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId), nameof(workflowId));

            return childWorkflowIdConstructor.Invoke(new object[] { client, dataConverter, parentWorkflow, workflowId, @namespace });
        }

        /// <summary>
        /// Creates a workflow stub instance suitable for continuing a workflow as new.
        /// </summary>
        /// <param name="client">The associated <see cref="TemporalClient"/>.</param>
        /// <param name="dataConverter">The data converter.</param>
        /// <param name="workflowTypeName">Specifies the workflow type name.</param>
        /// <param name="options">Optionally specifies the continuation options.</param>
        /// <returns>The workflow stub as an <see cref="object"/>.</returns>
        public object Create(TemporalClient client, IDataConverter dataConverter, string workflowTypeName, ContinueAsNewOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(dataConverter != null, nameof(dataConverter));
            Covenant.Requires<ArgumentNullException>(workflowTypeName != null, nameof(workflowTypeName));

            return continueConstructor.Invoke(new object[] { client, dataConverter, workflowTypeName, options });
        }

        /// <summary>
        /// <para>
        /// Creates a new untyped <see cref="WorkflowStub"/> from the dynamic stub.
        /// </para>
        /// <note>
        /// The workflow must have already been started via the stub.
        /// </note>
        /// </summary>
        /// <returns>The new <see cref="WorkflowStub"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if the stub passed is not external (e.g. it's a child stub).</exception>
        /// <exception cref="InvalidOperationException">Thrown if the stubbed workflow has not been started yet.</exception>
        public async Task<WorkflowStub> ToUntypedAsync()
        {
            return await (Task<WorkflowStub>)toUntypedAsync.Invoke(this, Array.Empty<object>());
        }
    }
}
