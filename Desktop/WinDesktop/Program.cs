//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Neon.Common;
using Neon.Kube;
using Neon.Windows;

namespace WinDesktop
{
    /// <summary>
    /// Manages application state.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Returns the global application thread synchronization object. 
        /// </summary>
        public static readonly object SyncLock = new object();

#if DEBUG_LOG
        //---------------------------------------------------------------------
        // Use these methods for debugging startup issues.  Note that the log
        // file and folder are hardcoded.
        //
        // IMPORTANT: THIS MUST NOT BE ENABLED FOR PRODUCTION RELEASES.

        private const bool              logEnabled = true;
        private const string            logFolder  = @"C:\temp";
        private static readonly string  logPath    = $@"{logFolder}\desktop.log";

        /// <summary>
        /// Clears the DEBUUG log.
        /// </summary>
        public static void LogClear()
        {
            Directory.CreateDirectory(logFolder);
            File.WriteAllText(logPath, string.Empty);
        }

        /// <summary>
        /// Appends a line of text to the DEBUG log.
        /// </summary>
        /// <param name="text">The text.</param>
        public static void Log(string text)
        {
            File.AppendAllText(logPath, text + "\r\n");
        }
#endif

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Change the current directory to the program folder because we
            // use relative references to the Icon and other resource files.

            ProgramFolder = Path.GetFullPath(NeonHelper.GetAssemblyFolder(Assembly.GetExecutingAssembly()));

            if (ProgramFolder.EndsWith("\\") || ProgramFolder.EndsWith("/"))
            {
                ProgramFolder = ProgramFolder.Substring(0, ProgramFolder.Length - 1);
            }

            Environment.CurrentDirectory = ProgramFolder;

            // Use the version of Powershell Core installed with the application,
            // if present.

            PowerShell.PwshPath = KubeHelper.PwshPath;

            // Start the app.

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            Application.Run(new MainForm());
        }

        /// <summary>
        /// Returns the absolute path to the program folder.
        /// </summary>
        public static string ProgramFolder { get; private set; }

        /// <summary>
        /// Returns a <see cref="ClusterProxy"/> for the current 
        /// Kubernetes context.
        /// </summary>
        /// <returns>
        /// The <see cref="ClusterProxy"/> or <c>null</c> when not 
        /// logged into a neonHUBE cluster.</returns>
        public static ClusterProxy GetCluster()
        {
            if (KubeHelper.CurrentContext == null)
            {
                return null;
            }

            return new ClusterProxy(KubeHelper.CurrentContext, Program.CreateNodeProxy<NodeDefinition>);
        }

        /// <summary>
        /// Creates a <see cref="SshProxy{TMetadata}"/> for the specified host and server name,
        /// configuring logging and the credentials as specified by the global command
        /// line options.
        /// </summary>
        /// <param name="name">The node name.</param>
        /// <param name="address">The node's private IP address.</param>
        /// <param name="appendToLog">
        /// Pass <c>true</c> to append to an existing log file (or create one if necessary)
        /// or <c>false</c> to replace any existing log file with a new one.
        /// </param>
        /// <typeparam name="TMetadata">Defines the metadata type the command wishes to associate with the server.</typeparam>
        /// <returns>The <see cref="SshProxy{TMetadata}"/>.</returns>
        public static SshProxy<TMetadata> CreateNodeProxy<TMetadata>(string name, IPAddress address, bool appendToLog)
            where TMetadata : class
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            var sshCredentials = KubeHelper.CurrentContext.Extension.SshCredentials; ;

            return new SshProxy<TMetadata>(name, address, sshCredentials);
        }

        /// <summary>
        /// Logs an exception as an error.
        /// </summary>
        /// <param name="e">The exception.</param>
        public static void LogError(Exception e)
        {
            lock (SyncLock)
            {
                // $todo(jefflill): Implement this
            }
        }

        /// <summary>
        /// Logs an error message.
        /// </summary>
        /// <param name="message">The message.</param>
        public static void LogError(string message)
        {
            lock (SyncLock)
            {
                // $todo(jefflill): Implement this
            }
        }

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        /// <param name="message">The message.</param>
        public static void LogWarning(string message)
        {
            lock (SyncLock)
            {
                // $todo(jefflill): Implement this
            }
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary>
        /// <param name="message">The message.</param>
        public static void LogInfo(string message)
        {
            lock (SyncLock)
            {
                // $todo(jefflill): Implement this
            }
        }
    }
}
