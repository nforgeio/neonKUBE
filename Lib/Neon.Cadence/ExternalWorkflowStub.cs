//-----------------------------------------------------------------------------
// FILE:	    ExternalWorkflowStub.cs
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
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Supports signalling and cancelling any workflow.  This is useful when an
    /// external workflow interface type is not known at compile time or to manage 
    /// workflows written in another language.
    /// </summary>
    public class ExternalWorkflowStub
    {
        private CadenceClient   client;
        private string          domain;

        /// <summary>
        /// Internal constructor by workflow execution.
        /// </summary>
        /// <param name="client">Specifies the associated client.</param>
        /// <param name="execution">Specifies the target workflow execution.</param>
        /// <param name="domain">Optionally specifies the target domain (defaults to the client's default domain).</param>
        internal ExternalWorkflowStub(CadenceClient client, WorkflowExecution execution, string domain = null)
        {
            Covenant.Requires<ArgumentNullException>(client != null);
            Covenant.Requires<ArgumentNullException>(execution != null);
            Covenant.Requires<ArgumentException>(execution.IsFullyInitialized);

            this.client    = client;
            this.domain    = client.ResolveDomain(domain);
            this.Execution = execution;
        }

        /// <summary>
        /// Returns the workflow execution.
        /// </summary>
        public WorkflowExecution Execution { get; private set; }

        /// <summary>
        /// Cancels the workflow.
        /// </summary>
        public async Task CancelAsync()
        {
            await client.CancelWorkflowAsync(Execution, domain);
        }

        /// <summary>
        /// Signals the workflow.
        /// </summary>
        /// <param name="signalName">Specifies the signal name.</param>
        /// <param name="args">Specifies the signal arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task Signal(string signalName, params object[] args)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalName));
            Covenant.Requires<ArgumentNullException>(args != null);

            await client.SignalWorkflowAsync(Execution, signalName, client.DataConverter.ToData(args));
        }

        /// <summary>
        /// Waits for the workflow complete if necessary, without returning the result.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task GetResultAsync()
        {
            await client.GetWorkflowResultAsync(Execution, domain);
        }

        /// <summary>
        /// Returns the workflow result, waiting for the workflow to complete if necessary.
        /// </summary>
        /// <typeparam name="TResult">The workflow result type.</typeparam>
        /// <returns>The workflow result.</returns>
        public async Task<TResult> GetResultAsync<TResult>()
        {
            return client.DataConverter.FromData<TResult>(await client.GetWorkflowResultAsync(Execution, domain));
        }
    }
}
