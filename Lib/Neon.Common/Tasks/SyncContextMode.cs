//-----------------------------------------------------------------------------
// FILE:        SyncContextMode.cs
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
//
// Code based on a MSDN article by Stephen Toub (MSFT):
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10267069.aspx

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Tasks
{
    /// <summary>
    /// Enumerates the <see cref="SyncContext"/> modes, configured by setting <see cref="SyncContext.Mode"/>.
    /// </summary>
    public enum SyncContextMode
    {
        /// <summary>
        /// Prevents `await SyncContext.Clear;` from actually doing anothing special.
        /// This mode is probably suitable for most non-UI applications.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// <para>
        /// Enables `await SyncContext.Clear;` such that the continuations within the
        /// nested scope of the method will not happen on the original thread.
        /// </para>
        /// <para>
        /// There may be some use for this mode for server applications.
        /// </para>
        /// </summary>
        ClearOnly,

        /// <summary>
        /// <para>
        /// Enables `await SyncContext.Clear;` such that the continuations within the
        /// nested scope of the method will not happen on the original thread and that
        /// the awaitee can immediately release the current thread for other uses.
        /// </para>
        /// <para>
        /// This mode is useful for UI applications because it ensures that any synchronous
        /// operations won't be executed on the original thread.
        /// </para>
        /// </summary>
        ClearAndYield
    }
}
