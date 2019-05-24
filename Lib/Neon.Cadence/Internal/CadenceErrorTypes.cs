//-----------------------------------------------------------------------------
// FILE:	    CadenceErrorTypes.cs
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

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Enumerates the Cadence error types.
    /// </summary>
    public enum CadenceErrorTypes
    {
        /// <summary>
        /// An operation was cancelled.
        /// </summary>
        [EnumMember(Value = "cancelled")]
        Cancelled,

        /// <summary>
        /// Custom error.
        /// </summary>
        [EnumMember(Value = "custom")]
        Custom,

        /// <summary>
        /// Generic error.
        /// </summary>
        [EnumMember(Value = "generic")]
        Generic,

        /// <summary>
        /// Panic error.
        /// </summary>
        [EnumMember(Value = "panic")]
        Panic,

        /// <summary>
        /// Terminated error.
        /// </summary>
        [EnumMember(Value = "terminated")]
        Terminated,

        /// <summary>
        /// Timeout error.
        /// </summary>
        [EnumMember(Value = "timeout")]
        Timeout
    }
}
