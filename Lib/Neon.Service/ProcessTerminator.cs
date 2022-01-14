//-----------------------------------------------------------------------------
// FILE:	    ProcessTerminator.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
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

namespace Neon.Service
{
    /// <summary>
    /// Gracefully handles SIGTERM signals sent to a process to terminate itself.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class listens for a termination signal and then gives the process some time
    /// to gracefully save state.  The termination timeout defaults to 30 seconds but
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
    /// <para>
    /// A <see cref="ProcessTerminator"/> is created automatically by <see cref="NeonService"/>
    /// for your service, with the constructor being passed optional parameters passed by your 
    /// service to the <see cref="NeonService"/> constructor.  You can use these to specify
    /// the minimum of amount of time the service will wait during graceful termination (i.e.
    /// for requests to be drained) as well as the maximum time allowed for graceful termination.
    /// </para>
    /// <para>
    /// Minimum graceful termination time defaults to <b>11 seconds</b> and the maximum graceful
    /// termination timeout defaults to <b>30 seconds</b>.  These are reasonable defaults for
    /// Kubernetes as discussed here:
    /// </para>
    /// <para>
    /// https://blog.markvincze.com/graceful-termination-in-kubernetes-with-asp-net-core/
    /// </para>
    /// <para>
    /// You can adjust these as required.  For serviced deployed to Kubernetes, you should try to have the 
    /// <b>gracefulShutdownTimeout</b> parameter match the pod's <b>terminationGracePeriodSeconds</b> 
    /// (which typically defaults to 30 seconds).
    /// </para>
    /// <para>
    /// Also, for ASP.NET applications, you should configure the shutdown timeout to <see cref="GracefulShutdownTimeout"/>
    /// seconds or what you specified in the <b>gracefulShutdownTimeout</b> constructor parameter.  This will
    /// look something like in your <see cref="NeonService"/> derived implementation:
    /// </para>
    /// <code language="C#">
    /// protected async override Task&lt;int&gt; OnRunAsync()
    /// {
    ///     _ = Host.CreateDefaultBuilder()
    ///             .ConfigureHostOptions(
    ///                 options =>
    ///                 {
    ///                     options.ShutdownTimeout = ProcessTerminator.DefaultMinShutdownTime; // &lt;--- set the ASP.NET shutdown timeout here
    ///                 })
    ///             .ConfigureWebHostDefaults(builder => builder.UseStartup&lt;Startup&gt;())
    ///             .Build()
    ///             .RunOperatorAsync(Array.Empty&lt;;string&gt;());
    ///
    ///     await StartedAsync();
    ///
    ///     // Wait for the process terminator to signal that the service is stopping.
    /// 
    ///    await Terminator.StopEvent.WaitAsync();
    /// }
    /// </code>
    /// </remarks>
    public sealed class ProcessTerminator
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// The default the maximum time a process terminator will wait for a service
        /// to terminate gracefully.  (30 seconds)
        /// </summary>
        public static readonly TimeSpan DefaultGracefulTimeout = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The default the minimum time to wait before allowing termination to proceed.
        /// This allows pending requests to be drained.  (11 seconds)
        /// </summary>
        public static readonly TimeSpan DefaultMinShutdownTime = TimeSpan.FromSeconds(11);

        //---------------------------------------------------------------------
        // Instance members

        private INeonLogger                 log;
        private CancellationTokenSource     cts;
        private bool                        terminating;
        private bool                        readyToExit;
        private List<Action>                handlers;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="log">Optionally specifies a <see cref="INeonLogger"/> used for logging.</param>
        /// <param name="gracefulShutdownTimeout">Optionally specifies the termination timeout (defaults to <see cref="GracefulShutdownTimeout"/>).</param>
        /// <param name="minShutdownTime">
        /// Optionally specifies the minimum time to wait before allowing termination to proceed.
        /// This defaults to the minimum of <paramref name="gracefulShutdownTimeout"/> and <see cref="DefaultMinShutdownTime"/>.
        /// See the remarks for more details.
        /// </param>
        /// <remarks>
        /// <para>
        /// <paramref name="gracefulShutdownTimeout"/> defaults to 30 seconds and for environments like Kubernetes, this
        /// should be set to the same value as the host pod's <b>terminationGracePeriodSeconds</b> when 
        /// that's different from its default value of 30 seconds.
        /// </para>
        /// <para>
        /// <paramref name="minShutdownTime"/> can be used to control the minimum period the service will continue 
        /// to run after receiving a TERM signal.  This can be important for ASPNET based services because 
        /// Kubernetes can take something like 10 seconds to remove the pod from the service load balancer
        /// after sending a TERM to the pod.  Having a pod terminate before the load balancer is updated means
        /// that other pods may see request errors during this time.  This blog post goes into this in some
        /// detail:
        /// </para>
        /// <para>
        /// https://blog.markvincze.com/graceful-termination-in-kubernetes-with-asp-net-core/
        /// </para>
        /// <para>
        /// <paramref name="minShutdownTime"/> defaults to the minimum of <paramref name="gracefulShutdownTimeout"/> and 
        /// <see cref="GracefulShutdownTimeout"/> to wait for the service load balancer to update.  This applies to both 
        /// ASP.NET and headless services so you may wish to reduce <paramref name="minShutdownTime"/> so that headless
        /// services will terminate quicker.  Pass a negative timespan to disable this behavior.
        /// </para>
        /// <note>
        /// The <see cref="Signal"/> method ignores <paramref name="minShutdownTime"/> to improve unit test performance.
        /// </note>
        /// </remarks>
        public ProcessTerminator(INeonLogger log = null, TimeSpan gracefulShutdownTimeout = default, TimeSpan minShutdownTime = default)
        {
            Covenant.Requires<ArgumentException>(gracefulShutdownTimeout >= TimeSpan.Zero, nameof(gracefulShutdownTimeout));
            Covenant.Requires<ArgumentException>(minShutdownTime >= TimeSpan.Zero, nameof(minShutdownTime));

            this.log = log;

            if (gracefulShutdownTimeout <= TimeSpan.Zero)
            {
                gracefulShutdownTimeout = DefaultGracefulTimeout;
            }

            if (minShutdownTime == TimeSpan.Zero)
            {
                minShutdownTime = DefaultMinShutdownTime;
            }

            this.GracefulShutdownTimeout = gracefulShutdownTimeout;
            this.MinShutdownTime         = NeonHelper.Min(gracefulShutdownTimeout, minShutdownTime);
            this.cts                     = new CancellationTokenSource();
            this.handlers                = new List<Action>();

            AssemblyLoadContext.Default.Unloading +=
                context =>
                {
                    ExitInternal();
                };
        }

        /// <summary>
        /// Returns the termination timeout.  This is specified in the constructor and 
        /// is the maximum time the service will be allowed to shutdown gracefully before
        /// being terminated.
        /// </summary>
        public TimeSpan GracefulShutdownTimeout { get; private set; }

        /// <summary>
        /// Returns the minimum amount of time the service should wait to quit after
        /// receiving a TERM signal.  This is specified in the constructor and is used
        /// to allow the service to continue processing straggler requests while Kubernetes
        /// service or other load balancers are updating their routes.
        /// </summary>
        public TimeSpan MinShutdownTime { get; private set; }

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
        /// <remarks>
        /// <note>
        /// <see cref="Signal"/> method ignores <see cref="MinShutdownTime"/> to improve unit test 
        /// performance.
        /// </note>
        /// </remarks>
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

            log?.LogInfo(() => $"Emulated stop request: [timeout={GracefulShutdownTimeout}]");

            cts.Cancel();

            lock (handlers)
            {
                foreach (var handler in handlers)
                {
                    NeonHelper.StartThread(handler);
                }
            }

            StopEvent.Set();

            try
            {
                NeonHelper.WaitFor(() => readyToExit, GracefulShutdownTimeout);
                log?.LogInfo(() => "Process stopped gracefully.");
            }
            catch (TimeoutException)
            {
                log?.LogWarn(() => $"Process did not stop within [{GracefulShutdownTimeout}].");
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

            var terminationStopwatch = new Stopwatch();

            terminationStopwatch.Start();

            if (explicitTermination)
            {
                log?.LogInfo(() => $"INTERNAL stop request: [timeout={GracefulShutdownTimeout}]");
            }
            else
            {
                log?.LogInfo(() => $"SIGTERM received: Stopping process [timeout={GracefulShutdownTimeout}]");
            }

            cts.Cancel();

            lock (handlers)
            {
                foreach (var handler in handlers)
                {
                    NeonHelper.StartThread(handler);
                }
            }

            // Hold here for up to [MinShutdownTime] to allow any straggling requests from
            // Kubernetes service load balancers that haven't removed the pod yet.

            var remainingWaitTime = MinShutdownTime - terminationStopwatch.Elapsed;

            if (remainingWaitTime > TimeSpan.Zero)
            {
                Thread.Sleep(remainingWaitTime);
            }

            StopEvent.Set();

            try
            {
                NeonHelper.WaitFor(() => readyToExit, GracefulShutdownTimeout);
                log?.LogInfo(() => "Process stopped gracefully.");
            }
            catch (TimeoutException)
            {
                log?.LogWarn(() => $"Process did not signal a graceful stop by calling [{nameof(ReadyToExit)}()] within [{GracefulShutdownTimeout}].");
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
