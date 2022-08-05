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
using System.Runtime.InteropServices;
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
        public const string Version = "2.1";

        /// <summary>
        /// Program entrypoint.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            try
            {
                Console.WriteLine();
                Console.WriteLine($".NET Core Publishing Utility: PUBCORE v{Version}");

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

                Console.WriteLine();
                Console.WriteLine(sbCommandLine.ToString());
                Console.WriteLine();

                // Parse the command line options and then remove any options
                // from the arguments.

                var noCmd = args.Any(arg => arg == "--no-cmd");

                args = args.Where(arg => !arg.StartsWith("--")).ToArray();

                // Verify the number of non-option arguments.

                if (args.Length != 5)
                {
                    Console.WriteLine(
$@"
NEON PUBCORE v{Version}

usage: pubcore [OPTIONS] PROJECT-PATH TARGET-NAME CONFIG OUTPUT-PATH RUNTIME

ARGUMENTS:

    PROJECT-PATH    - Path to the [.csproj] file
    TARGET-NAME     - Build target name
    CONFIG          - Build configuration (like: Debug or Release)
    OUTDIR-PATH     - Path to the output directory
    RUNTIME         - Target dotnet runtime, like: win10-x64,

OPTIONS:

    --no-cmd        - Do not generate a [PROJECT-NAME.cmd] file in PUBLISH-DIR
                      that executes the published program.

REMARKS:

This utility is designed to be called from within a .NET Core project's
POST-BUILD event using Visual Studio post-build event macros.  Here's
an example that publishes a standalone [win10-x64] app to: %NK_BUILD%\neon

    pubcore ""$(ProjectPath)"" ""$(TargetName)"" ""$(ConfigurationName)"" ""%NK_BUILD%\neon"" win10-x64

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
                var runtime    = args.ElementAtOrDefault(4);

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

                Directory.CreateDirectory(outputDir);

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

                for (int i = 0; i < tryCount; i++)
                {
                    var process  = new Process();
                    var sbOutput = new StringBuilder();

                    process.StartInfo.FileName               = "dotnet.exe";
                    process.StartInfo.Arguments              = $"publish \"{projectPath}\" -c \"{config}\" -r {runtime} -o \"{outputDir}\" --self-contained";
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

                // Create the CMD shell script when not disabled.

                var cmdPath = Path.Combine(Path.Combine(outputDir, ".."), $"{targetName}.cmd");

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

                // For some bizarre reason, [dotnet publish] copies [dotnet.exe] to the publish
                // folder and this is causing trouble running [dotnet] commands for other apps.
                // I'm also seeing other random DLLs being published as well for single-file
                // executables, which is really strange.
                //
                // This might be a new Visual Studio 2022 (bad?) behavior.  I'm going to mitigate
                // by removing the [dotnet.exe] file from the publish folder if present.

                var dotnetPath = Path.Combine(outputDir, "dotnet.exe");

                if (File.Exists(dotnetPath))
                {
                    File.Delete(dotnetPath);
                }

                // Finish up

                Console.WriteLine($"Publish time:   {stopwatch.Elapsed}");
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"** ERROR: [{e.GetType().Name}]: {e.Message}");
                Environment.Exit(1);
            }
        }
    }
}
