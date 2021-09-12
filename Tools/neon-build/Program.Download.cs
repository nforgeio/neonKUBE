//-----------------------------------------------------------------------------
// FILE:	    Program.Download.cs
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
using System.Net.Http;
using System.Text;

using Neon.Common;

namespace NeonBuild
{
    public static partial class Program
    {
        /// <summary>
        /// Implements the <b>download</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        public static void Download(CommandLine commandLine)
        {
            commandLine = commandLine.Shift(1);

            if (commandLine.Arguments.Length != 2)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            // We're going to download the file as a byte array and then compare this
            // against any existing file and only update the target file when the source
            // and target differ.  This will avoid writing to disk unnecessarily.

            var sourceUri  = commandLine.Arguments[0];
            var targetPath = commandLine.Arguments[1];

            try
            {
                using (var httpClient = new HttpClient())
                {
                    var downloadBytes = httpClient.GetByteArrayAsync(sourceUri).Result;

                    if (File.Exists(targetPath))
                    {
                        var existingBytes = File.ReadAllBytes(targetPath);

                        if (!NeonHelper.ArrayEquals(downloadBytes, existingBytes))
                        {
                            File.WriteAllBytes(targetPath, downloadBytes);
                        }
                    }
                    else
                    {
                        File.WriteAllBytes(targetPath, downloadBytes);
                    }
                }
            }
            catch (IOException e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Program.Exit(1);
            }
            catch (Exception e)
            {
                // Note that we're not returning non-zero exit codes for non-I/O errors
                // so that developers will be able to build when offline.

                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
            }
        }
    }
}
