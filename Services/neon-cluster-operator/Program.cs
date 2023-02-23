//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Operator;
using Neon.Net;
using Neon.Service;

using k8s;
using k8s.Models;
using Neon.Diagnostics;
using Prometheus.DotNetRuntime;

namespace NeonClusterOperator
{
    /// <summary>
    /// The <b>neon-cluster-operator</b> entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Returns the program's service implementation.
        /// </summary>
        public static Service Service { get; private set; }

        /// <summary>
        /// Returns the program resources as a static file system.
        /// </summary>
        public static IStaticDirectory Resources { get; private set; }

        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            try
            {
                // Initialize the static resource file system.

                Resources = Assembly.GetExecutingAssembly().GetResourceFileSystem("NeonClusterOperator.Resources");

                //-------------------------------------------------------------
                // Start the operator service.

                Service = new Service(KubeService.NeonClusterOperator);

                Service.MetricsOptions.Mode         = MetricsMode.Scrape;
                Service.MetricsOptions.Path         = "metrics/";
                Service.MetricsOptions.Port         = NeonHelper.IsDevWorkstation ? NetHelper.GetUnusedTcpPort(IPAddress.Loopback) : 9762;
                Service.MetricsOptions.GetCollector =
                    () =>
                    {
                        return DotNetRuntimeStatsBuilder
                            .Default()
                            .StartCollecting();
                    };

                if (!string.IsNullOrEmpty(args.FirstOrDefault()))
                {
                    await KubernetesOperatorHost
                       .CreateDefaultBuilder(args)
                       .ConfigureOperator(configure =>
                       {
                           configure.AssemblyScanningEnabled = true;
                           configure.Name = Service.Name;
                           configure.DeployedNamespace = KubeNamespace.NeonSystem;
                       })
                       .ConfigureNeonKube()
                       .AddSingleton<Service>(Service)
                       .UseStartup<OperatorStartup>()
                       .Build().RunAsync();

                    Environment.Exit(0);
                }

                Environment.Exit(await Service.RunAsync());
            }
            catch (Exception e)
            {
                // We really shouldn't see exceptions here but let's log something
                // just in case.  Note that logging may not be initialized yet so
                // we'll just output a string.

                Console.Error.WriteLine(NeonHelper.ExceptionError(e));
                Environment.Exit(-1);
            }
        }
    }
}
