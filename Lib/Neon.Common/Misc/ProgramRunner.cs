//-----------------------------------------------------------------------------
// FILE:	    ProgramRunner.cs
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;

namespace Neon.Common
{
    /// <summary>
    /// Program main entry point method signature.
    /// </summary>
    /// <param name="args">The command line arguments.</param>
    /// <returns>The exit code.</returns>
    public delegate int ProgramEntrypoint(params string[] args);

    /// <summary>
    /// Used to implement unit tests on command line tools by simulating
    /// their execution on a thread rather than forking the tool as a process.
    /// This is makes debugging easier and also deals with the fact that
    /// unit tests may leave orphan processes running.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is designed to simulate running a single executable
    /// by calling its main entrypoint.  To accomplish this, use the
    /// default constructor to create a <see cref="ProgramRunner"/> 
    /// instance.  The constructor will set <see cref="Current"/> to
    /// itself and then you can call <see cref="Execute(ProgramEntrypoint, string[])"/>
    /// to execute the program synchronously (waiting for it to return),
    /// or <see cref="Fork(ProgramEntrypoint, string[])"/> to simulate 
    /// forking the program by running it on a new thread.
    /// </para>
    /// <para>
    /// <see cref="Fork(ProgramEntrypoint, string[])"/> waits to return
    /// until the program calls <see cref="ProgramReady"/>.  This is used
    /// to ensure that program has completed the activities required 
    /// by the unit tests before the tests are executed.
    /// </para>
    /// <note>
    /// Only one <see cref="ProgramRunner"/> instance can active at any
    /// particular time.
    /// </note>
    /// <para>
    /// Simulated program entry points that will be called by <see cref="Fork(ProgramEntrypoint, string[])"/>
    /// and that run indefinitely, need to call <see cref="WaitForExit()"/> when
    /// after its started the operation.  This returns when the <see cref="TerminateFork"/>
    /// is called.  The simulated program should stop any operations being
    /// performed, release any important resources and exit cleanly its <c>Main</c>
    /// method cleanly.
    /// </para>
    /// <para>
    /// The <see cref="Arguments"/> dictionary can be used to pass additional
    /// arguments into the program being tested.  This maps case insensitve keys
    /// to <c>object</c> values.
    /// </para>
    /// <note>
    /// You should call <see cref="Dispose"/> when you're finished with
    /// the runner.
    /// </note>
    /// </remarks>
    public sealed class ProgramRunner : IDisposable
    {
        //---------------------------------------------------------------------
        // Static memebrs

        /// <summary>
        /// Returns the current <see cref="ProgramRunner"/> or <c>null</c>.
        /// </summary>
        public static ProgramRunner Current { get; private set; }

        //---------------------------------------------------------------------
        // Instance members

        private Thread          programThread;
        private AutoResetEvent  programReadyEvent;
        private AutoResetEvent  programExitEvent;
        private int             programExitCode;
        private bool            programIsReady;
        private bool            programExitBeforeReady;
        private TimeSpan        forkTimeout;
        private byte[]          inputBytes;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="forkTimeout">
        /// Specifies the maximum time for <see cref="Fork(ProgramEntrypoint, string[])"/>
        /// to wait for the program to signal that it's ready by calling <see cref="ProgramReady"/>.
        /// This defaults to <b>30 seconds</b>.
        /// </param>
        public ProgramRunner(TimeSpan forkTimeout = default)
        {
            if (forkTimeout <= TimeSpan.Zero)
            {
                forkTimeout = TimeSpan.FromSeconds(30);
            }

            this.forkTimeout       = forkTimeout;
            this.programReadyEvent = new AutoResetEvent(false);
            this.programExitEvent  = new AutoResetEvent(false);

            ProgramRunner.Current  = this;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            TerminateFork();

            if (programReadyEvent != null)
            {
                programReadyEvent.Dispose();
                programReadyEvent = null;
            }

            if (programExitEvent != null)
            {
                programExitEvent.Dispose();
                programExitEvent = null;
            }

            Current = null;
        }

        /// <summary>
        /// Returns a case insensitve dictionary of additional unit test related arguments
        /// that can be passed to the program being tested.
        /// </summary>
        public Dictionary<string, object> Arguments { get; private set; } = new Dictionary<string, object>(StringComparer.InvariantCultureIgnoreCase);

        /// <summary>
        /// Executes a program entry point synchronously, passing arguments and returning the result.
        /// </summary>
        /// <param name="main">The program entry point.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/> returned by the simulated program run.</returns>
        public ExecuteResponse Execute(ProgramEntrypoint main, params string[] args)
        {
            Covenant.Requires(main != null);

            if (programThread != null)
            {
                throw new InvalidOperationException("Only one simulated [program] can run at a time.");
            }

            var orgSTDOUT = Console.Out;
            var orgSTDERR = Console.Error;

            try
            {
                // Capture standard output and error.

                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();

                using (var stdOutCapture = new StringWriter(sbOut))
                {
                    using (var stdErrCapture = new StringWriter(sbErr))
                    {
                        var exitCode = 0;

                        Console.SetOut(stdOutCapture);
                        Console.SetError(stdErrCapture);

                        // Simulate executing the program.

                        programThread = new Thread(new ThreadStart(() => exitCode = main(args)));
                        programThread.Start();
                        programThread.Join();
                        programThread = null;

                        return new ExecuteResponse()
                        {
                            ExitCode   = exitCode,
                            OutputText = sbOut.ToString(),
                            ErrorText  = sbErr.ToString()
                        };
                    }
                }
            }
            finally
            {
                // Restore the standard files.

                Console.SetOut(orgSTDOUT);
                Console.SetError(orgSTDERR);
            }
        }

        /// <summary>
        /// Opens the standard input stream.  This will return a stream with the
        /// input specified when <see cref="ExecuteWithInput(ProgramEntrypoint, byte[], string[])"/>
        /// or <see cref="ExecuteWithInput(ProgramEntrypoint, string, string[])"/> were called
        /// or else it will simply return the result of <see cref="Console.OpenStandardInput()"/>.
        /// </summary>
        /// <returns>The input <see cref="Stream"/>.</returns>
        public Stream OpenStandardInput()
        {
            if (inputBytes == null)
            {
                return Console.OpenStandardInput();
            }
            else
            {
                return new MemoryStream(inputBytes);
            }
        }

        /// <summary>
        /// Executes a program entry point synchronously, streaming some bytes as standard input,
        /// passing arguments and returning the result.
        /// </summary>
        /// <param name="main">The program entry point.</param>
        /// <param name="inputBytes">The bytes to be passed as standard input.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/> returned by the simulated program run.</returns>
        public ExecuteResponse ExecuteWithInput(ProgramEntrypoint main, byte[] inputBytes, params string[] args)
        {
            Covenant.Requires(main != null);
            Covenant.Requires<ArgumentNullException>(inputBytes != null);

            this.inputBytes = inputBytes;

            if (programThread != null)
            {
                throw new InvalidOperationException("Only one simulated [program] can run at a time.");
            }

            var orgSTDOUT = Console.Out;
            var orgSTDERR = Console.Error;

            try
            {
                // Capture standard output and error and stream the input 
                // text as STDIN.

                var sbOut = new StringBuilder();
                var sbErr = new StringBuilder();

                using (var stdOutCapture = new StringWriter(sbOut))
                {
                    using (var stdErrCapture = new StringWriter(sbErr))
                    {
                        var exitCode = 0;

                        Console.SetOut(stdOutCapture);
                        Console.SetError(stdErrCapture);

                        // Simulate executing the program.

                        programThread = new Thread(new ThreadStart(() => exitCode = main(args)));
                        programThread.Start();
                        programThread.Join();
                        programThread = null;

                        return new ExecuteResponse()
                        {
                            ExitCode   = exitCode,
                            OutputText = sbOut.ToString(),
                            ErrorText  = sbErr.ToString()
                        };
                    }
                }
            }
            finally
            {
                // Restore the standard files.

                Console.SetOut(orgSTDOUT);
                Console.SetError(orgSTDERR);
            }
        }

        /// <summary>
        /// Executes a program entry point synchronously, streaming some text as standard input,
        /// passing arguments and returning the result.
        /// </summary>
        /// <param name="main">The program entry point.</param>
        /// <param name="inputText">The text to be passed as standard input.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The <see cref="ExecuteResponse"/> returned by the simulated program run.</returns>
        public ExecuteResponse ExecuteWithInput(ProgramEntrypoint main, string inputText, params string[] args)
        {
            Covenant.Requires(main != null);
            Covenant.Requires<ArgumentNullException>(inputText != null);

            return ExecuteWithInput(main, Encoding.UTF8.GetBytes(inputText), args);
        }

        /// <summary>
        /// <para>
        /// Executes a program entry point asynchronously, without waiting for the command to complete.
        /// This is useful for commands that don't terminate by themselves.  Call <see cref="TerminateFork()"/>
        /// to kill the running command.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> The <paramref name="main"/> simulated entry point must call
        /// <see cref="WaitForExit()"/>.  This will block until the <see cref="TerminateFork"/>
        /// is called, returning when the program is expected to terminate itself.
        /// </note>
        /// </summary>
        /// <param name="main">The program entry point.</param>
        /// <param name="args">The arguments.</param>
        public void Fork(ProgramEntrypoint main, params string[] args)
        {
            Covenant.Requires(main != null);

            if (programThread != null)
            {
                throw new InvalidOperationException("Only one simulated [program] can run at a time.");
            }

            programIsReady         = false;
            programExitBeforeReady = false;

            programThread = new Thread(
                new ThreadStart(
                    () =>
                    {
                        programExitCode = main(args);

                        if (!programIsReady)
                        {
                            programExitBeforeReady = true;
                        }
                    }));

            programThread.Name         = "program-runner";
            programThread.IsBackground = true;
            programThread.Start();

            // We need to give the program enough time to do enough initialization
            // so the tests can succeed.  We're going to rely on the program to
            // signal this by calling [ProgramReady()] which will set the event 
            // we'll listen on.

            if (!programReadyEvent.WaitOne(forkTimeout))
            {
                throw new TimeoutException($"The program runner timed out before the application called [{nameof(ProgramReady)}].");
            }

            if (programExitBeforeReady)
            {
                throw new InvalidOperationException($"The program returned with [exitcode={programExitCode}] before calling [{nameof(ProgramReady)}].");
            }
        }

        /// <summary>
        /// <para>
        /// Called by programs executed via <see cref="Fork(ProgramEntrypoint, string[])"/>
        /// when the program has initialized itself enough to be ready for testing.
        /// </para>
        /// <note>
        /// This must be called or else <see cref="Fork(ProgramEntrypoint, string[])"/> will
        /// never return.
        /// </note>
        /// </summary>
        public void ProgramReady()
        {
            programIsReady = true;
            programReadyEvent.Set();
        }

        /// <summary>
        /// Terminates the forked program if one is running.
        /// </summary>
        public void TerminateFork()
        {
            if (programThread != null)
            {
                programExitEvent.Set();
                programThread.Join();

                programThread = null;
            }
        }

        /// <summary>
        /// Called by the emulated program entry point for operations that are
        /// initiated via <see cref="Fork(ProgramEntrypoint, string[])"/>.  This
        /// method will block until <see cref="TerminateFork"/> is called.  The
        /// emulated program must exit cleanly when this returns.
        /// </summary>
        public void WaitForExit()
        {
            programExitEvent.WaitOne();
        }
    }
}
