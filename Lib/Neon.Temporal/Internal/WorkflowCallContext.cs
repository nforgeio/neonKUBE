//-----------------------------------------------------------------------------
// FILE:	    WorkflowCallContext.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Temporal.Internal;
using Neon.Time;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Enumerates the possible contexts workflow code may be executing within.
    /// This is used to limit what code can do (i.e. query methods shouldn't be
    /// allowed to execute activities).  This is also used in some situations to
    /// modify how workflow code behaves.
    /// </summary>
    internal enum WorkflowCallContext
    {
        /// <summary>
        /// The current task is not executing within the context
        /// of any workflow method.
        /// </summary>
        None = 0,

        /// <summary>
        /// The current task is executing within the context of
        /// a workflow entrypoint.
        /// </summary>
        Entrypoint,

        /// <summary>
        /// The current task is executing within the context of a
        /// workflow signal method.
        /// </summary>
        Signal,

        /// <summary>
        /// The current task is executing within the context of a
        /// workflow query method.
        /// </summary>
        Query,

        /// <summary>
        /// The current task is executing within the context of a
        /// normal or local activity.
        /// </summary>
        Activity
    }
}
