//-----------------------------------------------------------------------------
// FILE:        SyncContext.cs
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
    /// Used by public <c>async</c> library methods to reset the current task
    /// <see cref="SynchronizationContext"/> so that continuations won't be 
    /// marshalled back to the current thread which can cause serious problems
    /// for UI apps.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class was adapted from this blog post:
    /// </para>
    /// <para>
    /// <a href="https://blogs.msdn.microsoft.com/benwilli/2017/02/09/an-alternative-to-configureawaitfalse-everywhere/"/>
    /// </para>
    /// <para>
    /// The only real changes are that I renamed the structure and converted
    /// it into a singleton.
    /// </para>
    /// <para>
    /// The <b>async/await</b> pattern is more complex than it seems because the code after the await may
    /// run on the same thread that performed the await in some circumstances (e.g. for UI applications)
    /// or on another thread in other environments.  Library code needs to adapt to both situations.
    /// </para>
    /// <para>
    /// UI platforms like WinForms, WPF, UXP,... require that all user interface manipulation happen
    /// on the UI thread and the synchronization context in these cases will be configured to have 
    /// all awaits default to continuing on the calling (typically UI) thread to make it easy for 
    /// developers to await a long running operation and then update the UI afterwards.
    /// </para>
    /// <para>
    /// The problem for UI applications is that if the awaited operation internally awaits on additional
    /// operations (which is quite common), then each of the internal operations will also continue on
    /// the UI thread.  This can be big problem because there's only one UI thread and continuing on
    /// the UI thread means that the operation needs to be queued to the application's dispatcher possibly
    /// leading to serious performance and usability issues.
    /// </para>
    /// <para>
    /// This is less of a problem for console and server apps where awaited operations generally continue
    /// on any free threadpool thread, but <see cref="Task"/> scheduling can be customized so this isn't
    /// necessarily always the case.
    /// </para>
    /// <para>
    /// As the blog post linked above describes, developers are encouraged to call <see cref="Task.ConfigureAwait(bool)"/>,
    /// passing <c>false</c> for every <c>async</c> call where the result doesn't need to be marshalled back
    /// to the original synchronization context.  Non-UI class libraries typically don't care about this.
    /// The problem is that to do this properly, MSFT recommends that you call <c>Task.ConfigureAwait(false)</c>
    /// on <b>EVERY</b> <c>async</c> call you made in these situations.  This is pretty ugly and will be tough
    /// to enforce on large projects over long periods of time because it's just too easy to miss one.
    /// </para>
    /// <para>
    /// It's also likely that async library methods will be called serveral, perhaps hundreds of times by
    /// applications and it's a shame to require application developers to call <see cref="Task.ConfigureAwait(bool)"/>
    /// everywhere rather than somehow having the library APIs handle this.
    /// </para>
    /// <para>
    /// This <c>struct</c> implements a custom awaiter that saves the current synchronization context and then
    /// clears it for the rest of the current method execution and then restores the original context when
    /// when the method returns.  This means that every subsequent <c>await</c>  performed within the method will 
    /// simply fetch a pool thread to continue execution, rather than to the original context thread.  To
    /// accomplish this, you'll simply await <see cref="SyncContext.Clear"/> at or near the top of your method:
    /// </para>
    /// <code language="C#">
    /// using Neon.Task;
    /// 
    /// public async Task&lt;string&gt; HelloAsync()
    /// {
    ///     await SyncContext.Clear;
    ///     
    ///     await DoSomthingAsync();
    ///     await DoSomethingElseAsync();
    ///     
    ///     return "Hello World!";
    /// }
    /// </code>
    /// <note>
    /// <see cref="Clear"/> is not a method so you don't need to pass any parameters.
    /// </note>
    /// <para>
    /// This call clears the current synchronization context such that the
    /// subsequent <c>async</c> calls will each marshal back to threads
    /// obtained from the thread pool and due to compiler async magic,
    /// the original synchronization context will be restored before the
    /// <c>HelloAsync()</c> method returns.
    /// </para>
    /// </remarks>
    public struct SyncContext : INotifyCompletion
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// <c>await</c> this singleton to clear the current synchronization
        /// context for the scope of the current method as a potential performance
        /// optimization.  The original context will be restored when the method 
        /// returns.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You'll typically at or near the top of your method.  This will
        /// look something like:
        /// </para>
        /// <code language="C#">
        /// using Neon.Task;
        /// 
        /// public async Task&lt;string&gt; HelloAsync()
        /// {
        ///     await SyncContext.Clear;
        ///     
        ///     await DoSomthingAsync();
        ///     await DoSomethingElseAsync();
        ///     
        ///     return "Hello World!";
        /// }
        /// </code>
        /// <note>
        /// <see cref="Clear"/> is not a method so you don't
        /// need to pass any parameters.
        /// </note>
        /// <para>
        /// This call clears the current synchronization context such that the
        /// subsequent <c>async</c> calls will each marshal back to threads
        /// obtained from the thread pool and due to the compiler's async magic,
        /// the original synchronization context will be restored before the
        /// <c>HelloAsync()</c> method returns.
        /// </para>
        /// </remarks>
        public static SyncContext Clear { get; private set; }

        /// <summary>
        /// <para>
        /// Optionally disables context resetting globally.  This provides an
        /// escape hatch for situations where an application needs to revert
        /// back to the default synchronization context behavior.  This turns
        /// <c>await SyncContext.Clear</c> calls into a NOP.
        /// </para>
        /// <note>
        /// Most applications should never need to set this.
        /// </note>
        /// </summary>
        public static bool IsDisabled { get; set; } = false;

        /// <summary>
        /// Static constructor.
        /// </summary>
        static SyncContext()
        {
            Clear = new SyncContext(0);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="unused">Ignored.</param>
        private SyncContext(int unused)
        {
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Do not call this directly.
        /// </summary>
        public bool IsCompleted => SynchronizationContext.Current == null;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Do not call this directly.
        /// </summary>
        /// <param name="continuation">The continuation action.</param>
        public void OnCompleted(Action continuation)
        {
            if (IsDisabled)
            {
                continuation();
                return;
            }

            var previousContext = SynchronizationContext.Current;

            try
            {
                SynchronizationContext.SetSynchronizationContext(null);
                continuation();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Do not call this directly.
        /// </summary>
        public SyncContext GetAwaiter()
        {
            return this;
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Do not call this directly.
        /// </summary>
        public void GetResult()
        {
        }
    }
}
