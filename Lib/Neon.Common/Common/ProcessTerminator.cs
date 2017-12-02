//-----------------------------------------------------------------------------
// FILE:	    ProcessTerminator.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

namespace Neon.Common
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
    /// timeout, so your process may still be killed before the timeout is reached.
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

            this.Timeout = timeout;
            this.cts     = new CancellationTokenSource();

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
        /// Returns the <see cref="CancellationToken"/> that will be raised when a
        /// termination signal is received.
        /// </summary>
        public CancellationToken CancellationToken
        {
            get { return cts.Token; }
        }

        /// <summary>
        /// Indicates that the application has gracefully stopped and is 
        /// ready to be terminated.
        /// </summary>
        public void ReadyToExit()
        {
            readyToExit = true;
        }

        /// <summary>
        /// Cleanly terminates the current process (for internal use).
        /// </summary>
        /// <param name="exitCode">Optional process exit code (defaults to <b>0</b>).</param>
        private void ExitInternal(int exitCode = 0)
        {
            if (readyToExit)
            {
                // Application has already indicated that it has terminated.

                return;
            }

            log?.LogInfo(() => $"SIGTERM received: Stopping process [timeout={Timeout}]]");

            cts.Cancel();

            lock (handlers)
            {
                foreach (var handler in handlers)
                {
                    new Thread(new ThreadStart(handler)).Start();
                }
            }

            try
            {
                NeonHelper.WaitFor(() => readyToExit, Timeout);
                log?.LogInfo(() => "Process stopped gracefully.");
            }
            catch (TimeoutException)
            {
                log?.LogWarn(() => $"Process did not stop within [{Timeout}].");
            }

            Environment.Exit(exitCode);
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

                Environment.Exit(exitCode);
                return;
            }

            ExitInternal(exitCode);
        }
    }
}
