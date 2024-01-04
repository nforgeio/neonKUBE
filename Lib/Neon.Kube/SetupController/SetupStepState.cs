//-----------------------------------------------------------------------------
// FILE:        SetupStepState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Setup
{
    /// <summary>
    /// Enumerates possible status codes for a cluster setup step.
    /// </summary>
    public enum SetupStepState
    {
        /// <summary>
        /// Used to indicate that a node is not involved with a setup step.
        /// </summary>
        NotInvolved = 0,

        /// <summary>
        /// The step is awaiting execution.
        /// </summary>
        Pending,

        /// <summary>
        /// The step is running.
        /// </summary>
        Running,

        /// <summary>
        /// The step was cancelled.
        /// </summary>
        Cancelled,

        /// <summary>
        /// The step has completed successfully.
        /// </summary>
        Done,

        /// <summary>
        /// The step failed.
        /// </summary>
        Failed
    }
}
