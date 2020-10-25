//-----------------------------------------------------------------------------
// FILE:	    Workflow.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Threading;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// Provides useful information and functionality for workflow implementations.
    /// This will be available via the <see cref="WorkflowBase.Workflow"/> property.
    /// </summary>
    public class Workflow
    {
        /// <summary>
        /// The default workflow version returned by <see cref="GetVersionAsync(string, int, int)"/> 
        /// when a version has not been set yet.
        /// </summary>
        public const int DefaultVersion = -1;

        //---------------------------------------------------------------------
        // Static members

        private static AsyncLocal<Workflow> currentWorkflow = new AsyncLocal<Workflow>();

        /// <summary>
        /// Returns the <see cref="Workflow"/> information for the worflow executing within the
        /// current asynchronous flow or <c>null</c> if the current code is not executing within
        /// the context of a workflow.  This property use an internal <see cref="AsyncLocal{T}"/>
        /// to manage this state.
        /// </summary>
        public static Workflow Current
        {
            get => currentWorkflow.Value;
            internal set => currentWorkflow.Value = value;
        }

        //---------------------------------------------------------------------
        // Instance members

        private readonly object             syncLock = new object();
        private string                      workflowId;
        private int                         pendingOperationCount;
        private Dictionary<string, string>  pendingOperationStackTraces;
        private long                        nextLocalActivityActionId;
        private long                        nextActivityId;
        private long                        nextQueueId;
        private Random                      random;
        private bool                        isReplaying;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="parent">The parent workflow instance.</param>
        /// <param name="client">The associated client.</param>
        /// <param name="contextId">The workflow's context ID.</param>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="domain">The hosting domain.</param>
        /// <param name="taskList">The hosting task list.</param>
        /// <param name="workflowId">The workflow ID.</param>
        /// <param name="runId">The current workflow run ID.</param>
        /// <param name="isReplaying">Indicates whether the workflow is currently replaying from histor.</param>
        /// <param name="methodMap">Maps the workflow signal and query methods.</param>
        internal Workflow(
            WorkflowBase        parent,
            CadenceClient       client, 
            long                contextId, 
            string              workflowTypeName, 
            string              domain, 
            string              taskList,
            string              workflowId, 
            string              runId, 
            bool                isReplaying, 
            WorkflowMethodMap   methodMap)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain), nameof(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskList), nameof(taskList));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId), nameof(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId), nameof(runId));

            this.WorkflowBase              = parent;
            this.workflowId                = workflowId;
            this.ContextId                 = contextId;
            this.pendingOperationCount     = 0;
            this.nextLocalActivityActionId = 0;
            this.nextActivityId            = 0;
            this.nextQueueId               = 0;
            this.IdToLocalActivityAction   = new Dictionary<long, LocalActivityAction>();
            this.MethodMap                 = methodMap;
            this.Client                    = client;
            this.IsReplaying               = isReplaying;
            this.Execution                 = new WorkflowExecution(workflowId, runId);
            this.Logger                    = LogManager.Default.GetLogger(module: workflowTypeName, contextId: runId, () => !IsReplaying || Client.Settings.LogDuringReplay);

            if (client.Settings.Debug)
            {
                pendingOperationStackTraces = new Dictionary<string, string>();
            }

            // Initialize the random number generator with a fairly unique
            // seed for the workflow without consuming entropy to obtain
            // a cryptographically random number.
            //
            // Note that we can use a new seed every time the workflow is
            // invoked because the actual random numbers returned by the
            // methods below will be recorded and replayed from history.

            this.random = new Random(Environment.TickCount ^ (int)DateTime.Now.Ticks);

            // Initialize the workflow information.

            this.WorkflowInfo = new WorkflowInfo()
            {
                WorkflowType = workflowTypeName,
                Domain       = domain,
                TaskList     = taskList,
                WorkflowId   = workflowId,
                RunId        = runId,

                // $todo(jefflill): We need to initialize these from somewhere.
                //
                // ExecutionStartToCloseTimeout
                // ChildPolicy 
            };
        }

        /// <summary>
        /// Returns the parent <see cref="Cadence.WorkflowBase"/> implementation.
        /// </summary>
        internal WorkflowBase WorkflowBase { get; private set; }

        /// <summary>
        /// Returns the workflow's context ID.
        /// </summary>
        internal long ContextId { get; private set; }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this workflow.
        /// </summary>
        public CadenceClient Client { get; set; }

        /// <summary>
        /// Returns the logger to be used for logging workflow related events.
        /// </summary>
        public INeonLogger Logger { get; private set; }

        /// <summary>
        /// Returns information about the running workflow.
        /// </summary>
        public WorkflowInfo WorkflowInfo { get; set; }

        /// <summary>
        /// Returns the workflow types method map.
        /// </summary>
        internal WorkflowMethodMap MethodMap { get; private set; }

        /// <summary>
        /// Returns the dictionary mapping the IDs to local activity actions
        /// (the target activity type and method).
        /// </summary>
        internal Dictionary<long, LocalActivityAction> IdToLocalActivityAction { get; private set; }

        /// <summary>
        /// Returns the unique ID of the signal being called on the current task.
        /// </summary>
        internal string SignalId { get; set; }

        /// <summary>
        /// Returns the next available workflow local activity ID.
        /// </summary>
        /// <returns>The nextr ID.</returns>
        internal long GetNextActivityId()
        {
            return Interlocked.Increment(ref nextActivityId);
        }

        /// <summary>
        /// <para>
        /// Indicates whether the workflow code is being replayed.
        /// </para>
        /// <note>
        /// <b>WARNING:</b> Never have workflow logic depend on this flag as doing so will
        /// break determinism.  The only reasonable uses for this flag are for managing
        /// external things like logging or metric reporting.
        /// </note>
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public bool IsReplaying
        {
            get
            {
                Client.EnsureNotDisposed();
                WorkflowBase.CheckCallContext(allowWorkflow: true);

                return isReplaying;
            }

            set => isReplaying = value;
        }

        /// <summary>
        /// Returns the execution information for the current workflow.
        /// </summary>
        public WorkflowExecution Execution { get; internal set; }

        /// <summary>
        /// Returns the <see cref="SyncSignalStatus"/> information for the specified
        /// signal ID, adding a status record if one doesn't already exist.
        /// </summary>
        /// <param name="signalId">The unique signal ID.</param>
        /// <returns>The <see cref="SyncSignalStatus"/> for the signal.</returns>
        internal SyncSignalStatus GetSignalStatus(string signalId)
        {
            return WorkflowBase.GetSignalStatus(ContextId, signalId);
        }

        /// <summary>
        /// Handles saving the current stack trace to the parent <see cref="WorkflowBase.StackTrace"/>
        /// property so this will be available for the internal stack trace query.
        /// </summary>
        /// <param name="skipFrames">
        /// The number of frames to skip.  This defaults to 2 such that this method's
        /// stack frame will be skipped along with the caller (presumably one the public
        /// methods in this class.
        /// </param>
        internal void SetStackTrace(int skipFrames = 2)
        {
            WorkflowBase.StackTrace = new StackTrace(skipFrames, fNeedFileInfo: true);
        }

        /// <summary>
        /// Executes a Cadence workflow related operation, trying to detect
        /// when an attempt is made to perform more than one operation in 
        /// parallel, which will likely break workflow determinism.
        /// </summary>
        /// <typeparam name="TResult">The operation result type.</typeparam>
        /// <param name="actionAsync">The workflow action function.</param>
        /// <returns>The action result.</returns>
        /// <remarks>
        /// <note>
        /// This method performs the parallel check only when executing within
        /// the context of a workflow entry point method.
        /// </note>
        /// </remarks>
        internal async Task<TResult> ExecuteNonParallel<TResult>(Func<Task<TResult>> actionAsync)
        {
            var debugMode = Client.Settings.Debug;
            
            if (WorkflowBase.CallContext.Value == WorkflowBase.WorkflowCallContext.Entrypoint)
            {
                // Workflow entry points are restricted to one operation at a time.

                var operationId = string.Empty;

                if (debugMode)
                {
                    operationId = Guid.NewGuid().ToString("d");
                }

                try
                {
                    lock (syncLock)
                    {
                        if (pendingOperationCount > 0)
                        {
                            if (debugMode)
                            {
                                throw new WorkflowParallelOperationException(pendingOperationStackTraces.Values.ToArray());
                            }
                            else
                            {
                                throw new WorkflowParallelOperationException();
                            }
                        }

                        if (debugMode)
                        {
                            pendingOperationStackTraces.Add(operationId, new StackTrace(skipFrames: 1, fNeedFileInfo: true).ToString());
                        }

                        pendingOperationCount++;
                    }

                    return await actionAsync();
                }
                finally
                {
                    lock (syncLock)
                    {
                        if (debugMode)
                        {
                            lock (pendingOperationStackTraces)
                            {
                                if (pendingOperationStackTraces.ContainsKey(operationId))
                                {
                                    pendingOperationStackTraces.Remove(operationId);
                                }
                            }
                        }

                        pendingOperationCount--;
                    }
                }
            }
            else
            {
                // $note(jefflill):
                //
                // We're not going to check for parallel execution for query or
                // signal methods.  There isn't any actually restriction for queries
                // because they don't impact the workflow state.  Technically,
                // signals should be restricted to a single operation, but we're
                // not going to check that for simplicitly and because the only
                // operation a signal can do is write to a [WorkflowQueue], so
                // the chances of doing something stupid is pretty low.
                //
                // In theory, we could address this by adding another pending
                // operation counter just for signals.

                return await actionAsync();
            }
        }

        /// <summary>
        /// Updates the workflow's <see cref="IsReplaying"/> state to match the
        /// state specified in the reply from cadence-proxy.
        /// </summary>
        /// <typeparam name="TReply">The reply message type.</typeparam>
        /// <param name="reply">The reply message.</param>
        internal void UpdateReplay<TReply>(TReply reply)
            where TReply : WorkflowReply
        {
            switch (reply.ReplayStatus)
            {
                case InternalReplayStatus.NotReplaying:

                    IsReplaying = false;
                    break;

                case InternalReplayStatus.Replaying:

                    IsReplaying = true;
                    break;
            }
        }

        /// <summary>
        /// <para>
        /// Returns the current workflow time (UTC).
        /// </para>
        /// <note>
        /// This must used instead of calling <see cref="DateTime.UtcNow"/> or any other
        /// time method to guarantee determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns>The current workflow time.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public async Task<DateTime> UtcNowAsync()
        {
            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowGetTimeReply)await Client.CallProxyAsync(
                        new WorkflowGetTimeRequest()
                        {
                            ContextId = ContextId
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return reply.Time;
        }

        /// <summary>
        /// Continues the current workflow as a new run using the same workflow options.
        /// </summary>
        /// <param name="args">The new run arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public async Task ContinueAsNewAsync(params object[] args)
        {
            // This method doesn't currently do any async operations but I'd
            // like to keep the method signature async just in case this changes
            // in the future.

            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            await Task.CompletedTask;

            // We're going to throw a [CadenceWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new ContinueAsNewException(
                args:       CadenceHelper.ArgsToBytes(Client.DataConverter, args),
                workflow:   WorkflowInfo.WorkflowType,
                domain:     WorkflowInfo.Domain,
                taskList:   WorkflowInfo.TaskList);
        }

        /// <summary>
        /// Continues the current workflow as a new run allowing the specification of
        /// new workflow options.
        /// </summary>
        /// <param name="options">The continuation options.</param>
        /// <param name="args">The new run arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public async Task ContinueAsNewAsync(ContinueAsNewOptions options, params object[] args)
        {
            await SyncContext.ClearAsync;

            // This method doesn't currently do any async operations but I'd
            // like to keep the method signature async just in case this changes
            // in the future.

            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            await Task.CompletedTask;

            // We're going to throw a [CadenceWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new ContinueAsNewException(
                args:                       CadenceHelper.ArgsToBytes(Client.DataConverter, args),
                domain:                     options.Domain ?? WorkflowInfo.Domain,
                taskList:                   options.TaskList ?? WorkflowInfo.TaskList,
                workflow:                   options.Workflow ?? WorkflowInfo.WorkflowType,
                startToCloseTimeout:    options.ExecutionStartToCloseTimeout,
                scheduleToCloseTimeout:     options.ScheduleToCloseTimeout,
                scheduleToStartTimeout:     options.ScheduleToStartTimeout,
                decisionTaskTimeout:    options.TaskStartToCloseTimeout,
                retryOptions:               options.RetryOptions);
        }

        /// <summary>
        /// Used to implement backwards compatible changes to a workflow implementation.
        /// </summary>
        /// <param name="changeId">Identifies the change.</param>
        /// <param name="minSupported">
        /// Specifies the minimum supported version.  You may pass <see cref="Workflow.DefaultVersion"/> <b>(-1)</b>
        /// which will be set as the version for workflows that haven't been versioned yet.
        /// </param>
        /// <param name="maxSupported">Specifies the maximum supported version.</param>
        /// <returns>The workflow implementation version.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// It is possible to upgrade workflow implementation with workflows in flight using
        /// the <see cref="GetVersionAsync(string, int, int)"/> method.  The essential requirement
        /// is that the new implementation must execute the same logic for the decision steps
        /// that have already been executed and recorded to the history fo a previous workflow 
        /// to maintain workflow determinism.  Subsequent unexecuted steps, are free to implement
        /// different logic.
        /// </para>
        /// <note>
        /// Cadence attempts to detect when replaying workflow performs actions that are different
        /// from those recorded as history and will fail the workflow when this occurs.
        /// </note>
        /// <para>
        /// Upgraded workflows will use <see cref="GetVersionAsync(string, int, int)"/> to indicate
        /// where upgraded logic has been inserted into the workflow.  You'll pass a <b>changeId</b>
        /// string that identifies the change being made.  This can be anything you wish as long as
        /// it's not empty and is unique for each change made to the workflow.  You'll also pass
        /// <b>minSupported</b> and <b>maxSupported</b> integers.  <b>minSupported</b> specifies the 
        /// minimum version of the workflow implementation that will be allowed to continue to
        /// run.  Workflows start out with their version set to <see cref="Workflow.DefaultVersion"/>
        /// or <b>(-1)</b> and this will often be passed as <b>minSupported</b> such that upgraded
        /// workflow implementations will be able to take over newly scheduled workflows.  
        /// <b>maxSupported</b> essentially specifies the current (latest) version of the workflow 
        /// implementation. 
        /// </para>
        /// <para>
        /// When <see cref="GetVersionAsync(string, int, int)"/> called and is not being replayed
        /// from the workflow history, the method will record the <b>changeId</b> and <b>maxSupported</b>
        /// values to the workflow history.  When this is being replayed, the method will simply
        /// return the <b>maxSupported</b> value from the history.  Let's go through an example demonstrating
        /// how this can be used.  Let's say we start out with a simple two step workflow that 
        /// first calls <b>ActivityA</b> and then calls <b>ActivityB</b>:
        /// </para>
        /// <code lang="C#">
        /// public class MyWorkflow : WorkflowBase
        /// {
        ///     public async Task DoSomething()
        ///     {
        ///         var activities = Workflow.NewActivityStub&lt;MyActivities&gt;();
        /// 
        ///         await activities.ActivityAAsync();  
        ///         await activities.ActivityBAsync();  
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Now, let's assume that we need to replace the call to <b>ActivityA</b> with a call to
        /// <b>ActivityC</b>.  If there is no chance of any instances of <B>MyWorkflow</B> still
        /// being in flight, you could simply redepoy the recoded workflow:
        /// </para>
        /// <code lang="C#">
        /// public class MyWorkflow : WorkflowBase
        /// {
        ///     public async Task&lt;byte[]&gt; RunAsync(byte[] args)
        ///     {
        ///         var activities = Workflow.NewActivityStub&lt;MyActivities&gt;();
        /// 
        ///         await activities.ActivityCAsync();  
        ///         await activities.ActivityBAsync();
        ///     }
        /// }
        /// </code>
        /// <para>
        /// But, if instances of this workflow may be in flight you'll need to deploy a backwards
        /// compatible workflow implementation that handles workflows that have already executed 
        /// <b>ActivityA</b> but haven't yet executed <b>ActivityB</b>.  You can accomplish this
        /// via:
        /// </para>
        /// <code lang="C#">
        /// public class MyWorkflow : WorkflowBase
        /// {
        ///     public async Task&lt;byte[]&gt; RunAsync(byte[] args)
        ///     {
        ///         var activities = Workflow.NewActivityStub&lt;MyActivities&gt;();
        ///         var version    = await GetVersionAsync("Replace ActivityA", DefaultVersion, 1);    
        /// 
        ///         switch (version)
        ///         {
        ///             case DefaultVersion:
        ///             
        ///                 await activities.ActivityAAsync();  
        ///                 break;
        ///                 
        ///             case 1:
        ///             
        ///                 await activities.ActivityCAsync();  // &lt;-- change
        ///                 break;
        ///         }
        ///         
        ///         await activities.ActivityBAsync();  
        ///     }
        /// }
        /// </code>
        /// <para>
        /// This upgraded workflow calls <see cref="GetVersionAsync(string, int, int)"/> passing
        /// <b>minSupported=DefaultVersion</b> and <b>maxSupported=1</b>  For workflow instances
        /// that have already executed <b>ActivityA</b>, <see cref="GetVersionAsync(string, int, int)"/>
        /// will return <see cref="Workflow.DefaultVersion"/> and we'll call <b>ActivityA</b>, which will match
        /// what was recorded in the history.  For workflows that have not yet executed <b>ActivityA</b>,
        /// <see cref="GetVersionAsync(string, int, int)"/> will return <b>1</b>, which we'll use as
        /// the indication that we can call <b>ActivityC</b>.
        /// </para>
        /// <para>
        /// Now, lets say we need to upgrade the workflow again and change the call for <b>ActivityB</b>
        /// to <b>ActivityD</b>, but only for workflows that have also executed <b>ActivityC</b>.  This 
        /// would look something like:
        /// </para>
        /// <code lang="C#">
        /// public class MyWorkflow : WorkflowBase
        /// {
        ///     public async Task&lt;byte[]&gt; RunAsync(byte[] args)
        ///     {
        ///         var activities = Workflow.NewActivityStub&lt;MyActivities&gt;();
        ///         var version    = await GetVersionAsync("Replace ActivityA", DefaultVersion, 1);    
        /// 
        ///         switch (version)
        ///         {
        ///             case DefaultVersion:
        ///             
        ///                 await activities.ActivityAAsync();  
        ///                 break;
        ///                 
        ///             case 1:
        ///             
        ///                 await activities.ActivityCAsync();  // &lt;-- change
        ///                 break;
        ///         }
        ///         
        ///         version = await GetVersionAsync("Replace ActivityB", 1, 2);    
        /// 
        ///         switch (version)
        ///         {
        ///             case DefaultVersion:
        ///             case 1:
        ///             
        ///                 await activities.ActivityBAsync();
        ///                 break;
        ///                 
        ///             case 2:
        ///             
        ///                 await activities.ActivityDAsync();  // &lt;-- change
        ///                 break;
        ///         }
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Notice that the second <see cref="GetVersionAsync(string, int, int)"/> call passed a different
        /// change ID and also that the version range is now <b>1..2</b>.  The version returned will be
        /// <see cref="Workflow.DefaultVersion"/> or <b>1</b> if <b>ActivityA</b> and <b>ActivityB</b> were 
        /// recorded in the history or <b>2</b> if <b>ActivityC</b> was called.
        /// </para>
        /// </remarks>
        public async Task<int> GetVersionAsync(string changeId, int minSupported, int maxSupported)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(changeId), nameof(changeId));
            Covenant.Requires<ArgumentException>(minSupported <= maxSupported, nameof(minSupported));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowGetVersionReply)await Client.CallProxyAsync(
                        new WorkflowGetVersionRequest()
                        {
                            ContextId    = this.ContextId,
                            ChangeId     = changeId,
                            MinSupported = minSupported,
                            MaxSupported = maxSupported
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return reply.Version;
        }

        /// <summary>
        /// Returns the <see cref="WorkflowExecution"/> for a child workflow created via
        /// <see cref="NewChildWorkflowStub{TWorkflowInterface}(ChildWorkflowOptions, string)"/>
        /// or <see cref="NewExternalWorkflowStub(string, string)"/>.
        /// </summary>
        /// <param name="stub">The child workflow stub.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the stub has not been started.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public async Task<WorkflowExecution> GetWorkflowExecutionAsync(object stub)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(stub != null, nameof(stub));
            Covenant.Requires<ArgumentException>(stub is ITypedWorkflowStub, nameof(stub), "The parameter is not a workflow stub.");
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return await ((ITypedWorkflowStub)stub).GetExecutionAsync();
        }

        /// <summary>
        /// Calls the specified function and then searches the workflow history
        /// to see if a value was already recorded with the specified <paramref name="id"/>.
        /// If no value has been recorded for the <paramref name="id"/> or the
        /// value returned by the function will be recorded, replacing any existing
        /// value.  If the function value is the same as the history value, then
        /// nothing will be recorded.
        /// </summary>
        /// <typeparam name="T">Specifies the result type.</typeparam>
        /// <param name="id">Identifies the value in the workflow history.</param>
        /// <param name="function">The side effect function.</param>
        /// <returns>The latest value persisted to the workflow history.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// This is similar to what you could do with a local activity but is
        /// a bit easier since you don't need to declare the activity and create
        /// a stub to call it and it's also more efficient because it avoids
        /// recording the same value multiple times in the history.
        /// </para>
        /// <note>
        /// The function must return within the configured decision task timeout 
        /// and should avoid throwing exceptions.
        /// </note>
        /// <note>
        /// The function passed should avoid throwing exceptions.  When an exception
        /// is thrown, this method will catch it and simply return the default 
        /// value for <typeparamref name="T"/>.
        /// </note>
        /// <note>
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time 
        /// <see cref="MutableSideEffectAsync{T}(string, Func{T})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        public async Task<T> MutableSideEffectAsync<T>(string id, Func<T> function)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id), nameof(id));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            T value;

            try
            {
                value = function();
            }
            catch
            {
                value = default(T);
            }

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowMutableReply)await Client.CallProxyAsync(
                        new WorkflowMutableRequest()
                        {
                            ContextId = this.ContextId,
                            MutableId = id,
                            Result    = Client.DataConverter.ToData(value)
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData<T>(reply.Result);
        }

        /// <summary>
        /// <para>
        /// Calls the specified function and then searches the workflow history
        /// to see if a value was already recorded with the specified <paramref name="id"/>.
        /// If no value has been recorded for the <paramref name="id"/> or the
        /// value returned by the function will be recorded, replacing any existing
        /// value.  If the function value is the same as the history value, then
        /// nothing will be recorded.
        /// </para>
        /// <para>
        /// This version of the method uses a parameter to specify the expected
        /// result type.
        /// </para>
        /// </summary>
        /// <param name="resultType">Specifies the result type.</param>
        /// <param name="id">Identifies the value in the workflow history.</param>
        /// <param name="function">The side effect function.</param>
        /// <returns>The latest value persisted to the workflow history.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// This is similar to what you could do with a local activity but is
        /// a bit easier since you don't need to declare the activity and create
        /// a stub to call it and it's also more efficient because it avoids
        /// recording the same value multiple times in the history.
        /// </para>
        /// <note>
        /// The function must return within the configured decision task timeout 
        /// and should avoid throwing exceptions.
        /// </note>
        /// <note>
        /// The function passed should avoid throwing exceptions.  When an exception
        /// is thrown, this method will catch it and simply return <c>null</c>.
        /// </note>
        /// <note>
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time 
        /// <see cref="MutableSideEffectAsync(Type, string, Func{object})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        public async Task<object> MutableSideEffectAsync(Type resultType, string id, Func<dynamic> function)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id), nameof(id));
            Covenant.Requires<ArgumentNullException>(resultType != null, nameof(resultType));
            Covenant.Requires<ArgumentNullException>(function != null, nameof(function));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            object value;

            try
            {
                value = function();
            }
            catch
            {
                value = default(object);
            }

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowMutableReply)await Client.CallProxyAsync(
                        new WorkflowMutableRequest()
                        {
                            ContextId = this.ContextId,
                            MutableId = id,
                            Result    = Client.DataConverter.ToData(value)
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData(resultType, reply.Result);
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe <see cref="Guid"/>.
        /// </para>
        /// <note>
        /// This must be used instead of calling <see cref="Guid.NewGuid"/>
        /// to guarantee determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns>The new <see cref="Guid"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public async Task<Guid> NewGuidAsync()
        {
            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return await SideEffectAsync(() => Guid.NewGuid());
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe random non-negative integer greater than or equal to a minimum value
        /// less than a maximum value that is greater than or equal to 0.0 and less than 1.0.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns>The next random double between: <c>0  &lt;= value &lt; 1.0</c></returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<double> NextRandomDoubleAsync()
        {
            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return await SideEffectAsync(() => random.NextDouble());
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe random non-negative random integer.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <returns>The next random integer greater than or equal to 0</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync()
        {
            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return await SideEffectAsync(() => random.Next());
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe random non-negative integer less than a maximum value.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <param name="maxValue">The exclusive upper limit of the value returned.  This cannot be negative.</param>
        /// <returns>The next random integer between: <c>0  &lt;= value &lt; maxValue</c></returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync(int maxValue)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(maxValue > 0, nameof(maxValue));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return await SideEffectAsync(() => random.Next(maxValue));
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe random non-negative integer greater than or equal to a minimum value
        /// less than a maximum value.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <param name="minValue">The inclusive lower limit of the value returned (may be negative).</param>
        /// <param name="maxValue">The exclusive upper limit of the value returned (may be negative).</param>
        /// <returns>The next random integer between: <c>0  &lt;= value &lt; maxValue</c>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync(int minValue, int maxValue)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(minValue < maxValue, nameof(minValue));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return await SideEffectAsync(() => random.Next(minValue, maxValue));
        }

        /// <summary>
        /// <para>
        /// Returns a replay safe byte array filled with random values.
        /// </para>
        /// <note>
        /// This must be used instead of something like <see cref="Random"/> to guarantee 
        /// determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        /// <param name="size">The size of the byte array returned (must be positive)..</param>
        /// <returns>The random bytes.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<byte[]> NextRandomBytesAsync(int size)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(size > 0, nameof(size));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return await SideEffectAsync(
                () =>
                {
                    var bytes = new byte[size];

                    random.NextBytes(bytes);

                    return bytes;
                });
        }

        /// <summary>
        /// Calls the specified function and records the value returned in the workflow
        /// history such that subsequent calls will return the same value.
        /// </summary>
        /// <typeparam name="T">Specifies the result type.</typeparam>
        /// <param name="function">The side effect function.</param>
        /// <returns>The value returned by the first function call.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// This is similar to what you could do with a local activity but is
        /// a bit easier since you don't need to declare the activity and create
        /// a stub to call it.
        /// </para>
        /// <note>
        /// The function must return within the configured decision task timeout 
        /// and should avoid throwing exceptions.
        /// </note>
        /// <note>
        /// The function passed should avoid throwing exceptions.  When an exception
        /// is thrown, this method will catch it and simply return the default 
        /// value for <typeparamref name="T"/>.
        /// </note>
        /// <note>
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time <see cref="SideEffectAsync{T}(Func{T})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        public async Task<T> SideEffectAsync<T>(Func<T> function)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(function != null, nameof(function));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            T value;

            try
            {
                value = function();
            }
            catch
            {
                value = default(T);
            }

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowMutableReply)await Client.CallProxyAsync(
                        new WorkflowMutableRequest()
                        {
                            ContextId = this.ContextId,
                            MutableId = null,
                            Result    = Client.DataConverter.ToData(value)
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData<T>(reply.Result);
        }

        /// <summary>
        /// Calls the specified function and records the value returned in the workflow
        /// history such that subsequent calls will return the same value.  This version
        /// specifies the expected result type as a parameter.
        /// </summary>
        /// <param name="resultType">Specifies the result type.</param>
        /// <param name="function">The side effect function.</param>
        /// <returns>The value returned by the first function call.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// This is similar to what you could do with a local activity but is
        /// a bit easier since you don't need to declare the activity and create
        /// a stub to call it.
        /// </para>
        /// <note>
        /// The function must return within the configured decision task timeout 
        /// and should avoid throwing exceptions.
        /// </note>
        /// <note>
        /// The function passed should avoid throwing exceptions.  When an exception
        /// is thrown, this method will catch it and simply return <c>null</c>.
        /// </note>
        /// <note>
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time <see cref="SideEffectAsync(Type, Func{object})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        public async Task<object> SideEffectAsync(Type resultType, Func<object> function)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(resultType != null, nameof(resultType));
            Covenant.Requires<ArgumentNullException>(function != null, nameof(function));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            object value;

            try
            {
                value = function();
            }
            catch
            {
                value = default(object);
            }

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowMutableReply)await Client.CallProxyAsync(
                        new WorkflowMutableRequest()
                        {
                            ContextId = this.ContextId,
                            MutableId = null,
                            Result    = Client.DataConverter.ToData(value)
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData(resultType, reply.Result);
        }

        /// <summary>
        /// Pauses the workflow for at least the specified interval.
        /// </summary>
        /// <param name="duration">The duration to pause.</param>
        /// <returns>The tracking <see cref="Task"/></returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// This must be used instead of calling <see cref="Task.Delay(TimeSpan)"/> or <see cref="Thread.Sleep(TimeSpan)"/>
        /// to guarantee determinism when a workflow is replayed.
        /// </note>
        /// <note>
        /// Cadence time interval resolution is limited to whole seconds and
        /// the duration will be rounded up to the nearest second and the 
        /// workflow may resumed sometime after the requested interval 
        /// depending on how busy the registered workers are and how long
        /// it takes to actually wake the workflow.
        /// </note>
        /// </remarks>
        public async Task SleepAsync(TimeSpan duration)
        {
            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowSleepReply)await Client.CallProxyAsync(
                        new WorkflowSleepRequest()
                        {
                            ContextId = ContextId,
                            Duration  = duration
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);
        }

        /// <summary>
        /// Pauses the workflow until at least the specified time (UTC).
        /// </summary>
        /// <param name="time">The wake time.</param>
        /// <returns>The tracking <see cref="Task"/></returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// Cadence timers have a resolution of only one second at this time
        /// and due to processing delays, it's very possible that the workflow
        /// will wake several seconds later than scheduled.  You should not
        /// depend on time resolutions less than around 10 seconds.
        /// </note>
        /// </remarks>
        public async Task SleepUntilUtcAsync(DateTime time)
        {
            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            var utcNow = await UtcNowAsync();

            if (time > utcNow)
            {
                await SleepAsync(time - utcNow);
            }
        }

        /// <summary>
        /// Determines whether a previous run of the current CRON workflow completed
        /// and returned a result.
        /// </summary>
        /// <returns><c>true</c> if the a previous CRON workflow run returned a result.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public async Task<bool> IsSetLastCompletionResultAsync()
        {
            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowHasLastResultReply)await Client.CallProxyAsync(
                        new WorkflowHasLastResultRequest()
                        {
                            ContextId = ContextId
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return reply.HasResult;
        }

        /// <summary>
        /// Returns the result of the last run of the current CRON workflow or
        /// <c>null</c>.  This is useful for CRON workflows that would like to
        /// pass information from from one workflow run to the next.
        /// </summary>
        /// <typeparam name="TResult">The expected result type.</typeparam>
        /// <returns>The previous run result as bytes or <c>null</c>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public async Task<TResult> GetLastCompletionResultAsync<TResult>()
        {
            await SyncContext.ClearAsync;
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowGetLastLastReply)await Client.CallProxyAsync(
                        new WorkflowGetLastResultRequest()
                        {
                            ContextId = ContextId
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return Client.DataConverter.FromData<TResult>(reply.Result);
        }

        //---------------------------------------------------------------------
        // Stub creation methods

        /// <summary>
        /// Creates a client stub that can be used to launch one or more activity instances
        /// via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface.</typeparam>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new <see cref="ActivityStub"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be scheduled normally
        /// by Cadence to executed on one of the worker nodes.  Use 
        /// <see cref="NewLocalActivityStub{TActivityInterface, TActivityImplementation}(LocalActivityOptions)"/>
        /// to execute short-lived activities locally within the current process.
        /// </para>
        /// </remarks>
        public TActivityInterface NewActivityStub<TActivityInterface>(ActivityOptions options = null)
            where TActivityInterface : class
        {
            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return StubManager.NewActivityStub<TActivityInterface>(Client, this, options);
        }

        /// <summary>
        /// Creates an untyped client stub that can be used to launch one or more activity
        /// instances using a specific activity type name.  This is typically used to launch
        /// activities written in other languages.
        /// </summary>
        /// <param name="activityTypeName">Specifies the target activity type name.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The untyped <see cref="ActivityStub"/> you'll use to execute the activity.</returns>
        /// <remarks>
        /// <para>
        /// <paramref name="activityTypeName"/> specifies the target activity implementation type name and optionally,
        /// the specific activity method to be called for activity interfaces that have multiple methods.  For
        /// activity methods tagged by <c>ActivityMethod]</c>[ with specifying a name, the activity type name will default
        /// to the fully qualified interface type name or the custom type name specified by <see cref="ActivityAttribute.Name"/>.
        /// </para>
        /// <para>
        /// For activity methods with <see cref="ActivityMethodAttribute.Name"/> specified, the activity type will
        /// look like:
        /// </para>
        /// <code>
        /// ACTIVITY-TYPE-NAME::METHOD-NAME
        /// </code>
        /// <note>
        /// You may need to customize activity type name when interoperating with activities written
        /// in other languages.  See <a href="https://doc.neonkube.com/Neon.Cadence-CrossPlatform.htm">Cadence Cross-Platform</a>
        /// for more information.
        /// </note>
        /// </remarks>
        public ActivityStub NewExternalActivityStub(string activityTypeName, ActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new ActivityStub(Client, this, activityTypeName, options);
        }

        /// <summary>
        /// Creates a workflow client stub that can be used to launch, signal, and query child
        /// workflows via the type-safe workflow interface methods.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="options">Optionally specifies the child workflow options.</param>
        /// <param name="workflowTypeName">
        /// Optionally specifies the workflow type name by overriding the fully 
        /// qualified <typeparamref name="TWorkflowInterface"/> type name or the name
        /// specified by a <see cref="WorkflowAttribute"/>.
        /// </param>
        /// <returns>The child workflow stub.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        public TWorkflowInterface NewChildWorkflowStub<TWorkflowInterface>(ChildWorkflowOptions options = null, string workflowTypeName = null)
            where TWorkflowInterface : class
        {
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return StubManager.NewChildWorkflowStub<TWorkflowInterface>(Client, this, options, workflowTypeName);
        }

        /// <summary>
        /// Creates a typed-safe client stub that can be used to continue the workflow as a new run.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="options">Optionally specifies the new options to use when continuing the workflow.</param>
        /// <returns>The type-safe stub.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// The workflow stub returned is intended just for continuing the workflow by
        /// calling one of the workflow entry point methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// Any signal or query methods defined by <typeparamref name="TWorkflowInterface"/> will 
        /// throw a <see cref="InvalidOperationException"/> when called.
        /// </remarks>
        public TWorkflowInterface NewContinueAsNewStub<TWorkflowInterface>(ContinueAsNewOptions options = null) 
            where TWorkflowInterface : class
        {
            CadenceHelper.ValidateWorkflowInterface(typeof(TWorkflowInterface));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return StubManager.NewContinueAsNewStub<TWorkflowInterface>(Client, options);
        }

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by a <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <param name="execution">Identifies the workflow.</param>
        /// <returns>The workflow stub.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public ExternalWorkflowStub NewExternalWorkflowStub(WorkflowExecution execution)
        {
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new ExternalWorkflowStub(Client, execution);
        }

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by a workflow ID and optional domain.
        /// </summary>
        /// <param name="workflowId">Identifies the workflow.</param>
        /// <param name="domain">Optionally overrides the parent workflow domain.</param>
        /// <returns>The workflow stub.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public ExternalWorkflowStub NewExternalWorkflowStub(string workflowId, string domain = null)
        {
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new ExternalWorkflowStub(Client, new WorkflowExecution(workflowId), domain);
        }

        /// <summary>
        /// Creates a client stub that can be used to launch one or more local activity 
        /// instances via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface.</typeparam>
        /// <typeparam name="TActivityImplementation">The activity implementation.</typeparam>
        /// <param name="options">Optionally specifies activity options.</param>
        /// <returns>The new <see cref="ActivityStub"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be executed in the current
        /// process.  This is intended to easily and efficiently execute activities
        /// that will complete very quickly (usually within a few seconds).  Local
        /// activities are similar to normal activities with these differences:
        /// </para>
        /// <list type="bullet">
        ///     <item>
        ///     Local activities are always scheduled to executed within the current process.
        ///     </item>
        ///     <item>
        ///     Local activity types do not need to be registered with the worker.
        ///     </item>
        ///     <item>
        ///     Local activities must complete within the <see cref="WorkflowOptions.DecisionTaskTimeout"/>.
        ///     This defaults to 10 seconds and can be set to a maximum of 60 seconds.
        ///     </item>
        ///     <item>
        ///     Local activities cannot heartbeat.
        ///     </item>
        /// </list>
        /// </remarks>
        public TActivityInterface NewLocalActivityStub<TActivityInterface, TActivityImplementation>(LocalActivityOptions options = null)
            where TActivityInterface : class
            where TActivityImplementation : TActivityInterface
        {
            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));
            CadenceHelper.ValidateActivityImplementation(typeof(TActivityImplementation));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return StubManager.NewLocalActivityStub<TActivityInterface, TActivityImplementation>(Client, this, options);
        }

        /// <summary>
        /// Creates a specialized stub suitable for starting and running a child workflow in parallel
        /// with other workflow operations such as child workflows or activities.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The target workflow interface.</typeparam>
        /// <param name="methodName">
        /// Optionally identifies the target workflow method.  This is the name specified in
        /// <c>[WorkflowMethod]</c> attribute for the workflow method or <c>null</c>/empty for
        /// the default workflow method.
        /// </param>
        /// <param name="options">Optionally specifies custom <see cref="ChildWorkflowOptions"/>.</param>
        /// <returns>A <see cref="ChildWorkflowStub{TWorkflowInterface}"/> instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// Sometimes workflows need to run child workflows in parallel with other child workflows or
        /// activities.  Although the typed workflow stubs return a <see cref="Task"/> or <see cref="Task{T}"/>,
        /// workflow developers are required to immediately <c>await</c> every call to these stubs to 
        /// ensure that the workflow will execute consistently when replayed from history.  This 
        /// means that you must not do something like this:
        /// </para>
        /// <code language="C#">
        /// public interface IMyWorkflow : IWorkflow
        /// {
        ///     [WorkflowMethod]
        ///     Task MainAsync();
        ///     
        ///     [WorkflowMethod(Name = "child")]
        ///     Task&lt;string&gt; ChildAsync(string arg);
        /// }
        /// 
        /// public class MyWorkflow : WorkflowBase, IMyWorkflow
        /// {
        ///     public Task MainAsync()
        ///     {
        ///         var stub1     = Workflow.NewChildWorkflowStub&lt;IMyWorkflow&gt;("FOO");
        ///         var childTask = stub1.DoChildWorkflow();
        ///         var stub2     = Workflow.NewChildWorkflowStub&lt;IMyWorkflow&gt;();
        ///         var value2    = await stub2.DoChildWorkflow("BAR");
        ///         var value1    = await childTask;
        ///     }
        ///     
        ///     public Task&lt;string&gt; ChildAsync(string arg)
        ///     {
        ///         return await Task.FromResult(arg);
        ///     }
        /// }
        /// </code>
        /// <para>
        /// The <c>MainAsync()</c> workflow method here creates and starts a child workflow, but it 
        /// doesn't immediately <c>await</c> it.  It then runs another child workflow in parallel 
        /// and then after the second child returns, the workflow awaits the first child.  This pattern 
        /// is not supported by <b>Neon.Cadence</b> because all workflow related operations need to 
        /// be immediately awaited to ensure that operations will complete in a consistent order when
        /// workflows are replayed.
        /// </para>
        /// <note>
        /// The reason for this restriction is related to how the current <b>Neon.Cadence</b> implementation
        /// uses an embedded GOLANG Cadence client to actually communicate with a Cadence cluster.  This
        /// may be relaxed in the future if/when we implement native support for the Cadence protocol.
        /// </note>
        /// <para>
        /// A correct implementation would look something like this:
        /// </para>
        /// <code language="C#">
        /// public interface IMyWorkflow : IWorkflow
        /// {
        ///     [WorkflowMethod]
        ///     Task MainAsync();
        ///     
        ///     [WorkflowMethod(Name = "child")]
        ///     Task&lt;string&gt; ChildAsync(string arg);
        /// }
        /// 
        /// public class MyWorkflow : WorkflowBase, IMyWorkflow
        /// {
        ///     public Task MainAsync()
        ///     {
        ///         var stub1  = Workflow.NewChildWorkflowFutureStub&lt;IMyWorkflow&gt;("child");
        ///         var future = await stub1.StartAsync$lt;string&gt;("FOO");   // Starting the child with param: "FOO"
        ///         var stub2  = Workflow.NewChildWorkflowStub&lt;IMyWorkflow&gt;();
        ///         var value2 = await stub2.DoChildWorkflow("BAR");            // This returns: "BAR"
        ///         var value1 = await future.GetAsync();                       // This returns: "FOO"
        ///     }
        ///     
        ///     public Task&lt;string&gt; ChildAsync(string arg)
        ///     {
        ///         return await Task.FromResult(arg);
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Here we call <see cref="NewChildWorkflowFutureStub{TWorkflowInterface}(string, ChildWorkflowOptions)"/> specifying
        /// <b>"child"</b> as the workflow method name.  This matches the <c>[WorkflowMethod(Name = "child")]</c>
        /// attribute decorating the <c>ChildAsync()</c> workflow interface method.  Then we start the child workflow by awaiting 
        /// <see cref="ChildWorkflowStub{TWorkflowInterface}.StartAsync(object[])"/>. This returns an <see cref="ChildWorkflowFuture{T}"/> whose 
        /// <see cref="IAsyncFuture.GetAsync"/> method returns the workflow result.  The code above calls this to retrieve the 
        /// result from the first child after executing the second child in parallel.
        /// </para>
        /// <note>
        /// <para>
        /// You must take care to pass parameters that match the target method.  <b>Neon.Cadence</b> does check these at
        /// runtime, but there is no compile-time checking.
        /// </para>
        /// <para>
        /// You'll also need to cast the <see cref="IAsyncFuture.GetAsync"/> result to the actual type (if required).
        /// This method always returns the <c>object</c> type even if referenced workflow and activity methods return
        /// <c>void</c>.  <see cref="IAsyncFuture.GetAsync"/> will return <c>null</c> in these cases.
        /// </para>
        /// </note>
        /// </remarks>
        public ChildWorkflowStub<TWorkflowInterface> NewChildWorkflowFutureStub<TWorkflowInterface>(string methodName = null, ChildWorkflowOptions options = null)
            where TWorkflowInterface : class
        {
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new ChildWorkflowStub<TWorkflowInterface>(this, methodName, options);
        }

        /// <summary>
        /// Creates an untyped child workflow stub that can be used to start, signal, and wait
        /// for the child workflow completion.  Use this version for child workflows that
        /// don't return a value.
        /// </summary>
        /// <param name="workflowTypeName">The workflow type name (see the remarks).</param>
        /// <param name="options">Optionally specifies the child workflow options.</param>
        /// <returns>The <see cref="ChildWorkflowFutureStub"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </para>
        /// <para>
        /// <paramref name="workflowTypeName"/> specifies the target workflow implementation type name and optionally,
        /// the specific workflow method to be called for workflow interfaces that have multiple methods.  For
        /// workflow methods tagged by <c>[WorkflowMethod]</c> with specifying a name, the workflow type name will default
        /// to the fully qualified interface type name or the custom type name specified by <see cref="WorkflowAttribute.Name"/>.
        /// </para>
        /// <para>
        /// For workflow methods with <see cref="WorkflowMethodAttribute.Name"/> specified, the workflow type will
        /// look like:
        /// </para>
        /// <code>
        /// WORKFLOW-TYPE-NAME::METHOD-NAME
        /// </code>
        /// <para>
        /// You'll need to use this format when calling workflows using external untyped stubs or 
        /// from other languages.  The Java Cadence client works the same way.
        /// </para>
        /// </remarks>
        public ChildWorkflowFutureStub NewUntypedChildWorkflowFutureStub(string workflowTypeName, ChildWorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new ChildWorkflowFutureStub(this, workflowTypeName, options);
        }

        /// <summary>
        /// Creates an untyped child workflow stub that can be used to start, signal, and wait
        /// for the child workflow completion.  Use this version for child workflows that
        /// return a value.
        /// </summary>
        /// <typeparam name="TResult">Specifies the child workflow result type.</typeparam>
        /// <param name="workflowTypeName">The workflow type name (see the remarks).</param>
        /// <param name="options">Optionally specifies the child workflow options.</param>
        /// <returns>The <see cref="ChildWorkflowFutureStub"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </para>
        /// <para>
        /// <paramref name="workflowTypeName"/> specifies the target workflow implementation type name and optionally,
        /// the specific workflow method to be called for workflow interfaces that have multiple methods.  For
        /// workflow methods tagged by <c>[WorkflowMethod]</c> with specifying a name, the workflow type name will default
        /// to the fully qualified interface type name or the custom type name specified by <see cref="WorkflowAttribute.Name"/>.
        /// </para>
        /// <para>
        /// For workflow methods with <see cref="WorkflowMethodAttribute.Name"/> specified, the workflow type will
        /// look like:
        /// </para>
        /// <code>
        /// WORKFLOW-TYPE-NAME::METHOD-NAME
        /// </code>
        /// <para>
        /// You'll need to use this format when calling workflows using external untyped stubs or 
        /// from other languages.  The Java Cadence client works the same way.
        /// </para>
        /// </remarks>
        public UntypedChildWorkflowFutureStub<TResult> NewUntypedChildWorkflowFutureStub<TResult>(string workflowTypeName, ChildWorkflowOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName), nameof(workflowTypeName));

            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new UntypedChildWorkflowFutureStub<TResult>(this, workflowTypeName, options);
        }

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <param name="execution">The target <see cref="WorkflowExecution"/>.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="ExternalWorkflowStub"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public ExternalWorkflowStub NewUntypedExternalWorkflowStub(WorkflowExecution execution, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null, nameof(execution));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its workflow ID.
        /// </summary>
        /// <param name="workflowId">The target workflow ID.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="ExternalWorkflowStub"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        public ExternalWorkflowStub NewUntypedExternalWorkflowStub(string workflowId, string domain = null)
        {
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates a specialized stub suitable for starting and running an activity in parallel
        /// with other workflow operations such as child workflows or activities.
        /// </summary>
        /// <typeparam name="TActivityInterface">The activity interface.</typeparam>
        /// <param name="methodName">
        /// Optionally identifies the target activity method.  This is the name specified in
        /// <c>[ActivityMethod]</c> attribute for the activity method or <c>null</c>/empty for 
        /// the default activity method.
        /// </param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new <see cref="ActivityFutureStub{TActivityInterface}"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// Sometimes workflows need to run activities in parallel with other child workflows or
        /// activities.  Although the standard stubs return a <see cref="Task"/> or <see cref="Task{T}"/>,
        /// workflow developers are required to immediately <c>await</c> every call to these stubs to 
        /// ensure that the workflow will execute consistently when replayed from history.  This 
        /// means that you must not do something like this:
        /// </para>
        /// <code language="C#">
        /// public interface IMyActivity : IActivity
        /// {
        ///     [ActivityMethod(Name = "activity-1"]
        ///     Task&lt;string&gt; FooActivityAsync(string arg);
        ///     
        ///     [ActivityMethod(Name = "activity-2"]
        ///     Task&lt;string&gt; BarActivityAsync(string arg);
        /// }
        /// 
        /// public MyActivity : ActivityBase, IMyActivity
        /// {
        ///     public Task&lt;string&gt; FooActivityAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        ///     
        ///     public Task&lt;string&gt; BarActivityAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        /// }
        ///
        /// public class MyWorkflow : WorkflowBase, IMyWorkflow
        /// {
        ///     [WorkflowMethod]
        ///     public Task MainAsync()
        ///     {
        ///         var stub     = Workflow.NewActivityStub&lt;IMyActivity&gt;();
        ///         var fooTask  = stub.FooActivity("FOO");
        ///         var barValue = await stub.BarActivityAsync("BAR");
        ///         var fooValue = await fooTask;
        ///     }
        /// }
        /// </code>
        /// <para>
        /// The <c>MainAsync()</c> workflow method here starts an activity but doesn't immediately
        /// <c>await</c> it.  It then runs another activity in parallel and then after the second 
        /// activity returns, the workflow awaits the first activity.  This pattern is not supported 
        /// by <b>Neon.Cadence</b> because all workflow related operations need to be immediately
        /// awaited to ensure that operations will complete in a consistent order when workflows 
        /// are replayed.
        /// </para>
        /// <note>
        /// The reason for this restriction is related to how the current <b>Neon.Cadence</b> implementation
        /// uses an embedded GOLANG Cadence client to actually communicate with a Cadence cluster.  This
        /// may be relaxed in the future if/when we implement native support for the Cadence protocol.
        /// </note>
        /// <para>
        /// A correct implementation would look something like this:
        /// </para>
        /// <code language="C#">
        /// public interface IMyActivity : IActivity
        /// {
        ///     [ActivityMethod(Name = "foo"]
        ///     Task&lt;string&gt; FooAsync(string arg);
        ///     
        ///     [ActivityMethod(Name = "bar"]
        ///     Task&lt;string&gt; BarAsync(string arg);
        /// }
        /// 
        /// public MyActivity : ActivityBase, IMyActivity
        /// {
        ///     public Task&lt;string&gt; FooAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        ///     
        ///     public Task&lt;string&gt; BarAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        /// }
        ///
        /// public class MyWorkflow : WorkflowBase, IMyWorkflow
        /// {
        ///     [WorkflowMethod]
        ///     public Task MainAsync()
        ///     {
        ///         var fooStub  = Workflow.NewActivityFutureStub("foo");
        ///         var future   = fooStub.StartAsync&lt;string&gt;("FOO");
        ///         var barStub  = Workflow.NewActivityStub&lt;IMyActivity&gt;();
        ///         var barValue = await barStub.BarAsync("BAR");   // Returns: "BAR"
        ///         var fooValue = await future.GetAsync();         // Returns: "FOO"
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Here we call <see cref="NewActivityFutureStub{TActivityInterface}(string, ActivityOptions)"/> specifying
        /// <b>"foo"</b> as the workflow method name.  This matches the <c>[ActivityMethod(Name = "foo")]</c> decorating
        /// the <c>FooAsync()</c> activity interface method.  Then we start the first activity by awaiting 
        /// <see cref="ActivityFutureStub{TActivityInterface}"/>.  This returns an <see cref="IAsyncFuture{T}"/> whose 
        /// <see cref="IAsyncFuture.GetAsync"/> method returns the activity result.  The code above calls this to
        /// retrieve the result from the first activity after executing the second activity in parallel.
        /// </para>
        /// <note>
        /// <para>
        /// You must take care to pass parameters that match the target method.  <b>Neon.Cadence</b> does check these at
        /// runtime, but there is no compile-time checking for this scheme.
        /// </para>
        /// <para>
        /// You'll also need to cast the <see cref="IAsyncFuture.GetAsync"/> result to the actual type (if required).
        /// This method always returns the <c>object</c> type even if referenced workflow and activity methods return
        /// <c>void</c>.  <see cref="IAsyncFuture.GetAsync"/> will return <c>null</c> in these cases.
        /// </para>
        /// </note>
        /// </remarks>
        public ActivityFutureStub<TActivityInterface> NewActivityFutureStub<TActivityInterface>(string methodName = null, ActivityOptions options = null)
            where TActivityInterface : class
        {
            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new ActivityFutureStub<TActivityInterface>(this, methodName, options);
        }

        /// <summary>
        /// Creates a specialized untyped stub suitable for starting and running an activity in parallel
        /// with other workflow operations such as child workflows or activities.  This is typically
        /// used for executing activities written in another language.
        /// </summary>
        /// <param name="activityTypeName">Specifies the target activity type name.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new untyped <see cref="ActivityFutureStub"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// Sometimes workflows need to run activities written in other languages in parallel with other
        /// child workflows or activities.  Although the standard stubs return a <see cref="Task"/> or <see cref="Task{T}"/>,
        /// workflow developers are required to immediately <c>await</c> every call to these stubs to 
        /// ensure that the workflow will execute consistently when replayed from history.  This 
        /// means that you must not do something like this:
        /// </para>
        /// <code language="C#">
        /// public interface IMyActivity : IActivity
        /// {
        ///     [ActivityMethod(Name = "activity-1"]
        ///     Task&lt;string&gt; FooActivityAsync(string arg);
        ///     
        ///     [ActivityMethod(Name = "activity-2"]
        ///     Task&lt;string&gt; BarActivityAsync(string arg);
        /// }
        /// 
        /// public MyActivity : ActivityBase, IMyActivity
        /// {
        ///     public Task&lt;string&gt; FooActivityAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        ///     
        ///     public Task&lt;string&gt; BarActivityAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        /// }
        ///
        /// public class MyWorkflow : WorkflowBase, IMyWorkflow
        /// {
        ///     [WorkflowMethod]
        ///     public Task MainAsync()
        ///     {
        ///         var stub     = Workflow.NewActivityStub("MyActivity::FooActivityAsync");
        ///         var fooTask  = stub.StartAsync&lt;string&gt;("FOO");
        ///         var barStub  = Workflow.NewActivityStub&lt;IMyActivity&gt;();
        ///         var barValue = await barStub.BarActivityAsync("BAR");
        ///         var fooValue = await fooTask;
        ///     }
        /// }
        /// </code>
        /// <para>
        /// The <c>MainAsync()</c> workflow method here starts an activity but doesn't immediately
        /// <c>await</c> it.  It then runs another activity in parallel and then after the second 
        /// activity returns, the workflow awaits the first activity.  This pattern is not supported 
        /// by <b>Neon.Cadence</b> because all workflow related operations need to be immediately
        /// awaited to ensure that operations will complete in a consistent order when workflows 
        /// are replayed.
        /// </para>
        /// <note>
        /// The reason for this restriction is related to how the current <b>Neon.Cadence</b> implementation
        /// uses an embedded GOLANG Cadence client to actually communicate with a Cadence cluster.  This
        /// may be relaxed in the future if/when we implement native support for the Cadence protocol.
        /// </note>
        /// <para>
        /// A correct implementation would look something like this:
        /// </para>
        /// <code language="C#">
        /// public interface IMyActivity : IActivity
        /// {
        ///     [ActivityMethod(Name = "foo"]
        ///     Task&lt;string&gt; FooAsync(string arg);
        ///     
        ///     [ActivityMethod(Name = "bar"]
        ///     Task&lt;string&gt; BarAsync(string arg);
        /// }
        /// 
        /// public MyActivity : ActivityBase, IMyActivity
        /// {
        ///     public Task&lt;string&gt; FooAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        ///     
        ///     public Task&lt;string&gt; BarAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        /// }
        ///
        /// public class MyWorkflow : WorkflowBase, IMyWorkflow
        /// {
        ///     [WorkflowMethod]
        ///     public Task MainAsync()
        ///     {
        ///         var fooStub  = Workflow.NewActivityFutureStub("foo");
        ///         var future   = await fooStub.StartAsync&lt;string&gt;("FOO");
        ///         var barStub  = Workflow.NewActivityStub&lt;IMyActivity&gt;();
        ///         var barValue = await barStub.BarAsync("BAR");   // Returns: "BAR"
        ///         var fooValue = await future.GetAsync();         // Returns: "FOO"
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Here we call <see cref="NewActivityFutureStub(string, ActivityOptions)"/> specifying
        /// <b>"foo"</b> as the workflow method name.  This matches the <c>[ActivityMethod(Name = "foo")]</c> decorating
        /// the <c>FooAsync()</c> activity interface method.  Then we start the first activity by awaiting 
        /// <see cref="ActivityFutureStub{TActivityInterface}"/>.  This returns an <see cref="IAsyncFuture{T}"/> whose 
        /// <see cref="IAsyncFuture.GetAsync"/> method returns the activity result.  The code above calls this to
        /// retrieve the result from the first activity after executing the second activity in parallel.
        /// </para>
        /// <note>
        /// <para>
        /// You must take care to pass parameters that match the target method.  <b>Neon.Cadence</b> does check these at
        /// runtime, but there is no compile-time checking for this scheme.
        /// </para>
        /// <para>
        /// You'll also need to cast the <see cref="IAsyncFuture.GetAsync"/> result to the actual type (if required).
        /// This method always returns the <c>object</c> type even if referenced workflow and activity methods return
        /// <c>void</c>.  <see cref="IAsyncFuture.GetAsync"/> will return <c>null</c> in these cases.
        /// </para>
        /// </note>
        /// </remarks>
        public ActivityFutureStub NewActivityFutureStub(string activityTypeName, ActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new ActivityFutureStub(this, activityTypeName, options);
        }

        /// <summary>
        /// Creates a specialized stub suitable for starting and running a local activity in parallel
        /// with other workflow operations such as child workflows or activities.
        /// </summary>
        /// <typeparam name="TActivityInterface">Specifies the activity interface.</typeparam>
        /// <typeparam name="TActivityImplementation">Specifies the local activity implementation class.</typeparam> 
        /// <param name="methodName">
        /// Optionally identifies the target activity method.  This is the name specified in
        /// <c>[ActivityMethod]</c> attribute for the activity method or <c>null</c>/empty for
        /// the default activity method.
        /// </param>
        /// <param name="options">Optionally specifies the local activity options.</param>
        /// <returns>The new <see cref="NewStartLocalActivityStub{TActivityInterface, TActivityImplementation}(string, LocalActivityOptions)"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// Sometimes workflows need to run local activities in parallel with other child workflows or
        /// activities.  Although the standard stubs return a <see cref="Task"/> or <see cref="Task{T}"/>,
        /// workflow developers are required to immediately <c>await</c> every call to these stubs to 
        /// ensure that the workflow will execute consistently when replayed from history.  This 
        /// means that you must not do something like this:
        /// </para>
        /// <code language="C#">
        /// public interface IMyActivity : IActivity
        /// {
        ///     [ActivityMethod(Name = "activity-1"]
        ///     Task&lt;string&gt; FooActivityAsync(string arg);
        ///     
        ///     [ActivityMethod(Name = "activity-2"]
        ///     Task&lt;string&gt; BarActivityAsync(string arg);
        /// }
        /// 
        /// public MyActivity : ActivityBase, IMyActivity
        /// {
        ///     public Task&lt;string&gt; FooActivityAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        ///     
        ///     public Task&lt;string&gt; BarActivityAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        /// }
        ///
        /// public class MyWorkflow : WorkflowBase, IMyWorkflow
        /// {
        ///     [WorkflowMethod]
        ///     public Task MainAsync()
        ///     {
        ///         var stub     = Workflow.NewLocalActivityStub&lt;IMyActivity, MyActivity&gt;();
        ///         var fooTask  = stub.FooActivity("FOO");
        ///         var barValue = await stub.BarActivityAsync("BAR");
        ///         var fooValue = await fooTask;
        ///     }
        /// }
        /// </code>
        /// <para>
        /// The <c>MainAsync()</c> workflow method here starts a local activity but doesn't immediately
        /// <c>await</c> it.  It then runs another activity in parallel and then after the second 
        /// activity returns, the workflow awaits the first activity.  This pattern is not supported 
        /// by <b>Neon.Cadence</b> because all workflow related operations need to be immediately
        /// awaited to ensure that operations will complete in a consistent order when workflows 
        /// are replayed.
        /// </para>
        /// <note>
        /// The reason for this restriction is related to how the current <b>Neon.Cadence</b> implementation
        /// uses an embedded GOLANG Cadence client to actually communicate with a Cadence cluster.  This
        /// may be relaxed in the future if/when we implement native support for the Cadence protocol.
        /// </note>
        /// <para>
        /// A correct implementation would look something like this:
        /// </para>
        /// <code language="C#">
        /// public interface IMyActivity : IActivity
        /// {
        ///     [ActivityMethod(Name = "foo"]
        ///     Task&lt;string&gt; FooAsync(string arg);
        ///     
        ///     [ActivityMethod(Name = "bar"]
        ///     Task&lt;string&gt; BarAsync(string arg);
        /// }
        /// 
        /// public MyActivity : ActivityBase, IMyActivity
        /// {
        ///     public Task&lt;string&gt; FooAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        ///     
        ///     public Task&lt;string&gt; BarAsync(string arg)
        ///     {
        ///         await Task.FromResult(arg);
        ///     }
        /// }
        ///
        /// public class MyWorkflow : WorkflowBase, IMyWorkflow
        /// {
        ///     [WorkflowMethod]
        ///     public Task MainAsync()
        ///     {
        ///         var fooStub  = Workflow.NewStartLocalActivityStub("foo");
        ///         var future   = fooStub.StartAsync&lt;string&gt;("FOO");
        ///         var barStub  = Workflow.NewActivityStub&lt;IMyActivity&gt;();
        ///         var barValue = await barStub.BarAsync("BAR");   // Returns: "BAR"
        ///         var fooValue = await future.GetAsync();         // Returns: "FOO"
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Here we call <see cref="NewActivityFutureStub{TActivityInterface}(string, ActivityOptions)"/> specifying
        /// <b>"foo"</b> as the workflow method name.  This matches the <c>[ActivityMethod(Name = "foo")]</c> decorating
        /// the <c>FooAsync()</c> activity interface method.  Then we start the first activity by awaiting 
        /// <see cref="ActivityFutureStub{TActivityInterface}"/>.  This returns an <see cref="IAsyncFuture{T}"/> whose 
        /// <see cref="IAsyncFuture.GetAsync"/> method returns the activity result.  The code above calls this to
        /// retrieve the result from the first activity after executing the second activity in parallel.
        /// </para>
        /// <note>
        /// <para>
        /// You must take care to pass parameters that match the target method.  <b>Neon.Cadence</b> does check these at
        /// runtime, but there is no compile-time checking for this scheme.
        /// </para>
        /// <para>
        /// You'll also need to cast the <see cref="IAsyncFuture.GetAsync"/> result to the actual type (if required).
        /// This method always returns the <c>object</c> type even if referenced workflow and activity methods return
        /// <c>void</c>.  <see cref="IAsyncFuture.GetAsync"/> will return <c>null</c> in these cases.
        /// </para>
        /// </note>
        /// </remarks>
        public LocalActivityFutureStub<TActivityInterface, TActivityImplementation> NewStartLocalActivityStub<TActivityInterface, TActivityImplementation>(string methodName = null, LocalActivityOptions options = null)
            where TActivityInterface : class
            where TActivityImplementation : TActivityInterface
        {
            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);
            SetStackTrace();

            return new LocalActivityFutureStub<TActivityInterface, TActivityImplementation>(this, methodName, options);
        }

        /// <summary>
        /// Creates a new workflow safe queue.  These are typically used by workflow signal
        /// methods for communicating with the workflow logic.
        /// </summary>
        /// <typeparam name="T">Specifies the queued data type.</typeparam>
        /// <param name="capacity">
        /// <para>
        /// Specifies the maximum number items the queue may hold.
        /// </para>
        /// <note>
        /// This defaults to <see cref="WorkflowQueue{T}.DefaultCapacity"/>.
        /// </note>
        /// </param>
        /// <returns>The new <see cref="WorkflowQueue{T}"/>.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="NotSupportedException">Thrown when this is called outside of a workflow entry point method.</exception>
        /// <remarks>
        /// <para>
        /// You may write and read data items from the returned queue.  Writes
        /// will block when the queue is full until an item has been read, freeing
        /// a slot.
        /// </para>
        /// <note>
        /// Items will be serialized internally using the current <see cref="IDataConverter"/> to
        /// bytes before actually enqueuing the item.  This serialized data must be less
        /// than 64KiB.
        /// </note>
        /// <para>
        /// See <see cref="WorkflowQueue{T}"/> for more information.
        /// </para>
        /// </remarks>
        public async Task<WorkflowQueue<T>> NewQueueAsync<T>(int capacity = WorkflowQueue<T>.DefaultCapacity)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentException>(capacity >= 2, nameof(capacity), "Queue capacity cannot be less than [2].");
            Client.EnsureNotDisposed();
            WorkflowBase.CheckCallContext(allowWorkflow: true);

            var queueId = Interlocked.Increment(ref nextQueueId);

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (WorkflowQueueNewReply)await Client.CallProxyAsync(
                        new WorkflowQueueNewRequest()
                        {
                            ContextId = this.ContextId,
                            QueueId   = queueId,
                            Capacity  = capacity
                        });
                });

            reply.ThrowOnError();

            return new WorkflowQueue<T>(this, queueId, capacity);
        }

        //---------------------------------------------------------------------
        // Internal activity related methods used by dynamically generated activity stubs.

        /// <summary>
        /// Executes an activity with a specific activity type name and waits for it to complete.
        /// </summary>
        /// <param name="activityTypeName">Identifies the activity.</param>
        /// <param name="args">Specifies the encoded activity arguments or <c>null</c> when there are no arguments.</param>
        /// <param name="options">Specifies the activity options.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="EntityNotExistsException">Thrown if the Cadence does not exist.</exception>
        /// <exception cref="BadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="InternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="ServiceBusyException">Thrown when Cadence is too busy.</exception>
        internal async Task<byte[]> ExecuteActivityAsync(string activityTypeName, byte[] args, ActivityOptions options)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName), nameof(activityTypeName));
            Client.EnsureNotDisposed();
            SetStackTrace(skipFrames: 3);

            options = ActivityOptions.Normalize(Client, options);

            Client.RaiseActivityExecuteEvent(options);

            var reply = await ExecuteNonParallel(
                async () => (ActivityExecuteReply)await Client.CallProxyAsync(
                    new ActivityExecuteRequest()
                    {
                        ContextId = ContextId,
                        Activity  = activityTypeName,
                        Args      = args,
                        Options   = options.ToInternal(),
                        Domain    = options.Domain,
                    }));

            reply.ThrowOnError();
            UpdateReplay(reply);

            return reply.Result;
        }

        /// <summary>
        /// Registers a local activity type and method with the workflow and returns 
        /// its local activity action ID.
        /// </summary>
        /// <param name="activityType">The activity type.</param>
        /// <param name="activityConstructor">The activity constructor.</param>
        /// <param name="activityMethod">The target local activity method.</param>
        /// <returns>The new local activity action ID.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        internal long RegisterActivityAction(Type activityType, ConstructorInfo activityConstructor, MethodInfo activityMethod)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));
            Covenant.Requires<ArgumentNullException>(activityConstructor != null, nameof(activityConstructor));
            Covenant.Requires<ArgumentException>(activityType.BaseType == typeof(ActivityBase), nameof(activityType));
            Covenant.Requires<ArgumentNullException>(activityMethod != null, nameof(activityMethod));
            Client.EnsureNotDisposed();

            var activityActionId = Interlocked.Increment(ref nextLocalActivityActionId);

            lock (syncLock)
            {
                IdToLocalActivityAction.Add(activityActionId, new LocalActivityAction(activityType, activityConstructor, activityMethod));
            }

            return activityActionId;
        }

        /// <summary>
        /// Executes a local activity and waits for it to complete.
        /// </summary>
        /// <param name="activityType">The activity type.</param>
        /// <param name="activityConstructor">The activity constructor.</param>
        /// <param name="activityMethod">The target local activity method.</param>
        /// <param name="args">Specifies specifies the encoded activity arguments or <c>null</c> when there are no arguments.</param>
        /// <param name="options">Specifies the local activity options.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <remarks>
        /// This method can be used to optimize activities that will complete quickly
        /// (within seconds).  Rather than scheduling the activity on any worker that
        /// has registered an implementation for the activity, this method will simply
        /// instantiate an instance of <paramref name="activityType"/> and call its
        /// <paramref name="activityMethod"/> method.
        /// </remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the associated Cadence client is disposed.</exception>
        /// <exception cref="EntityNotExistsException">Thrown if the Cadence domain does not exist.</exception>
        /// <exception cref="BadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="InternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="ServiceBusyException">Thrown when Cadence is too busy.</exception>
        internal async Task<byte[]> ExecuteLocalActivityAsync(Type activityType, ConstructorInfo activityConstructor, MethodInfo activityMethod, byte[] args, LocalActivityOptions options)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null, nameof(activityType));
            Covenant.Requires<ArgumentException>(activityType.BaseType == typeof(ActivityBase), nameof(activityType));
            Covenant.Requires<ArgumentNullException>(activityConstructor != null, nameof(activityConstructor));
            Covenant.Requires<ArgumentNullException>(activityMethod != null, nameof(activityMethod));
            Client.EnsureNotDisposed();
            SetStackTrace(skipFrames: 3);

            var activityActionId = RegisterActivityAction(activityType, activityConstructor, activityMethod);

            options = LocalActivityOptions.Normalize(this.Client, options);

            Client.RaiseLocalActivityExecuteEvent(options);

            var reply = await ExecuteNonParallel(
                async () =>
                {
                    return (ActivityExecuteLocalReply)await Client.CallProxyAsync(
                        new ActivityExecuteLocalRequest()
                        {
                            ContextId      = ContextId,
                            ActivityTypeId = activityActionId,
                            Args           = args,
                            Options        = options.ToInternal(),
                        });
                });

            reply.ThrowOnError();
            UpdateReplay(reply);

            return reply.Result;
        }

        /// <summary>
        /// Forces the current workflow execution to terminate such that it will be rescheduled
        /// and replayed as required.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        internal async Task ForceReplayAsync()
        {
            await Task.CompletedTask;

            throw new ForceReplayException();
        }
    }
}
