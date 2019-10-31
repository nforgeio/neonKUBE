//-----------------------------------------------------------------------------
// FILE:	    Program.cs
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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon;
using Neon.Common;

namespace Text
{
    /// <summary>
    /// Text file manipulation utility.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Usage:
    /// </para>
    /// <code language="none">
    /// text replace -VAR=VALUE... FILE
    /// </code>
    /// </remarks>
    public static class Program
    {
        /// <summary>
        /// Tool version number.
        /// </summary>
        public const string Version = "1.0";

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">The list of files to be processed with optional wildcards.</param>
        public static void Main(string[] args)
        {
            var commandLine = new CommandLine(args);

            if (commandLine.Arguments.Length == 0 ||
                commandLine.HasHelpOption ||
                commandLine.Arguments[0].Equals("help", StringComparison.OrdinalIgnoreCase) ||
                commandLine.Arguments[0].Equals("--help", StringComparison.OrdinalIgnoreCase))
            {
                PrintUsage();
                Program.Exit(0);
            }

            Console.WriteLine(string.Empty);

            try
            {
                switch (commandLine.Arguments[0].ToLower())
                {
                    case "replace":

                        Replace(commandLine);
                        break;

                    case "replace-var":

                        ReplaceVar(commandLine);
                        break;

                    default:

                        PrintUsage();
                        Program.Exit(1);
                        break;
                }

                Program.Exit(0);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"{NeonHelper.ExceptionError(e)}");
                Console.Error.WriteLine(string.Empty);

                Program.Exit(1);
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine(
$@"
Neon Text File Utility: text [v{Version}]
{Build.Copyright}

usage: text replace     -TEXT=VALUE... FILE
       text replace-var -VAR=VALUE... FILE
       text help   
    
    --help              Print usage

");
        }

        /// <summary>
        /// Exits the program returning the specified process exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Performs the variable substitutions for variable references like: <b>${variable-name}</b>.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private static void ReplaceVar(CommandLine commandLine)
        {
            var sb = new StringBuilder();

            using (var reader = new StreamReader(new FileStream(commandLine.Arguments[1], FileMode.Open, FileAccess.Read)))
            {
                foreach (var line in reader.Lines())
                {
                    var temp = line;

                    foreach (var variable in commandLine.Options)
                    {
                        temp = temp.Replace($"${{{variable.Key.Substring(1)}}}", variable.Value);
                    }

                    sb.AppendLine(temp);
                }
            }

            using (var writer = new StreamWriter(new FileStream(commandLine.Arguments[1], FileMode.Create, FileAccess.ReadWrite)))
            {
                writer.Write(sb);
            }
        }

        /// <summary>
        /// Performs the text replacement operations specified in a command line.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private static void Replace(CommandLine commandLine)
        {
            var sb = new StringBuilder();

            using (var reader = new StreamReader(new FileStream(commandLine.Arguments[1], FileMode.Open, FileAccess.Read)))
            {
                foreach (var line in reader.Lines())
                {
                    var temp = line;

                    foreach (var variable in commandLine.Options)
                    {
                        temp = temp.Replace(variable.Key.Substring(1), variable.Value);
                    }

                    sb.AppendLine(temp);
                }
            }

            using (var writer = new StreamWriter(new FileStream(commandLine.Arguments[1], FileMode.Create, FileAccess.ReadWrite)))
            {
                writer.Write(sb);
            }
        }
    }
}
