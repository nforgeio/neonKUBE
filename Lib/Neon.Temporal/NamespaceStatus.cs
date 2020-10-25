//-----------------------------------------------------------------------------
// FILE:	    Namespacetatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Indicates a Temporal namespace status.
    /// </summary>
    public enum NamespaceStatus
    {
        /// <summary>
        /// The namespace is registered and active.
        /// </summary>
        [EnumMember(Value = "REGISTERED")]
        Registered = 0,

        /// <summary>
        /// The namespace is closed for new workflows but will remain
        /// until already running workflows are completed and the
        /// history retention period for the last executed workflow
        /// has been satisified.
        /// </summary>
        [EnumMember(Value = "DEPRECATED")]
        Deprecated,

        /// <summary>
        /// The namespace is deleted.
        /// </summary>
        [EnumMember(Value = "DELETED")]
        Deleted
    }
}
