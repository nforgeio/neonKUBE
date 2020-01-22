//-----------------------------------------------------------------------------
// FILE:	    SignalRequest.cs
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
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Tasks;

namespace Neon.Cadence
{
    /// <summary>
    /// <para>
    /// <b>EXPERIMENTAL:</b> Used to relay a received synchronous signal's 
    /// arguments to the workflow logic via a <see cref="WorkflowQueue{T}"/> allowing
    /// the workflow handle the signal by executing activities, child workflows, etc.
    /// This class also provides a way for the workflow to specify the signal reply. 
    /// </para>
    /// <para>
    /// This non-generic version of the class is intended for signals that return <c>void</c>.
    /// Use <see cref="SignalRequest{TResult}"/> for signals that return a result.
    /// </para>
    /// <note>
    /// This synchronous signals are considered experimental which means that this feature will 
    /// probably have a limited lifespan.  Cadence will introduce new <b>update</b>
    /// semantics soon that will ultimately replace synchronous signals.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Args"/> property returns a dictionary that is intialized with the
    /// signal arguments keyed by parameter name.  Your signal method should queue this
    /// invocation to a workflow queue your workflow logic is listening on and then call 
    /// <see cref="WaitForReturnAsync()"/> wait signal result returned from the workflow
    /// logic via a <see cref="ReturnAsync(TimeSpan?)"/> call.
    /// </para>
    /// <para>
    /// Your workflow logic will dequeue the signal invocation, extract the signal arguments 
    /// cast them to the appropriate types, and then perform any necessary operations.
    /// Then call <see cref="ReturnAsync(TimeSpan?)"/> which causes the <see cref="WaitForReturnAsync()"/> 
    /// call in your signal method to return so your signal method can return as well.
    /// </para>
    /// <note>
    /// <para>
    /// By default, <see cref="ReturnAsync(TimeSpan?)"/> will wait <see cref="CadenceSettings.SyncSignalReturnDelaySeconds"/> 
    /// (10 seconds) after informing the signal method to return before the <see cref="ReturnAsync(TimeSpan?)"/> itself returns
    /// to the workflow logic.  This is a bit of a hack that tries to ensure that there's enough
    /// time for Cadence client to query for the signal result before the workflow terminates,
    /// because queries will no longer succeed after the workflow is terminated.
    /// </para>
    /// <para>
    /// This is somewhat fragile because it depends on the client signal query polling happening at a frequency
    /// less than this delay.  You can customize the delay by passing a value to optional <see cref="ReturnAsync(TimeSpan?)"/>
    /// <c>delay</c> parameter or modifying <see cref="CadenceSettings.SyncSignalReturnDelaySeconds"/> before
    /// you create the <see cref="CadenceClient"/>.
    /// </para>
    /// <para>
    /// If you know that your workflow logic will run for some time after processing a synchronous
    /// signal, you can pass <see cref="TimeSpan.Zero"/> to <see cref="ReturnAsync(TimeSpan?)"/>
    /// to avoid unnecessary delays.
    /// </para>
    /// </note>
    /// <para>
    /// See the documentation site for more information: <a href="https://doc.neonkube.com/Neon.Cadence-Workflow-SynchronousSignals.htm">Synchronous Signals</a>
    /// </para>
    /// </remarks>
    public class SignalRequest 
    {
        private SyncSignalStatus    signalStatus;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SignalRequest()
        {
            // This class may only be constructed within signal or
            // workflow methods.

            WorkflowBase.CheckCallContext(allowSignal: true, allowWorkflow: true);

            SignalId     = Workflow.Current.SignalId;
            signalStatus = Workflow.Current.GetSignalStatus(SignalId);
        }

        /// <summary>
        /// Uniquely identifies the signal.
        /// </summary>
        [JsonProperty(PropertyName = "SignalId", Required = Required.Always)]
        public string SignalId { get; set; }

        /// <summary>
        /// Returns a dictionary holding the signal arguments keyed by parameter name.  You can
        /// access the arguments here, casting the <see cref="object"/> values as required or
        /// you can use the generic <see cref="Arg{T}(string)"/> method, which is a bit nicer.
        /// </summary>
        [JsonProperty(PropertyName = "Args", Required = Required.Always)]
        public Dictionary<string, object> Args { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Returns the named argument cast into the specified type.
        /// </summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <param name="name">The argument name.</param>
        /// <returns>The argument value cast to <typeparamref name="T"/>.</returns>
        public T Arg<T>(string name)
        {
            return (T)Args[name];
        }

        /// <summary>
        /// Called by the workflow logic when its time for the synchronous signal to
        /// return to the caller.  This will cause the <see cref="WaitForReturnAsync"/>
        /// method called by the signal method to complete.
        /// </summary>
        /// <param name="delay">Optionally overrides <see cref="CadenceSettings.SyncSignalReturnDelaySeconds"/>.</param>
        public async Task ReturnAsync(TimeSpan? delay = null)
        {
            // This may only be called within a workflow method.

            WorkflowBase.CheckCallContext(allowWorkflow: true);

            // Signal [WaitForReturnAsync()] that it should return now.

            signalStatus.Result    = null;  // No result for void signals
            signalStatus.Completed = true;
            signalStatus.ReturnEvent.Set();

            // Delay returning to give the client a chance to poll for
            // the signal result.

            delay = delay ?? Workflow.Current.Client.Settings.SyncSignalReturnDelay;

            await Workflow.Current.SleepAsync(delay.Value);
        }

        /// <summary>
        /// Called by saynchronous signal methods after queuing the operation to the
        /// workflow logic to wait for the workflow to indicate that its time to return
        /// to the caller.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task WaitForReturnAsync()
        {
            // This may only be called within a signal method.

            WorkflowBase.CheckCallContext(allowSignal: true);

            // Wait for the signal from [ReturnAsync()] before returning.

            await signalStatus.ReturnEvent.WaitAsync();
        }
    }
}
