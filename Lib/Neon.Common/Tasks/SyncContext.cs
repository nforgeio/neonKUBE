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

using Neon.Common;
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
    /// I renamed the structure, converted it into a singleton and added an optional <see cref="Task.Yield()"/>
    /// call and a global mode to tune the operation for server vs. UI applications.
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
    /// accomplish this, you'll simply await <see cref="SyncContext.Clear()"/> at or near the top of your 
    /// async methods:
    /// </para>
    /// <para>
    /// The global <see cref="Mode"/> property controls what the <see cref="Clear()"/> method actually does.
    /// This defaults to <see cref="SyncContextMode.Disabled"/> which turns <see cref="Clear()"/> into a NOP
    /// which is probably suitable for most non-UI applications that reduce overhead and increase performance.
    /// </para>
    /// <para>
    /// UI applications should probably set the <see cref="SyncContextMode.ClearAndYield"/> which prevents
    /// nested method continuations from running on the UI thread and also ensures that any initial synchronous
    /// code won't run on the UI thread either.
    /// </para>
    /// <code language="C#">
    /// using Neon.Task;
    /// 
    /// public async Task&lt;string&gt; HelloAsync()
    /// {
    ///     // On UI thread
    ///     
    ///     await SyncContext.Clear;
    ///     
    ///     // On background thread
    ///     
    ///     SlowSyncOperation();
    ///     
    ///     // On background thread
    ///     
    ///     await DoSomthingAsync();
    ///     
    ///     // On background thread
    ///     
    ///     await DoSomethingElseAsync();
    ///     
    ///     // On background thread
    ///     
    ///     return "Hello World!";
    /// }
    ///
    /// public async Task Main(string[] args)
    /// {
    ///     // Set a mode suitable for UI apps.
    ///     
    ///     SyncContext.Mode = SyncContextMode.ClearAndYield;
    ///     
    ///     // Assume that we're running on a UI thread here.
    ///     
    ///     var greeting = await HelloAsync();
    ///     
    ///     // On UI thread
    /// }
    /// </code>
    /// <para>
    /// This example sets the <see cref="SyncContextMode.ClearAndYield"/> mode 
    /// and then awaits <c>HelloAsync()</c> which clears the sync context and
    /// then performs a long running synchronous operation and then two async
    /// operations.  Note how all of the continuations in <c>HelloAsync()</c>
    /// after the clear are running on a background thread but the continuation
    /// after <c>await HelloAsync()</c> is back to running on the UI thread.
    /// </para>
    /// <para>
    /// This is pretty close to being ideal behavior.
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
        /// <see cref="Clear"/> is not a method.
        /// </note>
        /// <para>
        /// Awaiting this property clears the current synchronization context such 
        /// that the subsequent <c>async</c> calls will each marshal back to threads
        /// obtained from the thread pool and due to the compiler's async magic,
        /// the original synchronization context will be restored before the
        /// <c>HelloAsync()</c> method returns.
        /// </para>
        /// <para>
        /// The <see cref="Mode"/> property controls what awaiting <see cref="Clear"/>
        /// actually does.  This defaults to <see cref="SyncContextMode.Disabled"/>
        /// which is probably suitable for most non-UI applications.  UI applications
        /// will probably want to explicitly set <see cref="SyncContextMode.ClearAndYield"/>
        /// to help keep continations off the UI thread, which is often desirable.
        /// </para>
        /// </remarks>
        public static SyncContext Clear { get; private set; } = new SyncContext(0);

        /// <summary>
        /// <para>
        /// Used to control what <see cref="Clear()"/> actually does.  This defaults to
        /// <see cref="SyncContextMode.Disabled"/> which is probably suitable for most
        /// non-UI applications by reducing task overhead.  UI application will probably
        /// want to set <see cref="SyncContextMode.ClearAndYield"/> to keep work from
        /// running on the UI thread.
        /// </para>
        /// <para>
        /// This defaults to <see cref="SyncContextMode.Disabled"/> for server code,
        /// because we're writing more server applications than UI applications these
        /// days.
        /// </para>
        /// </summary>
        public static SyncContextMode Mode { get; set; } = SyncContextMode.Disabled;

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="stub">Ignored.</param>
        private SyncContext(int stub)
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
            if (Mode == SyncContextMode.Disabled)
            {
                return;
            }

            var previousContext = SynchronizationContext.Current;

            try
            {
                SynchronizationContext.SetSynchronizationContext(null);

                if (Mode == SyncContextMode.ClearAndYield)
                {
                    Task.Run(() => continuation());
                }
                else
                {
                    continuation();
                }
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
