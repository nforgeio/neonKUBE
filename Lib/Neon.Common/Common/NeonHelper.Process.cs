//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Process.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
        /// Starts a process to run an executable file and waits for the process to terminate.
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
        /// If <paramref name="timeout"/> is and execution has not commpleted in time then
        /// a <see cref="TimeoutException"/> will be thrown and the process will be killed
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
        /// Asyncrhonously starts a process to run an executable file and waits for the process to terminate.
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
        /// If <paramref name="timeout"/> is and execution has not commpleted in time then
        /// a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<int> ExecuteAsync(string path, string args, TimeSpan? timeout = null, Process process = null)
        {
            return await Task.Run(() => Execute(path, args, timeout, process));
        }

        /// <summary>
        /// Used by <see cref="ExecuteCaptureStreams(string, string, TimeSpan?, Process)"/> to redirect process output streams.
        /// </summary>
        private sealed class StreamRedirect
        {
            private object          syncLock       = new object();
            public StringBuilder    sbOutput       = new StringBuilder();
            public StringBuilder    sbError        = new StringBuilder();
            public bool             isOutputClosed = false;
            public bool             isErrorClosed  = false;

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
        /// Starts a process to run an executable file and waits for the process to terminate
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
        /// <returns>
        /// The <see cref="ExecuteResult"/> including the process exit code and capture 
        /// standard output and error streams.
        /// </returns>
        /// <exception cref="TimeoutException">Thrown if the process did not exit within the <paramref name="timeout"/> limit.</exception>
        /// <remarks>
        /// <note>
        /// If <paramref name="timeout"/> is and execution has not commpleted in time then
        /// a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static ExecuteResult ExecuteCaptureStreams(string path, string args, TimeSpan? timeout = null, Process process = null)
        {
            var processInfo     = new ProcessStartInfo(GetProgramPath(path), args != null ? args : string.Empty);
            var redirect        = new StreamRedirect();
            var externalProcess = process != null;

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
                process.OutputDataReceived        += new DataReceivedEventHandler(redirect.OnOutput);
                process.ErrorDataReceived         += new DataReceivedEventHandler(redirect.OnError);
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

                redirect.Wait();    // Wait for the standard output/error streams
                                    // to receive all the data

                return new ExecuteResult()
                    {
                        ExitCode   = process.ExitCode,
                        OutputText = redirect.sbOutput.ToString(),
                        ErrorText  = redirect.sbError.ToString()
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
        /// Asynchronously starts a process to run an executable file and waits for the process to terminate
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
        /// If <paramref name="timeout"/> is and execution has not commpleted in time then
        /// a <see cref="TimeoutException"/> will be thrown and the process will be killed
        /// if it was created by this method.  Process instances passed via the <paramref name="process"/>
        /// parameter will not be killed in this case.
        /// </note>
        /// </remarks>
        public static async Task<ExecuteResult> ExecuteCaptureStreamsAsync(string path, string args, 
                                                                           TimeSpan? timeout = null, Process process = null)
        {
            return await Task.Run(() => ExecuteCaptureStreams(path, args, timeout, process));
        }

#if NETSTANDARD1_5
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
            Covenant.Requires<ArgumentNullException>(uri != null);

            if (IsWindows)
            {
                Process.Start("cmd", $"/C start {uri}");
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
    }
}
