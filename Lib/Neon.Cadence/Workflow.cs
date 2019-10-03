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

        private object      syncLock = new object();
        private int         pendingOperationCount;
        private long        nextLocalActivityActionId;
        private long        nextActivityId;
        private Random      random;

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
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowTypeName));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(domain));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(taskList));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(workflowId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(runId));

            this.Parent                    = parent;
            this.ContextId                 = contextId;
            this.pendingOperationCount     = 0;
            this.nextLocalActivityActionId = 0;
            this.nextActivityId            = 0;
            this.IdToLocalActivityAction   = new Dictionary<long, LocalActivityAction>();
            this.MethodMap                 = methodMap;
            this.Client                    = client;
            this.IsReplaying               = isReplaying;
            this.Execution                 = new WorkflowExecution(workflowId, runId);
            this.Logger                    = LogManager.Default.GetLogger(sourceModule: Client.Settings?.ClientIdentity, contextId: runId, () => !IsReplaying || Client.Settings.LogDuringReplay);

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

                // $todo(jeff.lill): We need to initialize these from somewhere.
                //
                // ExecutionStartToCloseTimeout
                // ChildPolicy 
            };
        }

        /// <summary>
        /// Returns the parent <see cref="WorkflowBase"/> implementation.
        /// </summary>
        internal WorkflowBase Parent { get; private set; }

        /// <summary>
        /// Returns the parent workflow's context ID.
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
        public bool IsReplaying { get; internal set; }

        /// <summary>
        /// Returns the execution information for the current workflow.
        /// </summary>
        public WorkflowExecution Execution { get; internal set; }

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
            Parent.StackTrace = new StackTrace(skipFrames, fNeedFileInfo: true);
        }

        /// <summary>
        /// Executes a workflow Cadence related operation, attempting to detect
        /// when an attempt is made to perform more than one operation in 
        /// parallel, which will likely break workflow determinism.
        /// </summary>
        /// <typeparam name="TResult">The operation result type.</typeparam>
        /// <param name="actionAsync">The workflow action function.</param>
        /// <returns>The action result.</returns>
        internal async Task<TResult> ExecuteNonParallel<TResult>(Func<Task<TResult>> actionAsync)
        {
            try
            {
                if (Interlocked.Increment(ref pendingOperationCount) > 1)
                {
                    throw new WorkflowParallelOperationException();
                }

                return await actionAsync();
            }
            finally
            {
                Interlocked.Decrement(ref pendingOperationCount);
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
        public async Task<DateTime> UtcNowAsync()
        {
            Client.EnsureNotDisposed();
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
        public async Task ContinueAsNewAsync(params object[] args)
        {
            // This method doesn't currently do any async operations but I'd
            // like to keep the method signature async just in case this changes
            // in the future.

            Client.EnsureNotDisposed();
            SetStackTrace();

            await Task.CompletedTask;

            // We're going to throw a [CadenceWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new CadenceContinueAsNewException(
                args:       Client.DataConverter.ToData(args),
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
        public async Task ContinueAsNewAsync(ContinueAsNewOptions options, params object[] args)
        {
            // This method doesn't currently do any async operations but I'd
            // like to keep the method signature async just in case this changes
            // in the future.

            Client.EnsureNotDisposed();
            SetStackTrace();

            await Task.CompletedTask;

            // We're going to throw a [CadenceWorkflowRestartException] with the
            // parameters.  This exception will be caught and handled by the 
            // [WorkflowInvoke()] method which will configure the reply such
            // that the cadence-proxy will be able to signal Cadence to continue
            // the workflow with a clean history.

            throw new CadenceContinueAsNewException(
                args:                       Client.DataConverter.ToData(args),
                domain:                     options.Domain ?? WorkflowInfo.Domain,
                taskList:                   options.TaskList ?? WorkflowInfo.TaskList,
                workflow:                   options.Workflow ?? WorkflowInfo.WorkflowType,
                executionToStartTimeout:    options.ExecutionStartToCloseTimeout,
                scheduleToCloseTimeout:     options.ScheduleToCloseTimeout,
                scheduleToStartTimeout:     options.ScheduleToStartTimeout,
                taskStartToCloseTimeout:    options.TaskStartToCloseTimeout,
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(changeId));
            Covenant.Requires<ArgumentException>(minSupported <= maxSupported);
            Client.EnsureNotDisposed();
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
        /// or <see cref="NewExternalWorkflowStub{TWorkflowInterface}(string, string)"/>.
        /// </summary>
        /// <param name="stub">The child workflow stub.</param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        public async Task<WorkflowExecution> GetWorkflowExecutionAsync(object stub)
        {
            Client.EnsureNotDisposed();
            SetStackTrace();

            // $todo(jeff.lill):
            //
            // Come back to this one after we've implemented the stubs.  This information
            // comes back to the .NET side in [WorkflowExecuteChildReply].

            Covenant.Requires<ArgumentNullException>(stub != null);

            await Task.CompletedTask;
            throw new NotImplementedException();
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));
            Client.EnsureNotDisposed();
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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(id));
            Covenant.Requires<ArgumentNullException>(resultType != null);
            Covenant.Requires<ArgumentNullException>(function != null);
            Client.EnsureNotDisposed();
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
        public async Task<Guid> NewGuidAsync()
        {
            Client.EnsureNotDisposed();
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
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<double> NextRandomDoubleAsync()
        {
            Client.EnsureNotDisposed();
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
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync()
        {
            Client.EnsureNotDisposed();
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
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync(int maxValue)
        {
            Covenant.Requires<ArgumentNullException>(maxValue > 0);
            Client.EnsureNotDisposed();
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
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<int> NextRandomAsync(int minValue, int maxValue)
        {
            Covenant.Requires<ArgumentNullException>(minValue < maxValue);
            Client.EnsureNotDisposed();
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
        /// <remarks>
        /// <note>
        /// The internal random number generator is seeded such that workflow instances
        /// will generally see different sequences of random numbers.
        /// </note>
        /// </remarks>
        public async Task<byte[]> NextRandomBytesAsync(int size)
        {
            Covenant.Requires<ArgumentNullException>(size > 0);
            Client.EnsureNotDisposed();
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
            Covenant.Requires<ArgumentNullException>(function != null);
            Client.EnsureNotDisposed();
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
            Covenant.Requires<ArgumentNullException>(resultType != null);
            Covenant.Requires<ArgumentNullException>(function != null);
            Client.EnsureNotDisposed();
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
            Client.EnsureNotDisposed();
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
            Client.EnsureNotDisposed();
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
        public async Task<bool> IsSetLastCompletionResultAsync()
        {
            Client.EnsureNotDisposed();
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
        public async Task<TResult> GetLastCompletionResultAsync<TResult>()
        {
            Client.EnsureNotDisposed();
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
            SetStackTrace();

            return StubManager.NewActivityStub<TActivityInterface>(Client, this, options);
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
            SetStackTrace();

            return StubManager.NewChildWorkflowStub<TWorkflowInterface>(Client, this, options, workflowTypeName);
        }

        /// <summary>
        /// Creates a typed-safe client stub that can be used to continue the workflow as a new run.
        /// </summary>
        /// <typeparam name="TWorkflowInterface">The workflow interface.</typeparam>
        /// <param name="options">Optionally specifies the new options to use when continuing the workflow.</param>
        /// <returns>The type-safe stub.</returns>
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
            SetStackTrace();

            return StubManager.NewContinueAsNewStub<TWorkflowInterface>(Client, options);
        }

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by a <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <param name="execution">Identifies the workflow.</param>
        /// <returns>The workflow stub.</returns>
        public ExternalWorkflowStub NewExternalWorkflowStub(WorkflowExecution execution)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            Client.EnsureNotDisposed();
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
        public ExternalWorkflowStub NewExternalWorkflowStub(string workflowId, string domain = null)
        {
            Client.EnsureNotDisposed();
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
        ///     Local activity types do not need to be registered and local activities.
        ///     </item>
        ///     <item>
        ///     Local activities must complete within the <see cref="WorkflowOptions.TaskStartToCloseTimeout"/>.
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
            SetStackTrace();

            return StubManager.NewLocalActivityStub<TActivityInterface, TActivityImplementation>(Client, this, options);
        }

#if TODO
        // $todo(jeff.lill): https://github.com/nforgeio/neonKUBE/issues/615

        /// <summary>
        /// Creates a new untyped activity client stub that can be used to launch activities.
        /// </summary>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
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
        public IActivityStub NewUntypedActivityStub(ActivityOptions options = null)
        {
            Client.EnsureNotDisposed();
            SetStackTrace();

            throw new NotImplementedException();
        }
#endif

        /// <summary>
        /// Creates an untyped child workflow stub that can be used to start, signal, and query
        /// child workflows.
        /// </summary>
        /// <param name="workflowTypeName">The workflow type name (see the remarks).</param>
        /// <param name="options">Optionally specifies the child workflow options.</param>
        /// <returns></returns>
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
        public IChildWorkflowStub NewUntypedChildWorkflowStub(string workflowTypeName, ChildWorkflowOptions options = null)
        {
            Client.EnsureNotDisposed();
            SetStackTrace();

            throw new NotImplementedException();
        }

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <param name="execution">The target <see cref="WorkflowExecution"/>.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="ExternalWorkflowStub"/>.</returns>
        public ExternalWorkflowStub NewUntypedExternalWorkflowStub(WorkflowExecution execution, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(execution != null);
            Client.EnsureNotDisposed();
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
        public ExternalWorkflowStub NewUntypedExternalWorkflowStub(string workflowId, string domain = null)
        {
            Client.EnsureNotDisposed();
            SetStackTrace();

            throw new NotImplementedException();
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
        /// <returns>A <see cref="StartChildWorkflowStub{TWorkflowInterface}"/> instance.</returns>
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
        ///         var stub1  = Workflow.NewStartChildWorkflowStub&lt;IMyWorkflow&gt;("child");
        ///         var future = await stub1.StartAsync("FOO");         // Starting the child with param: "FOO"
        ///         var stub2  = Workflow.NewChildWorkflowStub&lt;IMyWorkflow&gt;();
        ///         var value2 = await stub2.DoChildWorkflow("BAR");    // This returns: "BAR"
        ///         var value1 = (int)await future.GetAsync();          // This returns: "FOO"
        ///     }
        ///     
        ///     public Task&lt;string&gt; ChildAsync(string arg)
        ///     {
        ///         return await Task.FromResult(arg);
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Here we call <see cref="NewStartChildWorkflowStub{TWorkflowInterface}(string, ChildWorkflowOptions)"/> specifying
        /// <b>"child"</b> as the workflow method name.  This matches the <c>[WorkflowMethod(Name = "child")]</c>
        /// attribute decorating the <c>ChildAsync()</c> workflow interface method.  Then we start the child workflow by awaiting 
        /// <see cref="StartChildWorkflowStub{TWorkflowInterface}.StartAsync(object[])"/>. This returns an <see cref="IAsyncFuture{T}"/> whose 
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
        public StartChildWorkflowStub<TWorkflowInterface> NewStartChildWorkflowStub<TWorkflowInterface>(string methodName = null, ChildWorkflowOptions options = null)
            where TWorkflowInterface : class
        {
            Client.EnsureNotDisposed();
            SetStackTrace();

            return new StartChildWorkflowStub<TWorkflowInterface>(this, methodName, options);
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
        /// <returns>The new <see cref="StartActivityStub{TActivityInterface}"/>.</returns>
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
        ///         var fiiValue = await fooTask;
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
        ///         var fooStub  = Workflow.NewStartActivityStub("foo");
        ///         var future   = fooStub.StartAsync("FOO");
        ///         var barStub  = Workflow.NewActivityStub&lt;IMyActivity&gt;();
        ///         var barValue = await barStub.BarAsync("BAR");   // Returns: "BAR"
        ///         var fooValue = (int)await future.GetAsync();    // Returns: "FOO"
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Here we call <see cref="NewStartActivityStub{TActivityInterface}(string, ActivityOptions)"/> specifying
        /// <b>"foo"</b> as the workflow method name.  This matches the <c>[ActivityMethod(Name = "foo")]</c> decorating
        /// the <c>FooAsync()</c> activity interface method.  Then we start the first activity by awaiting 
        /// <see cref="StartActivityStub{TActivityInterface}"/>.  This returns an <see cref="IAsyncFuture{T}"/> whose 
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
        public StartActivityStub<TActivityInterface> NewStartActivityStub<TActivityInterface>(string methodName = null, ActivityOptions options = null)
            where TActivityInterface : class
        {
            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));
            Client.EnsureNotDisposed();
            SetStackTrace();

            return new StartActivityStub<TActivityInterface>(this, methodName, options);
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
        ///         var future   = fooStub.StartAsync("FOO");
        ///         var barStub  = Workflow.NewActivityStub&lt;IMyActivity&gt;();
        ///         var barValue = await barStub.BarAsync("BAR");   // Returns: "BAR"
        ///         var fooValue = (int)await future.GetAsync();    // Returns: "FOO"
        ///     }
        /// }
        /// </code>
        /// <para>
        /// Here we call <see cref="NewStartActivityStub{TActivityInterface}(string, ActivityOptions)"/> specifying
        /// <b>"foo"</b> as the workflow method name.  This matches the <c>[ActivityMethod(Name = "foo")]</c> decorating
        /// the <c>FooAsync()</c> activity interface method.  Then we start the first activity by awaiting 
        /// <see cref="StartActivityStub{TActivityInterface}"/>.  This returns an <see cref="IAsyncFuture{T}"/> whose 
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
        public StartLocalActivityStub<TActivityInterface, TActivityImplementation> NewStartLocalActivityStub<TActivityInterface, TActivityImplementation>(string methodName = null, LocalActivityOptions options = null)
            where TActivityInterface : class
            where TActivityImplementation : TActivityInterface
        {
            CadenceHelper.ValidateActivityInterface(typeof(TActivityInterface));
            Client.EnsureNotDisposed();
            SetStackTrace();

            return new StartLocalActivityStub<TActivityInterface, TActivityImplementation>(this, methodName, options);
        }
        
        //---------------------------------------------------------------------
        // Internal activity related methods used by dynamically generated activity stubs.

        /// <summary>
        /// Executes an activity with a specific activity type name and waits for it to complete.
        /// </summary>
        /// <param name="activityTypeName">Identifies the activity.</param>
        /// <param name="args">Optionally specifies the encoded activity arguments.</param>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        internal async Task<byte[]> ExecuteActivityAsync(string activityTypeName, byte[] args = null, ActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(activityTypeName));
            Client.EnsureNotDisposed();
            SetStackTrace(skipFrames: 3);

            options = options ?? new ActivityOptions();
            options = options.Clone();

            if (options.HeartbeatTimeout <= TimeSpan.Zero)
            {
                options.HeartbeatTimeout = Client.Settings.ActivityHeartbeatTimeout;
            }

            if (options.ScheduleToCloseTimeout <= TimeSpan.Zero)
            {
                options.ScheduleToCloseTimeout = Client.Settings.WorkflowScheduleToStartTimeout;
            }

            if (options.ScheduleToStartTimeout <= TimeSpan.Zero)
            {
                options.ScheduleToStartTimeout = Client.Settings.WorkflowScheduleToStartTimeout;
            }

            if (options.StartToCloseTimeout <= TimeSpan.Zero)
            {
                options.StartToCloseTimeout = Client.Settings.WorkflowScheduleToCloseTimeout;
            }

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
        internal long RegisterActivityAction(Type activityType, ConstructorInfo activityConstructor, MethodInfo activityMethod)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null);
            Covenant.Requires<ArgumentNullException>(activityConstructor != null);
            Covenant.Requires<ArgumentException>(activityType.BaseType == typeof(ActivityBase));
            Covenant.Requires<ArgumentNullException>(activityMethod != null);
            Client.EnsureNotDisposed();

            var activityActionId    = Interlocked.Increment(ref nextLocalActivityActionId);

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
        /// <param name="args">Optionally specifies the activity arguments.</param>
        /// <param name="options">Optionally specifies any local activity options.</param>
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
        /// <exception cref="CadenceEntityNotExistsException">Thrown if the named domain does not exist.</exception>
        /// <exception cref="CadenceBadRequestException">Thrown when the request is invalid.</exception>
        /// <exception cref="CadenceInternalServiceException">Thrown for internal Cadence cluster problems.</exception>
        /// <exception cref="CadenceServiceBusyException">Thrown when Cadence is too busy.</exception>
        internal async Task<byte[]> ExecuteLocalActivityAsync(Type activityType, ConstructorInfo activityConstructor, MethodInfo activityMethod, byte[] args = null, LocalActivityOptions options = null)
        {
            Covenant.Requires<ArgumentNullException>(activityType != null);
            Covenant.Requires<ArgumentException>(activityType.BaseType == typeof(ActivityBase));
            Covenant.Requires<ArgumentNullException>(activityConstructor != null);
            Covenant.Requires<ArgumentNullException>(activityMethod != null);
            Client.EnsureNotDisposed();
            SetStackTrace(skipFrames: 3);

            options = options ?? new LocalActivityOptions();
            options = options.Clone();

            if (options.ScheduleToCloseTimeout <= TimeSpan.Zero)
            {
                options.ScheduleToCloseTimeout = Client.Settings.WorkflowScheduleToCloseTimeout;
            }

            var activityActionId = RegisterActivityAction(activityType, activityConstructor, activityMethod);
            
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

            throw new CadenceForceReplayException();
        }
    }
}
