//-----------------------------------------------------------------------------
// FILE:        ClusterDeleteCommand.cs
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
using Neon.Kube.Config;
using Neon.Kube.Hosting;
using Neon.Kube.Proxy;
using Neon.Net;
using Neon.Retry;
using Neon.SSH;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>cluster delete</b> command.
    /// </summary>
    [Command]
    public class ClusterDeleteCommand : CommandBase
    {
        private const string usage = @"
Permanently deletes a NEONKUBE cluster.

USAGE:

    neon cluster remove [CLUSTERNAME] [--force]

ARGUMENTS:

    CLUSTERNAME     - Optionally identifies cluster to be removed by name.
                      The current context's cluster will be removed when
                      this isn't present.

OPTIONS:

    --force         - Forces cluster removal without user confirmation and
                      also without checking the cluster lock status

REMARKS:

This command will not work on a locked clusters as a safety measure.  The idea
it to add some friction to avoid impacting production clusters by accident.

All clusters besides NEONDESKTOP clusters are locked by default when they're
deployed.  You can disable this by setting [IsLocked=false] in your cluster
definition or by executing this command on your cluster:

    neon cluster unlock

";
        /// <inheritdoc/>
        public override string[] Words => new string[] { "cluster", "delete" };

        /// <inheritdoc/>
        public override string[] AltWords => new string[] { "cluster", "rm" };

        /// <inheritdoc/>
        public override string[] ExtendedOptions => new string[] { "--force" };

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
            if (commandLine.HasHelpOption)
            {
                Help();
                Program.Exit(0);
            }

            var clusterName = commandLine.Arguments.ElementAtOrDefault(0);
            var contextName = $"root@{clusterName}";
            var force       = commandLine.HasOption("--force");
            var orgContext  = (KubeConfigContext)null;

            Console.WriteLine();

            if (!string.IsNullOrEmpty(clusterName))
            {
                // Special case the scenario where we're deleting a partially deployed
                // cluster using it's temporary setup state.

                if (!KubeHelper.KubeConfig.Clusters.Any(cluster => cluster.Name == clusterName))
                {
                    var setupStatePath = Path.Combine(KubeHelper.SetupFolder, $"{contextName}.yaml");

                    if (!File.Exists(setupStatePath))
                    {
                        Console.Error.WriteLine($"*** ERROR: Cannot delete unknown cluster: {clusterName}");
                        Program.Exit(1);
                    }

                    var setupState = NeonHelper.JsonDeserialize<KubeSetupState>(File.ReadAllText(setupStatePath));

                    // $note(jefflill):
                    //
                    // The [cloudMarketplace] parameter value doesn't matter below and we're going to
                    // pass [force=true] to the remove call because we won't be able to query the
                    // cluster lock state state without a context.

                    using (var cluster = ClusterProxy.Create(setupState, new HostingManagerFactory(), cloudMarketplace: false))
                    {
                        await RemoveCluster(cluster, force: true);
                    }

                    Program.Exit(0);
                }

                orgContext = KubeHelper.CurrentContext;

                KubeHelper.SetCurrentContext(contextName);
            }
            
            var context = KubeHelper.CurrentContext;

            if (context == null)
            {
                Console.Error.WriteLine($"*** ERROR: There is no current cluster.");
                Program.Exit(1);
            }

            try
            {
                using (var cluster = ClusterProxy.Create(KubeHelper.KubeConfig, new HostingManagerFactory()))
                {
                    await RemoveCluster(cluster, force);
                }
            }
            finally
            {
                // Restore the original context if we switched clusters.

                if (orgContext != null && 
                    !string.IsNullOrEmpty(clusterName) && 
                    !orgContext.Name.Equals(contextName))
                {
                    KubeHelper.SetCurrentContext(orgContext.Name);
                }
            }
        }

        /// <summary>
        /// Removes the cluster associated with a <see cref="ClusterProxy"/>.
        /// </summary>
        /// <param name="cluster">The cluster proxy.</param>
        /// <param name="force">Pass <c>true</c> when the <c>--force</c> option is specified.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task RemoveCluster(ClusterProxy cluster, bool force)
        {
            var capabilities = cluster.Capabilities;

            if ((capabilities & HostingCapabilities.Removable) == 0)
            {
                Console.Error.WriteLine($"*** ERROR: Cluster is not removable.");
                Program.Exit(1);
            }

            if (!force)
            {
                var isLocked = await cluster.IsLockedAsync();

                if (!isLocked.HasValue)
                {
                    Console.Error.WriteLine($"*** ERROR: [{cluster.Name}] lock status is unknown.");
                    Program.Exit(1);
                }

                if (isLocked.Value)
                {
                    Console.Error.WriteLine($"*** ERROR: [{cluster.Name}] is locked.");
                    Program.Exit(1);
                }

                if (!Program.PromptYesNo($"Are you sure you want to delete cluster: {cluster.Name}?"))
                {
                    Program.Exit(0);
                }
            }

            try
            {
                Console.WriteLine($"Removing: {cluster.Name}...");
                await cluster.DeleteClusterAsync();

                // Remove all contexts that reference the cluster.

                foreach (var delContext in KubeHelper.KubeConfig.Contexts
                    .Where(context => context.Context.Cluster == context.Context.Cluster)
                    .ToArray())
                {
                    KubeHelper.KubeConfig.RemoveContext(delContext);
                }

                KubeHelper.KubeConfig.Save();
                Console.WriteLine($"REMOVED:  {cluster.Name}");
            }
            catch (TimeoutException)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine($"*** ERROR: Timeout waiting for cluster.");
            }
        }
    }
}
