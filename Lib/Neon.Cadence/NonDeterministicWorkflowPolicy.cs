//-----------------------------------------------------------------------------
// FILE:	    WorkerOptions .cs
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Enumerates how a decision task handler deals with mismatched history events 
    /// (presumably arising from non-deterministic workflow definitions). 
    /// </summary>
    public enum NonDeterministicWorkflowPolicy
    {
        /// <summary>
        /// NonDeterministicWorkflowPolicyBlockWorkflow is the default policy for handling detected non-determinism.
        /// This option simply logs to console with an error message that non-determinism is detected, but
        /// does *NOT* reply anything back to the server.
        /// It is chosen as default for backward compatibility reasons because it preserves the old behavior
        /// for handling non-determinism that we had before NonDeterministicWorkflowPolicy type was added to
        /// allow more configurability.
        /// </summary>
        NonDeterministicWorkflowPolicyBlockWorkflow = 0,

        /// <summary>
        /// NonDeterministicWorkflowPolicyFailWorkflow behaves exactly the same as Ignore, up until the very
        /// end of processing a decision task.
        /// Whereas default does *NOT* reply anything back to the server, fail workflow replies back with a request
        /// to fail the workflow execution.
        /// </summary>
        NonDeterministicWorkflowPolicyFailWorkflow = 1
    }
}
