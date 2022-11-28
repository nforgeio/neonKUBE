//-----------------------------------------------------------------------------
// FILE:	    ClusterPurposeCommand.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Microsoft.Extensions.DependencyInjection;

using Neon.Common;
using Neon.Cryptography;
using Neon.Deployment;
using Neon.IO;
using Neon.Kube;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster purpose</b> command.
    /// </summary>
    [Command]
    public class ClusterPurposeCommand : CommandBase
    {
        private const string usage = @"
Prints or sets the current cluster's purpose.

USAGE:

    neon cluster purpose [unspecified | development | test | stage | production]

REMARKS:

Use this command to print the purpose of the current cluster:

    neon cluster purpose

and this command to change the purpose to one of the possible values:

    neon cluster purpose PURPOSE

where PURPOSE can be passed as (case insensitive):

    unspecified
    development
    test
    stage
    production

";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "purpose" };

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption)
            {
                Help();
                Program.Exit(0);
            }

            var context = KubeHelper.CurrentContext;

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: There is no current cluster.");
                Program.Exit(1);
            }

            using (var cluster = new ClusterProxy(context, new HostingManagerFactory(), cloudMarketplace: false))   // [cloudMarketplace] arg doesn't matter here.
            {
                var purposeArg  = commandLine.Arguments.ElementAtOrDefault(0);
                var clusterInfo = await cluster.GetClusterInfoAsync();

                if (purposeArg == null)
                {
                    Console.WriteLine(NeonHelper.EnumToString(clusterInfo.Purpose));
                }
                else
                {
                    Console.WriteLine();

                    if (!NeonHelper.TryParse<ClusterPurpose>(purposeArg, out var newPurpose))
                    {
                        Console.Error.WriteLine($"*** ERROR: Unknown cluster purpose: {purposeArg}");
                        Console.Error.WriteLine();
                        Console.Error.WriteLine("Specify one of these purposes:");
                        Console.Error.WriteLine();

                        foreach (var purpose in Enum.GetValues<ClusterPurpose>())
                        {
                            Console.Error.WriteLine($"    {NeonHelper.EnumToString(purpose)}");
                        }

                        Program.Exit(1);
                    }

                    clusterInfo.Purpose = newPurpose;

                    Console.WriteLine($"Updating cluster purpose...");
                    await cluster.SetClusterInfo(clusterInfo);
                    Console.WriteLine($"Cluster purpose is now: {NeonHelper.EnumToString(newPurpose)}");

                    Program.Exit(0);
                }
            }
        }
    }
}
