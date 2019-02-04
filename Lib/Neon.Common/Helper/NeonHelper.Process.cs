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

            if (!NeonHelper.IsWindows)
            {
                return program;
            }

            if (Path.IsPathRooted(program) || program.Contains(Path.PathSeparator))
            {
                return program; // The path is already fully qualified or is relative.
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
                        foreach (var extension in new string[] { ".exe", ".cmd" })
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
        public static Process Fork(string path, object[] args)
        {
            var processInfo = new ProcessStartInfo(GetProgramPath(path), NormalizeExecArgs(args));

            processInfo.UseShellExecute        = false;
            processInfo.RedirectStandardError  = false;
            processInfo.RedirectStandardOutput = false;
            processInfo.CreateNoWindow         = true;

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
            var processInfo   = new ProcessStartInfo(GetProgramPath(path), args != null ? args : string.Empty);
            var killOnTimeout = process == null;

            if (process == null)
            {
                process = new Process();
            }

            try
            {
                processInfo.UseShellExecute        = false;
                processInfo.RedirectStandardError  = false;
                processInfo.RedirectStandardOutput = false;
                processInfo.CreateNoWindow         = true;
                process.StartInfo                  = processInfo;
                process.EnableRaisingEvents        = true;

                process.Start();

                if (!timeout.HasValue || timeout.Value >= TimeSpan.FromDays(1))
                {
                    process.WaitForExit();
                }
                else
                {
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
        /// The <see cref="ExecuteResult"/> including the process exit code and capture 
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
        public static ExecuteResult ExecuteCapture(
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
        /// The <see cref="ExecuteResult"/> including the process exit code and capture 
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
        public static ExecuteResult ExecuteCapture(
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

                return new ExecuteResult()
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
        /// The <see cref="ExecuteResult"/> including the process exit code and capture 
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
        public static async Task<ExecuteResult> ExecuteCaptureAsync(string path, object[] args,
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
        /// The <see cref="ExecuteResult"/> including the process exit code and capture 
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
        public static async Task<ExecuteResult> ExecuteCaptureAsync(string path, string args, 
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
                // $todo(jeff.lill):
                //
                // Firefox manages its own certificate store and does not trust Windows/OSX
                // certificates by default.  It looks like it is possible to configure
                // Firefox to trust the platform certificates but it then complained about
                // he certificate being self-signed, even though it was in the store.
                // store.
                //
                // I tried using Microsoft Edge but that didn't work either due to
                // apparent timing problems when reading the hosts file.  I'm going
                // to mitigate the issue by requiring and launching Chrome instead.
                //
                // Here's the tracking issue:
                //
                //      https://github.com/jefflill/NeonForge/issues/282

                // This code launches the default browser"
                //
                //      Process.Start("cmd", $"/C start {uri}");

                // This code launched Microsoft Edge:
                //
                //      Process.Start("cmd", $"/C start microsoft-edge:{uri}");

                var chromePath = Environment.ExpandEnvironmentVariables("%ProgramFiles(x86)%\\Google\\Chrome\\application\\chrome.exe"); ;

                if (!File.Exists(chromePath))
                {
                    throw new Exception("Google Chrome is required.  Please install this from: https://www.google.com/chrome");
                }

                Process.Start(chromePath, uri);
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
    }
}
