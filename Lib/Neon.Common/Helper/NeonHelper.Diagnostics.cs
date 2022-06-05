//-----------------------------------------------------------------------------
// FILE:	    NeonHelper.Diagnostics.cs
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
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Diagnostics;
using System.Diagnostics;

namespace Neon.Common
{
    public static partial class NeonHelper
    {
        private static object       debugLogSyncRoot  = new object();
        private static string       debugLogPath      = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "debug-log.txt");
        private static Stopwatch    debugLogStopwatch = null;

        /// <summary>
        /// Creates and starts the debug log stopwatch when it's not already running.
        /// </summary>
        /// <param name="restart">Optionally restart the stopwatch.</param>
        private static void StartDebugLogStopwatch(bool restart = false)
        {
            lock (debugLogSyncRoot)
            {
                if (debugLogStopwatch == null)
                {
                    debugLogStopwatch = new Stopwatch();
                    debugLogStopwatch.Start();
                }
                else if (restart)
                {
                    debugLogStopwatch.Restart();
                }
            }
        }

        /// <summary>
        /// <para>
        /// The fully qualified path to the file where the simple <see cref="LogDebug(string)"/>
        /// method will write debug lines.  This defaults to <b>debug-log.txt</b> within the
        /// current user's home folder.
        /// </para>
        /// <para>
        /// You may change this to a different location.
        /// </para>
        /// </summary>
        public static string DebugLogPath
        {
            get
            {
                lock (debugLogSyncRoot)
                {
                    return debugLogPath;
                }
            }

            set
            {
                lock (debugLogSyncRoot)
                {
                    debugLogPath = value;
                    StartDebugLogStopwatch(restart: true);
                }
            }
        }

        /// <summary>
        /// Clears the debug log file if it exists.
        /// </summary>
        public static void ClearDebugLog()
        {
            lock (debugLogSyncRoot)
            {
                if (!string.IsNullOrEmpty(DebugLogPath) && File.Exists(DebugLogPath))
                {
                    File.WriteAllText(DebugLogPath, string.Empty);
                }

                StartDebugLogStopwatch(restart: true);
            }
        }

        /// <summary>
        /// Appends a line of text to the file at <see cref="DebugLogPath"/>.  This is intended for
        /// low-level debugging when normal logging via <see cref="LogManager"/> isn't suitable (i.e.
        /// when debugging logging code or application initialization code running before normal 
        /// logging is configured.
        /// </summary>
        /// <param name="line">Optionally specifies the line of text.</param>
        public static void LogDebug(string line = null)
        {
            lock (debugLogSyncRoot)
            {
                var folder = Path.GetDirectoryName(DebugLogPath);

                line  = line ?? string.Empty;
                line += Environment.NewLine;

                Directory.CreateDirectory(folder);
                File.AppendAllText(DebugLogPath, $"[{debugLogStopwatch.Elapsed}] {line}");
            }
        }

        /// <summary>
        /// Appends exception information to the file at <see cref="DebugLogPath"/>.  This is intended for
        /// low-level debugging when normal logging via <see cref="LogManager"/> isn't suitable.
        /// </summary>
        /// <param name="e">The exception.</param>
        public static void LogDebug(Exception e)
        {
            Covenant.Assert(e != null, nameof(e));

            lock (debugLogSyncRoot)
            {
                var folder = Path.GetDirectoryName(DebugLogPath);

                LogDebug();
                LogDebug($"EXCEPTION: {e.GetType().FullName}");
                LogDebug(e.StackTrace);
                LogDebug();
                LogDebug();
            }
        }
    }
}
