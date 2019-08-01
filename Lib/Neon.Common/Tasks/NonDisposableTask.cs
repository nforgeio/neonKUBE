//-----------------------------------------------------------------------------
// FILE:        NonDisposableTask.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Tasks
{
    /// <summary>
    /// Used to convert a <see cref="Task"/> into an awaitable that that does not
    /// implement <see cref="IDisposable"/>.  This is useful for avoiding confusion
    /// and hard to debug problems when async methods return an <see cref="IDisposable"/>
    /// intended to be referenced in a <c>using</c> statement.  It is very easy to forget
    /// the <c>await</c> keyword in this situation and because <see cref="Task"/>
    /// also implements <see cref="IDisposable"/>, there will be no compiler error
    /// or warning.  Wrapping the task with this structure addresses this.
    /// </summary>
    public struct NonDisposableTask
    {
        private readonly Task task;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="task">The task being wrapped.</param>
        public NonDisposableTask(Task task)
        {
            Covenant.Requires<ArgumentNullException>(task != null);

            this.task = task;
        }

        /// <summary>
        /// Returns the task's awaiter.
        /// </summary>
        public TaskAwaiter GetAwaiter() => task.GetAwaiter();
    }

    /// <summary>
    /// Used to convert a <see cref="Task{T}"/> into an awaitable that that does not
    /// implement <see cref="IDisposable"/>.  This is useful for avoiding confusion
    /// and hard to debug problems when async methods return an <see cref="IDisposable"/>
    /// intended to be referenced in a <c>using</c> statement.  It is very easy to forget
    /// the <c>await</c> keyword in this situation and because <see cref="Task{T}"/>
    /// also implements <see cref="IDisposable"/>, there will be no compiler error
    /// or warning.  Wrapping the task with this structure addresses this.
    /// </summary>
    /// <typeparam name="T">The task result type.</typeparam>
    public struct NonDisposableTask<T>
    {
        private readonly Task<T> task;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="task">The task being wrapped.</param>
        public NonDisposableTask(Task<T> task)
        {
            Covenant.Requires<ArgumentNullException>(task != null);

            this.task = task;
        }

        /// <summary>
        /// Returns the task's awaiter.
        /// </summary>
        public TaskAwaiter<T> GetAwaiter() => task.GetAwaiter();
    }
}
