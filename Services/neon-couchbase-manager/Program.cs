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

using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Management;

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
        private static TimeSpan                 pollInterval       = TimeSpan.FromSeconds(10);
        private static string                   database;
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
            Test();

            LogManager.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.GetLogger("neon-couchbase-manager");

            log.Info(() => $"Starting [{serviceNameVersion}]");

            // Parse settings.

            database = Environment.GetEnvironmentVariable("DATABASE") ?? string.Empty;

            var pollSecondsVar = Environment.GetEnvironmentVariable("POLL_SECONDS");

            if (!string.IsNullOrEmpty(pollSecondsVar))
            {
                if (int.TryParse(pollSecondsVar, out var pollSeconds) && pollSeconds > 0)
                {
                    pollInterval = TimeSpan.FromSeconds(pollSeconds);
                }
            }

            log.Info(() => $"LOG_LEVEL    = [{LogManager.LogLevel}]");
            log.Info(() => $"DATABASE     = [{database}]");
            log.Info(() => $"POLL_SECONDS = [{pollInterval.TotalSeconds}]");

            if (string.IsNullOrEmpty(database))
            {
                log.Fatal(() => "[DATABASE] environment variable must be set.");
                Program.Exit(1);
            }

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
                    log.Fatal(() => "Container does not appear to be running on a NeonCluster.");
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
            // Launch any sub-tasks.  These will run until the service is terminated.

            await Poller();
            terminated = true;
        }

        /// <summary>
        /// Periodically checks the database cluster status and also handles cluster initialization.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task Poller()
        {
            Cluster     cluster            = null;
            string      clientSettingsJson = null;

            while (true)
            {
                try
                {
                    log.Debug(() => "Checking cluster");

                    if (ctsTerminate.Token.IsCancellationRequested)
                    {
                        log.Debug(() => "Cancelled");
                        return; // We've been signalled to terminate
                    }

                    // Read the cluster information from Consul.

                    var databaseKey = $"neon/databases/{database}";

                    try
                    {
                        var clusterInfo    = await consul.KV.GetObject<DbClusterInfo>(databaseKey);
                        var clientSettings = NeonHelper.JsonDeserialize<DbCouchbaseSettings>(clusterInfo.ClientSettings);

                        if (clusterInfo.ClientSettings != clientSettingsJson || cluster != null)
                        {
                            cluster = new Cluster(
                                new ClientConfiguration()
                                {
                                    ApiPort       = clientSettings.ApiPort,
                                    DirectPort    = clientSettings.DirectPort,
                                    HttpsApiPort  = clientSettings.HttpsApiPort,
                                    HttpsMgmtPort = clientSettings.HttpsMgmtPort,
                                    SslPort       = clientSettings.SslPort,
                                    UseSsl        = clientSettings.UseSsl,
                                    Servers       = clientSettings.Servers
                                });


                        };
                    }
                    catch (Exception e)
                    {
                        if (e.TriggeredBy<KeyNotFoundException>())
                        {
                            log.Error(() => $"Consul key does not exist: [{databaseKey}]");
                        }
                        else
                        {
                            throw;
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    log.Debug(() => "Cancelled");
                    return;
                }
                catch (Exception e)
                {
                    log.Error(e);
                }

                await Task.Delay(pollInterval, ctsTerminate.Token);
            }
        }

        private static void Test()
        {
            var config = new ClientConfiguration()
            {
                Servers = new List<Uri>()
                {
                    new Uri("http://10.0.1.40:8091/"),
                    new Uri("http://10.0.1.41:8091/"),
                    new Uri("http://10.0.1.42:8091/")
                }
            };

            var cluster = new Cluster(config);

            using (var provisioner = new ClusterProvisioner(cluster, "Administrator", "password"))
            {
                var results = provisioner.ProvisionEntryPointAsync().Result;
            }

            //config.Servers.Add(new Uri("http://10.0.1.40:8091/pools"));

            //using (var clusterManager = cluster.CreateManager("Administrator", "password"))
            //{
            //    var result = clusterManager.ConfigureAdminAsync("10.0.1.41").Result;
            //}
        }
    }
}
