//-----------------------------------------------------------------------------
// FILE:	    SignalMethodAttribute.cs
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

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Used to identify a workflow interface methods as a signal.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
    public class SignalMethodAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">Specifies the Temporal signal name.</param>
        public SignalMethodAttribute(string name)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            this.Name = name;
        }

        /// <summary>
        /// Returns the signal name. 
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// <para>
        /// <b>EXPERIMENTAL:</b> Indicates whether the tagged signal method should be generated 
        /// as a synchronous method rather than as a fire-and-forget asynchronous call, which is
        /// the Temporal default.  This property defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// Synchronous signals are considered experimental which means that this feature will 
        /// probably have a limited lifespan.  Temporal will introduce new <b>update</b>
        /// semantics at some point that will ultimately obsolete synchronous signals.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Normal Temporal signals are sent to workflows asynchronously.  This means that the
        /// signal method being called by the application will return ragardless of whether the
        /// workflow has actually received and processed the signal or not.  This is quite
        /// efficient and has the advantage of not requring the sending application to wait
        /// for a somewhat indeterminate period of time for the workflow to receive and process
        /// the signal.
        /// </para>
        /// <para>
        /// Sometimes though, you calling applications really need to know that the workflow
        /// actually handled a signal before moving on.  Applications may also need information
        /// back from the workflow, such as whether the workflow was able to process the signal
        /// request sucessfully.  So it would be nice if workflow signals could also return a
        /// result.
        /// </para>
        /// <para>
        /// The Neon Temporal client supports synchronous signals by setting this property to
        /// <c>true</c>.  When you do this, the Temporal client allows the signal method to
        /// return a result as a <see cref="Task{T}"/> as well returning just a simple
        /// <see cref="Task"/>.  For both cases, the Temporal client will generate a signal
        /// stub that waits for the signal to be processed by the target workflow before
        /// returning.
        /// </para>
        /// <para>
        /// This is an experimental feature.  Temporal server doesn't currently have a 
        /// synchronous way to interact with a running workflow, so the Neon Temporal client emulates
        /// this behavior using a combination of internal signals and queries.  As a developer, 
        /// you couild have done something like this yourself, but we felt this was going to be 
        /// such a useful  pattern that it was worth building into the client.  This will ultimately
        /// be replaced by upcoming Temporal server features.
        /// </para>
        /// <para>
        /// See the documentation site for more information: <a href="https://doc.neonkube.com/Neon.Temporal-Workflow-SyncSignals.htm">Synchronous Signals</a>
        /// </para>
        /// </remarks>
        public bool Synchronous { get; set; } = false;
    }
}
