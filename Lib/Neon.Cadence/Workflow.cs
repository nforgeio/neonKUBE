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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all application Cadence workflow implementations.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Workflows are pretty easy to implement.  You'll need to derive your custom
    /// workflow class from <see cref="Workflow"/> and implement a public constructor
    /// with a single <see cref="WorkerConstructorArgs"/> parameter and have your
    /// constructor call the corresponding base <see cref="Workflow(WorkerConstructorArgs)"/>)
    /// constructor to initialize the instance.  You'll also need to implement the
    /// <see cref="RunAsync(byte[])"/> method, which is where your workflow logic
    /// will reside.  
    /// </para>
    /// <para>
    /// Here's an overview describing the steps necessary to implement, deploy, and
    /// start a workflow:
    /// </para>
    /// <list type="number">
    /// <item>
    ///     A custom workflow is implemented by deriving a class from <see cref="Workflow"/>,
    ///     implementing the workflow logic via a <see cref="Workflow.RunAsync(byte[])"/>
    ///     method.  Any custom workflow activities will need to be implemented as classes
    ///     derived from <see cref="Activity"/>.
    /// </item>
    /// <item>
    ///     <para>
    ///     The custom <see cref="Workflow"/> class needs to be deployed as a service or
    ///     application that creates a <see cref="CadenceClient"/> connected to a Cadence
    ///     cluster.  This application needs to call <see cref="CadenceClient.StartWorkflowWorkerAsync{TWorkflow}(string, string, WorkerOptions, string)"/>
    ///     and <see cref="CadenceClient.StartActivityWorkerAsync{TActivity}(string, string, WorkerOptions, string)"/> to
    ///     start the workflow and activity workers as required.
    ///     </para>
    ///     <note>
    ///     By default, both workflow and activity workers will be registered using the
    ///     fully qualified name of the custom <see cref="Workflow"/> or <see cref="Activity"/>
    ///     derived implementation classes.  These names can be customized as required.
    ///     </note>
    /// </item>
    /// <item>
    ///     <para>
    ///     A global workflow instance can be started by calling <see cref="CadenceClient.StartWorkflowAsync(string, string, WorkflowOptions, byte[])"/>,
    ///     passing an optional byte array as workflow arguments as well as optional workflow options.  
    ///     Global workflows have no parent, as opposed to child workflows that run in the context of 
    ///     another workflow (the parent).
    ///     </para>
    ///     <note>
    ///     <see cref="CadenceClient.StartWorkflowAsync(string, string, WorkflowOptions, byte[])"/> returns immediately
    ///     after the new workflow has been submitted to Cadence.  This method does not wait
    ///     for the workflow to finish.
    ///     </note>
    /// </item>
    /// <item>
    ///     For Neon Cadence client instances that have started a worker that handles the named workflow,
    ///     Cadence will choose one of the workers and begin executing the workflow there.  The Neon Cadence
    ///     client will instantiate the registered custom <see cref="Workflow"/> call its
    ///     <see cref="Workflow.RunAsync(byte[])"/> method, passing the optional workflow arguments
    ///     encoded as a byte array.
    /// </item>
    /// <item>
    ///     The custom <see cref="Workflow.RunAsync(byte[])"/> method implements the workflow by
    ///     
    /// </item>
    /// <item>
    /// </item>
    /// <item>
    /// </item>
    /// <item>
    /// </item>
    /// </list>
    /// </remarks>
    public abstract class Workflow
    {
        private long workflowContextId;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="args">The low-level worker initialization arguments.</param>
        protected Workflow(WorkerConstructorArgs args)
        {
            Covenant.Requires<ArgumentNullException>(args != null);

            this.Client            = args.Client;
            this.workflowContextId = args.WorkerContextId;
        }

        /// <summary>
        /// Returns the <see cref="CadenceClient"/> managing this workflow.
        /// </summary>
        public CadenceClient Client { get; private set; }

        /// <summary>
        /// Called by Cadence to execute a workflow.  Derived classes will need to implement
        /// their workflow logic here.
        /// </summary>
        /// <param name="args">The workflow arguments encoded into a byte array or <c>null</c>.</param>
        /// <returns>The workflow result encoded as a byte array or <c>null</c>.</returns>
        protected abstract Task<byte[]> RunAsync(byte[] args);

        /// <summary>
        /// Returns <c>true</c> if there is a completion result from previous runs of
        /// this workflow.  This is useful for CRON workflows that would like to pass
        /// ending state from from one workflow run to the next.  This property
        /// indicates whether the last run (if any) returned any state.
        /// </summary>
        protected async Task<bool> HasPreviousRunResultAsync()
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the result from the last workflow run or <c>null</c>.  This is useful 
        /// for CRON workflows that would like to pass information from from one workflow
        /// run to the next.
        /// </summary>
        protected async Task<byte[]> GetPreviousRunResultAsync()
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Called when a workflow has been cancelled and additional cleanup related work
        /// must be performed.  Calling this method allows the workflow to continue
        /// executing activities after the parent workflow has been cancelled.
        /// </summary>
        /// <remarks>
        /// Under the covers, this replaces the underlying workflow context with
        /// a new disconnected context that is independent from the parent workflow's
        /// context.  This method only substitutes the new context for the first call. 
        /// Subsequent calls won't actually do anything.
        /// </remarks>
        protected async Task DisconnectContextAsync()
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns the current time (UTC).
        /// </summary>
        /// <returns>The current workflow time (UTC).</returns>
        protected async Task<DateTime> UtcNowAsync()
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Use this when your workflow needs to obtain an external value that 
        /// may change at runtime.  When a workflow executes this for the first
        /// time, the <paramref name="getter"/> function will be called to
        /// fetch the value and persist it to the workflow history.  When
        /// the workflow is being replayed, the value from the history
        /// will be returned rather than calling the function again.
        /// </summary>
        /// <param name="getter">The value retrival function.</param>
        /// <returns>The requested value as a byte array or <c>null</c>.</returns>
        /// <remarks>
        /// <para>
        /// This mirrors the <b>MutableSideEffect</b> context function
        /// provided by the GOLANG client and is used to ensure that
        /// workflow replays will use the same values as the original
        /// execution.
        /// </para>
        /// <para>
        /// For example, a workflow step may require a random number
        /// when making a decision.  In this case, the workflow would
        /// call <see cref="GetMutableValueAsync(Func{byte[]})"/>, passing a function
        /// that generates a random number.
        /// </para>
        /// <para>
        /// The first time the step is executed, the function will be called,
        /// a random number would be returned, be persisted to the history,
        /// and then to the workflow implementation which would use the 
        /// value when making a decision.  Then, if the workflow needs
        /// to be replayed, and this step is reached, the random number
        /// will be returned from the history rather than calling the 
        /// function again.  This ensures that the original random number
        /// would be returned resulting in the same decision being made
        /// during the replay.
        /// </para>
        /// </remarks>
        protected async Task<byte[]> GetMutableValueAsync(Func<byte[]> getter)
        {
            Covenant.Requires<ArgumentNullException>(getter != null);

            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Pauses the workflow for at least the period specified.
        /// </summary>
        /// <param name="delay">The time to delay.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns></returns>
        /// <exception cref="TaskCanceledException">
        /// Thrown if the operation was cancelled via <see cref="CancellationToken"/> or the
        /// workflow was cancelled externally.
        /// </exception>
        protected async Task SleepAsync(TimeSpan delay, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes a child workflow an waits for it to complete.
        /// </summary>
        /// <param name="name">The workflow name.</param>
        /// <param name="args">Optionally specifies the workflow arguments.</param>
        /// <param name="options">Optionally specifies the workflow options.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The workflow result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        protected async Task<byte[]> CallWorkflow(string name, byte[] args = null, ChildWorkflowOptions options = null, CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes an activity and waits for it to complete.
        /// </summary>
        /// <param name="name">Identifies the activity.</param>
        /// <param name="args">Optionally specifies the activity name.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        protected async Task<byte[]> CallActivity(string name, byte[] args = null)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Executes a local activity and waits for it to complete.
        /// </summary>
        /// <typeparam name="TActivity">Specifies the local activity implementation type.</typeparam>
        /// <param name="args">Optionally specifies the activity name.</param>
        /// <param name="options">Optionally specifies any local activity options.</param>
        /// <returns>The activity result encoded as a byte array.</returns>
        /// <exception cref="CadenceException">
        /// An exception derived from <see cref="CadenceException"/> will be be thrown 
        /// if the child workflow did not complete successfully.
        /// </exception>
        /// <remarks>
        /// This method can be used to optimize activities that will complety quickly
        /// (within seconds).  Rather than scheduling the activity on any worker that
        /// has registered an implementation for the activity, this method will simply
        /// instantiate an instance of <typeparamref name="TActivity"/> and call its
        /// <see cref="Activity.RunAsync(byte[])"/> method.
        /// </remarks>
        protected async Task<byte[]> CallLocalActivity<TActivity>(byte[] args = null, LocalActivityOptions options = null)
            where TActivity : Activity
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Exits and completes the current running workflow and then restarts it, passing the
        /// optional workflow arguments.
        /// </summary>
        /// <param name="args">Optionally specifies the arguments for the new workflow run.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        protected async Task RestartAsync(byte[] args = null)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();
        }
    }
}
