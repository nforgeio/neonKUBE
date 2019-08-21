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
    /// Implements a specialized awaitable that returns <c>void</c> and allows operations to
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
    /// Occasionally, you may need more control over exactly when the operation is
    /// performed or perhaps even splitting the operation into two stages, the <b>hot</b>
    /// stage that starts immediately and the completion stage that starts after the caller
    /// awaits the operation.  An operation that defers its implementation until after
    /// it is awaited is known as a cold task and operations that split their implementation
    /// with a <b>hot</b> and <b>cold</b> parts is known as a <b>warm</b> task.
    /// </para>
    /// <para>
    /// This first example demonstrates how to create a task that doesn't schedule a
    /// (cold) action until after the task is awaited.
    /// </para>
    /// <code langiage="c#">
    /// var coldCompleted = false;
    /// var warmTask = new WarmTask(null,
    ///     async () =>
    ///     {
    ///         coldCompleted = true;
    ///         await Task.CompletedTask;
    ///     });
    ///     
    /// // The warm task's cold action won't actually start until after the warm task
    /// // is awaited, so this assertion will always pass.
    /// 
    /// await Task.Delay(TimeSpan.FromSections(1));
    /// Assert.False(coldCompleted);
    /// 
    /// // Await the warm task now and then verify that the cold action executed.
    /// 
    /// await warmTask;
    /// Assert.True(coldCompleted);
    /// </code>
    /// <para>
    /// Here's another example with both hot and cold actions:
    /// </para>
    /// <code language="c#">
    /// var hotCompleted  = false;
    /// var coldCompleted = false;
    /// 
    /// var warmTask = new WarmTask(
    ///     hotAction: async () =>
    ///     {
    ///         hotCompleted = true;
    ///         await Task.CompletedTask;
    ///     },
    ///     coldAction: async () =>
    ///     {
    ///         coldCompleted = true;
    ///         await Task.CompletedTask;
    ///     });
    ///     
    /// // Wait for a bit and then confirm that the hot action 
    /// // executed and the cold action did not.
    /// 
    /// await Task.Delay(TimeSpan.FromSections(1));
    /// Assert.True(hotCompleted);
    /// Assert.False(coldCompleted);
    /// 
    /// // Await the warm task.  This will cause the cold action
    /// // to be scheduled.  We'll wait for a bit and verify that
    /// // both actions now report being executed.
    /// 
    /// await warmTask;
    /// 
    /// Assert.True(hotCompleted);
    /// Assert.True(coldCompleted);
    /// </code>
    /// </remarks>
    public class WarmTask : ICriticalNotifyCompletion
    {
        private TaskCompletionSource<object>    tcs;
        private Func<Task>                      coldAction;
        private Task                            hotTask;

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

            this.coldAction = coldAction;

            if (hotAction != null)
            {
                this.hotTask = Task.Run(() => hotAction());
            }
            else
            {
                this.hotTask = null;
            }

            this.tcs = new TaskCompletionSource<object>();
        }

        /// <summary>
        /// The magic method the C# compiler uses to implement async/await.
        /// </summary>
        /// <returns>The operation's awaiter.</returns>
        public TaskAwaiter GetAwaiter()
        {
            if (hotTask != null)
            {
                // We have a hot action so we need to ensure that it
                // has completed before we start the cold action.

                hotTask.ContinueWith(
                    async state =>
                    {
                        await coldAction();
                        tcs.SetResult(null);
                    });
            }
            else
            {
                // There is no hot action, so we can immediately start the
                // cold action when the WarmTask is awaited.

                Task.Run(() => coldAction())
                    .ContinueWith(
                        async state =>
                        {
                            tcs.SetResult(null);
                            await Task.CompletedTask;
                        });
            }

            return ((Task)tcs.Task).GetAwaiter();
        }

        /// <inheritdoc/>
        public bool IsCompleted { get; private set; }

        /// <inheritdoc/>
        public void OnCompleted(Action continuation)
        {
            if (continuation != null)
            {
                Task.Run(continuation);
            }
        }

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (continuation != null)
            {
                Task.Run(continuation);
            }
        }
    }

    /// <summary>
    /// Implements a specialized awaitable that returns <typeparamref name="TResult"/> and 
    /// allows operations to divide their implementation into two stages: the part that 
    /// executes immediately and the part that executes after the task is actually awaited 
    /// by the caller.
    /// </summary>
    /// <typeparam name="TResult">Specifies the result type.</typeparam>
    /// <remarks>
    /// <para>
    /// Async operation implementations start <b>hot</b>.  This means that the async operation
    /// will be scheduled to run immediately and that the operation may have already completed
    /// before it is awaited.  This is usually the desired behavior.
    /// </para>
    /// <para>
    /// Occasionally, you may need more control over exactly when the operation is
    /// performed or perhaps even splitting the operation into two stages, the <b>hot</b>
    /// stage that starts immediately and the completion stage that starts after the caller
    /// awaits the operation.  An operation that defers its implementation until after
    /// it is awaited is known as a cold task and operations that split their implementation
    /// with a <b>hot</b> and <b>cold</b> parts is known as a <b>warm</b> task.
    /// </para>
    /// <para>
    /// This first example demonstrates how to create a task that doesn't schedule a
    /// (cold) action until after the task is awaited.
    /// </para>
    /// <code langiage="c#">
    /// var coldCompleted = false;
    /// var warmTask = new WarmTask&lt;string&gt;(null,
    ///     async () =>
    ///     {
    ///         coldCompleted = true;
    ///         await Task.FromResult("Hello World!");
    ///     });
    ///     
    /// // The warm task's cold action won't actually start until after the warm task
    /// // is awaited, so this assertion will always pass.
    /// 
    /// await Task.Delay(TimeSpan.FromSections(1));
    /// Assert.False(coldCompleted);
    /// 
    /// // Await the warm task now and then verify that the cold action executed.
    /// 
    /// Assert.Equal("Hello World!", await warmTask);
    /// Assert.True(coldCompleted);
    /// </code>
    /// <para>
    /// Here's another example with both hot and cold actions:
    /// </para>
    /// <code language="c#">
    /// var hotCompleted  = false;
    /// var coldCompleted = false;
    /// 
    /// var warmTask = new WarmTask&lt;string&gt;(
    ///     hotAction: async () =>
    ///     {
    ///         hotCompleted = true;
    ///         await Task.CompletedTask;
    ///     },
    ///     coldAction: async () =>
    ///     {
    ///         coldCompleted = true;
    ///         await Task.FromResult("Hello World!");
    ///     });
    ///     
    /// // Wait for a bit and then confirm that the hot action 
    /// // executed and the cold action did not.
    /// 
    /// await Task.Delay(TimeSpan.FromSections(1));
    /// Assert.True(hotCompleted);
    /// Assert.False(coldCompleted);
    /// 
    /// // Await the warm task.  This will cause the cold action
    /// // to be scheduled.  We'll wait for a bit and verify that
    /// // both actions now report being executed.
    /// 
    /// Assert.Equal("Hello World!", await warmTask);
    /// Assert.True(hotCompleted);
    /// Assert.True(coldCompleted);
    /// </code>
    /// </remarks>
    public class WarmTask<TResult> : ICriticalNotifyCompletion
    {
        private TaskCompletionSource<TResult>   tcs;
        private Func<Task<TResult>>             coldAction;
        private Task                            hotTask;

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
        /// <paramref name="coldAction"/> is the action that returns the overall task result.
        /// </note>
        /// </remarks>
        public WarmTask(Func<Task> hotAction, Func<Task<TResult>> coldAction)
        {
            Covenant.Requires<ArgumentNullException>(coldAction != null);

            this.coldAction = coldAction;

            if (hotAction != null)
            {
                this.hotTask = global::System.Threading.Tasks.Task.Run(() => hotAction());
            }
            else
            {
                this.hotTask = null;
            }

            this.tcs = new TaskCompletionSource<TResult>();
        }

        /// <summary>
        /// The magic method the C# compiler uses to implement async/await.
        /// </summary>
        /// <returns>The operation's awaiter.</returns>
        public TaskAwaiter<TResult> GetAwaiter()
        {
            if (hotTask != null)
            {
                // We have a hot action so we need to ensure that it
                // has completed before we start the cold action.

                hotTask.ContinueWith(
                    async state =>
                    {
                        tcs.SetResult(await coldAction());
                    });
            }
            else
            {
                // There is no hot action, so we can immediately start the
                // cold action when the WarmTask is awaited.

                global::System.Threading.Tasks.Task.Run(() => coldAction())
                    .ContinueWith(
                        async state =>
                        {
                            tcs.SetResult(state.Result);
                            await global::System.Threading.Tasks.Task.CompletedTask;
                        });
            }

            return tcs.Task.GetAwaiter();
        }

        /// <inheritdoc/>
        public bool IsCompleted { get; private set; }

        /// <inheritdoc/>
        public void OnCompleted(Action continuation)
        {
            if (continuation != null)
            {
                global::System.Threading.Tasks.Task.Run(continuation);
            }
        }

        /// <inheritdoc/>
        public void UnsafeOnCompleted(Action continuation)
        {
            if (continuation != null)
            {
                Task.Run(continuation);
            }
        }
    }
}
