//-----------------------------------------------------------------------------
// FILE:	    ProcessTerminator.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Tasks;

namespace Neon.Kube.Service
{
    /// <summary>
    /// Gracefully handles SIGTERM signals sent to a process to terminate itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class listens for a termination signal and then gives the process some time
    /// to gracefully save state.  The termination timeout defaults to 10 seconds but
    /// a custom value may be passed to the constructor.
    /// </para>
    /// <note>
    /// The parent process or operating system typically enforces its own maximum
    /// timeout, so your process may still be killed before your timeout is reached.
    /// </note>
    /// <para>
    /// This class provides two ways for the application to reach to a termination
    /// signal.  Programs using the async/await pattern can monitor the <see cref="System.Threading.CancellationToken"/>
    /// returned by the <see cref="CancellationToken"/> property.
    /// </para>
    /// <para>
    /// Applications may also use <see cref="AddHandler(Action)"/> to add one more more
    /// methods that will be called when a termination signal is received.  Each handler
    /// will be called in parallel on its own thread.
    /// </para>
    /// <para>
    /// Finally, you map pass one or more <see cref="IDisposable"/> instances to <see cref="AddDisposable(IDisposable)"/>.
    /// <see cref="IDisposable.Dispose()"/> will be called for each of these in parallel
    /// on its own thread.  This can be a handy way of hooking <see cref="AsyncPeriodicTask"/>
    /// instances and other structures into a <see cref="ProcessTerminator"/>.
    /// </para>
    /// <para>
    /// Applications should call <see cref="ReadyToExit"/> when they have gracefully stopped
    /// any activities and saved state so that the process will be terminated immediately.
    /// Otherwise, the process will be terminated when the parent process' timeout
    /// is finally exceeded.
    /// </para>
    /// <para>
    /// Applications can also call <see cref="Exit(int)"/> to proactively signal that
    /// the process should terminate gracefully.
    /// </para>
    /// </remarks>
    public sealed class ProcessTerminator
    {
        private INeonLogger                 log;
        private CancellationTokenSource     cts;
        private bool                        terminating;
        private bool                        readyToExit;
        private List<Action>                handlers;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="log">The optional <see cref="INeonLogger"/> used for logging.</param>
        /// <param name="timeout">The optional termination timeout (defaults to 10 seconds).</param>
        public ProcessTerminator(INeonLogger log = null, TimeSpan timeout = default)
        {
            this.log = log;

            if (timeout <= TimeSpan.Zero)
            {
                timeout = TimeSpan.FromSeconds(10);
            }

            this.Timeout  = timeout;
            this.cts      = new CancellationTokenSource();
            this.handlers = new List<Action>();

            AssemblyLoadContext.Default.Unloading +=
                context =>
                {
                    ExitInternal();
                };
        }

        /// <summary>
        /// Returns the termination timeout.
        /// </summary>
        public TimeSpan Timeout { get; private set; }

        /// <summary>
        /// Adds a termination handler.
        /// </summary>
        /// <param name="handler">The handler callback.</param>
        public void AddHandler(Action handler)
        {
            if (handler == null)
            {
                return;
            }

            lock (handlers)
            {
                handlers.Add(handler);
            }
        }

        /// <summary>
        /// Adds a <see cref="IDisposable"/> instance that will be disposed when the
        /// process is being terminated.  This can be a handy way to hook <see cref="AsyncPeriodicTask"/>
        /// and other components into a <see cref="ProcessTerminator"/>.
        /// </summary>
        /// <param name="disposable"></param>
        public void AddDisposable(IDisposable disposable)
        {
            Covenant.Requires<ArgumentNullException>(disposable != null, nameof(disposable));

            // We're simply going to add a handler that disposes the instance
            // to keep things super simple.

            AddHandler(() => disposable.Dispose());
        }

        /// <summary>
        /// Returns the <see cref="CancellationTokenSource"/> that can be used to
        /// cancel any outstanding operations before terminating a process.
        /// </summary>
        public CancellationTokenSource CancellationTokenSource
        {
            get { return cts; }
        }

        /// <summary>
        /// Returns the <see cref="CancellationToken"/> that will be cancelled when a
        /// termination signal is received or <see cref="Exit(int)"/> is called explicitly.
        /// </summary>
        public CancellationToken CancellationToken
        {
            get { return cts.Token; }
        }

        /// <summary>
        /// Returns <c>true</c> when the application has been signalled to terminate.
        /// </summary>
        public bool TerminateNow => CancellationToken.IsCancellationRequested;

        /// <summary>
        /// Optionally indicates that the terminator should not actually terminate
        /// the hosting process.  This is typically enabled for testing or debugging.
        /// </summary>
        public bool DisableProcessExit { get; set; }

        /// <summary>
        /// Indicates that the application has gracefully stopped and is 
        /// ready to be terminated.
        /// </summary>
        public void ReadyToExit()
        {
            readyToExit = true;
        }

        /// <summary>
        /// Emulates a signal instructing the service to close.  This will typically be used
        /// for unit testing services.
        /// </summary>
        /// <exception cref="TimeoutException">
        /// Thrown if the service did not exit gracefully in time before it would have 
        /// been killed (e.g. by Kubernetes or Docker).
        /// </exception>
        public void Signal()
        {
            if (readyToExit)
            {
                // Application has already indicated that it has terminated.

                return;
            }

            var isTerminating = terminating;

            terminating = true;

            if (isTerminating)
            {
                return;     // Already terminating.
            }

            log?.LogInfo(() => $"Emulated stop request: [timeout={Timeout}]");

            cts.Cancel();

            lock (handlers)
            {
                foreach (var handler in handlers)
                {
                    new Thread(new ThreadStart(handler)).Start();
                }
            }

            StopEvent.Set();

            try
            {
                NeonHelper.WaitFor(() => readyToExit, Timeout);
                log?.LogInfo(() => "Process stopped gracefully.");
            }
            catch (TimeoutException)
            {
                log?.LogWarn(() => $"Process did not stop within [{Timeout}].");
                throw;
            }
        }

        /// <summary>
        /// Returns the <see cref="AsyncManualResetEvent"/> that will be raised when
        /// the service is being stopped.
        /// </summary>
        public AsyncManualResetEvent StopEvent { get; private set; } = new AsyncManualResetEvent();

        /// <summary>
        /// Cleanly terminates the current process (for internal use).
        /// </summary>
        /// <param name="exitCode">Optional process exit code (defaults to <b>0</b>).</param>
        /// <param name="explicitTermination">Optionally indicates that termination is not due to receiving an external signal.</param>
        private void ExitInternal(int exitCode = 0, bool explicitTermination = false)
        {
            if (readyToExit)
            {
                // Application has already indicated that it has terminated.

                return;
            }

            var isTerminating = terminating;

            terminating = true;

            if (isTerminating)
            {
                return;     // Already terminating.
            }

            if (explicitTermination)
            {
                log?.LogInfo(() => $"INTERNAL stop request: [timeout={Timeout}]");
            }
            else
            {
                log?.LogInfo(() => $"SIGTERM received: Stopping process [timeout={Timeout}]");
            }

            cts.Cancel();

            lock (handlers)
            {
                foreach (var handler in handlers)
                {
                    new Thread(new ThreadStart(handler)).Start();
                }
            }

            StopEvent.Set();

            try
            {
                NeonHelper.WaitFor(() => readyToExit, Timeout);
                log?.LogInfo(() => "Process stopped gracefully.");
            }
            catch (TimeoutException)
            {
                log?.LogWarn(() => $"Process did not stop within [{Timeout}].");
            }

            if (!DisableProcessExit)
            {
                Environment.Exit(exitCode);
            }
        }

        /// <summary>
        /// Cleanly terminates the current process.
        /// </summary>
        /// <param name="exitCode">Optional process exit code (defaults to <b>0</b>).</param>
        public void Exit(int exitCode = 0)
        {
            if (readyToExit)
            {
                // Application has already indicated that it has terminated so we don't
                // need to go through the normal shutdown sequence.

                if (!DisableProcessExit)
                {
                    Environment.Exit(exitCode);
                }
                
                return;
            }

            ExitInternal(exitCode, explicitTermination: true);
        }
    }
}
