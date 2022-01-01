//-----------------------------------------------------------------------------
// FILE:	    ConsoleTTY.cs
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

// This code was adapted from: https://github.com/microsoft/terminal/tree/main/samples/ConPTY/MiniTerm/MiniTerm/Native

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
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
    /// <remarks>
    /// <para>
    /// This is very easy to use.  Just instantiate an instance in your console application and
    /// then call <see cref="Run(string, IDictionary{ConsoleKeyInfo, string})"/>, passing the command line to be executed with a TTY.
    /// </para>
    /// <code lang="C#">
    /// using System;
    /// using Neon.WinTTY;
    /// 
    /// namespace MyConsoleApp
    /// {
    ///     public class Program
    ///     {
    ///         public static void Main(string[] args)
    ///         {
    ///             new ConsoleTTY().Run(@"docker exec -it alpine /bin/sh");
    ///         }
    ///     }
    /// }
    /// </code>
    /// <para>
    /// <see cref="Run(string, IDictionary{ConsoleKeyInfo, string})"/> receives user keystrokes and then forwards them to the remote
    /// process, optionally translating the keystroke into an <a href="https://www.ecma-international.org/publications-and-standards/standards/ecma-48/">ECMA-48</a>
    /// control sequence.  By default, this methods uses the <see cref="DefaultKeyMap"/> dictionary to translate keystrokes but users
    /// may override this by passing a custom dictionary.
    /// </para>
    /// <para>
    /// The <see cref="ConsoleKeyInfo"/> values received as the user types include flag bits indicating the current state of the
    /// <b>ALT</b>, <b>CONTROL</b>, and <b>SHIFT</b> keys, the <see cref="ConsoleKey"/> code identifying the specific key, and
    /// the key character.  The key character is either the Unicode value for the keystroke or 0 when the keystroke doesn't map
    /// to a character (e.g. for an ARROW key).
    /// </para>
    /// <para>
    /// Here's how keypress handling work:
    /// </para>
    /// <list type="number">
    /// <item>
    /// A new <see cref="ConsoleKeyInfo"/> is received by <see cref="Run(string, IDictionary{ConsoleKeyInfo, string})"/>.
    /// </item>
    /// <item>
    /// The key map is searched for a control sequence string for the <see cref="ConsoleKeyInfo"/>.
    /// </item>
    /// <item>
    /// If a control sequence is found then it will be sent to the remote process.   Note that the control
    /// sequence string is <c>null</c> then nothing will be sent and the keypress will essentially be ignored.
    /// </item>
    /// <item>
    /// If there's no matching control sequence in the key map and the key character is not zero, then the 
    /// key character will be sent to the remote process.  Zero key characters are never transmitted.
    /// </item>
    /// </list>
    /// </remarks>
    public sealed class ConsoleTTY
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the default mapping used to translate a keyboard keypress into the
        /// <a href="https://www.ecma-international.org/publications-and-standards/standards/ecma-48/">ECMA-48</a>
        /// (or other) control sequence to be sent to the remote process.
        /// </summary>
        public static IDictionary<ConsoleKeyInfo, string> DefaultKeyMap { get; private set; }

        /// <summary>
        /// Static constructor.
        /// </summary>
        static ConsoleTTY()
        {
            var keyMap = new Dictionary<ConsoleKeyInfo, string>()
            {
                { new ConsoleKeyInfo((char)0, ConsoleKey.PageUp,        alt: false, control: false, shift: false),  "\x001b[5~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.PageDown,      alt: false, control: false, shift: false),  "\x001b[6~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.End,           alt: false, control: false, shift: false),  "\x001b[4~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.Home,          alt: false, control: false, shift: false),  "\x001b[1~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.LeftArrow,     alt: false, control: false, shift: false),  "\x001b[D" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.UpArrow,       alt: false, control: false, shift: false),  "\x001b[A" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.RightArrow,    alt: false, control: false, shift: false),  "\x001b[C" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.DownArrow,     alt: false, control: false, shift: false),  "\x001b[B" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.Insert,        alt: false, control: false, shift: false),  "\x001b[2~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.Delete,        alt: false, control: false, shift: false),  "\x001b[3~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad0,       alt: false, control: false, shift: false),  "0" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad1,       alt: false, control: false, shift: false),  "1" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad2,       alt: false, control: false, shift: false),  "2" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad3,       alt: false, control: false, shift: false),  "3" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad4,       alt: false, control: false, shift: false),  "4" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad5,       alt: false, control: false, shift: false),  "5" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad6,       alt: false, control: false, shift: false),  "6" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad7,       alt: false, control: false, shift: false),  "7" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad8,       alt: false, control: false, shift: false),  "8" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.NumPad9,       alt: false, control: false, shift: false),  "9" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.Multiply,      alt: false, control: false, shift: false),  "*" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.Add,           alt: false, control: false, shift: false),  "+" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.Subtract,      alt: false, control: false, shift: false),  "-" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.Decimal,       alt: false, control: false, shift: false),  "." },
                { new ConsoleKeyInfo((char)0, ConsoleKey.Divide,        alt: false, control: false, shift: false),  "/" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F1,            alt: false, control: false, shift: false),  "\x001b[11~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F2,            alt: false, control: false, shift: false),  "\x001b[12~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F3,            alt: false, control: false, shift: false),  "\x001b[13~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F4,            alt: false, control: false, shift: false),  "\x001b[14~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F5,            alt: false, control: false, shift: false),  "\x001b[15~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F6,            alt: false, control: false, shift: false),  "\x001b[17~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F7,            alt: false, control: false, shift: false),  "\x001b[18~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F8,            alt: false, control: false, shift: false),  "\x001b[19~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F9,            alt: false, control: false, shift: false),  "\x001b[20~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F10,           alt: false, control: false, shift: false),  "\x001b[21~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F11,           alt: false, control: false, shift: false),  "\x001b[23~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F12,           alt: false, control: false, shift: false),  "\x001b[24~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F13,           alt: false, control: false, shift: false),  "\x001b[25~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F14,           alt: false, control: false, shift: false),  "\x001b[26~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F15,           alt: false, control: false, shift: false),  "\x001b[28~" },
                { new ConsoleKeyInfo((char)0, ConsoleKey.F16,           alt: false, control: false, shift: false),  "\x001b[29~" }
            };

            DefaultKeyMap = new ReadOnlyDictionary<ConsoleKeyInfo, string>(keyMap);
        }

        //---------------------------------------------------------------------
        // Instance members

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
        private void EnableVTRendering()
        {
            var hStdOut = GetStdHandle(STD_OUTPUT_HANDLE);

            if (!GetConsoleMode(hStdOut, out uint consoleMode))
            {
                throw new InvalidOperationException("Could not obtain the console mode.");
            }

            consoleMode |= ENABLE_VIRTUAL_TERMINAL_PROCESSING | DISABLE_NEWLINE_AUTO_RETURN;

            if (!SetConsoleMode(hStdOut, consoleMode))
            {
                throw new InvalidOperationException("Could not enable virtual terminal mode.");
            }
        }

        /// <summary>
        /// Modifies the command line passed by converting the command into a fully qualified
        /// path to the executable when necessary as well as resolving the executable's extension
        /// when not specified.  The command line returned can then be used to start the process.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        /// <returns>The updated command line.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the executable could not be located.</exception>
        /// <exception cref="FormatException">Thrown when the command exectable could not be parsed.</exception>
        private string NormalizeCommand(string commandLine)
        {
            commandLine = commandLine.Trim();

            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(commandLine), nameof(commandLine));

            // Split the command from the command line into two parts: the command and the
            // arguments.  We need to handle any single or double quotes in the command
            // name.

            // $hack(jefflill):
            //
            // This is somewhat simplistic and doesn't handle single or double quotes embedded 
            // in the command path or executable name and we also don't handle any escaping.
            // This will probably never be an issue in real life.

            string  command;
            string  arguments;
            int     endCommandPos;

            switch (commandLine[0])
            {
                case '"':   // Double quoted command

                    endCommandPos = commandLine.IndexOf('"', 1);

                    if (endCommandPos == -1)
                    {
                        throw new FormatException($"Invalid command, missing closing double quote: [{commandLine}]");
                    }
                    else
                    {
                        command   = commandLine.Substring(1, endCommandPos - 1);
                        arguments = commandLine.Substring(endCommandPos + 1).Trim();
                    }
                    break;

                case '\'':  // Single quoted command

                    endCommandPos = commandLine.IndexOf('\'', 1);

                    if (endCommandPos == -1)
                    {
                        throw new FormatException($"Invalid command, missing closing single quote: [{commandLine}]");
                    }
                    else
                    {
                        command   = commandLine.Substring(1, endCommandPos - 1);
                        arguments = commandLine.Substring(endCommandPos + 1).Trim();
                    }
                    break;

                default:  // Space terminated command

                    endCommandPos = commandLine.IndexOf(' ');

                    if (endCommandPos == -1)
                    {
                        command   = commandLine;
                        arguments = string.Empty;
                    }
                    else
                    {
                        command   = commandLine.Substring(0, endCommandPos).Trim();
                        arguments = commandLine.Substring(endCommandPos).Trim();
                    }
                    break;
            }

            // Ensure that [command] is a fully qualified path, including the file extension
            // and that the executable actually exists.

            var paths            = Environment.GetEnvironmentVariable("PATH").Split(new char[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries);
            var extensions       = new string[] { ".exe", ".cmd", ".bat" };
            var directoryPath    = Path.GetDirectoryName(command);
            var qualifiedCommand = string.Empty;

            if (!string.IsNullOrEmpty(directoryPath))
            {
                // Handle commands that include a directory path by locating the binary.

                if (Path.GetExtension(command) == string.Empty)
                {
                    foreach (var extension in extensions)
                    {
                        var commandWithExtension = command + extension;

                        if (File.Exists(commandWithExtension))
                        {
                            qualifiedCommand = commandWithExtension;
                            break;
                        }
                    }
                }
                else
                {
                    qualifiedCommand = command;
                }
            }
            else
            {
                // Search the current directory and then the PATH for the command binary.

                foreach (var directory in (new string[] { Environment.CurrentDirectory }).Union(paths))
                {
                    if (!string.IsNullOrEmpty(Path.GetExtension(command)))
                    {
                        var commandWithExtension = Path.Combine(directory, command);

                        if (File.Exists(commandWithExtension))
                        {
                            qualifiedCommand = commandWithExtension;
                            break;
                        }
                    }
                    else
                    {
                        foreach (var extension in extensions)
                        {
                            var commandWithExtension = Path.Combine(directory, command + extension);

                            if (File.Exists(commandWithExtension))
                            {
                                qualifiedCommand = commandWithExtension;
                                break;
                            }
                        }

                        if (!string.IsNullOrEmpty(qualifiedCommand))
                        {
                            break;
                        }
                    }
                }
            }

            if (string.IsNullOrEmpty(qualifiedCommand))
            {
                throw new FileNotFoundException($"Cannot locate executable for: {commandLine}");
            }

            qualifiedCommand = Path.GetFullPath(qualifiedCommand);

            if (qualifiedCommand.IndexOf(' ') != -1)
            {
                qualifiedCommand = $"\"{qualifiedCommand}\"";
            }

            // Return the normalized command line.

            if (string.IsNullOrEmpty(arguments))
            {
                return qualifiedCommand;
            }
            else
            {
                return $"{qualifiedCommand} {arguments}";
            }
        }

        /// <summary>
        /// Starts a remote process by executing a command line and then wiring up a pseudo
        /// TTY that forwards keystrokes to the remote process and also receives VTx formatted
        /// output from the process and handle rendering on the local <see cref="Console"/>.
        /// </summary>
        /// <param name="command">
        /// <para>
        /// Specifies the local command to execute as the remote process.
        /// </para>
        /// <note>
        /// You must take care to quote the command executable path or any arguments that
        /// include spaces.
        /// </note>
        /// </param>
        /// <param name="keyMap">
        /// Optionally specifies the map to be used for translating keystrokes into 
        /// <a href="https://www.ecma-international.org/publications-and-standards/standards/ecma-48/">ECMA-48</a>
        /// (or other) control sequences.  This defaults to <see cref="DefaultKeyMap"/> but you
        /// may pass a custom map when required.
        /// </param>
        /// <remarks>
        /// <para>
        /// If the command path specifies an absolute or relative directory then the command
        /// will be execute from there, otherwise the method will first attempt executing the
        /// command from the current directory before searching the PATH for the command.
        /// </para>
        /// <para>
        /// You may omit the command file extension and the method will try <b>.exe</b>,
        /// <b>.cmd</b>, and <b>.bat</b> extensions in that order.
        /// </para>
        /// </remarks>
        public void Run(string command, IDictionary<ConsoleKeyInfo, string> keyMap = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command), nameof(command));

            command = NormalizeCommand(command);

            using (var inputPipe = new PseudoConsolePipe())
            using (var outputPipe = new PseudoConsolePipe())
            using (var pseudoConsole = PseudoConsole.Create(inputPipe.ReadSide, outputPipe.WriteSide, (short)Console.WindowWidth, (short)Console.WindowHeight))
            using (var process = Process.Start(command, PseudoConsole.PseudoConsoleThreadAttribute, pseudoConsole.Handle))
            {
                // Copy all output from the remote process to the console.

                Task.Run(() => CopyPipeToOutput(outputPipe.ReadSide));

                // Process user key presses as required and forward them to the remote process.

                Task.Run(() => CopyInputToPipe(inputPipe.WriteSide, keyMap ?? DefaultKeyMap));

                // Free resources in case the console is ungracefully closed (e.g. by the 'X' in the window titlebar).

                OnClose(() => DisposeResources(process, pseudoConsole, outputPipe, inputPipe));

                // We need to detect when the console is resized by the user and 
                // then resize the pseudo TTY to match.

                var processExitEvent = WaitForExit(process);
                var consoleWidth     = Console.WindowWidth;
                var consoleHeight    = Console.WindowHeight;

                while (!processExitEvent.WaitOne(500))
                {
                    var newWidth  = Console.WindowWidth;
                    var newHeight = Console.WindowHeight;

                    if (consoleHeight != newHeight || consoleWidth != newWidth)
                    {
                        pseudoConsole.Resize((short)newWidth, (short)newHeight);

                        consoleWidth  = newWidth;
                        consoleHeight = newHeight;
                    }
                }
            }
        }

        /// <summary>
        /// Reads terminal input and copies it to the PseudoConsole
        /// </summary>
        /// <param name="inputWriteSide">the write side of the pseudo console input pipe.</param>
        /// <param name="keyMap">The key map used for translating keystroks into ECMA-48 control sequences.</param>
        private static void CopyInputToPipe(SafeFileHandle inputWriteSide, IDictionary<ConsoleKeyInfo, string> keyMap)
        {
            Covenant.Requires<ArgumentNullException>(inputWriteSide != null, nameof(inputWriteSide));
            Covenant.Requires<ArgumentNullException>(keyMap != null, nameof(keyMap));

            using (var writer = new StreamWriter(new FileStream(inputWriteSide, FileAccess.Write)) { AutoFlush = true })
            {
                InterceptCtrlC(writer);

                while (true)
                {
                    var keyInfo = Console.ReadKey(intercept: true);

                    if (keyMap.TryGetValue(keyInfo, out var sequence))
                    {
                        if (!string.IsNullOrEmpty(sequence))
                        {
                            foreach (var ch in sequence)
                            {
                                writer.Write(ch);
                            }
                        }
                    }

                    if (keyInfo.KeyChar != (char)0)
                    {
                        writer.Write(keyInfo.KeyChar);
                    }
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
