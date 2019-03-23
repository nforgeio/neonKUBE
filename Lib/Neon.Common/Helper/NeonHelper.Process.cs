//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Process.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        /// <summary>
        /// Returns the path to an executable. 
        /// </summary>
        /// <param name="program">The program file name or fully qualified path.</param>
        /// <returns>The executable path.</returns>
        /// <remarks>
        /// <para>
        /// The behavior of this method varies based on whether the host operating system
        /// is Windows or Linux/OSX.
        /// </para>
        /// <para>
        /// For Windows, the <see cref="Process.Start(ProcessStartInfo)"/> method does not 
        /// search the PATH for the application.  This method attempts to convert the program
        /// file name into a fully qualified path by actually searching the PATH.
        /// </para>
        /// <para>
        /// For Linux/OSX, the <paramref name="program"/> value is returned unchanged.
        /// </para>
        /// </remarks>
        private static string GetProgramPath(string program)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(program));

            var programExtensions = new string[] { ".exe", ".cmd" };

            if (!NeonHelper.IsWindows)
            {
                return program;
            }

            if (Path.IsPathRooted(program) || program.Contains(Path.PathSeparator))
            {
                return program; // The path is already fully qualified or is relative.
            }
            else if (Path.HasExtension(program))
            {
                if (File.Exists(program))
                {
                    return program; // The program exists in the current directory.
                }
            }
            else
            {
                foreach (var extension in programExtensions)
                {
                    var withExtension = program + extension;

                    if (File.Exists(withExtension))
                    {
                        return withExtension; // The program exists in the current directory.
                    }
                }
            }

            var path = Environment.GetEnvironmentVariable("PATH");

            foreach (var item in path.Split(';'))
            {
                try
                {
                    var directory = item.Trim();

                    if (directory == "")
                    {
                        continue;
                    }

                    var fullPath = Path.Combine(directory, program);

                    if (File.Exists(fullPath))
                    {
                        return fullPath;
                    }

                    // For programs paths without an extension, we're going
                    // to try appending ".exe" and ".cmd".

                    if (string.IsNullOrEmpty(Path.GetExtension(fullPath)))
                    {
                        foreach (var extension in programExtensions)
                        {
                            var testPath = fullPath + extension;

                            if (File.Exists(testPath))
                            {
                                return testPath;
                            }
                        }
                    }
                }
                catch
                {
                    // Ignoring any directories that don't exist or have security restrictions.
                }
            }

            // Return the original path on the off chance that it might work.

            return program;
        }

        /// <summary>
        /// Normalizes an array of argument objects into a form that can
        /// be passed to an invoked process by adding a quotes and escape
        /// characters as necessary. 
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The formatted argument string.</returns>
        /// <remarks>
        /// <note>
        /// <c>null</c> and empty arguments are ignored.
        /// </note>
        /// </remarks>
        public static string NormalizeExecArgs(params object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder();

            foreach (var arg in args)
            {
                if (arg == null)
                {
                    continue;
                }

                var stringEnumerable = arg as IEnumerable<string>;

                if (stringEnumerable != null)
                {
                    foreach (var value in stringEnumerable)
                    {
                        if (value == null || value == string.Empty)
                        {
                            continue;
                        }

                        sb.AppendWithSeparator(NormalizeArg(value));
                    }

                    continue;
                }

                var argValue = arg.ToString();

                if (argValue == string.Empty)
                {
                    continue;
                }

                sb.AppendWithSeparator(NormalizeArg(argValue));
            }

            return sb.ToString();
        }

        /// <summary>
        /// Normalizes a string argument.
        /// </summary>
        /// <param name="argValue">The argument.</param>
        /// <returns>The argument string with any required quotes and escapes.</returns>
        private static string NormalizeArg(string argValue)
        {
            if (argValue.Contains('"'))
            {
                argValue = argValue.Replace("\"", "\\\"");
                argValue = $"\"{argValue}\"";
            }
            else if (argValue.Contains(' '))
            {
                argValue = $"\"{argValue}\"";
            }

            return argValue;
        }

        /// <summary>
        /// Forks a child process that will run in parallel with the current process.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <returns>The <see cref="Process"/> information.</returns>
        public static Process Fork(string path, params object[] args)
        {
            var processInfo = new ProcessStartInfo(GetProgramPath(path), NormalizeExecArgs(args));

            processInfo.UseShellExecute        = false;
            processInfo.RedirectStandardError  = false;
            processInfo.RedirectStandardOutput = false;
            processInfo.CreateNoWindow         = true;
            processInfo.WorkingDirectory       = Environment.CurrentDirectory;

            var process = new Process()
            {
                StartInfo = processInfo
            };

            process.Start();

            return process;
        }

        /// <summary>
        /// Starts a process with an array of arguments to run an executable file and
        /// then waits for the process to terminate.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>The process exit code.</returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not commpleted in time 
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static int Execute(string path, object[] args, TimeSpan? timeout = null, Process process = null)
        {
            return Execute(path, NormalizeExecArgs(args), timeout, process);
        }

        /// <summary>
        /// Starts a process to run an executable file and then waits for the process to terminate.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>The process exit code.</returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not commpleted in time
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static int Execute(string path, string args, TimeSpan? timeout = null, Process process = null)
        {
            var processInfo   = new ProcessStartInfo(GetProgramPath(path), args ?? string.Empty);
            var killOnTimeout = process == null;

            if (process == null)
            {
                process = new Process();
            }

            try
            {
                processInfo.UseShellExecute        = false;
                processInfo.CreateNoWindow         = true;
                processInfo.RedirectStandardError  = true;
                processInfo.RedirectStandardOutput = true;
                processInfo.WorkingDirectory       = Environment.CurrentDirectory;

                process.StartInfo                  = processInfo;
                process.EnableRaisingEvents        = true;

                // Configure the sub-process STDOUT and STDERR streams to use
                // code page 1252 which simply passes byte values through.

                processInfo.StandardErrorEncoding  =
                processInfo.StandardOutputEncoding = ByteEncoding.Instance;

                // Relay STDOUT and STDERR output from the child process
                // to this process's STDOUT and STDERR streams.

                // $todo(jeff.lill):
                //
                // This won't work properly for binary data streaming
                // back from the process because we're not going to be
                // sure whether the "line" was terminated by a CRLF or
                // just a CR.  I'm not sure if there's a clean way to
                // address this in .NET code.
                //
                //      https://github.com/nforgeio/neonKUBE/issues/461

                var stdErrClosed = false;
                var stdOutClosed = false;

                process.ErrorDataReceived +=
                    (s, a) =>
                    {
                        if (a.Data == null)
                        {
                            stdErrClosed = true;
                        }
                        else
                        {
                            Console.Error.WriteLine(a.Data);
                        }
                    };

                process.OutputDataReceived +=
                    (s, a) =>
                    {
                        if (a.Data == null)
                        {
                            stdOutClosed = true;
                        }
                        else
                        {
                            Console.Out.WriteLine(a.Data);
                        }
                    };

                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                if (!timeout.HasValue || timeout.Value >= TimeSpan.FromDays(1))
                {
                    NeonHelper.WaitFor(() => stdErrClosed && stdOutClosed, timeout: timeout ?? TimeSpan.FromDays(365), pollTime: TimeSpan.FromMilliseconds(250));
                    process.WaitForExit();
                }
                else
                {
                    NeonHelper.WaitFor(() => stdErrClosed && stdOutClosed, timeout: timeout.Value, pollTime: TimeSpan.FromMilliseconds(250));
                    process.WaitForExit((int)timeout.Value.TotalMilliseconds);

                    if (!process.HasExited)
                    {
                        if (killOnTimeout)
                        {
                            process.Kill();
                        }

                        throw new TimeoutException(string.Format("Process [{0}] execute has timed out.", path));
                    }
                }

                return process.ExitCode;
            }
            finally
            {
                process.Dispose();
            }
        }

        /// <summary>
        /// Asyncrhonously starts a process to run an executable file with an array of
        /// arguments and then and waits for the process to terminate.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>The process exit code.</returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not commpleted in time
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<int> ExecuteAsync(string path, object[] args, TimeSpan? timeout = null, Process process = null)
        {
            return await ExecuteAsync(path, NormalizeExecArgs(args), timeout, process);
        }

        /// <summary>
        /// Asyncrhonously starts a process to run an executable file and then waits for the process to terminate.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>The process exit code.</returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not commpleted in time
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<int> ExecuteAsync(string path, string args, TimeSpan? timeout = null, Process process = null)
        {
            return await Task.Run(() => Execute(path, args, timeout, process));
        }

        /// <summary>
        /// Used to redirect process output streams.
        /// </summary>
        private sealed class ProcessStreamRedirector
        {
            private object          syncLock       = new object();
            public StringBuilder    sbOutput       = new StringBuilder();
            public StringBuilder    sbError        = new StringBuilder();
            public bool             isOutputClosed = false;
            public bool             isErrorClosed  = false;
            public Action<string>   outputAction   = null;
            public Action<string>   errorAction    = null;

            public void OnOutput(object sendingProcess, DataReceivedEventArgs args)
            {
                lock (syncLock)
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                    {
                        isOutputClosed = true;
                    }
                    else
                    {
                        sbOutput.AppendLine(args.Data);

                        if (outputAction != null)
                        {
                            outputAction(args.Data + NeonHelper.LineEnding);
                        }
                    }
                }
            }

            public void OnError(object sendingProcess, DataReceivedEventArgs args)
            {
                lock (syncLock)
                {
                    if (string.IsNullOrWhiteSpace(args.Data))
                    {
                        isErrorClosed = true;
                    }
                    else
                    {
                        sbError.AppendLine(args.Data);

                        if (errorAction != null)
                        {
                            errorAction(args.Data + NeonHelper.LineEnding);
                        }
                        else if (outputAction != null)
                        {
                            outputAction(args.Data + NeonHelper.LineEnding);
                        }
                    }
                }
            }

            public void Wait()
            {
                while (!isOutputClosed || !isErrorClosed)
                {
                    Thread.Sleep(100);
                }
            }
        }

        /// <summary>
        /// Starts a process to run an executable file and then waits for the process to terminate
        /// while capturing any output written to the standard output and error streams.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <param name="outputAction">Optional action that will be called when the process outputs some text.</param>
        /// <param name="errorAction">Optional action that will be called when the process outputs some error text.</param>
        /// <returns>
        /// The <see cref="ExecuteResponse"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not commpleted in time 
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// <para>
        /// You can optionally specify the <paramref name="outputAction"/> and/or <paramref name="errorAction"/>
        /// callbacks to receive the process output as it is received.  <paramref name="outputAction"/> will 
        /// be called with both the STDOUT and STDERR streams if <paramref name="errorAction"/> is <c>null</c>
        /// otherwise it will called only with STDOUT text.
        /// </para>
        /// </remarks>
        public static ExecuteResponse ExecuteCapture(
            string          path, 
            object[]        args, 
            TimeSpan?       timeout = null,
            Process         process = null,
            Action<string>  outputAction = null,
            Action<string>  errorAction = null)
        {
            return ExecuteCapture(path, NormalizeExecArgs(args), timeout, process, outputAction, errorAction);
        }

        /// <summary>
        /// Starts a process to run an executable file and then waits for the process to terminate
        /// while capturing any output written to the standard output and error streams.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Optional maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <param name="outputAction">Optional action that will be called when the process outputs some text.</param>
        /// <param name="errorAction">Optional action that will be called when the process outputs some error text.</param>
        /// <returns>
        /// The <see cref="ExecuteResponse"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not commpleted in time 
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// <para>
        /// You can optionally specify the <paramref name="outputAction"/> and/or <paramref name="errorAction"/>
        /// callbacks to receive the process output as it is received.  <paramref name="outputAction"/> will 
        /// be called with both the STDOUT and STDERR streams if <paramref name="errorAction"/> is <c>null</c>
        /// otherwise it will called only with STDOUT text.
        /// </para>
        /// </remarks>
        public static ExecuteResponse ExecuteCapture(
            string          path, 
            string          args, 
            TimeSpan?       timeout = null,
            Process         process = null,
            Action<string>  outputAction = null,
            Action<string>  errorAction = null)
        {
            var processInfo     = new ProcessStartInfo(GetProgramPath(path), args ?? string.Empty);
            var externalProcess = process != null;
            var redirector      = new ProcessStreamRedirector()
            {
                outputAction = outputAction,
                errorAction  = errorAction
            };

            if (process == null)
            {
                process = new Process();
            }

            try
            {
                processInfo.UseShellExecute        = false;
                processInfo.RedirectStandardError  = true;
                processInfo.RedirectStandardOutput = true;
                processInfo.CreateNoWindow         = true;
                processInfo.WorkingDirectory       = Environment.CurrentDirectory;

                process.StartInfo                  = processInfo;
                process.OutputDataReceived        += new DataReceivedEventHandler(redirector.OnOutput);
                process.ErrorDataReceived         += new DataReceivedEventHandler(redirector.OnError);
                process.EnableRaisingEvents        = true;

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                if (!timeout.HasValue || timeout.Value >= TimeSpan.FromDays(1))
                {
                    process.WaitForExit();
                }
                else
                {
                    process.WaitForExit((int)timeout.Value.TotalMilliseconds);

                    if (!process.HasExited)
                    {
                        if (!externalProcess)
                        {
                            process.Kill();
                        }

                        throw new TimeoutException(string.Format("Process [{0}] execute has timed out.", path));
                    }
                }

                redirector.Wait();  // Wait for the standard output/error streams
                                    // to receive all the data

                return new ExecuteResponse()
                {
                    ExitCode   = process.ExitCode,
                    OutputText = redirector.sbOutput.ToString(),
                    ErrorText  = redirector.sbError.ToString()
                };
            }
            finally
            {
                if (!externalProcess)
                {
                    process.Dispose();
                }
            }
        }

        /// <summary>
        /// Asynchronously starts a process to run an executable file and then waits for the process to terminate
        /// while capturing any output written to the standard output and error streams.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>
        /// The <see cref="ExecuteResponse"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not commpleted in time 
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<ExecuteResponse> ExecuteCaptureAsync(string path, object[] args,
                                                                    TimeSpan? timeout = null, Process process = null)
        {
            return await ExecuteCaptureAsync(path, NormalizeExecArgs(args), timeout, process);
        }

        /// <summary>
        /// Asynchronously starts a process to run an executable file and then waits for the process to terminate
        /// while capturing any output written to the standard output and error streams.
        /// </summary>
        /// <param name="path">Path to the executable file.</param>
        /// <param name="args">Command line arguments (or <c>null</c>).</param>
        /// <param name="timeout">
        /// Maximum time to wait for the process to complete or <c>null</c> to wait
        /// indefinitely.
        /// </param>
        /// <param name="process">
        /// The optional <see cref="Process"/> instance to use to launch the process.
        /// </param>
        /// <returns>
        /// The <see cref="ExecuteResponse"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is exceeded and execution has not commpleted in time 
        /// then a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<ExecuteResponse> ExecuteCaptureAsync(string path, string args, 
                                                                    TimeSpan? timeout = null, Process process = null)
        {
            return await Task.Run(() => ExecuteCapture(path, args, timeout, process));
        }

#if NETSTANDARD2_0
        /// <summary>
        /// Starts a process for an <see cref="Assembly" /> by calling the assembly's <b>main()</b>
        /// entry point method. 
        /// </summary>
        /// <param name="assembly">The assembly to be started.</param>
        /// <param name="args">The command line arguments (or <c>null</c>).</param>
        /// <returns>The process started.</returns>
        /// <remarks>
        /// <note>
        /// This method works only for executable assemblies with
        /// an appropriate <b>main</b> entry point that reside on the
        /// local file system.
        /// </note>
        /// </remarks>
        public static Process StartProcess(Assembly assembly, string args)
        {
            string path = assembly.CodeBase;

            if (!path.StartsWith("file://"))
            {
                throw new ArgumentException("Assembly must reside on the local file system.", "assembly");
            }

            return Process.Start(NeonHelper.StripFileScheme(path), args != null ? args : string.Empty);
        }
#endif

        /// <summary>
        /// Launches the default browser to display the specified URI.
        /// </summary>
        /// <param name="uri">The target URI.</param>
        public static void OpenBrowser(string uri)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            if (IsWindows)
            {
                var processStart = new ProcessStartInfo(uri)
                {
                    CreateNoWindow = true
                };

                Process.Start(processStart);
            }
            else if (IsOSX)
            {
                // $todo(jeff.lill): Test this.

                Process.Start("open", uri);
            }
            else if (IsLinux)
            {
                // $todo(jeff.lill): test this.

                Process.Start("xdg-open", uri);
            }
            else
            {
                throw new NotImplementedException("Browser launch support is not implemented on the current platform.");
            }
        }

        /// <summary>
        /// Executes a command using the local shell, <b>CMD.EXE</b> for Windows and
        /// <b>Bash</b> for OSX and Linux.
        /// </summary>
        /// <param name="command">The command and arguments to be executed.</param>
        /// <returns>The process exit code.</returns>
        public static int ExecuteShell(string command)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(command));

            Process process;

            if (IsWindows)
            {
                process = Process.Start("cmd", $"/C {command}");
            }
            else if (IsOSX)
            {
                // $todo(jeff.lill): Test this.

                process = Process.Start("bash", $"-c '{command}'");
            }
            else if (IsLinux)
            {
                // $todo(jeff.lill): test this.

                process = Process.Start("bash", $"-c '{command}'");
            }
            else
            {
                throw new NotImplementedException("Shell launch support is not implemented on the current platform.");
            }

            process.WaitForExit();

            return process.ExitCode;
        }

        /// <summary>
        /// Returns the <see cref="Process"/> associated with an ID
        /// or <c>null</c> if no process with this ID exists.
        /// </summary>
        /// <param name="id">The target process ID.</param>
        /// <returns>The <see cref="Process"/> or <c>null</c>.</returns>
        /// <remarks>
        /// This is slightly different from how <see cref="Process.GetProcessById(int)"/>
        /// works.  That method throws an <see cref="ArgumentException"/> if there's
        /// no process with the ID where as this one will return <c>null</c>.
        /// </remarks>
        public static Process GetProcessById(int id)
        {
            try
            {
                return Process.GetProcessById(id);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }
    }
}
