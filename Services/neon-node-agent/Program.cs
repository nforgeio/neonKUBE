//-----------------------------------------------------------------------------
// FILE:        Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.Service;

using Prometheus.DotNetRuntime;

namespace NeonNodeAgent
{
    /// <summary>
    /// The <b>neon-node-agent</b> entry point.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Returns the program's service implementation.
        /// </summary>
        public static Service Service { get; private set; }

        /// <summary>
        /// The program entry point.
        /// </summary>
        /// <param name="args">The command line arguments.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public static async Task Main(string[] args)
        {
            try
            {
                Service = new Service(KubeService.NeonNodeAgent);

                Service.MetricsOptions.Mode         = MetricsMode.Scrape;
                Service.MetricsOptions.Port         = NeonHelper.IsDevWorkstation ? NetHelper.GetUnusedTcpPort(IPAddress.Loopback) : 9762;
                Service.MetricsOptions.GetCollector =
                    () =>
                    {
                        return DotNetRuntimeStatsBuilder
                            .Default()
                            .StartCollecting();
                    };

                Environment.Exit(await Service.RunAsync());
            }
            catch (Exception e)
            {
                if (Service?.Logger != null)
                {
                    Service.Logger.LogCriticalEx(e);
                }
                else
                {
                    // Logging isn't initialized, so fallback to just writing to SDTERR.

                    Console.Error.WriteLine("CRITICAL: " + NeonHelper.ExceptionError(e, stackTrace: true));

                    if (e.StackTrace != null)
                    {
                        Console.Error.WriteLine("STACK TRACE:");
                        Console.Error.WriteLine(e.StackTrace);
                    }
                }

                Environment.Exit(1);
            }
        }
    }
}
