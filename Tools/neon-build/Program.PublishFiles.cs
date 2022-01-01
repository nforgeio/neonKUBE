//-----------------------------------------------------------------------------
// FILE:	    Program.PublishFiles.cs
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
        /// Implements the <b>publish-files</b> command.
        /// </summary>
        /// <param name="commandLine">The command line.</param>
        public static void PublishFiles(CommandLine commandLine)
        {
            commandLine = commandLine.Shift(1);

            if (commandLine.Arguments.Length != 2)
            {
                Console.WriteLine(usage);
                Program.Exit(1);
            }

            var sourcePattern    = commandLine.Arguments[0];
            var targetFolder     = commandLine.Arguments[1];
            var excludeKustomize = commandLine.HasOption("--exclude-kustomize");
            var noDelete         = commandLine.HasOption("--no-delete");
            var sourceFolder     = Path.GetDirectoryName(sourcePattern);

            Console.WriteLine($"neon-build publish-files: {sourcePattern} --> {targetFolder}");

            if (!Directory.Exists(sourceFolder))
            {
                Console.Error.WriteLine($"*** ERROR: [SOURCE-FOLDER={sourceFolder}] does not exist!");
                Program.Exit(1);
            }

            if (!noDelete && Directory.Exists(targetFolder))
            {
                Directory.Delete(targetFolder, recursive: true);
            }
            
            Directory.CreateDirectory(targetFolder);

            var count = 0;

            foreach (var file in Directory.GetFiles(sourceFolder, Path.GetFileName(sourcePattern), SearchOption.TopDirectoryOnly))
            {
                var filename = Path.GetFileName(file);

                if (excludeKustomize && filename.Equals("kustomization.yaml", StringComparison.InvariantCultureIgnoreCase))
                {
                    continue;
                }

                File.Copy(file, Path.Combine(targetFolder, Path.GetFileName(file)));
                count++;
            }

            if (count == 0)
            {
                Console.Error.WriteLine("*** WARNING: [0] files published");
            }
        }
    }
}
