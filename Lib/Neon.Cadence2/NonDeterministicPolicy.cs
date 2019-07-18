//-----------------------------------------------------------------------------
// FILE:	    NonDeterministicPolicy .cs
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
    public enum NonDeterministicPolicy
    {
        /// <summary>
        /// This policy logs an error to the console but does not reply to the server.  This is
        /// the default policy.
        /// </summary>
        BlockWorkflow = 0,

        /// <summary>
        /// This policy signals Cadence to fail the workflow.
        /// </summary>
        FailWorkflow = 1
    }
}
