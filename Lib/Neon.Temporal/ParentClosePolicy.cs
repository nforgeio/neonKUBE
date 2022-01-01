//-----------------------------------------------------------------------------
// FILE:	    ParentClosePolicy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Enumerates the possible child workflow behaviors when the parent
    /// workflow is closed.
    /// </summary>
    public enum ParentClosePolicy
    {
        // WARNING: These definitions must match those defined for [InternalParentClosePolicy].

        /// <summary>
        /// Unspecified parent close policy.
        /// </summary>
        Unspecified = 0,

        /// <summary>
        /// All open child workflows will be terminated when parent workflow is closed.
        /// </summary>
        Terminate = 1,

        /// <summary>
        /// Cancel requests will be sent to all open child workflows to all open child 
        /// workflows when parent workflow is closed.    This is the <b>default policy</b>.
        /// </summary>
        RequestCancel = 2,

        /// <summary>
        /// Child workflow execution will continue unaffected when parent workflow is closed,
        /// essentially becoming orphaned.
        /// </summary>
        Abandon = 3
    }
}
