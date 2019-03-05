//-----------------------------------------------------------------------------
// FILE:	    RunCommandz.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

namespace NShell
{
    /// <summary>
    /// Implements the <b>run</b> command.
    /// </summary>
    public class RunCommand : CommandBase
    {
        private const string usage = @"
Runs a sub-command, optionally injecting settings and secrets and/or
decrypting input files.

USAGE:

    nshell run [OPTIONS] [VARIABLES...] -- COMMAND ARG ^VAR ^^VAR-FILE @PATH

ARGUMENTS:

    VARIABLES       - You may pass zero or more paths to text files defining
                      environment variables like: NAME=VALUE

    --              - Indicates that start of the sub-command
                  
    COMMAND         - The command to be executed

    ARG             - Zero or more command line arguments that will be passed
                      to the command unmodified

    VAR             - The ^ prefix indicates that the environment variable
                      named VAR should replace this argument before 
                      executing the command

    VAR-FILE        - The ^^ prefix indicates that the VAR-FILE text file
                      should be written (decrypting if necessary) to a secure 
                      temporary file and that any environment references
                      like $<<VAR>> will be replaced by the variable value.
                      The command line will be updated to reference the
                      temporary file.

    PATH            - The @ prefix indicates that the file at the PATH
                      should be temporarily decrypted (if necessary) and
                      the command line will be updated to reference the
                      temporary file.

OPTIONS:

    --name=value    - Can be used to explicitly set an environment variable.
                      You can specify more than one of these.

REMARKS:

You can use this command in concert with the [nshell password] and 
[nshell file] commands to securely inject secrets into your CI/CD,
and other operational scripts.

The basic idea is to use the [nshell password ...] commands to create
or import named passwords on your workstation and then use the
[nshell file ...] commands to encrypt VARIABLES and other sensitive
files so that you can use this [nshell run ...] command to inject
secrets and settings into a sub-command.

Note that it will be quite safe to commit any encrypted files to
a source repository (even a public one) because the actual passwords
used to encrypt the files will not be included in the repository.
Be sure to use secure passwords.

Examples
--------

Inject the PATH environment variable value into a sub-command:

    nshell run -- echo ^PATH

Read a VARIABLES file and inject a variable into a sub-command:

    [variables.txt file]:
    # Lines beginning with ""#"" are ignored as comments
    MYVAR=hello

    nshell run variables.txt -- echo ^MYVAR

Use a command line option set an environment variable:

    nshell nshell run --MYVAR=hello -- echo ^MYHVAR

Inject an environment variable into a text file:

    [file.txt]:
    $<<MYVAR>>

    nshell nshell run --MYVAR=hello -- type ^^file.txt

Pass a potentially encrypted file:

    nshell nshell run -- type @encrypted.txt
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "run" }; }
        }

        /// <inheritdoc/>
        public override bool CheckOptions => false;

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Program.Exit(0);
        }
    }
}
