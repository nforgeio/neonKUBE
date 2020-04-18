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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Tasks;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
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
    /// probably have a limited lifespan.  Temporal will introduce new <b>update</b>
    /// semantics at some point that will ultimately obsolete synchronous signals.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <see cref="Args"/> property returns a dictionary that is intialized with the
    /// signal arguments keyed by parameter name.  Your signal method should queue this
    /// request to a workflow queue your workflow logic is listening on and then return
    /// from your signal method.
    /// </para>
    /// <para>
    /// Your workflow logic will dequeue the signal request, extract the signal arguments 
    /// cast them to the appropriate types, and then perform any necessary operations.
    /// Then call <see cref="ReplyAsync()"/> which indicates that signal processing
    /// is finished.
    /// </para>
    /// <para>
    /// See the documentation site for more information: <a href="https://doc.neonkube.com/Neon.Temporal-Workflow-SynchronousSignals.htm">Synchronous Signals</a>
    /// </para>
    /// </remarks>
    public class SignalRequest 
    {
        private SyncSignalStatus                    cachedSignalStatus;
        private ReadOnlyDictionary<string, object>  cachedArgs;

        /// <summary>
        /// Constructor.
        /// </summary>
        public SignalRequest()
        {
            // This class may only be constructed within signal or workflow methods.

            WorkflowBase.CheckCallContext(allowSignal: true, allowWorkflow: true);

            SignalId = Workflow.Current.SignalId;
        }

        /// <summary>
        /// Uniquely identifies the signal.
        /// </summary>
        [JsonProperty(PropertyName = "SignalId", Required = Required.Always)]
        public string SignalId { get; set; }

        /// <summary>
        /// Returns the signal status for this signal request.
        /// </summary>
        [JsonIgnore]
        private SyncSignalStatus SignalStatus
        {
            get
            {
                if (cachedSignalStatus != null)
                {
                    return cachedSignalStatus;
                }

                return cachedSignalStatus = Workflow.Current.GetSignalStatus(SignalId);
            }
        }

        /// <summary>
        /// Returns the dictionary holding the signal arguments keyed by parameter name.  You can
        /// access the arguments here, casting the <see cref="object"/> values as required or
        /// you can use the generic <see cref="Arg{T}(string)"/> method, which is a bit nicer.
        /// </summary>
        [JsonIgnore]
        public ReadOnlyDictionary<string, object> Args
        {
            get
            {
                if (cachedArgs != null)
                {
                    return cachedArgs;
                }

                return cachedArgs = new ReadOnlyDictionary<string, object>(SignalStatus.Args);
            }
        }

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
        /// Called by your workflow logic to indicate that processing for the synchronous
        /// signal is complete.  This method also waits for a period of time before
        /// returning to help ensure that the signal result can be delivered back to
        /// the calling client before the workflow terminates.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task ReplyAsync()
        {
            // This may only be called within a workflow method.

            WorkflowBase.CheckCallContext(allowWorkflow: true);

            // Save the signal completion so a subsequent polling query can retrieve it.

            SignalStatus.Result    = null;  // NULL result for void signal methods
            SignalStatus.Completed = true;

            await Task.CompletedTask;
        }
    }
}
