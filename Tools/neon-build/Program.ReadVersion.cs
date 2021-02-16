//-----------------------------------------------------------------------------
// FILE:	    Program.ReadVersion.cs
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
using System.IO;
using System.Linq;
using System.Text;

using Neon.Common;

namespace NeonBuild
{
    public static partial class Program
    {
        /// <summary>
        /// Reads named version constant from a C# source file and writes it to SDTOUT.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private static void ReadVersion(CommandLine commandLine)
        {
            var csPath     = commandLine.Arguments.ElementAtOrDefault(1);
            var name       = commandLine.Arguments.ElementAtOrDefault(2);
            var terminator = !commandLine.HasOption("-n");

            if (string.IsNullOrEmpty(csPath))
            {
                Console.Error.WriteLine("*** ERROR: CSPROJ argument is required.");
                Program.Exit(1);
            }

            if (string.IsNullOrEmpty(name))
            {
                Console.Error.WriteLine("*** ERROR: NAME argument is required.");
                Program.Exit(1);
            }

            try
            {
                Console.Write(ReadVersion(csPath, name));

                if (terminator)
                {
                    Console.WriteLine();
                }

                Program.Exit(0);
            }
            catch
            {
                Console.Error.WriteLine($"*** ERROR: Cannot locate the constant [{name}] in [{csPath}].");
                Console.Error.WriteLine("            Make sure the constant definition is formatted exactly like:");
                Console.Error.WriteLine();
                Console.Error.WriteLine("            public const string NAME = \"VALUE\";");
                Program.Exit(1);
            }
        }
    }
}
