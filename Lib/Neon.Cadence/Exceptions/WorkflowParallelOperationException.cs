//-----------------------------------------------------------------------------
// FILE:	    WorkflowParallelOperationException.cs
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

using Neon.Cadence.Internal;

namespace Neon.Cadence
{
    /// <summary>
    /// Thrown when an operation is requested on an executing workflow while 
    /// another operation is already pending.  Workflows cannot have multiple
    /// operations running in parallel because this will likely break 
    /// workflow determinism.
    /// </summary>
    public class WorkflowParallelOperationException : Exception
    {
        /// <summary>
        /// Consutuctor.
        /// </summary>
        public WorkflowParallelOperationException()
            : base("Workflows cannot perform multiple operations in parallel.  Are you missing an \"await\"?")
        {
        }
    }
}
