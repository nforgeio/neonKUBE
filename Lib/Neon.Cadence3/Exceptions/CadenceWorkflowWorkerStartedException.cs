//-----------------------------------------------------------------------------
// FILE:	    CadenceWorkflowWorkerStartedException.cs
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

namespace Neon.Cadence
{
    /// <summary>
    /// Thrown when an attempt is made to register a workflow implementation after an
    /// workflow worker has been started on a client.  You must register all implementations
    /// before starting a worker.
    /// </summary>
    public class CadenceWorkflowWorkerStartedException : CadenceException
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        internal CadenceWorkflowWorkerStartedException()
            : base("Cannot register a workflow implementation after a workflow worker has been started.")
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Optionally specifies message.</param>
        /// <param name="innerException">Optionally specifies the inner exception.</param>
        public CadenceWorkflowWorkerStartedException(string message = null, Exception innerException = null)
            : base(message, innerException)
        {
        }

        /// <inheritdoc/>
        internal override CadenceErrorTypes CadenceErrorType => CadenceErrorTypes.Custom;
    }
}
