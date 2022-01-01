//-----------------------------------------------------------------------------
// FILE:        Program.cs
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
using System.Linq;
using System.Text;

using Neon.Common;

namespace ExecTarget
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            try
            {
                var commandLine  = new CommandLine(args);
                var encodingName = commandLine.GetOption("--encoding");
                var exitCode     = int.Parse(commandLine.GetOption("--exitcode", "1"));
                var lineCount    = int.Parse(commandLine.GetOption("--lines", "1"));
                var text         = commandLine.GetOption("--text", "Hello World!");
                var writeOutput  = commandLine.HasOption("--write-output");
                var writeError   = commandLine.HasOption("--write-error");

                if (!string.IsNullOrEmpty(encodingName))
                {
                    Console.OutputEncoding = Encoding.GetEncoding(encodingName);
                }

                for (int i = 0; i < lineCount; i++)
                {
                    if (writeOutput)
                    {
                        Console.Out.WriteLine(text);
                    }

                    if (writeError)
                    {
                        Console.Error.WriteLine(text);
                    }
                }

                Environment.Exit(exitCode);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {e.Message}");
                Environment.Exit(-1);
            }
        }
    }
}
