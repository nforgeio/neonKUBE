//-----------------------------------------------------------------------------
// FILE:	    RunCommandz.cs
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>run</b> command.
    /// </summary>
    [Command]
    public class RunCommand : CommandBase
    {
        private const string usage = @"
Runs a sub-command, optionally injecting settings and secrets and/or
decrypting input files.

USAGE:

    neon tool run [OPTIONS] [VARIABLES...] -- COMMAND ARG _.VAR
    neon tool run [OPTIONS] [VARIABLES...] -- COMMAND ARG _..VAR-FILE
    neon tool run [OPTIONS] [VARIABLES...] -- COMMAND ARG _...PATH

ARGUMENTS:

    VARIABLES       - You may pass zero or more paths to text files defining
                      environment variables like: NAME=VALUE

    --              - Indicates that start of the sub-command
                  
    COMMAND         - The command to be executed

    ARG             - Zero or more command line arguments that will be passed
                      to the command unmodified

    _.VAR           - The ""."" prefix indicates that the environment variable
                      named VAR should replace this argument before 
                      executing the command

    _..VAR-FILE     - The "".."" prefix indicates that the VAR-FILE text file
                      should be written (decrypting if necessary) to a secure 
                      temporary file and that any environment references
                      like $<env:VAR> will be replaced by the variable value.
                      The command line will be updated to reference the
                      temporary file.

    _...PATH        - The ""..."" prefix indicates that the file at the PATH
                      should be temporarily decrypted (if necessary) and
                      the command line will be updated to reference the
                      temporary file.

OPTIONS:

    --name=value    - Can be used to explicitly set an environment variable.
                      You can specify more than one of these.

REMARKS:

You can use this command in concert with the [neon password] and 
[neon vault] commands to securely inject secrets into your CI/CD,
and other operational scripts.

The basic idea is to use the [neon password ...] commands to create
or import named passwords on your workstation and then use the
[neon vault ...] commands to encrypt VARIABLES and other sensitive
files so that you can use this [neon run ...] command to inject
secrets and settings into a sub-command.

Note that it will be quite safe to commit any encrypted files to
a source repository (even a public one) because the actual passwords
used to encrypt the files will not be included in the repository.
Be sure to use secure passwords.

Examples
--------

Inject the PATH environment variable value into a sub-command:

    neon run -- echo .PATH

Read a VARIABLES file and inject a variable into a sub-command:

    [variables.txt file]:
    # Lines beginning with ""#"" are ignored as comments
    MYVAR=hello

    neon run variables.txt -- echo _.MYVAR

Inject an environment variable into a text file:

    [file.txt]:
    $<env:MYVAR>

    neon neon run --MYVAR=hello -- cat _..file.txt

Pass a potentially encrypted file:

    neon neon run -- cat _...encrypted.txt
";

        NeonVault vault = new NeonVault(Program.LookupPassword);

        /// <inheritdoc/>
        public override string[] Words => new string[] { "tool", "run" }; 

        /// <inheritdoc/>
        public override bool CheckOptions => false;

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            var splitCommandLine = commandLine.Split("--");
            var leftCommandLine  = splitCommandLine.Left;
            var rightCommandLine = splitCommandLine.Right;

            if (rightCommandLine == null || rightCommandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine("*** ERROR: Expected a command after a [--] argument.");
                Program.Exit(1);
            }

            // All arguments on the left command line should be VARIABLES files.
            // We're going to open each of these and set any enviroment variables
            // like [NAME=VALUE] we find.
            //
            // Note that these files may be encrypted.  If any are, we'll decrypt
            // to a temporary file before we read them.

            foreach (var path in leftCommandLine.Arguments)
            {
                if (!File.Exists(path))
                {
                    Console.Error.WriteLine($"*** ERROR: File [{path}] does not exist.");
                    Program.Exit(1);
                }

                DecryptWithAction(path,
                    decryptedPath =>
                    {
                        var lineNumber = 1;

                        foreach (var line in File.ReadAllLines(decryptedPath))
                        {
                            var trimmed = line.Trim();

                            if (line == string.Empty || line.StartsWith("#"))
                            {
                                continue;
                            }

                            var fields = line.Split( '=', 2);

                            if (fields.Length != 2 || fields[0] == string.Empty)
                            {
                                Console.Error.WriteLine($"*** ERROR: [{path}:{lineNumber}] is not formatted like: NAME=VALUE");
                                Program.Exit(1);
                            }

                            var name  = fields[0].Trim();
                            var value = fields[1].Trim();

                            Environment.SetEnvironmentVariable(name, value);

                            lineNumber++;
                        }
                    });
            }

            // Any left command line options with a "--" prefix also specify environment variables.

            foreach (var option in leftCommandLine.Options.Where(o => o.Key.StartsWith("--")))
            {
                Environment.SetEnvironmentVariable(option.Key.Substring(2), option.Value);
            }

            // We've read all of the variable files and left command line options
            // and initialized all environment variables.  Now we need to process
            // and then execute the right command line.

            var tempFiles = new List<TempFile>();

            try
            {
                var subcommand = rightCommandLine.Items;

                // Note that the first element of the subcommand specifies the
                // executable so we don't need to process that.

                for (int i = 1; i < subcommand.Length; i++)
                {
                    var arg = subcommand[i];

                    if (arg.StartsWith("_..."))
                    {
                        // Argument is a reference to a potentially encrypted 
                        // file that needs to be passed decrypted.

                        var path = arg.Substring(4);

                        if (!File.Exists(path))
                        {
                            Console.Error.WriteLine($"*** ERROR: File [{path}] does not exist.");
                            Program.Exit(1);
                        }

                        if (NeonVault.IsEncrypted(path))
                        {
                            var tempFile = new TempFile();

                            tempFiles.Add(tempFile);
                            vault.Decrypt(path, tempFile.Path);

                            path = tempFile.Path;
                        }

                        subcommand[i] = path;
                    }
                    else if (arg.StartsWith("_.."))
                    {
                        // Argument is a reference to a potentially encrypted text file
                        // with environment variable references we'll need to update.

                        var path = arg.Substring(3);

                        if (!File.Exists(path))
                        {
                            Console.Error.WriteLine($"*** ERROR: File [{path}] does not exist.");
                            Program.Exit(1);
                        }

                        if (NeonVault.IsEncrypted(path))
                        {
                            var tempFile = new TempFile();

                            tempFiles.Add(tempFile);
                            vault.Decrypt(path, tempFile.Path);

                            path = tempFile.Path;
                        }

                        subcommand[i] = path;

                        // Perform the subsitutions.

                        var unprocessed      = File.ReadAllText(path);
                        var processed        = string.Empty;
                        var linuxLineEndings = !unprocessed.Contains("\r\n");

                        using (var reader = new StreamReader(path))
                        {
                            using (var preprocessor = new PreprocessReader(reader))
                            {
                                preprocessor.ExpandVariables        = true;
                                preprocessor.LineEnding             = linuxLineEndings ? LineEnding.LF : LineEnding.CRLF;
                                preprocessor.ProcessStatements      = false;
                                preprocessor.StripComments          = false;
                                preprocessor.VariableExpansionRegex = PreprocessReader.AngleVariableExpansionRegex;

                                processed = preprocessor.ReadToEnd();
                            }
                        }

                        File.WriteAllText(path, processed);
                    }
                    else if (arg.StartsWith("_."))
                    {
                        // Argument is a reference to an environment variable.

                        var name = arg.Substring(2);

                        if (name == string.Empty)
                        {
                            Console.Error.WriteLine($"*** ERROR: Subcommand argument [{arg}] is not valid.");
                            Program.Exit(1);
                        }

                        var value = Environment.GetEnvironmentVariable(name);

                        if (value == null)
                        {
                            Console.Error.WriteLine($"*** ERROR: Subcommand argument [{arg}] references an undefined environment variable.");
                            Program.Exit(2);
                        }

                        subcommand[i] = value;
                    }
                    else if (arg.StartsWith("-"))
                    {
                        // Argument is a command line option.  We'll check to see if
                        // it contains a reference to an environment variable.

                        var valuePos = arg.IndexOf("=_.");

                        if (valuePos != -1)
                        {
                            var optionPart = arg.Substring(0, valuePos);
                            var name       = arg.Substring(valuePos + 3);

                            if (name == string.Empty)
                            {
                                Console.Error.WriteLine($"*** ERROR: Subcommand argument [{arg}] is not valid.");
                                Program.Exit(1);
                            }

                            var value = Environment.GetEnvironmentVariable(name);

                            if (value == null)
                            {
                                Console.Error.WriteLine($"*** ERROR: Subcommand argument [{arg}] references an undefined environment variable.");
                                Program.Exit(1);
                            }

                            subcommand[i] = $"{optionPart}={value}";
                        }
                    }
                    else
                    {
                        // Otherwise, expand any envrionment variable references.

                        subcommand[i] = Environment.ExpandEnvironmentVariables(subcommand[i]);
                    }
                }

                // Execute the subcommand.

                var subcommandArgs = new List<object>();

                foreach (var subcommandArg in subcommand)
                {
                    subcommandArgs.Add(subcommandArg);
                }

                var exitCode = NeonHelper.Execute(subcommand[0], subcommandArgs.Skip(1).ToArray());

                Program.Exit(exitCode);
            }
            finally
            {
                foreach (var tempFile in tempFiles)
                {
                    tempFile.Dispose();
                }
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }

        /// <summary>
        /// Examines the file at <paramref name="path"/>, decrypting it to a temporary file
        /// if necessary.  The method will then call <paramref name="action"/> passing the
        /// path to the original file (if it wasn't encrypted or to the decrypted file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="action">Called with the path to a decrypted file.</param>
        private void DecryptWithAction(string path, Action<string> action)
        {
            if (!NeonVault.IsEncrypted(path))
            {
                action(path);
            }
            else
            {
                using (var tempFile = new TempFile())
                {
                    vault.Decrypt(path, tempFile.Path);
                    action(tempFile.Path);
                }
            }
        }
    }
}
