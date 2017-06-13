//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;

namespace NeonCouchbaseManager
{
    /// <summary>
    /// Implements the <b>neon-couchbase-manager</b> service which is responsible for 
    /// configuring and monitoring Couchbase clusters.  See 
    /// <a href="https://hub.docker.com/r/neoncluster/neon-couchbase-manager/">neoncluster/neon-couchbase-manager</a>
    /// for more information.
    /// </summary>
    public static class Program
    {
        private const string serviceName = "neon-couchbase-manager";

        private static readonly string dbRootKey     = "neon/databases";
        private static readonly string clusterDefKey = "neon/cluster/definition.deflate";

        private static string                   serviceNameVersion = $"{serviceName} v{Neon.Build.ClusterVersion}";
        private static CancellationTokenSource  ctsTerminate       = new CancellationTokenSource();
        private static TimeSpan                 terminateTimeout   = TimeSpan.FromSeconds(10);
        private static bool                     terminated;
        private static ILog                     log;
        private static ConsulClient             consul;
        private static DockerClient             docker;

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            LogManager.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.GetLogger("neon-couchbase-manager");

            log.Info(() => $"Starting [{serviceNameVersion}]");

            // Gracefully handle SIGTERM events.

            AssemblyLoadContext.Default.Unloading +=
                context =>
                {
                    // Signal the sub-tasks that we're terminating and then 
                    // give them a chance to exit.

                    log.Info(() => "SIGTERM received: Stopping tasks...");

                    ctsTerminate.Cancel();

                    try
                    {
                        NeonHelper.WaitFor(() => terminated, terminateTimeout);
                        log.Info(() => "Tasks stopped gracefully.");
                        Program.Exit(0);
                    }
                    catch (TimeoutException)
                    {
                        log.Warn(() => $"Tasks did not stop within [{terminateTimeout}].");
                    }
                };

            try
            {
                // Establish the cluster connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    NeonClusterHelper.ConnectCluster();
                }
                else
                {
                    NeonClusterHelper.ConnectClusterService();
                }

                // Ensure that we're running on a manager node.  We won't be able
                // to query swarm status otherwise.

                var nodeRole = Environment.GetEnvironmentVariable("NEON_NODE_ROLE");

                if (string.IsNullOrEmpty(nodeRole))
                {
                    log.Fatal(() => "Container does not appear to be running in a NeonCluster.");
                    Program.Exit(1);
                }

                if (!string.Equals(nodeRole, NodeRole.Manager, StringComparison.OrdinalIgnoreCase))
                {
                    log.Fatal(() => $"[neon-couchbase-manager] service is running on a [{nodeRole}] cluster node.  Running on only [{NodeRole.Manager}] nodes are supported.");
                    Program.Exit(1);
                }

                // Open the cluster data services and then start the main service task.

                log.Debug(() => $"Opening Consul");

                using (consul = NeonClusterHelper.OpenConsul())
                {
                    log.Debug(() => $"Opening Docker");

                    using (docker = NeonClusterHelper.OpenDocker())
                    {
                        Task.Run(
                            async () =>
                            {
                                await RunAsync();

                            }).Wait();
                    }
                }
            }
            catch (Exception e)
            {
                log.Fatal(e);
                Program.Exit(1);
            }
            finally
            {
                NeonClusterHelper.DisconnectCluster();
            }

            Program.Exit(0);
        }

        /// <summary>
        /// Exits the service with an exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            log.Info(() => $"Exiting: [{serviceName}]");
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Implements the service as a <see cref="Task"/>.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        private static async Task RunAsync()
        {
            // Launch the sub-tasks.  These will run until the service is terminated.

            await NeonHelper.WaitAllAsync(
                NodePoller());

            terminated = true;
        }

        /// <summary>
        /// Handles polling of Docker swarm about the cluster nodes and updating the cluster
        /// definition and hash as changes are detected.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task NodePoller()
        {
            while (true)
            {
                //try
                //{
                //    log.Debug(() => "NodePoller: Polling");

                //    if (ctsTerminate.Token.IsCancellationRequested)
                //    {
                //        log.Debug(() => "NodePoller: Cancelled");
                //        return; // We've been signalled to terminate
                //    }

                //    // Retrieve the current cluster definition from Consul if we don't already
                //    // have it or if it's changed from what we've cached.

                //    cachedClusterDefinition = await NeonClusterHelper.GetClusterDefinitionAsync(cachedClusterDefinition, ctsTerminate.Token);

                //    // Retrieve the swarm nodes from Docker.

                //    log.Debug(() => $"NodePoller: Querying [{docker.Settings.Uri}]");

                //    var swarmNodes = await docker.NodeListAsync();

                //    // Parse the node definitions from the swarm nodes and build a new definition with
                //    // using the new nodes.  Then compare the hashes of the cached and new cluster definitions
                //    // and then update Consul if they're different.

                //    var currentClusterDefinition = NeonHelper.JsonClone<ClusterDefinition>(cachedClusterDefinition);

                //    currentClusterDefinition.NodeDefinitions.Clear();

                //    foreach (var swarmNode in swarmNodes)
                //    {
                //        var nodeDefinition = NodeDefinition.ParseFromLabels(swarmNode.Labels);

                //        nodeDefinition.Name = swarmNode.Hostname;

                //        currentClusterDefinition.NodeDefinitions.Add(nodeDefinition.Name, nodeDefinition);
                //    }

                //    currentClusterDefinition.ComputeHash();

                //    if (currentClusterDefinition.Hash != cachedClusterDefinition.Hash)
                //    {
                //        log.Info(() => "NodePoller: Changed cluster definition.  Updating Consul.");

                //        await NeonClusterHelper.PutClusterDefinitionAsync(currentClusterDefinition, ctsTerminate.Token);

                //        cachedClusterDefinition = currentClusterDefinition;
                //    }
                //    else
                //    {
                //        log.Debug(() => "NodePoller: Unchanged cluster definition.");
                //    }
                //}
                //catch (OperationCanceledException)
                //{
                //    log.Debug(() => "NodePoller: Cancelled");
                //    return;
                //}
                //catch (KeyNotFoundException)
                //{
                //    // We'll see this when no cluster definition has been persisted to the
                //    // cluster.  This is a serious problem.  This is configured during setup
                //    // and there should always be a definition in Consul.

                //    log.Error(() => $"NodePoller: No cluster definition has been found at [{clusterDefKey}] in Consul.  This is a serious error that will have to be corrected manually.");
                //}
                //catch (Exception e)
                //{
                //    log.Error("NodePoller", e);
                //}

                //await Task.Delay(nodePollInterval, ctsTerminate.Token);
            }
        }
    }
}
