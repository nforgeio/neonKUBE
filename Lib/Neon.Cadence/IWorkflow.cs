//-----------------------------------------------------------------------------
// FILE:	    IWorkflow.cs
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
    /// <summary>
    /// Provides useful information and functionality for workflow implementations.
    /// This will be available via the <see cref="IWorkflowBase.Workflow"/> property.
    /// </summary>
    public interface IWorkflow
    {
        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this workflow.
        /// </summary>
        CadenceClient Client { get; }

        /// <summary>
        /// Creates a client stub that can be used to launch one or more activity instances
        /// via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TActivity">The activity interface.</typeparam>
        /// <param name="options">Optionally specifies the activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
        /// <remarks>
        /// <note>
        /// Unlike workflow stubs, a single activity stub instance can be used to
        /// launch multiple activities.
        /// </note>
        /// <para>
        /// Activities launched by the returned stub will be scheduled normally
        /// by Cadence to executed on one of the worker nodes.  Use <see cref="NewLocalActivityStub{TActivity}(ActivityOptions)"/>
        /// to execute short-lived activities locally within the current process.
        /// </para>
        /// </remarks>
        TActivity NewActivityStub<TActivity>(ActivityOptions options = null)
            where TActivity : IActivityBase;

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
        /// by Cadence to executed on one of the worker nodes.  Use <see cref="NewLocalActivityStub{TActivity}(ActivityOptions)"/>
        /// to execute short-lived activities locally within the current process.
        /// </para>
        /// </remarks>
        IActivityStub NewUntypedActivityStub(ActivityOptions options = null);

        /// <summary>
        /// Creates a client stub that can be used to launch one or more local activity 
        /// instances via the type-safe interface methods.
        /// </summary>
        /// <typeparam name="TActivity">The activity interface.</typeparam>
        /// <param name="options">Optionally specifies activity options.</param>
        /// <returns>The new <see cref="IActivityStub"/>.</returns>
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
        ///     Local activities must complete within the <see cref="WorkflowOptions.DecisionTaskStartToCloseTimeout"/>.
        ///     This defaults to 10 seconds and can be set to a maximum of 60 seconds.
        ///     </item>
        ///     <item>
        ///     Local activities cannot heartbeat.
        ///     </item>
        /// </list>
        /// </remarks>
        TActivity NewLocalActivityStub<TActivity>(ActivityOptions options = null)
            where TActivity : IActivityBase;

        /// <summary>
        /// Creates a workflow client stub that can be used to launch, signal, and query child
        /// workflows via the type-safe workflow interface methods.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow interface.</typeparam>
        /// <param name="options"></param>
        /// <returns>The child workflow stub.</returns>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        TWorkflow NewChildWorkflowStub<TWorkflow>(ChildWorkflowOptions options = null)
            where TWorkflow : IWorkflowBase;

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow interface.</typeparam>
        /// <param name="execution">Identifies the workflow execution.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the domain of the parent workflow.</param>
        /// <returns>The workflow stub.</returns>
        TWorkflow NewExternalWorkflowStub<TWorkflow>(WorkflowExecution execution, string domain = null)
            where TWorkflow : IWorkflowBase;

        /// <summary>
        /// Creates a workflow client stub that can be used communicate with an
        /// existing workflow identified by workflow ID.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow interface.</typeparam>
        /// <param name="workflowId">Identifies the workflow.</param>
        /// <param name="domain">Optionally specifies the domain.  This defaults to the domain of the parent workflow.</param>
        /// <returns>The workflow stub.</returns>
        TWorkflow NewExternalWorkflowStub<TWorkflow>(string workflowId, string domain = null)
            where TWorkflow : IWorkflowBase;

        /// <summary>
        /// Creates an untyped child workflow stub that can be used to start, signal, and query
        /// child workflows.
        /// </summary>
        /// <param name="workflowTypeName">The workflow type name.</param>
        /// <param name="options">Optionally specifies the child workflow options.</param>
        /// <returns></returns>
        /// <remarks>
        /// Unlike activity stubs, a workflow stub may only be used to launch a single
        /// workflow.  You'll need to create a new stub for each workflow you wish to
        /// invoke and then the first method called on a workflow stub must be
        /// the one of the methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// </remarks>
        IChildWorkflowStub NewUntypedChildWorkflowStub(string workflowTypeName, ChildWorkflowOptions options = null);

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its <see cref="WorkflowExecution"/>.
        /// </summary>
        /// <param name="execution">The target <see cref="WorkflowExecution"/>.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="IExternalWorkflowStub"/>.</returns>
        IExternalWorkflowStub NewUntypedExternalWorkflowStub(WorkflowExecution execution, string domain = null);

        /// <summary>
        /// Creates an untyped stub that can be used to signal or cancel a child
        /// workflow identified by its workflow ID.
        /// </summary>
        /// <param name="workflowId">The target workflow ID.</param>
        /// <param name="domain">Optionally specifies the target domain.  This defaults to the parent workflow's domain.</param>
        /// <returns>The <see cref="IExternalWorkflowStub"/>.</returns>
        IExternalWorkflowStub NewUntypedExternalWorkflowStub(string workflowId, string domain = null);

        /// <summary>
        /// Returns the <see cref="WorkflowExecution"/> for a child workflow.
        /// </summary>
        /// <param name="childWorkflowStub">
        /// A child workflow stub created.  This may be a type-safe,
        /// external, or untyped workflow stub instance.
        /// </param>
        /// <returns>The <see cref="WorkflowExecution"/>.</returns>
        Task<WorkflowExecution> GetWorkflowExecutionAsync(object childWorkflowStub);

        /// <summary>
        /// Creates a typed-safe client stub that can be used to continue the workflow as a new run.
        /// </summary>
        /// <typeparam name="TWorkflow">The workflow interface.</typeparam>
        /// <param name="options">Optionally specifies the new options to use when continuing the workflow.</param>
        /// <returns>The type-safe stub.</returns>
        /// <remarks>
        /// The workflow stub returned is intended just for continuing the workflow by
        /// calling one of the workflow entry point methods tagged by <see cref="WorkflowMethodAttribute"/>.
        /// Any signal or query methods defined by <typeparamref name="TWorkflow"/> will 
        /// throw a <see cref="InvalidOperationException"/> when called.
        /// </remarks>
        Task<TWorkflow> NewContinueAsNewStub<TWorkflow>(ContinueAsNewOptions options = null)
            where TWorkflow : IWorkflowBase;

        /// <summary>
        /// Continues the current workflow as a new run using the same workflow options.
        /// </summary>
        /// <param name="args">The new run arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task ContinueAsNew(params object[] args);

        /// <summary>
        /// Continues the current workflow as a new run allowing the specification of
        /// new workflow uptions.
        /// </summary>
        /// <param name="options">The continuation options.</param>
        /// <param name="args">The new run arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task ContinueAsNew(ContinueAsNewOptions options, params object[] args);

        /// <summary>
        /// Returns information about the running workflow.
        /// </summary>
        WorkflowInfo WorkflowInfo { get; }

        // $todo(jeff.lill): Do something equivalent to the Java client's cancellation scope.
        // $todo(jeff.lill): Do something equivalent to the Java client's queues.

        /// <summary>
        /// <para>
        /// Returns the current workflow time (UTC).
        /// </para>
        /// <note>
        /// This must used instead of calling <see cref="DateTime.UtcNow"/> or any other
        /// time method to guarantee determinism when a workflow is replayed.
        /// </note>
        /// </summary>
        DateTime UtcNow { get; }

        /// <summary>
        /// <para>
        /// Pauses the workflow for the specified time span.
        /// </para>
        /// <note>
        /// This must be used instead of calling <see cref="Task.Delay(TimeSpan)"/> or <see cref="Thread.Sleep(TimeSpan)"/>
        /// to guarantee determinism when a workflow is replayed.
        /// </note>
        /// <note>
        /// Cadence time interval resolution is limited to whole seconds and
        /// the duration will be rounded up to the nearest second.
        /// </note>
        /// </summary>
        /// <param name="duration">The duration to pause.</param>
        /// <returns>The tracking <see cref="Task"/></returns>
        Task SleepAsync(TimeSpan duration);

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
        Task<Guid> NewGuidAsync();

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
        /// The random number generator is seeded with a different value for each
        /// workflow run.
        /// </note>
        /// </remarks>
        Task<int> NextRandomAsync();

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
        /// The random number generator is seeded with a different value for each
        /// workflow run.
        /// </note>
        /// </remarks>
        Task<int> NextRandomAsync(int maxValue);

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
        /// The random number generator is seeded with a different value for each
        /// workflow run.
        /// </note>
        /// </remarks>
        Task<int> NextRandomAsync(int minValue, int maxValue);

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
        /// The random number generator is seeded with a different value for each
        /// workflow run.
        /// </note>
        /// </remarks>
        Task<double> NextDouble();

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
        bool IsReplaying { get; }

        /// <summary>
        /// Returns the execution information for the current workflow.
        /// </summary>
        WorkflowExecution Execution { get; }

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
        Task<int> GetVersionAsync(string changeId, int minSupported, int maxSupported);

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
        Task<T> SideEffectAsync<T>(Func<T> function);

        /// <summary>
        /// Calls the specified function and records the value returned in the workflow
        /// history such that subsequent calls will return the same value.
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
        Task<object> SideEffectAsync(Type resultType, Func<object> function);

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
        Task<T> MutableSideEffectAsync<T>(string id, Func<T> function);

        /// <summary>
        /// Calls the specified function and then searches the workflow history
        /// to see if a value was already recorded with the specified <paramref name="id"/>.
        /// If no value has been recorded for the <paramref name="id"/> or the
        /// value returned by the function will be recorded, replacing any existing
        /// value.  If the function value is the same as the history value, then
        /// nothing will be recorded.
        /// </summary>
        /// <param name="id">Identifies the value in the workflow history.</param>
        /// <param name="resultType">Specifies the result type.</param>
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
        /// <para>
        /// The .NET version of this method currently works a bit differently than
        /// the Java and GOLANG clients which will only call the function once.
        /// The .NET implementation calls the function every time 
        /// <see cref="MutableSideEffectAsync(string, Type, Func{object})"/>
        /// is called but it will ignore the all but the first call's result.
        /// </para>
        /// <para>
        /// This is an artifact of how the .NET client is currently implemented
        /// and may change in the future.  You should take care not to code your
        /// application to depend on this behavior (one way or the other).
        /// </para>
        /// </note>
        /// </remarks>
        Task<object> MutableSideEffectAsync(string id, Type resultType, Func<object> function);
    }
}
