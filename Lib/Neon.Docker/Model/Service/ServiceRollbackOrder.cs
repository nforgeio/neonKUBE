//-----------------------------------------------------------------------------
// FILE:	    ServiceRollbackOrder.cs
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
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service task rollback order options.
    /// </summary>
    public enum ServiceRollbackOrder
    {
        /// <summary>
        /// Stop the current service task before rolling back to the 
        /// previous settings.
        /// </summary>
        [EnumMember(Value = "stop-first")]
        StopFirst = 0,

        /// <summary>
        /// Rollback a current service task to the previous setting first
        /// before stopping the current task.
        /// </summary>
        [EnumMember(Value = "start-first")]
        StartFirst
    }
}
