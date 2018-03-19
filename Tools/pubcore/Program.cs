//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.
//
// This tool hacks the publishing of a .NET Core console application to
// an external output folder as part of a build.  We can't use dotnet
// publish within a post-build event because this causes recursive builds
// so we're just going to hack this.

using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        /// Tool version number.
        /// </summary>
        public const string Version = "1.1";

        /// <summary>
        /// Program entrypoint.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            if (args.Length != 6)
            {
                Console.WriteLine(
$@"
.NET Core Publishing Utility: pubcore [v{Version}]

usage: pubcore PROJECT-PATH CONFIG TARGET-NAME PUBLISH-PATH [RUNTIME]

    PROJECT-PATH    - Path to the [.csproj] file
    TARGET-NAME     - Build target name
    CONFIG          - Build configuration (like: Debug or Release)
    TARGET-PATH     - Build target path
    PUBLISH-PATH    - Path to the publication folder

REMARKS:

This utility is designed to be called from within a .NET Core project's
POST-BUILD event using Visual Studio post-build event macros.  Here's
an example that publishes a standalone [win10-x64] app to OUTPUT-PATH.

    pubcore ""$(ProjectPath)"" ""$(TargetName)"" ""$(ConfigurationName)"" ""OUTPUT-PATH"" win10-x64

Note that you MUST ADD the following to the <PropertyGroup>...</PropertyGroup>
section on your project CSPROJ file for this to work:

    <RuntimeIdentifier>win10-x64</RuntimeIdentifier>
");
                Environment.Exit(1);
            }

            // We need to examine/set an environment variable to prevent the [dotnet publish...]
            // command below from recursively invoking the project build event that will invoke
            // the program again.

            const string recursionVar = "unix-text-68A780E5-00E7-4158-B5DE-E95C1D284911";

            if (Environment.GetEnvironmentVariable(recursionVar) == "true")
            {
                // Looks like we've recursed, so just bail right now.

                return;
            }

            Environment.SetEnvironmentVariable(recursionVar, "true");

            // Parse the arguments.

            var projectPath = args[0];

            if (!File.Exists(projectPath))
            {
                Console.WriteLine($"Project file [{projectPath}] does not exist.");
                Environment.Exit(1);
            }

            var targetName  = args[1];
            var config      = args[2];
            var targetPath  = Path.Combine(Path.GetDirectoryName(args[3]), "publish");
            var publishPath = args[4];
            var runtime     = args.ElementAtOrDefault(5);
            var binFolder   = Path.Combine(publishPath, targetName);

            Directory.CreateDirectory(publishPath);

            // Ensure that the runtime identifier is present in the project file.

            var projectText     = File.ReadAllText(projectPath);
            var runtimeProperty = $"<RuntimeIdentifier>{runtime}</RuntimeIdentifier>";

            if (!projectText.Contains(runtimeProperty))
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("ERROR: Make sure the runtime identifier below is present in your");
                Console.Error.WriteLine("       project's <PropertyGroup/> section:");
                Console.Error.WriteLine();
                Console.Error.WriteLine($"    <PropertyGroup>");
                Console.Error.WriteLine($"        {runtimeProperty}");
                Console.Error.WriteLine($"    </PropertyGroup>");
                Console.Error.WriteLine();
                Console.Error.WriteLine($"PROJECT: {Path.GetFullPath(projectPath)}");
                Console.Error.WriteLine();

                Environment.Exit(1);
            }

            // Publish the project.

            var startInfo = new ProcessStartInfo("dotnet.exe")
            {
                CreateNoWindow  = true,
                UseShellExecute = false
            };

            startInfo.Arguments = $"publish \"{projectPath}\" -c \"{config}\" -r {runtime}";

            var process = Process.Start(startInfo);

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                Environment.Exit(process.ExitCode);
            }

            // It appears that references to published files can remain open 
            // after previous tool executions, even after the program has
            // exited.  I'm not sure what's causing this, but we'll mitigate
            // pausing and retrying a few times.

            const int tryCount = 5;

            for (int i = 1; i <= tryCount; i++)
            {
                try
                {
                    File.WriteAllText(Path.Combine(publishPath, $"{targetName}.cmd"), 
$@"@echo off
%~dp0\{targetName}\{targetName}.exe %*
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
