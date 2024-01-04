//-----------------------------------------------------------------------------
// FILE:        ClusterPropertyCommand.cs
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
    /// Implements the <b>cluster property</b> command.
    /// </summary>
    [Command]
    public class ClusterPropertyCommand : CommandBase
    {
        private const string usage = @"
Prints a cluster property to standard output.

USAGE:

    neon cluster property [--line] NAME     - Retrieves a cluster property

ARGUMENTS:

    NAME        - Identifies the desired property, one of:

        domain          - Cluster DNS domain
        id              - Cluster ID
        sso-username    - Cluster root SSO username
        sso-password    - Cluster root SSO password

OPTIONS:

    --line      - Print the property value to STDOUT with a line ending

";

        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "property" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--line" };

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

            KubeHelper.KubeConfig.GetCurrent(out var _, out var cluster, out var _);

            var propertyName = commandLine.Arguments.ElementAtOrDefault(0);
            var lineEnding   = commandLine.HasOption("--line");

            if (string.IsNullOrEmpty(propertyName))
            {
                Console.Error.WriteLine("*** ERROR: NAME argument is required.");
                Program.Exit(1);
            }

            var value = string.Empty;

            switch (propertyName)
            {
                case "domain":

                    value = cluster.ClusterInfo.ClusterDomain;
                    break;

                case "id":

                    value = cluster.ClusterInfo.ClusterId;
                    break;

                case "sso-username":

                    value = "root";
                    break;

                case "sso-password":

                    value = cluster.ClusterInfo.SsoPassword;
                    break;

                default:

                    Console.Error.WriteLine($"*** ERROR: [{propertyName}] is not a valid property name.");
                    Program.Exit(1);
                    break;
            }

            if (lineEnding)
            {
                Console.WriteLine(value);
            }
            else
            {
                Console.Write(value);
            }

            await Task.CompletedTask;
            Program.Exit(0);
        }
    }
}
