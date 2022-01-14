//-----------------------------------------------------------------------------
// FILE:	    Program.cs
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
    /// Program class.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Tool version number.
        /// </summary>
        public const string Version = "1.10";

        /// <summary>
        /// Program entrypoint.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            try
            {
                if (args.Length != 6 && args.Length != 7)
                {
                    Console.WriteLine(
$@"
.NET Core Publishing Utility: PUBCORE v{Version}

usage: pubcore [--no-cmd] PROJECT-PATH TARGET-NAME CONFIG OUTPUT-PATH PUBLISH-DIR RUNTIME

    PROJECT-PATH    - Path to the [.csproj] file
    TARGET-NAME     - Build target name
    CONFIG          - Build configuration (like: Debug or Release)
    OUTDIR-PATH     - Project relative path to the output directory
    PUBLISH-DIR     - Path to the publication folder
    RUNTIME         - Target dotnet runtime, like: win10-x64

REMARKS:

This utility is designed to be called from within a .NET Core project's
POST-BUILD event using Visual Studio post-build event macros.  Here's
an example that publishes a standalone [win10-x64] app to OUTPUT-PATH.

    pubcore ""$(ProjectPath)"" ""$(TargetName)"" ""$(ConfigurationName)"" ""$(OutDir).TrimEnd('\')"" ""PUBLISH-DIR"" win10-x64

Note that you MUST ADD the following to the <PropertyGroup>...</PropertyGroup>
section on your project CSPROJ file for this to work:

    <RuntimeIdentifier>win10-x64</RuntimeIdentifier>

or:

    <RuntimeIdentifiers>win10-x64;...</RuntimeIdentifiers>

This command publishes the executable files to a new PUBLISH-DIR/TARGET-NAME directory
then creates a CMD.EXE batch file named PUBLISH-DIR/TARGET-NAME.cmd that launches the
application, forwarding any command line arguments.

The [--no-cmd] option prevents the CMD.EXE batch file from being created.
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

                Console.WriteLine();
                Console.WriteLine($"===========================================================");
                Console.WriteLine($".NET Core Publishing Utility: PUBCORE v{Version}");
                Console.WriteLine($"===========================================================");
                Console.WriteLine();

                var sbCommandLine = new StringBuilder("pubcore");

                foreach (var arg in args)
                {
                    sbCommandLine.Append(' ');

                    if (arg.Contains(' '))
                    {
                        sbCommandLine.Append($"\"{arg}\"");
                    }
                    else
                    {
                        sbCommandLine.Append(arg);
                    }
                }

                Console.WriteLine(sbCommandLine.ToString());
                Console.WriteLine();

                // Look for the [--no-cmd] option and then remove it from the
                // arguments when present.

                var noCmd = args.Any(arg => arg == "--no-cmd");

                if (noCmd)
                {
                    args = args.Where(arg => arg != "--no-cmd").ToArray();
                }

                // Parse the arguments.

                var projectPath = args[0];

                if (!File.Exists(projectPath))
                {
                    Console.WriteLine($"Project file [{projectPath}] does not exist.");
                    Environment.Exit(1);
                }

                var targetName = args[1];
                var config     = args[2];
                var outputDir  = Path.Combine(Path.GetDirectoryName(projectPath), args[3]);
                var publishDir = args[4];
                var runtime    = args.ElementAtOrDefault(5);
                var binFolder  = Path.Combine(publishDir, targetName);

                // Ensure that the runtime identifier is present in the project file.

                // $hack(jefflill): 
                //
                // I'm hacking this test right now using string matching.  Ultimately,
                // this should be accomplished by actually parsing the project XML.

                var projectText = File.ReadAllText(projectPath);

                if (!projectText.Contains(runtime))
                {
                    Console.Error.WriteLine();
                    Console.Error.WriteLine("ERROR: Make sure the runtime identifier below is present in your");
                    Console.Error.WriteLine("       project's <PropertyGroup/> section:");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"    <PropertyGroup>");
                    Console.Error.WriteLine($"        <RuntimeIdentifier>{runtime}</RuntimeIdentifier>");
                    Console.Error.WriteLine($"    </PropertyGroup>");
                    Console.Error.WriteLine();
                    Console.Error.WriteLine($"PROJECT: {Path.GetFullPath(projectPath)}");
                    Console.Error.WriteLine();

                    Environment.Exit(1);
                }

                // Ensure that the output folder exists.

                Directory.CreateDirectory(publishDir);

                // Time how long publication takes.

                var stopwatch = new Stopwatch();

                stopwatch.Start();

                // It appears that [dotnet publish] is sometimes unable to write
                // output files due to locks being held by somebody else (I'm guessing
                // some part of the Visual Studio build process).  This appears to
                // be transient.  We're going to mitigate this by introducing an
                // initial 5 second delay to hopefully avoid the situation entirely
                // and then try the operation up to five times.

                var tryCount = 5;
                var delay    = TimeSpan.FromSeconds(5);

                // Publish the project.

                Thread.Sleep(delay);

                for (int i = 0; i < tryCount; i++)
                {
                    var process  = new Process();
                    var sbOutput = new StringBuilder();

                    process.StartInfo.FileName               = "dotnet.exe";
                    process.StartInfo.Arguments              = $"publish \"{projectPath}\" -c \"{config}\" -r {runtime} --self-contained --no-restore --no-dependencies";
                    process.StartInfo.CreateNoWindow         = true;
                    process.StartInfo.UseShellExecute        = false;
                    process.StartInfo.RedirectStandardError  = true;
                    process.StartInfo.RedirectStandardOutput = true;

                    process.OutputDataReceived += (s, e) => sbOutput.AppendLine(e.Data);
                    process.ErrorDataReceived  += (s, e) => sbOutput.AppendLine(e.Data);

                    if (i > 0)
                    {
                        Console.WriteLine($"===========================================================");
                        Console.WriteLine($"PUBCORE RETRY: dotnet {process.StartInfo.Arguments}");
                        Console.WriteLine($"===========================================================");
                    }

                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    process.WaitForExit();

                    // Write the output capture from the [dotnet publish ...] operation to the program
                    // STDOUT, prefixing each line with some text so that MSBUILD/Visual Studio won't
                    // try to interpret error/warning messages.

                    using (var reader = new StringReader(sbOutput.ToString()))
                    {
                        for (var line = reader.ReadLine(); line != null; line = reader.ReadLine())
                        {
                            Console.WriteLine($"   publish: {line}");
                        }
                    }

                    // Report any errors and handle retries.

                    if (process.ExitCode != 0)
                    {
                        if (i < tryCount - 1)
                        {
                            Console.Error.WriteLine($"warning: [dotnet publish] failed with [exitcode={process.ExitCode}].  Retrying after [{delay}].");
                            Thread.Sleep(delay);
                        }
                        else
                        {
                            Console.Error.WriteLine($"error: [dotnet publish] failed with [exitcode={process.ExitCode}].");
                            Environment.Exit(process.ExitCode);
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                // Copy build binaries to the output folder.  This also seems to
                // experience transient errors, so we'll retry here with delays.

                for (int i = 1; i <= tryCount; i++)
                {
                    try
                    {
                        if (i > 1)
                        {
                            Console.WriteLine($"===========================================================");
                            Console.WriteLine($"PUBCORE RETRY: Copy to output folder");
                            Console.WriteLine($"===========================================================");
                        }

                        // Create the CMD.EXE script when not disabled.

                        var cmdPath = Path.Combine(publishDir, $"{targetName}.cmd");

                        if (!noCmd)
                        {
                            File.WriteAllText(cmdPath,
$@"@echo off
""%~dp0\{targetName}\{targetName}.exe"" %*
");
                        }
                        else
                        {
                            // Delete any existing CMD.EXE script.

                            if (File.Exists(cmdPath))
                            {
                                File.Delete(cmdPath);
                            }
                        }

                        // Remove the output folder and then recreate it to ensure
                        // that all old files will be removed.

                        if (Directory.Exists(binFolder))
                        {
                            Directory.Delete(binFolder, recursive: true);
                        }

                        Directory.CreateDirectory(binFolder);

                        CopyRecursive(GetPublishDir(outputDir, runtime), binFolder);
                        break;
                    }
                    catch (Exception e)
                    {
                        if (i < tryCount)
                        {
                            Thread.Sleep(delay);
                            continue;
                        }

                        Console.WriteLine($"ERROR(retry): {e.GetType().FullName}: {e.Message}");
                        Environment.Exit(1);
                    }
                }

                // For some bizarre reason, [dotnet publish] copies [dotnet.exe] to the publish
                // folder and this is causing trouble running [dotnet] commands for other apps.
                // I'm also seeing other random DLLs being published as well for single-file
                // executables, which is really strange.
                //
                // This might be a new Visual Studio 2022 (bad?) behavior.  I'm going to mitigate
                // by removing the [dotnet.exe] file from the publish folder if present.

                var dotnetPath = Path.Combine(binFolder, "dotnet.exe");

                if (File.Exists(dotnetPath))
                {
                    File.Delete(dotnetPath);
                }

                // Finish up

                Console.WriteLine($"Publish time [{targetName}]: {stopwatch.Elapsed}");
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"ERROR: [{e.GetType().Name}]: {e.Message}");
                Environment.Exit(1);
            }
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

        /// <summary>
        /// Returns the directory path where <b>dotnet publish</b> actually published
        /// the tool binaries.
        /// </summary>
        /// <param name="outputDir">The project's output directory path.</param>
        /// <param name="runtime">The runtime identifier.</param>
        /// <returns>The projects publish directory path.</returns>
        private static string GetPublishDir(string outputDir, string runtime)
        {
            // Projects specifying a single runtime identifier like:
            //
            //      <RuntimeIdentifier>win10-x64</RuntimeIdentifier>
            //
            // will publish their output to:
            //
            //      PROJECT-DIR\bin\CONFIGURATION\net6.0\publish
            //
            // Projects that use <RuntimeIdentifiers/> (plural) with one
            // or more runtime identifiers like:
            //
            //      <RuntimeIdentifiers>win10-x64</RuntimeIdentifiers>
            //
            // will publish output to:
            //
            //      PROJECT-DIR\bin\CONFIGURATION\net6.0\win10-x64\publish
            //
            // We're going to probe for the existence of the first folder
            // and assume the second if the first doesn't exist.

            var probeDir1 = Path.Combine(outputDir, "publish");

            if (Directory.Exists(probeDir1))
            {
                return probeDir1;
            }
            else
            {
                var probeDir2 = Path.Combine(outputDir, "..", runtime, "publish");

                if (!Directory.Exists(probeDir2))
                {
                    Console.Error.WriteLine($"*** ERROR: Cannot locate publication directory:");
                    Console.Error.WriteLine($"***        ...at: {probeDir1}");
                    Console.Error.WriteLine($"***        ...or: {probeDir2}");
                    Environment.Exit(1);
                }

                return probeDir2;
            }
        }
    }
}
