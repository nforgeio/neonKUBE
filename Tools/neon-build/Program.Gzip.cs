//-----------------------------------------------------------------------------
// FILE:	    Program.Shfb.cs
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
using System.IO;
using System.Linq;
using System.Text;

using Neon.Common;

namespace NeonBuild
{
    public static partial class Program
    {
        /// <summary>
        /// GZIPs a file as required.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        private static void Gzip(CommandLine commandLine)
        {
            var sourcePath = commandLine.Arguments.ElementAtOrDefault(1);
            var targetPath = commandLine.Arguments.ElementAtOrDefault(2);

            if (sourcePath == null)
            {
                Console.Error.WriteLine("*** ERROR: SOURCE argument is required.");
                Program.Exit(1);
            }

            if (targetPath == null)
            {
                Console.Error.WriteLine("*** ERROR: TARGET argument is required.");
                Program.Exit(1);
            }

            if (!File.Exists(sourcePath))
            {
                Console.Error.WriteLine($"*** ERROR: SOURCE file [{sourcePath}] does not exist.");
                Program.Exit(1);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath));

            if (File.Exists(targetPath) && File.GetLastWriteTimeUtc(targetPath) > File.GetLastWriteTimeUtc(sourcePath))
            {
                Console.WriteLine($"File [{targetPath}] is up to date.");
                Program.Exit(0);
            }

            Console.WriteLine($"GZIP: [{sourcePath}] --> [{targetPath}].");

            var uncompressed = File.ReadAllBytes(sourcePath);
            var compressed = NeonHelper.GzipBytes(uncompressed);

            File.WriteAllBytes(targetPath, compressed);
        }
    }
}
