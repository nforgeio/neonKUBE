//-----------------------------------------------------------------------------
// FILE:	    DomainStatus.cs
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
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Indicates a Cadence domain status.
    /// </summary>
    public enum DomainStatus
    {
        /// <summary>
        /// The status cannot be determined.
        /// </summary>
        [EnumMember(Value = "UNSPECIFIED")]
        Unspecified = 0,

        /// <summary>
        /// The domain is registered and active.
        /// </summary>
        [EnumMember(Value = "REGISTERED")]
        Registered,

        /// <summary>
        /// The domain is closed for new workflows but will remain
        /// until already running workflows are completed and the
        /// history retention period for the last executed workflow
        /// has been satisified.
        /// </summary>
        [EnumMember(Value = "DEPRECATED")]
        Deprecated,

        /// <summary>
        /// The domain is deleted.
        /// </summary>
        [EnumMember(Value = "DELETED")]
        Deleted
    }
}
