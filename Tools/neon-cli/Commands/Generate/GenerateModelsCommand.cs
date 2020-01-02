//-----------------------------------------------------------------------------
// FILE:	    GenerateModelsCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;

using Neon.ModelGen;
using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>generate models</b> command.
    /// </summary>
    public class GenerateModelsCommand : CommandBase
    {
        private const string usage = @"
Generates C# source code for data and service models defined as interfaces
within a compiled assembly.

USAGE:

    neon generate models [OPTIONS] ASSEMBLY-PATH [OUTPUT-PATH]

ARGUMENTS:

    ASSEMBLY-PATH       - Path to the assembly being scanned.

    OUTPUT-PATH         - Optional path to the output file, otherwise
                          the generated code will be written to STDOUT.

OPTIONS:

    --source-namespace=VALUE    - Specifies the namespace to be used when
                                  scanning for models.  By default, all
                                  classes within the assembly wll be scanned.

    --target-namespace=VALUE    - Specifies the namespace to be used when
                                  generating the models.  This overrides 
                                  the original type namespaces as scanned
                                  from the source assembly.

    --persisted                 - Generate database persistence related code.

    --ux=xaml                   - Generate additional code for the specified
                                  UX framework.  Currently, only [xaml] is
                                  supported

    --no-services               - Don't generate any service clients.

    --targets=LIST              - Specifies the comma separated list of target 
                                  names.  Any input models that are not tagged
                                  with these target will not be generated.

    --debug-allow-stepinto      - Indicates that generated class methods will
                                  not include the [DebuggerStepThrough]
                                  attribute allowing the debugger to step
                                  into the generated methods.

REMARKS:

This command is used to generate enhanced JSON based data models and
REST API clients suitable for applications based on flexible noSQL
style design conventions.  See this GitHub issue for more information:

    https://github.com/nforgeio/neonKUBE/issues/463

";

        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "generate", "models" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--source-namespace", "--target-namespace", "--persisted", "--ux", "--no-services", "--targets", "--debug-allow-stepinto" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.Arguments.Length == 0)
            {
                Help();
                Program.Exit(1);
            }

            var assemblyPath = commandLine.Arguments.ElementAtOrDefault(0);
            var outputPath   = commandLine.Arguments.ElementAtOrDefault(1);
            var targets      = new List<string>();

            var targetOption = commandLine.GetOption("--targets");

            if (!string.IsNullOrEmpty(targetOption))
            {
                foreach (var target in targetOption.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    targets.Add(target);
                }
            }

            var settings = new ModelGeneratorSettings(targets.ToArray())
            {
                SourceNamespace       = commandLine.GetOption("--source-namespace"),
                TargetNamespace       = commandLine.GetOption("--target-namespace"),
                Persisted             = commandLine.HasOption("--persisted"),
                NoServiceClients      = commandLine.HasOption("--no-services"),
                AllowDebuggerStepInto = commandLine.HasOption("--debug-allow-stepinto")
            };

            var ux = commandLine.GetOption("--ux");

            if (ux != null)
            {
                if (ux.Equals("xaml", StringComparison.InvariantCultureIgnoreCase))
                {
                    settings.UxFramework = UxFrameworks.Xaml;
                }
                else
                {
                    Console.Error.WriteLine($"*** ERROR: [--ux={ux}] does not specify one of the supported UX frameworks: XAML");
                    Program.Exit(1);
                }
            }

            var assembly       = Assembly.LoadFile(Path.GetFullPath(assemblyPath));
            var modelGenerator = new ModelGenerator(settings);
            var output         = modelGenerator.Generate(assembly);

            if (output.HasErrors)
            {
                foreach (var error in output.Errors)
                {
                    Console.Error.WriteLine(error);
                }

                Program.Exit(1);
            }

            if (!string.IsNullOrEmpty(outputPath))
            {
                // Ensure that all of the parent folders exist.

                var folderPath = Path.GetDirectoryName(outputPath);

                Directory.CreateDirectory(folderPath);

                // Don't write the output file if its contents are already
                // the same as the generated output.  This will help reduce
                // wear on SSDs and also make things a tiny bit easier for
                // source control.

                if (!File.Exists(outputPath) || File.ReadAllText(outputPath) != output.SourceCode)
                {
                    File.WriteAllText(outputPath, output.SourceCode);
                }
            }
            else
            {
                Console.Write(output.SourceCode);
            }

            Program.Exit(0);
        }
    }
}
