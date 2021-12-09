//-----------------------------------------------------------------------------
// FILE:	    ConsoleTTY.cs
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

// This code was adapted from: https://github.com/microsoft/terminal/tree/main/samples/ConPTY/MiniTerm/MiniTerm/Native

using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Win32.SafeHandles;

using static Neon.WinTTY.ConsoleApi;
using Process = Neon.WinTTY.Process;

namespace Neon.WinTTY
{
    /// <summary>
    /// Implements a pseudo TTY that links the <see cref="Console"/> for the current application
    /// to a remote process started via a command line.
    /// </summary>
    public sealed class ConsoleTTY
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ConsoleTTY()
        {
            EnableVTRendering();
        }

        /// <summary>
        /// Opt into having the console interpret and implement VTx commands.
        /// </summary>
        private static void EnableVTRendering()
        {
            var hStdOut = GetStdHandle(STD_OUTPUT_HANDLE);

            if (!GetConsoleMode(hStdOut, out uint outConsoleMode))
            {
                throw new InvalidOperationException("Could not obtain the console mode.");
            }

            outConsoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;

            if (!SetConsoleMode(hStdOut, outConsoleMode))
            {
                throw new InvalidOperationException("Could not enable virtual terminal processing.");
            }
        }

        /// <summary>
        /// Starts a remote process by executing a command line and then wiring up a pseudo
        /// TTY that forwards keystrokes to the remote process and also receives VTx formatted
        /// output from the process and handle rendering on the local <see cref="Console"/>.
        /// </summary>
        /// <param name="command">Specifies the local command to execute as the remote process.</param>
        public void Run(string command)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            using (var inputPipe = new PseudoConsolePipe())
            using (var outputPipe = new PseudoConsolePipe())
            using (var pseudoConsole = PseudoConsole.Create(inputPipe.ReadSide, outputPipe.WriteSide, (short)Console.WindowWidth, (short)Console.WindowHeight))
            using (var process = Process.Start(command, PseudoConsole.PseudoConsoleThreadAttribute, pseudoConsole.Handle))
            {
                // Copy all output from the remote process to the console.

                Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));

                // Copy all STDIN from the console to the remote process.

                Task.Run(() => CopyInputToPipe(inputPipe.WriteSide));

                // Free resources in case the console is ungracefully closed (e.g. by the 'x' in the window titlebar)

                OnClose(() => DisposeResources(process, pseudoConsole, outputPipe, inputPipe));

                WaitForExit(process).WaitOne(Timeout.Infinite);
            }
        }

        /// <summary>
        /// Reads terminal input and copies it to the PseudoConsole
        /// </summary>
        /// <param name="inputWriteSide">the write side of the pseudo console input pipe.</param>
        private static void CopyInputToPipe(SafeFileHandle inputWriteSide)
        {
            Covenant.Requires<ArgumentNullException>(inputWriteSide != null, nameof(inputWriteSide));

            using (var writer = new StreamWriter(new FileStream(inputWriteSide, FileAccess.Write)) { AutoFlush = true })
            {
                InterceptCtrlC(writer);

                while (true)
                {
                    // Send user input one character at a time to the remote process.

                    writer.Write(Console.ReadKey(intercept: true).KeyChar);
                }
            }
        }

        /// <summary>
        /// Handle CRTL-C keys from the console by intercepting these and 
        /// and sending them to the remote process via the pseudo console.
        /// </summary>
        private static void InterceptCtrlC(StreamWriter writer)
        {
            Covenant.Requires<ArgumentNullException>(writer != null, nameof(writer));

            Console.CancelKeyPress += 
                (sender, e) =>
                {
                    e.Cancel = true;
                    writer.Write("\x3");
                };
        }

        /// <summary>
        /// Reads pseudo console output and copies it to the console.
        /// </summary>
        /// <param name="outputReadSide">the "read" side of the pseudo console output pipe.</param>
        private static void CopyPipeToOutput(SafeFileHandle outputReadSide)
        {
            Covenant.Requires<ArgumentNullException>(outputReadSide != null, nameof(outputReadSide));

            using (var terminalOutput = Console.OpenStandardOutput())
            using (var pseudoConsoleOutput = new FileStream(outputReadSide, FileAccess.Read))
            {
                pseudoConsoleOutput.CopyTo(terminalOutput);
            }
        }

        /// <summary>
        /// Returns the <see cref="AutoResetEvent"/> that signals when the process exits.
        /// </summary>
        private static AutoResetEvent WaitForExit(Process process)
        {
            Covenant.Requires<ArgumentNullException>(process != null, nameof(process));

            return new AutoResetEvent(false)
            {
                SafeWaitHandle = new SafeWaitHandle(process.ProcessInfo.hProcess, ownsHandle: false)
            };
        }

        /// <summary>
        /// Set a callback to be called when the console window is closed (e.g. via the "X" window decoration button).
        /// </summary>
        private static void OnClose(Action handler)
        {
            Covenant.Requires<ArgumentNullException>(handler != null, nameof(handler));

            SetConsoleCtrlHandler(
                eventType =>
                {
                    if (eventType == CtrlTypes.CTRL_CLOSE_EVENT)
                    {
                        handler();
                    }
                    return false;
                }, 
                true);
        }

        /// <summary>
        /// Disposes the items passed.
        /// </summary>
        /// <param name="disposables">The dispoable items.</param>
        private void DisposeResources(params IDisposable[] disposables)
        {
            foreach (var disposable in disposables)
            {
                disposable?.Dispose();
            }
        }
    }
}
