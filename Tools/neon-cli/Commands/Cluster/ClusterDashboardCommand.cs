//-----------------------------------------------------------------------------
// FILE:	    ClusterDashboardCommand.cs
// CONTRIBUTOR: Jeff Lill
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
using Neon.Kube;
using Neon.Kube.Proxy;
using Neon.Kube.Hosting;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster dashboard</b> command.
    /// </summary>
    [Command]
    public class ClusterDashboardCommand : CommandBase
    {
        private const string usage = @"
Lists the dashboards available for the current NEONKUBE cluster or displays
a named dashboard in a browser window.

USAGE:

    neon cluster dashboard              - Lists available dashboard names
    neon cluster dashboard NAME [--url] - Launches a browser for the dashboard

ARGUMENTS:

    NAME        - identifies the desired dashboard

OPTIONS:

    --url       - Returns the dashboard URL in the output rather than
                  lanching a browser
";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "dashboard" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--url" };

        /// <inheritdoc/>
        public override bool NeedsHostingManager => true;

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            var currentContext = KubeHelper.CurrentContext;

            if (currentContext == null)
            {
                Console.Error.WriteLine("*** ERROR: No NEONKUBE cluster is selected.");
                Program.Exit(1);
            }

            var dashboardName = commandLine.Arguments.ElementAtOrDefault(0);
            var url           = commandLine.HasOption("--url");

            using (var cluster = ClusterProxy.Create(KubeHelper.KubeConfig, new HostingManagerFactory()))
            {
                var dashboards = await cluster.ListClusterDashboardsAsync();

                if (string.IsNullOrEmpty(dashboardName))
                {
                    Console.WriteLine();

                    if (dashboards.Count > 0)
                    {
                        Console.WriteLine("Available Dashboards:");
                        Console.WriteLine("---------------------");
                    }
                    else
                    {
                        Console.WriteLine("*** No dashboards are available.");
                        Console.WriteLine();
                        return;
                    }

                    foreach (var item in dashboards
                        .OrderBy(item => item.Key, StringComparer.CurrentCultureIgnoreCase))
                    {
                        Console.WriteLine(item.Key);
                    }

                    Console.WriteLine();
                }
                else
                {
                    if (dashboards.TryGetValue(dashboardName, out var dashboard))
                    {
                        if (url)
                        {
                            Console.WriteLine(dashboard.Spec.Url);
                        }
                        else
                        {
                            NeonHelper.OpenPlatformBrowser(dashboard.Spec.Url, newWindow: true);
                        }
                    }
                    else
                    {
                        Console.Error.WriteLine($"*** ERROR: Dashboard [{dashboardName}] does not exist.");
                        Program.Exit(1);
                    }
                }
            }
        }
    }
}
