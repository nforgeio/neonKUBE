//-----------------------------------------------------------------------------
// FILE:        WarmTaskAwaiter.cs
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
//
// Code based on a MSDN article by Stephen Toub (MSFT):
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/12/10267069.aspx

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Tasks
{
    /// <summary>
    /// Used by <see cref="WarmTask"/> to implement warm/cold tasks that
    /// don't return a result.
    /// </summary>
    public struct WarmTaskAwaiter : INotifyCompletion
    {
        /// <summary>
        /// Indicates when the operation has been completed.
        /// </summary>
        public bool IsCompleted { get; private set; }

        /// <inheritdoc/>
        public void OnCompleted(Action continuation)
        {
            IsCompleted = true;
            continuation?.Invoke();
        }

        /// <summary>
        /// Returns the <c>void</c> operation result.
        /// </summary>
        public void GetResult()
        {
        }
    }

    /// <summary>
    /// Used by <see cref="WarmTask"/> to implement warm/cold tasks that
    /// do return a result.
    /// </summary>
    /// <typeparam name="T">The operation result type.</typeparam>
    public struct WarmTaskAwaiter<T> : INotifyCompletion
    {
        /// <summary>
        /// Indicates when the operation has been completed.
        /// </summary>
        public bool IsCompleted { get; private set; }

        /// <summary>
        /// The operation result.
        /// </summary>
        public T Result { get; set; }

        /// <inheritdoc/>
        public void OnCompleted(Action continuation)
        {
            IsCompleted = true;
            continuation?.Invoke();
        }

        /// <summary>
        /// Returns the operation result.
        /// </summary>
        public T GetResult()
        {
            return Result;
        }
    }
}