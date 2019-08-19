//-----------------------------------------------------------------------------
// FILE:        WarmTask.cs
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

#if TODO

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Tasks
{
    /// <summary>
    /// Implements a specialized form of <see cref="Task"/> that allows operations to
    /// divide their implementation into two stages: the part that executes immediately
    /// and the part that executes after the task is actually awaited by the caller.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Async operation implementations start <b>hot</b>.  This means that the async operation
    /// will be scheduled to run immediately and that the operation may have already completed
    /// before it is awaited.  This is usually the desired behavior.
    /// </para>
    /// <para>
    /// Occassionally though, you may need more control over exactly when the operation is
    /// performed or perhaps even splitting the operation into two stages, the <b>hot</b>
    /// stage that starts immediately and the completion stage that starts after the caller
    /// awaits the operation.  An operation that defers its implementation until after
    /// it is awaited is known as a cold task and operations that split their implementation
    /// with a <b>hot</b> and <b>cold</b> parts is known as a <b>warm</b> task.
    /// </para>
    /// <para>
    /// The <see cref="WarmTask"/> and <see cref="WarmTask{T}"/> classes provide an
    /// easy way to implement both warm and cold tasks using asynchronous unctions.
    /// </para>
    /// </remarks>
    public class WarmTask
    {
        private readonly TaskCompletionSource<object> awaiter;

        public WarmTask()
        {
        }

        /// <summary>
        /// Constructs a <see cref="WarmTask"/> that immediately schedules the optional
        /// <paramref name="hotAction"/> for asynchronous execution and then executes the
        /// <paramref name="coldAction"/> when the constructed <see cref="WarmTask"/>
        /// is awaited.
        /// </summary>
        /// <param name="hotAction">The hot action (can be <c>null</c>).</param>
        /// <param name="coldAction">The cold action (required).</param>
        public WarmTask(Func<Task> hotAction, Func<Task> coldAction)
        {
            Covenant.Requires<ArgumentNullException>(coldAction != null);
        }

        /// <summary>
        /// The magic method the C# compiler uses to implement async/await.
        /// </summary>
        /// <returns>The operation's awaiter.</returns>
        public Task GetAwaiter() => awaiter.Task;
    }

    /// <summary>
    /// Implements a specialized form of <see cref="Task{T}"/> that allows operations to
    /// divide their implementation into two stages: the part that executes immediately
    /// and the part that executes after the task is actually awaited by the caller.
    /// </summary>
    /// <typeparam name="TResult">The operation result type.</typeparam>
    /// <remarks>
    /// <para>
    /// Async operation implementations start <b>hot</b>.  This means that the async operation
    /// will be scheduled to run immediately and that the operation may have already completed
    /// before it is awaited.  This is usually the desired behavior.
    /// </para>
    /// <para>
    /// Occassionally though, you may need more control over exactly when the operation is
    /// performed or perhaps even splitting the operation into two stages, the <b>hot</b>
    /// stage that starts immediately and the completion stage that starts after the caller
    /// awaits the operation.  An operation that defers its implementation until after
    /// it is awaited is known as a cold task and operations that split their implementation
    /// with a <b>hot</b> and <b>cold</b> parts is known as a <b>warm</b> task.
    /// </para>
    /// <para>
    /// The <see cref="WarmTask"/> and <see cref="WarmTask{T}"/> classes provide an
    /// easy way to implement both warm and cold tasks using asynchronous unctions.
    /// </para>
    /// </remarks>
    public class WarmTask<TResult>
    {
        private readonly TaskCompletionSource<TResult> awaiter;

        /// <summary>
        /// Constructs a <see cref="WarmTask"/> that immediately schedules the optional
        /// <paramref name="hotAction"/> for asynchronous execution and then executes the
        /// <paramref name="coldAction"/> when the constructed <see cref="WarmTask"/>
        /// is awaited.
        /// </summary>
        /// <param name="hotAction">The hot action (can be <c>null</c>).</param>
        /// <param name="coldAction">The cold action (required).</param>
        /// <remarks>
        /// <note>
        /// The <paramref name="hotAction"/> returns only void because it's intended
        /// only to initiate the operation where as the required <paramref name="coldAction"/>
        /// is actually a function that returns the result.
        /// </note>
        /// </remarks>
        public WarmTask(Action hotAction, Func<TResult> coldAction)
        {
            Covenant.Requires<ArgumentNullException>(coldAction != null);
        }

        /// <summary>
        /// The magic method the C# compiler uses to implement async/await.
        /// </summary>
        /// <returns>The operation's awaiter.</returns>
        public Task<TResult> GetAwaiter() => awaiter.Task;
    }
}

#endif
