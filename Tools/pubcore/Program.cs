//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.
//
// This tool hacks the publishing of a .NET Core console application to
// an external output folder as part of a build.  We can't use dotnet
// publish within a post-build event because this causes recursive builds
// so we're just going to hack this.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace pubcore
{
    /// <summary>
    /// Program clss.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Program entrypoint.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine(
@"
usage: pubcore CSPROJ-PATH BIN-PATH PUBLISH-PATH

    TARGET-PATH     - Path to the build target.
    PUBLISH-PATH    - Path to the output folder. 
");
                Environment.Exit(1);
            }

            if (!File.Exists(args[0]))
            {
                Console.WriteLine($"File [{args[0]}] does not exist.");
                Environment.Exit(1);
            }

            var programName = Path.GetFileNameWithoutExtension(args[0]);
            var targetPath  = Path.GetDirectoryName(args[0]);
            var publishPath = args[1];
            var binFolder   = Path.Combine(publishPath, programName);

            Directory.CreateDirectory(publishPath);

            // It appears that references to published files can remain open 
            // after previous tool executions, even after the program has
            // exited.  I'm not sure what's causing this, but we'll mitigate
            // pausing and retrying a few times.

            const int tryCount = 5;

            for (int i = 1; i <= tryCount; i++)
            {
                try
                {
                    File.WriteAllText(Path.Combine(publishPath, $"{programName}.cmd"),
$@"@echo off
dotnet {binFolder}\{Path.GetFileName(args[0])} %*
");
                    // Remove the output folder and then recreate it to ensure
                    // that all old files will be removed.

                    if (Directory.Exists(binFolder))
                    {
                        Directory.Delete(binFolder, recursive: true);
                    }

                    Directory.CreateDirectory(binFolder);

                    CopyRecursive(targetPath, binFolder);
                    break;
                }
                catch (Exception e)
                {
                    if (i < tryCount)
                    {
                        Thread.Sleep(TimeSpan.FromSeconds(5));
                        continue;
                    }

                    Console.WriteLine($"{e.GetType().FullName}: {e.Message}");
                    Environment.Exit(1);
                }
            }

            Environment.Exit(0);
        }

        /// <summary>
        /// Recursively copies the contents of one folder to another.
        /// </summary>
        /// <param name="sourceFolder">The source folder path.</param>
        /// <param name="targetFolder">The target folder path.</param>
        private static void CopyRecursive(string sourceFolder, string targetFolder)
        {
            Directory.CreateDirectory(targetFolder);

            foreach (var file in Directory.GetFiles(sourceFolder, "*.*", SearchOption.TopDirectoryOnly))
            {
                File.Copy(file, Path.Combine(targetFolder, Path.GetFileName(file)), overwrite: true);
            }

            foreach (var folder in Directory.GetDirectories(sourceFolder))
            {
                var subfolder = folder.Split(Path.DirectorySeparatorChar).Last();

                CopyRecursive(Path.Combine(sourceFolder, subfolder), Path.Combine(targetFolder, subfolder));
            }
        }
    }
}
