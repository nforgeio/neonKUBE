//-----------------------------------------------------------------------------
// FILE:	    SignalInvocation.cs
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
    /// arguments to  the workflow logic via a <see cref="WorkflowQueue{T}"/> allowing
    /// the workflow handle the signal by executing activities, child workflows, etc.
    /// This class also provides a way for the workflow to specify the signal reply. 
    /// </para>
    /// <para>
    /// This non-generic version of the class is intended for signals that return <c>void</c>.
    /// Use <see cref="SignalInvocation{TResult}"/> for signals that return a result.
    /// </para>
    /// <note>
    /// This synchronous signals are considered experimental which means that this feature will 
    /// probably have a limited lifespan.  Cadence will probably introduce new <b>update</b>
    /// semantics that will ultimately replace synchronous signals.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Args"/> property returns a dictionary that you can use to marshal 
    /// signal arguments to the workflow logic by adding each argument to the dictionary by
    /// parameter name.  Your signal method should then queue the invocation to a workflow queue
    /// the workflow logic is listening on and then call <see cref="WaitForReturnAsync()"/> wait 
    /// for the workflow logic to call <see cref="ReturnAsync(TimeSpan?)"/> to indicating
    /// that it's time for the signal method to return.
    /// </para>
    /// <para>
    /// Your workflow logic will dequeue the signal invocation, extract the signal arguments 
    /// cast them to the appropriate types, and then perform any necessary operations.
    /// Then call <see cref="ReturnAsync(TimeSpan?)"/> which causes the <see cref="WaitForReturnAsync()"/> 
    /// call in your signal method to complete.
    /// </para>
    /// <note>
    /// <para>
    /// By default, <see cref="ReturnAsync(TimeSpan?)"/> will wait 10 seconds after informing the
    /// signal method to return before the <see cref="ReturnAsync(TimeSpan?)"/> itself returns
    /// to the workflow logic.  This is a bit of a hack that tries to ensure that there's enough
    /// time for Cadence client to query for the signal result before the workflow terminates,
    /// because queries will no longer work after the workflow is terminated.
    /// </para>
    /// <para>
    /// This is somewhat fragile because it depends on the client signal query polling happening at a frequency
    /// less than this delay.  You can customize the delay by setting <see cref="ReturnDelay"/> to
    /// a different value or passing a custome <see cref="TimeSpan"/> to <see cref="ReturnAsync(TimeSpan?)"/>.
    /// </para>
    /// <para>
    /// If you know that your workflow logic will run for some time after processing a synchronous
    /// signal, you can pass <see cref="TimeSpan.Zero"/> to <see cref="ReturnAsync(TimeSpan?)"/>
    /// to avoid any delays.
    /// </para>
    /// </note>
    /// <para>
    /// See the documentation site for more information: <a href="https://doc.neonkube.com/Neon.Cadence-Workflow-SynchronousSignals.htm">Synchronous Signals</a>
    /// </para>
    /// </remarks>
    public class SignalInvocation 
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public SignalInvocation()
        {
            if (string.IsNullOrEmpty(Workflow.Current?.SignalId))
            {
                throw new NotSupportedException($"[{nameof(SignalInvocation)}] may only be constructed within a synchronous workflow signal method.");
            }

            SignalId = Workflow.Current.SignalId;
        }

        /// <summary>
        /// Returns the signal arguments as a dictionary.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, object> Args { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Uniquely identifies the synchronous signal.
        /// </summary>
        [JsonProperty(PropertyName = "SignalId", Required = Required.Always)]
        public string SignalId { get; set; }

        /// <summary>
        /// Specifies the default amount of time the <see cref="ReturnAsync(TimeSpan?)"/> method will
        /// pause before returning to the workflow logic.  This can be overridden via an
        /// optional parameter passed to <see cref="ReturnAsync(TimeSpan?)"/>.  This defaults
        /// to <b>10 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ReturnDelay", Required = Required.Always)]
        public TimeSpan ReturnDelay { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Returns the <see cref="AsyncManualResetEvent"/> used to inform the signal method
        /// that its time to return to its caller.
        /// </summary>
        [JsonIgnore]
        private AsyncManualResetEvent ReturnEvent => Workflow.Current.GetSignalStatus(SignalId).ReturnEvent;

        /// <summary>
        /// Called by the workflow logic when its time for the synchronous signal to
        /// return to the caller.  This will cause the <see cref="WaitForReturnAsync"/>
        /// method called by the signal method to complete.
        /// </summary>
        /// <param name="delay">Optionally overrides <see cref="ReturnDelay"/>.</param>
        public async Task ReturnAsync(TimeSpan? delay = null)
        {
            delay = delay ?? ReturnDelay;

            ReturnEvent.Set();
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
            await ReturnEvent.WaitAsync();
        }
    }

    /// <summary>
    /// <para>
    /// <b>EXPERIMENTAL:</b> Used to relay a received synchronous signal's 
    /// arguments to  the workflow logic via a <see cref="WorkflowQueue{T}"/> allowing
    /// the workflow handle the signal by executing activities, child workflows, etc.
    /// This class also provides a way for the workflow to specify the signal reply. 
    /// </para>
    /// <para>
    /// This generic version of the class is intended for signals that return results.
    /// Use <see cref="SignalInvocation"/> for signals that return <c>void</c>.
    /// </para>
    /// <note>
    /// This synchronous signals are considered experimental which means that this feature will 
    /// probably have a limited lifespan.  Cadence will probably introduce new <b>update</b>
    /// semantics that will ultimately replace synchronous signals.
    /// </note>
    /// </summary>
    /// <typeparam name="TResult">The signal result type.</typeparam>
    /// <remarks>
    /// <para>
    /// The <see cref="Args"/> property returns a dictionary that you can use to marshal 
    /// signal arguments to the workflow logic by adding each argument to the dictionary by
    /// parameter name.  Your signal method should then queue the invocation to a workflow queue
    /// the workflow logic is listening on and then call <see cref="WaitForReturnAsync()"/> wait 
    /// for the workflow logic to call <see cref="ReturnAsync(TResult, TimeSpan?)"/>, indicating
    /// that it's time for the signal method to return.
    /// </para>
    /// <para>
    /// Your workflow logic will dequeue the signal invocation, extract the signal arguments 
    /// cast them to the appropriate types, and then perform any necessary operations.
    /// Then call <see cref="ReturnAsync(TResult, TimeSpan?)"/> which causes the
    /// <see cref="WaitForReturnAsync()"/> call in your signal method to complete.
    /// </para>
    /// <note>
    /// <para>
    /// By default, <see cref="ReturnAsync(TResult, TimeSpan?)"/> will wait 10 seconds after informing the
    /// signal method to return before the <see cref="ReturnAsync(TResult, TimeSpan?)"/> itself returns
    /// to the workflow logic.  This is a bit of a hack that tries to ensure that there's enough
    /// time for Cadence client to query for the signal result before the workflow terminates,
    /// because queries will no longer work after the workflow is terminated.
    /// </para>
    /// <para>
    /// This is somewhat fragile because it depends on the client signal query polling happening at a frequency
    /// less than this delay.  You can customize the delay by setting <see cref="ReturnDelay"/> to
    /// a different value or passing a custome <see cref="TimeSpan"/> to <see cref="ReturnAsync(TResult, TimeSpan?)"/>.
    /// </para>
    /// <para>
    /// If you know that your workflow logic will run for some time after processing a synchronous
    /// signal, you can pass <see cref="TimeSpan.Zero"/> to <see cref="ReturnAsync(TResult, TimeSpan?)"/>
    /// to avoid any delays.
    /// </para>
    /// </note>
    /// <para>
    /// See the documentation site for more information: <a href="https://doc.neonkube.com/Neon.Cadence-Workflow-SynchronousSignals.htm">Synchronous Signals</a>
    /// </para>
    /// </remarks>
    public class SignalInvocation<TResult> : Dictionary<string, object>
    {
        private TResult     result;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SignalInvocation()
        {
        }

        /// <summary>
        /// Returns the signal arguments as a dictionary.
        /// </summary>
        [JsonIgnore]
        public Dictionary<string, object> Args { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Identifies the synchronous signal.
        /// </summary>
        [JsonProperty(PropertyName = "SignalId", Required = Required.Always)]
        public string SignalId { get; set; }

        /// <summary>
        /// Specifies the default amount of time the <see cref="ReturnAsync(TResult, TimeSpan?)"/> method will
        /// pause before returning to the workflow logic.  This can be overridden via an
        /// optional parameter passed to <see cref="ReturnAsync(TResult, TimeSpan?)"/>.
        /// This defaults to <b>10 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ReturnDelay", Required = Required.Always)]
        public TimeSpan ReturnDelay { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Returns the <see cref="AsyncManualResetEvent"/> used to inform the signal method
        /// that its time to return to its caller.
        /// </summary>
        private AsyncManualResetEvent ReturnEvent => Workflow.Current.GetSignalStatus(SignalId).ReturnEvent;

        /// <summary>
        /// Called by the workflow logic when its time for the synchronous signal to
        /// return to the caller.  This will cause the <see cref="WaitForReturnAsync"/>
        /// method called by the signal method to complete.
        /// </summary>
        /// <param name="result">The value to be returned by the signal.</param>
        /// <param name="delay">Optionally overrides <see cref="ReturnDelay"/>.</param>
        public async Task ReturnAsync(TResult result, TimeSpan? delay = null)
        {
            delay = delay ?? ReturnDelay;

            this.result = result;

            ReturnEvent.Set();
            await Workflow.Current.SleepAsync(delay.Value);
        }

        /// <summary>
        /// Called by saynchronous signal methods after queuing the operation to the
        /// workflow logic to wait for the workflow to indicate that its time to return
        /// to the caller.
        /// </summary>
        /// <returns>The result to be returned by the signal.</returns>
        public async Task<TResult> WaitForReturnAsync()
        {
            await ReturnEvent.WaitAsync();

            return result;
        }
    }
}
