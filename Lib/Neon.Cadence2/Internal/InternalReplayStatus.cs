//-----------------------------------------------------------------------------
// FILE:	    InternalReplayStatus.cs
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
using System.Runtime.Serialization;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Indicates a workflow's current replay status.
    /// </summary>
    internal enum InternalReplayStatus
    {
        /// <summary>
        /// Indicates that the corresponding operation cannot determine the replay
        /// status (e.g. because the it didn't relate to an executing workflow).
        /// This is the default value.
        /// </summary>
        [EnumMember(Value = "Unspecified")]
        Unspecified = 0,

        /// <summary>
        /// The related workflow is not replaying.
        /// </summary>
        [EnumMember(Value = "NotReplaying")]
        NotReplaying,

        /// <summary>
        /// The related workflow is replaying.
        /// </summary>
        [EnumMember(Value = "Replaying")]
        Replaying
    }
}
