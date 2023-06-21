//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Kube.Operator.Builder;
using Neon.Kube.Operator.Rbac;
using Neon.Kube.Resources.CertManager;
using Neon.Tasks;

using k8s;
using k8s.Models;

using OperatorCli.Commands.Generate;
using Prometheus;

namespace OperatorCli
{
    /// <summary>
    /// Hosts the program entrypoint.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">Specifiees the command line argument.</param>
        public static void Main(string[] args)
        {
            var rootCommand         = new RootCommand();
            var generateCommand     = new GenerateCommand();
            var generateRbacCommand = new GenerateRbacCommand();
            var generateCrdsCommand = new GenerateCrdsCommand();

            rootCommand.AddCommand(generateCommand);
            generateCommand.AddCommand(generateRbacCommand);
            generateCommand.AddCommand(generateCrdsCommand);

            var commandLineBuilder = new CommandLineBuilder(rootCommand);
            var parser             = commandLineBuilder.UseDefaults().Build();

            // Invoke the command line parser which then invokes the respective command handlers.

            try
            {
                parser.Invoke(args);
            }
            catch (Exception e)
            {
                Console.Error.WriteLine($"*** ERROR: {NeonHelper.ExceptionError(e)}");
                Environment.Exit(1);
            }
        }
    }
}
