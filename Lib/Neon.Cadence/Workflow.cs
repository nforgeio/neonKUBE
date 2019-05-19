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
    /// Here's how workflows work:
    /// </para>
    /// <list type="number">
    ///     <item>
    ///     Somebody kicks off a workflow by calling <see cref="CadenceClient.address"/>
    ///     </item>
    ///     <item>
    ///     </item>
    ///     <item>
    ///     </item>
    ///     <item>
    ///     </item>
    ///     <item>
    ///     </item>
    ///     <item>
    ///     </item>
    ///     <item>
    ///     </item>
    ///     <item>
    ///     </item>
    ///     <item>
    ///     </item>
    ///     <item>
    ///     </item>
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
        protected Task<byte[]> GetMutableValueAsync(Func<byte[]> getter)
        {
            Covenant.Requires<ArgumentNullException>(getter != null);

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

            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            throw new NotImplementedException();
        }
    }
}
