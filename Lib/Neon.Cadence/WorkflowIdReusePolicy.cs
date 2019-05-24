//-----------------------------------------------------------------------------
// FILE:	    WorkflowIdReusePolicy.cs
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
    /// Enumerates the workflow ID reuse policies.
    /// </summary>
    public enum WorkflowIdReusePolicy
    {
        /// <summary>
        /// Allows starting a workflow with the same ID as another workflow when
        /// that workflow is not running and the last execution close state 
        /// is in [terminated, cancelled, timeouted, failed].
        /// </summary>
        AllowDuplicateFailedOnly = 0,

        /// <summary>
        /// Allows starting a workflow with the same ID as another workflow when
        /// that workflow is not running for any reason.
        /// </summary>
        AllowDuplicate = 1,

        /// <summary>
        /// Prohibits starting a workflow with the same ID as another workflow,
        /// running or not.
        /// </summary>
        RejectDuplicate = 2
    }
}
