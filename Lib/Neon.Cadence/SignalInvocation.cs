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
    /// This class inherits from a dictionary that maps strings to objects with the intention
    /// that you'll use this to marshal signal arguments to the workflow logic by adding each
    /// argument to the dictionary by parameter name.  The signal method will then queue the
    /// invocation to a workflow queue the workflow logic is listening on and then call
    /// <see cref="WaitForReturnAsync()"/> wait for the workflow logic to call <see cref="Return"/>
    /// to indicate that it's time for the signal method to return.
    /// </para>
    /// <para>
    /// Your workflow logic will dequeue the signal invocation, extract the signal arguments 
    /// and cast them to the appropriate types, and then perform any necessary operations.
    /// When the operations are complete, thew workflow logic and call <see cref="Return()"/> 
    /// which causes <see cref="WaitForReturnAsync()"/> and ultimately the signal method to 
    /// return.
    /// </para>
    /// <para>
    /// See the documentation site for more information: <a href="https://doc.neonkube.com/Neon.Cadence-Workflow-SynchronousSignals.htm">Synchronous Signals</a>
    /// </para>
    /// </remarks>
    public class SignalInvocation : Dictionary<string, object>
    {
        private AsyncManualResetEvent   returnEvent = new AsyncManualResetEvent(initialState: false);

        /// <summary>
        /// Constructor.
        /// </summary>
        public SignalInvocation()
        {
        }

        /// <summary>
        /// Called by the workflow logic when its time for the synchronous signal to
        /// return to the caller.  This will cause the <see cref="WaitForReturnAsync"/>
        /// method called by the signal method to complete.
        /// </summary>
        public void Return()
        {
            returnEvent.Set();
        }

        /// <summary>
        /// Called by saynchronous signal methods after queuing the operation to the
        /// workflow logic to wait for the workflow to indicate that its time to return
        /// to the caller.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task WaitForReturnAsync()
        {
            await returnEvent.WaitAsync();
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
    /// This class inherits from a dictionary that maps strings to objects with the intention
    /// that you'll use this to marshal signal arguments to the workflow logic by adding each
    /// argument to the dictionary by parameter name.  The signal method will then queue the
    /// invocation to a workflow queue the workflow logic is listening on and then call
    /// <see cref="WaitForReturnAsync()"/> to wait for the the workflow logic to call
    /// <see cref="Return(TResult)"/>, passing the result to be returned by the signal method.
    /// </para>
    /// <para>
    /// Your workflow logic will dequeue the signal invocation, extract the signal arguments 
    /// and cast them to the appropriate types, and then perform any necessary operations.
    /// When the operations are complete, thew workflow logic and call <see cref="Return(TResult)"/> 
    /// which causes <see cref="WaitForReturnAsync()"/> and ultimately the signal method to 
    /// return.
    /// </para>
    /// <para>
    /// See the documentation site for more information: <a href="https://doc.neonkube.com/Neon.Cadence-Workflow-SynchronousSignals.htm">Synchronous Signals</a>
    /// </para>
    /// </remarks>
    public class SignalInvocation<TResult> : Dictionary<string, object>
    {
        private AsyncManualResetEvent   returnEvent = new AsyncManualResetEvent(initialState: false);
        private TResult                 result;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SignalInvocation()
        {
        }

        /// <summary>
        /// Called by the workflow logic when its time for the synchronous signal to
        /// return to the caller.  This will cause the <see cref="WaitForReturnAsync"/>
        /// method called by the signal method to complete.
        /// </summary>
        /// <param name="result">The value to be returned by the signal.</param>
        public void Return(TResult result)
        {
            this.result = result;

            returnEvent.Set();
        }

        /// <summary>
        /// Called by saynchronous signal methods after queuing the operation to the
        /// workflow logic to wait for the workflow to indicate that its time to return
        /// to the caller.
        /// </summary>
        /// <returns>The result to be returned by the signal.</returns>
        public async Task<TResult> WaitForReturnAsync()
        {
            await returnEvent.WaitAsync();

            return result;
        }
    }
}
