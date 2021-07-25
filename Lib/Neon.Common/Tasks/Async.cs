//-----------------------------------------------------------------------------
// FILE:        Async.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
// http://blogs.msdn.com/b/pfxteam/archive/2012/02/11/10266920.aspx

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Tasks
{
    /// <summary>
    /// <see cref="Task"/> related utilities.
    /// </summary>
    public static class Async
    {
        // $todo(jefflill):
        //
        // We should consider supporting async work item selectors like Stephen Toub
        // does in his example:
        //
        //      https://devblogs.microsoft.com/pfxteam/implementing-a-simple-foreachasync/
        //
        // This is low-priority though and perhaps we'll never really need that.
        // I'm just including the link here in case we want to revisit this in
        // the future.

        /// <summary>
        /// Iterates over a set of work items and executes an async action for each item.
        /// The method returns when all of the actions have completed.
        /// </summary>
        /// <typeparam name="TWorkItem">The work item type.</typeparam>
        /// <param name="workItems">The work item collection.</param>
        /// <param name="action">The async action to be executed on each work item.</param>
        /// <param name="maxParallel">Optionally specifies the maximum number of tasks to execute in parallel (defaults to <c>1</c>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="AggregateException">Thrown when any of the actions failed.</exception>
        /// <remarks>
        /// <para>
        /// The actions will be executed on threads from the thread pool which means
        /// that the number of tasks that can be executed in parallel will be limited
        /// by the number of available pooled threads.
        /// </para>
        /// <note>
        /// The order in which work items are executed is not defined.
        /// </note>
        /// </remarks>
        public static async Task ForEachAsync<TWorkItem>(
            IEnumerable<TWorkItem>  workItems, 
            Func<TWorkItem, Task>   action, 
            int                     maxParallel = 1)
        {
            Covenant.Requires<ArgumentNullException>(workItems != null, nameof(workItems));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));
            Covenant.Requires<ArgumentException>(maxParallel >= 1, nameof(maxParallel));

            var taskGate = new SemaphoreSlim(initialCount: maxParallel, maxCount: maxParallel);

            await Task.WhenAll(from item in workItems select ProcessAsync(item, action, taskGate));
        }

        /// <summary>
        /// Handles async processing of each work item from a <see cref="ForEachAsync{TWorkItem}(IEnumerable{TWorkItem}, Func{TWorkItem, CancellationToken, Task}, CancellationToken, int)"/> 
        /// call (without a <see cref="CancellationToken"/>).
        /// </summary>
        /// <typeparam name="TWorkItem">The work item type.</typeparam>
        /// <param name="workItem">The work item.</param>
        /// <param name="action">The async action to be executed on each work item.</param>
        /// <param name="taskGate">The <see cref="SemaphoreSlim"/> used to limit the number of tasks executing in parallel.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ProcessAsync<TWorkItem>(
            TWorkItem                   workItem,
            Func<TWorkItem, Task>       action,
            SemaphoreSlim               taskGate)
        {
            await taskGate.WaitAsync();

            try 
            {
                await action(workItem);
            }
            finally
            {
                taskGate.Release(); 
            }
        }

        /// <summary>
        /// Iterates over a set of work items and executes an async action for each item.
        /// The method returns when all of the actions have completed.
        /// </summary>
        /// <typeparam name="TWorkItem">The work item type.</typeparam>
        /// <param name="workItems">The work item collection.</param>
        /// <param name="action">The async action to be executed on each work item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="maxParallel">Optionally specifies the maximum number of tasks to execute in parallel (defaults to <c>1</c>).</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="AggregateException">Thrown when any of the actions failed.</exception>
        /// <remarks>
        /// <para>
        /// The actions will be executed on threads from the thread pool which means
        /// that the number of tasks that can be executed in parallel will be limited
        /// by the number of available pooled threads.
        /// </para>
        /// <note>
        /// The order in which work items are executed is not defined.
        /// </note>
        /// </remarks>
        public static async Task ForEachAsync<TWorkItem>(
            IEnumerable<TWorkItem>                      workItems, 
            Func<TWorkItem, CancellationToken, Task>    action,
            CancellationToken                           cancellationToken,
            int                                         maxParallel = 1)
        {
            Covenant.Requires<ArgumentNullException>(workItems != null, nameof(workItems));
            Covenant.Requires<ArgumentNullException>(action != null, nameof(action));
            Covenant.Requires<ArgumentException>(maxParallel >= 1, nameof(maxParallel));

            var taskGate = new SemaphoreSlim(initialCount: maxParallel, maxCount: maxParallel);

            await Task.WhenAll(from item in workItems select ProcessAsync(item, action, cancellationToken, taskGate));
        }

        /// <summary>
        /// Handles async processing of each work item from a <see cref="ForEachAsync{TWorkItem}(IEnumerable{TWorkItem}, Func{TWorkItem, CancellationToken, Task}, CancellationToken, int)"/> 
        /// call (with a <see cref="CancellationToken"/>).  This override supports a <see cref="CancellationToken"/>.
        /// </summary>
        /// <typeparam name="TWorkItem">The work item type.</typeparam>
        /// <param name="workItem">The work item.</param>
        /// <param name="action">The async action to be executed on each work item.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="taskGate">The <see cref="SemaphoreSlim"/> used to limit the number of tasks executing in parallel.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ProcessAsync<TWorkItem>(
            TWorkItem                                   workItem,
            Func<TWorkItem, CancellationToken, Task>    action,
            CancellationToken                           cancellationToken,
            SemaphoreSlim                               taskGate)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await taskGate.WaitAsync(cancellationToken);

            try
            {
                await action(workItem, cancellationToken);
            }
            finally
            {
                taskGate.Release();
            }
        }
    }
}
