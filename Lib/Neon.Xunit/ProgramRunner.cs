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
using Neon.Xunit;

using Xunit;

namespace Neon.Xunit
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
    public sealed class ProgramRunner : IDisposable
    {
        private static Thread           programThread;
        private static AutoResetEvent   programReadyEvent;

        /// <summary>
        /// Constructor.
        /// </summary>
        public ProgramRunner()
        {
            programReadyEvent = new AutoResetEvent(false);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            if (programThread != null)
            {
                programThread.Abort();
                programThread = null;
            }

            if (programReadyEvent != null)
            {
                programReadyEvent.Dispose();
                programReadyEvent = null;
            }
        }

        /// <summary>
        /// Executes a program entry point synchronously, passing arguments and returning the result.
        /// </summary>
        /// <param name="main">The program entry point.</param>
        /// <param name="args">The arguments.</param>
        /// <returns>The exit code.</returns>
        public int Execute(ProgramEntrypoint main, params string[] args)
        {
            Covenant.Requires(main != null);

            if (programThread != null)
            {
                throw new InvalidOperationException("Only one simulated [program] can run at a time.");
            }

            // Prepend the [--unit-test] flag to the arguments.

            args = (new string[] { "--unit-test" }).Union(args).ToArray();

            int exitCode = 0;

            programThread = new Thread(new ThreadStart(() => exitCode = main(args)));
            programThread.Join();

            return exitCode;
        }

        /// <summary>
        /// Executes a program entry point asynchronously, without waiting for the command to complete.
        /// This is useful for commands that don't terminate by themselves (like <b>nshell proxy</b>).
        /// Call <see cref="Terminate()"/> to kill the running command.
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

            // Prepend the [--unit-test] flag to the arguments.

            args = (new string[] { "--unit-test" }).Union(args).ToArray();

            programThread = new Thread(
                new ThreadStart(
                    () =>
                    {
                        main(args);
                    }));

            programThread.Start();

            // $hack(jeff.lill):
            //
            // We need to give the tool some time to actually start the operation.
            // Ideally, we'd have some way for the tool to signal that it's ready.
            // Perhaps, we should implement an Xunit helper that implements a tool
            // test context that replaces this class and provides a method the
            // program can use to signal readiness.
            //
            // For now, we're just going to wait a bit.

            Thread.Sleep(TimeSpan.FromSeconds(1));
        }

        /// <summary>
        /// Terminates the <b>nshell</b> tool if one is running.
        /// </summary>
        public void Terminate()
        {
            if (programThread != null)
            {
                programThread.Abort();
                programThread = null;
            }
        }
    }
}
