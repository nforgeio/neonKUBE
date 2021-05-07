//-----------------------------------------------------------------------------
// FILE:	    Program.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace NeonCli
{
    /// <summary>
    /// Hosts the program entrypoint.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program entrypoint.
        /// </summary>
        /// <param name="args">The command line arguments. (the "--test" argument implements a primitve test mode).</param>
        public static void Main(string[] args)
        {
            try
            {
                var testMode = args.Any(arg => arg == "--test");

                // Discover the file name of this executable and the related .INI file.

                var assembly = Assembly.GetExecutingAssembly();
                var exePath  = assembly.CodeBase;

                if (exePath.StartsWith("file://"))
                {
                    exePath = exePath.Substring(8);
                }

                var exeFolder = Path.GetDirectoryName(exePath);
                var exeName   = Path.GetFileNameWithoutExtension(exePath);
                var iniPath   = Path.Combine(exeFolder, $"{exeName}.ini");

                if (testMode)
                {
                    // We'll overwrite the INI file with the path to [test.cmd].

                    File.WriteAllText(iniPath, $"{exeFolder}/test.cmd");
                }

                // The first line of the INI file should be the path to the
                // CMD script being executed.

                if (!File.Exists(iniPath))
                {
                    Console.Error.WriteLine($"*** ERROR: INI file at [{iniPath}] does not exist.");
                    Environment.Exit(1);
                }

                var cmdPath = File.ReadAllLines(iniPath).First().Trim();

                if (String.IsNullOrWhiteSpace(cmdPath))
                {
                    Console.Error.WriteLine($"*** ERROR: [{iniPath}] does not specifiy a script path.");
                    Environment.Exit(1);
                }

                if (!File.Exists(cmdPath))
                {
                    Console.Error.WriteLine($"*** ERROR: Script file at [{cmdPath}] does not exist.");
                    Environment.Exit(1);
                }

                // Start the script.

                var processInfo = new ProcessStartInfo("cmd.exe")
                {
                    Arguments        = $"/c \"{cmdPath}\"",
                    UseShellExecute  = false,
                    WorkingDirectory = Path.GetDirectoryName(cmdPath)
                };

                var process = new Process()
                {
                    StartInfo = processInfo
                };

                process.Start();
                process.WaitForExit();
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** {e.GetType().FullName}: {e.Message}");
                Environment.Exit(1);
            }

            Environment.Exit(0);
        }

        /// <summary>
        /// Appends a line of text to a fixed log file at <b>C:\Temp\log.txt</b>.  This is
        /// used internally for debugging.
        /// </summary>
        /// <param name="line">The line of text.</param>
        private static void Log(string line = null)
        {
            var path   = @"C:\Temp\log.txt";
            var folder = Path.GetDirectoryName(path);

            line = line ?? string.Empty;

            Directory.CreateDirectory(folder);
            File.AppendAllLines(path, new string[] { line });
        }
    }
}
