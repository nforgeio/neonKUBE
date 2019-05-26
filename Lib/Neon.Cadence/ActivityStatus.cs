//-----------------------------------------------------------------------------
// FILE:	    ActivityStatus.cs
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
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Enumerates the state of an activity.
    /// </summary>
    public enum ActivityStatus
    {
        // WARNING: These values must match those defined by [InternalPendingActivityState].

        /// <summary>
        /// The activity is waiting to be started.
        /// </summary>
        Scheduled = 0,

        /// <summary>
        /// The activity is running.
        /// </summary>
        Started = 1,

        /// <summary>
        /// The activity has a cancellation request pending.
        /// </summary>
        CancelRequested = 2
    }
}
