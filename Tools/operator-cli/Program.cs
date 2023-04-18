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

using Prometheus;
using OperatorCli.Commands.Generate;

namespace OperatorCli
{
    /// <summary>
    /// Program entry
    /// </summary>
    public static class Program
    {

        /// <summary>
        /// Main.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static async Task Main(string[] args)
        {
            // Build the commands from what's registered in the DI container.
            await SyncContext.Clear;
            var rootCommand = new RootCommand();

            var generateCommand = new GenerateCommand();

            rootCommand.AddCommand(generateCommand);

            var generateRbacCommand = new GenerateRbacCommand();

            generateCommand.AddCommand(generateRbacCommand);

            var generateCrdsCommand = new GenerateCrdsCommand();

            generateCommand.AddCommand(generateCrdsCommand);

            var commandLineBuilder = new CommandLineBuilder(rootCommand);
            var parser             = commandLineBuilder.UseDefaults().Build();

            // Invoke the command line parser which then invokes the respective command handlers.

            parser.Invoke(args);
        }
    }
}