//-----------------------------------------------------------------------------
// FILE:	    CadenceClient.Workflow.cs
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
using System.Reflection;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Time;

namespace Neon.Cadence
{
    public partial class CadenceClient
    {
        //---------------------------------------------------------------------
        // Cadence workflow related operations.

        /// <inheritdoc/>
        public async Task RegisterWorkflowAsync<TWorkflow>(string workflowTypeName = null)
            where TWorkflow : WorkflowBase
        {
            if (string.IsNullOrEmpty(workflowTypeName))
            {
                workflowTypeName = workflowTypeName ?? typeof(TWorkflow).FullName;
            }

            if (workflowWorkerStarted)
            {
                throw new CadenceWorkflowWorkerStartedException();
            }

            if (!WorkflowBase.Register(this, typeof(TWorkflow), workflowTypeName))
            {
                var reply = (WorkflowRegisterReply)await CallProxyAsync(
                    new WorkflowRegisterRequest()
                    {
                        Name = workflowTypeName
                    });

                reply.ThrowOnError();
            }
        }

        /// <inheritdoc/>
        public async Task RegisterAssemblyWorkflowsAsync(Assembly assembly)
        {
            Covenant.Requires<ArgumentNullException>(assembly != null);

            if (workflowWorkerStarted)
            {
                throw new CadenceWorkflowWorkerStartedException();
            }

            foreach (var type in assembly.GetTypes())
            {
                var autoRegisterAttribute = type.GetCustomAttribute<AutoRegisterAttribute>();

                if (autoRegisterAttribute != null)
                {
                    if (type.IsSubclassOf(typeof(WorkflowBase)))
                    {
                        var workflowTypeName = autoRegisterAttribute.TypeName ?? type.FullName;

                        if (!WorkflowBase.Register(this, type, workflowTypeName))
                        {
                            var reply = (WorkflowRegisterReply)await CallProxyAsync(
                                new WorkflowRegisterRequest()
                                {
                                    Name = workflowTypeName
                                });

                            reply.ThrowOnError();
                        }
                    }
                    else if (type.IsSubclassOf(typeof(ActivityBase)))
                    {
                        // Ignore these here.
                    }
                    else
                    {
                        throw new TypeLoadException($"Type [{type.FullName}] is tagged by [{nameof(AutoRegisterAttribute)}] but is not derived from [{nameof(WorkflowBase)}].");
                    }
                }
            }
        }

        /// <inheritdoc/>
        public async Task<WorkflowExecution> StartWorkflowAsync<TWorkflow>(byte[] args = null, string taskList = DefaultTaskList, WorkflowOptions options = null)
            where TWorkflow : WorkflowBase
        {
            return await StartWorkflowAsync(typeof(TWorkflow).FullName, args, taskList, options);
        }

        /// <inheritdoc/>
        public async Task<WorkflowExecution> StartWorkflowAsync(string workflowTypeName, byte[] args = null, string taskList = DefaultTaskList, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));

            options = options ?? new WorkflowOptions();

            var reply = (WorkflowExecuteReply)await CallProxyAsync(
                new WorkflowExecuteRequest()
                {
                    Workflow = workflowTypeName,
                    Args     = args,
                    Options  = options.ToInternal(taskList)
                });

            reply.ThrowOnError();

            var execution = reply.Execution;

            return new WorkflowExecution(execution.ID, execution.RunID);
        }

        /// <inheritdoc/>
        public async Task<WorkflowDescription> DescribeWorkflowExecutionAsync(string domain, WorkflowExecution workflowExecution)
        {
            Covenant.Requires<ArgumentNullException>(workflowExecution != null);

            var reply = (WorkflowDescribeExecutionReply)await CallProxyAsync(
                new WorkflowDescribeExecutionRequest()
                {
                    WorkflowId = workflowExecution.WorkflowId,
                    RunId      = workflowExecution.RunId ?? string.Empty
                });

            reply.ThrowOnError();

            return reply.Details.ToPublic();
        }

        /// <inheritdoc/>
        public async Task<byte[]> GetWorkflowResultAsync(WorkflowExecution workflowExecution)
        {
            Covenant.Requires<ArgumentNullException>(workflowExecution != null);

            var reply = (WorkflowGetResultReply)await CallProxyAsync(
                new WorkflowGetResultRequest()
                {
                    WorkflowId = workflowExecution.WorkflowId,
                    RunId      = workflowExecution.RunId ?? string.Empty
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <inheritdoc/>
        public async Task CancelWorkflowExecution(string domain, WorkflowExecution workflowExecution)
        {
            Covenant.Requires<ArgumentNullException>(workflowExecution != null);

            var reply = (WorkflowCancelReply)await CallProxyAsync(
                new WorkflowCancelRequest()
                {
                    WorkflowId = workflowExecution.WorkflowId,
                    RunId      = workflowExecution.RunId ?? string.Empty
                });

            reply.ThrowOnError();
        }

        /// <inheritdoc/>
        public async Task TerminateWorkflowAsync(string domain, WorkflowExecution workflowExecution, string reason = null, byte[] details = null)
        {
            Covenant.Requires<ArgumentNullException>(workflowExecution != null);

            var reply = (WorkflowTerminateReply)await CallProxyAsync(
                new WorkflowTerminateRequest()
                {
                    WorkflowId = workflowExecution.WorkflowId,
                    RunId      = workflowExecution.RunId ?? string.Empty,
                    Reason     = reason,
                    Details    = details
                });

            reply.ThrowOnError();
        }

        /// <inheritdoc/>
        public async Task<byte[]> CallWorkflowAsync<TWorkflow>(byte[] args = null, string domain = null, string taskList = DefaultTaskList, WorkflowOptions options = null)
            where TWorkflow : WorkflowBase
        {
            return await CallWorkflowAsync(typeof(TWorkflow).FullName, args, domain ?? Settings.DefaultDomain, taskList, options);
        }

        /// <inheritdoc/>
        public async Task<byte[]> CallWorkflowAsync(string workflowTypeName, byte[] args = null, string domain = null, string taskList = DefaultTaskList, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));

            options = options ?? new WorkflowOptions();

            var reply = (WorkflowExecuteReply)await CallProxyAsync(
                new WorkflowExecuteRequest()
                {
                    Workflow = workflowTypeName,
                    Args     = args,
                    Options  = options.ToInternal(taskList)
                });

            reply.ThrowOnError();

            var execution         = reply.Execution;
            var workflowExecution = new WorkflowExecution(execution.ID, execution.RunID);

            return await GetWorkflowResultAsync(workflowExecution);
        }

        /// <inheritdoc/>
        public async Task SignalWorkflowAsync(string workflowId, string signalName, byte[] signalArgs = null, string runId = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            var reply = (WorkflowSignalReply)await CallProxyAsync(
                new WorkflowSignalRequest()
                {
                    WorkflowId = workflowId,
                    SignalName = signalName,
                    SignalArgs = signalArgs,
                    RunId      = runId ?? string.Empty
                });

            reply.ThrowOnError();
        }

        /// <inheritdoc/>
        public async Task SignalWorkflowAsync(WorkflowExecution workflowExecution, string signalName, byte[] signalArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(workflowExecution != null);

            var reply = (WorkflowSignalReply)await CallProxyAsync(
                new WorkflowSignalRequest()
                {
                    WorkflowId = workflowExecution.WorkflowId,
                    SignalName = signalName,
                    SignalArgs = signalArgs,
                    RunId      = workflowExecution.RunId ?? string.Empty
                });

            reply.ThrowOnError();
        }

        /// <inheritdoc/>
        public async Task SignalWorkflowWithStartAsync(string domain, string workflowId, string signalName, byte[] signalArgs = null, byte[] workflowArgs = null, string taskList = DefaultTaskList, WorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));

            options = options ?? new WorkflowOptions();

            var reply = (WorkflowSignalWithStartReply)await CallProxyAsync(
                new WorkflowSignalWithStartRequest()
                {
                    WorkflowId   = workflowId,
                    Options      = options.ToInternal(taskList),
                    SignalName   = signalName,
                    SignalArgs   = signalArgs,
                    WorkflowArgs = workflowArgs
                });

            reply.ThrowOnError();
        }

        /// <inheritdoc/>
        public async Task<byte[]> QueryWorkflowAsync(string domain, WorkflowExecution workflowExecution, string queryName, byte[] queryArgs = null)
        {
            Covenant.Requires<ArgumentNullException>(workflowExecution != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(queryName));

            var reply = (WorkflowQueryReply)await CallProxyAsync(
                new WorkflowQueryRequest()
                {
                    WorkflowId = workflowExecution.WorkflowId,
                    QueryName  = queryName,
                    QueryArgs  = queryArgs,
                    RunId      = workflowExecution.RunId ?? string.Empty
                });

            reply.ThrowOnError();

            return reply.Result;
        }

        /// <inheritdoc/>
        public async Task SetWorkflowCacheSizeAsync(int maxCacheSize)
        {
            Covenant.Requires<ArgumentNullException>(maxCacheSize >= 0);

            var reply = (WorkflowSetCacheSizeReply)await CallProxyAsync(
                new WorkflowSetCacheSizeRequest()
                {
                    Size = maxCacheSize
                });

            reply.ThrowOnError();

            workflowCacheSize = maxCacheSize;
        }

        /// <inheritdoc/>
        public async Task<int> GetworkflowCacheSizeAsync()
        {
            return await Task.FromResult(workflowCacheSize);
        }
    }
}
