//-----------------------------------------------------------------------------
// FILE:	    Workflow.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;

namespace Neon.Cadence
{
    /// <inheritdoc/>
    public class Workflow : IWorkflow
    {
        /// <summary>
        /// The default workflow version returned by <see cref="IWorkflow.GetVersionAsync(string, int, int)"/> 
        /// when a version has not been set yet.
        /// </summary>
        public int DefaultVersion = -1;

        /// <inheritdoc/>
        public CadenceClient Client { get; private set; }

        /// <inheritdoc/>
        public WorkflowInfo WorkflowInfo => throw new NotImplementedException();

        /// <inheritdoc/>
        public DateTime UtcNow => throw new NotImplementedException();

        /// <inheritdoc/>
        public bool IsReplaying => throw new NotImplementedException();

        /// <inheritdoc/>
        public WorkflowExecution Execution => throw new NotImplementedException();

        /// <inheritdoc/>
        public Task ContinueAsNew(params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task ContinueAsNew(ContinueAsNewOptions options, params object[] args)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<int> GetVersionAsync(string changeId, int minSupported, int maxSupported)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<WorkflowExecution> GetWorkflowExecutionAsync(object childWorkflowStub)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<T> MutableSideEffectAsync<T>(string id, Func<T> function)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<object> MutableSideEffectAsync(string id, Type resultType, Func<object> function)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public TActivity NewActivityStub<TActivity>(ActivityOptions options = null) where TActivity : IActivityBase
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public TWorkflow NewChildWorkflowStub<TWorkflow>(ChildWorkflowOptions options = null) where TWorkflow : IWorkflowBase
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<TWorkflow> NewContinueAsNewStub<TWorkflow>(ContinueAsNewOptions options = null) where TWorkflow : IWorkflowBase
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public TWorkflow NewExternalWorkflowStub<TWorkflow>(WorkflowExecution execution, string domain = null) where TWorkflow : IWorkflowBase
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public TWorkflow NewExternalWorkflowStub<TWorkflow>(string workflowId, string domain = null) where TWorkflow : IWorkflowBase
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<Guid> NewGuidAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public TActivity NewLocalActivityStub<TActivity>(ActivityOptions options = null) where TActivity : IActivityBase
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IActivityStub NewUntypedActivityStub(ActivityOptions options = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IChildWorkflowStub NewUntypedChildWorkflowStub(string workflowTypeName, ChildWorkflowOptions options = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IExternalWorkflowStub NewUntypedExternalWorkflowStub(WorkflowExecution execution, string domain = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IExternalWorkflowStub NewUntypedExternalWorkflowStub(string workflowId, string domain = null)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<double> NextDouble()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<int> NextRandomAsync()
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<int> NextRandomAsync(int maxValue)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<int> NextRandomAsync(int minValue, int maxValue)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<T> SideEffectAsync<T>(Func<T> function)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<object> SideEffectAsync(Type resultType, Func<object> function)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task SleepAsync(TimeSpan duration)
        {
            throw new NotImplementedException();
        }
    }
}
