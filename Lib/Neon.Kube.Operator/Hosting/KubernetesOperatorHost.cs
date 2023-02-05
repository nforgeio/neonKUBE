//-----------------------------------------------------------------------------
// FILE:	    KubernetesOperatorHost.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

using Neon.Kube.Operator.Commands.Generate;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Kubernetes operator Host.
    /// </summary>
    public class KubernetesOperatorHost : IKubernetesOperatorHost
    {
        /// <inheritdoc/>
        public IHost Host { get; set; }
        
        /// <inheritdoc/>
        public IHostBuilder HostBuilder { get; set; }

        private string[] args { get; set; }

        /// <summary>
        /// Consructor.
        /// </summary>
        /// <param name="args"></param>
        public KubernetesOperatorHost(string[] args = null)
        {
            this.args = args;
        }


        /// <inheritdoc/>
        public static KubernetesOperatorHostBuilder CreateDefaultBuilder(string[] args = null)
        {
            var builder = new KubernetesOperatorHostBuilder(args);
            return builder;
        }

        /// <inheritdoc/>
        public Task RunAsync()
        {
            if (args == null) 
            { 
                return HostBuilder.Build().RunAsync();
            }

            
            HostBuilder.ConfigureServices(services =>
            {
                services.AddSingleton<GenerateCommand>();
                services.AddSingleton<GenerateCommandBase, GenerateRbacCommand>();
            });

            var host = HostBuilder.Build();

            // Build the commands from what's registered in the DI container
            var rootCommand = new RootCommand();
            foreach (Command command in host.Services.GetServices<GenerateCommand>())
            {
                rootCommand.AddCommand(command);
            }

            var generateCommand = host.Services.GetService<GenerateCommand>();
            foreach (Command command in host.Services.GetServices<GenerateCommandBase>())
            {
                generateCommand.AddCommand(command);
            }

            var commandLineBuilder = new CommandLineBuilder(rootCommand);
            Parser parser = commandLineBuilder.UseDefaults().Build();

            // Invoke the command line parser which then invokes the respective command handlers
            return parser.InvokeAsync(args);
        }
    }
}
