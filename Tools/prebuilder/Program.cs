//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using k8s;

using Neon.Common;

namespace Prebuilder
{
    /// <summary>
    /// Implements misc neonKUBE pre-build activities.
    /// </summary>
    public static class Program
    {
        private const string usage =
@"
-------------------------------------------------------------------------------
prebuilder generate-kubernetes-with-retry TARGET-FILE NAMESPACE

ARGUMENTS:

    TARGET-FILE     - path to the generated C# source file
    NAMESPACE       - namespace where the generated class will be defined

Generates a wrapper class for the [k8s.Kubernetes] client class that adds additional
retry logic to each method.  The new class will be named [k8s.KubernetesWrapper] and
will be written to TARGET-FILE.

This is currently executed by the [Neon.Kube] library before building.  Note that this
assumes that [Neon.Kube] and this tool are referencing the same version of the
[KubernetesClient] nuget package.
";

        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            var commandLine = new CommandLine(args);

            if (commandLine.HasHelpOption)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }
            else if (commandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine("*** ERROR: command required");
                Console.Error.WriteLine();
                Console.Error.WriteLine(usage);
                Program.Exit(1);
            }

            try
            {
                var command = commandLine.Arguments.First();

                commandLine = commandLine.Shift(1);

                switch (command)
                {
                    case "generate-kubernetes-with-retry":

                        GenerateKubernetesWithRetry.Run(commandLine);
                        return;
                }
            }
            catch (Exception e)
            {
                Console.Error.WriteLine(NeonHelper.ExceptionError(e));
                Program.Exit(1);
            }

            Program.Exit(0);
        }

        /// <summary>
        /// Exits the program.
        /// </summary>
        /// <param name="exitCode">The program exit code.</param>
        public static void Exit(int exitCode)
        {
            Environment.Exit(exitCode);
        }
    }
}
