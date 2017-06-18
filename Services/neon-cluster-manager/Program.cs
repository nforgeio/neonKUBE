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
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;

namespace NeonClusterManager
{
    /// <summary>
    /// Implements the <b>neon-cluster-manager</b> service.  See 
    /// <a href="https://hub.docker.com/r/neoncluster/neon-cluster-manager/">neoncluster/neon-cluster-manager</a>
    /// for more information.
    /// </summary>
    public static class Program
    {
        private const string serviceName = "neon-cluster-manager";

        private static readonly string serviceRootKey      = "neon/service/neon-proxy-manager";
        private static readonly string nodePollSecondsKey  = $"{serviceRootKey}/node_poll_seconds";
        private static readonly string vaultPollSecondsKey = $"{serviceRootKey}/vault_poll_seconds";
        private static readonly string clusterDefKey       = "neon/cluster/definition.deflate";

        private static CancellationTokenSource  ctsTerminate       = new CancellationTokenSource();
        private static TimeSpan                 terminateTimeout   = TimeSpan.FromSeconds(10);
        private static bool                     terminated;
        private static ILog                     log;
        private static ConsulClient             consul;
        private static DockerClient             docker;
        private static VaultCredentials         vaultCredentials;
        private static TimeSpan                 nodePollInterval;
        private static TimeSpan                 vaultPollInterval;
        private static ClusterDefinition        cachedClusterDefinition;

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            LogManager.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.GetLogger(serviceName);

            log.Info(() => $"Starting [{serviceName}]");

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
                    log.Fatal(() => $"[neon-cluster-manager] service is running on a [{nodeRole}] cluster node.  Running on only [{NodeRole.Manager}] nodes are supported.");
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
            // Load the settings.
            //
            // Initialize the proxy manager settings to their default values
            // if they don't already exist.

            if (!await consul.KV.Exists(nodePollSecondsKey))
            {
                log.Info($"Persisting setting [{nodePollSecondsKey}=30.0]");
                await consul.KV.PutDouble(nodePollSecondsKey, 30.0);
            }

            if (!await consul.KV.Exists(vaultPollSecondsKey))
            {
                log.Info($"Persisting setting [{vaultPollSecondsKey}=30.0]");
                await consul.KV.PutDouble(vaultPollSecondsKey, 30.0);
            }

            nodePollInterval  = TimeSpan.FromSeconds(await consul.KV.GetDouble(nodePollSecondsKey));
            vaultPollInterval = TimeSpan.FromSeconds(await consul.KV.GetDouble(vaultPollSecondsKey));

            log.Info(() => $"Using setting [{nodePollSecondsKey}={nodePollInterval}]");
            log.Info(() => $"Using setting [{vaultPollSecondsKey}={vaultPollInterval}]");

            // Parse the Vault credentials from the [neon-cluster-manager-vaultkeys] 
            // secret, if it exists.

            var vaultCredentialsJson = NeonClusterHelper.GetSecret("neon-cluster-manager-vaultkeys");

            if (string.IsNullOrWhiteSpace(vaultCredentialsJson))
            {
                log.Info(() => "Vault unsealing is DISABLED because [neon-cluster-manager-vaultkeys] Docker secret is not specified.");
            }
            else
            {
                try
                {
                    vaultCredentials = NeonHelper.JsonDeserialize<VaultCredentials>(vaultCredentialsJson);

                    log.Info(() => "Vault unsealing is ENABLED.");
                }
                catch (Exception e)
                {
                    log.Error("Vault unsealing is DISABLED because the [neon-cluster-manager-vaultkeys] Docker secret could not be parsed.", e);
                }
            }

            // Launch the sub-tasks.  These will run until the service is terminated.

            await NeonHelper.WaitAllAsync(
                NodePoller(),
                VaultPoller());

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
                try
                {
                    log.Debug(() => "NodePoller: Polling");

                    if (ctsTerminate.Token.IsCancellationRequested)
                    {
                        log.Debug(() => "NodePoller: Terminating");
                        return;
                    }

                    // Retrieve the current cluster definition from Consul if we don't already
                    // have it or if it's changed from what we've cached.

                    cachedClusterDefinition = await NeonClusterHelper.GetClusterDefinitionAsync(cachedClusterDefinition, ctsTerminate.Token);

                    // Retrieve the swarm nodes from Docker.

                    log.Debug(() => $"NodePoller: Querying [{docker.Settings.Uri}]");

                    var swarmNodes = await docker.NodeListAsync();

                    // Parse the node definitions from the swarm nodes and build a new definition with
                    // using the new nodes.  Then compare the hashes of the cached and new cluster definitions
                    // and then update Consul if they're different.

                    var currentClusterDefinition = NeonHelper.JsonClone<ClusterDefinition>(cachedClusterDefinition);

                    currentClusterDefinition.NodeDefinitions.Clear();

                    foreach (var swarmNode in swarmNodes)
                    {
                        var nodeDefinition = NodeDefinition.ParseFromLabels(swarmNode.Labels);

                        nodeDefinition.Name = swarmNode.Hostname;

                        currentClusterDefinition.NodeDefinitions.Add(nodeDefinition.Name, nodeDefinition);
                    }

                    currentClusterDefinition.ComputeHash();

                    if (currentClusterDefinition.Hash != cachedClusterDefinition.Hash)
                    {
                        log.Info(() => "NodePoller: Changed cluster definition.  Updating Consul.");

                        await NeonClusterHelper.PutClusterDefinitionAsync(currentClusterDefinition, ctsTerminate.Token);

                        cachedClusterDefinition = currentClusterDefinition;
                    }
                    else
                    {
                        log.Debug(() => "NodePoller: Unchanged cluster definition.");
                    }
                }
                catch (OperationCanceledException)
                {
                    log.Debug(() => "NodePoller: Terminating");
                    return;
                }
                catch (KeyNotFoundException)
                {
                    // We'll see this when no cluster definition has been persisted to the
                    // cluster.  This is a serious problem.  This is configured during setup
                    // and there should always be a definition in Consul.

                    log.Error(() => $"NodePoller: No cluster definition has been found at [{clusterDefKey}] in Consul.  This is a serious error that will have to be corrected manually.");
                }
                catch (Exception e)
                {
                    log.Error("NodePoller", e);
                }

                await Task.Delay(nodePollInterval, ctsTerminate.Token);
            }
        }

        /// <summary>
        /// Handles polling of Vault seal status and automatic unsealing if enabled.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task VaultPoller()
        {
            // Each cluster manager instance is only going to manage the Vault instance
            // running on the same host manager node.

            // $todo(jeff.lill):
            //
            // This will need to change in the future when (hopefully) we'll be able
            // deploy [neon-cluster-manager] as a service.  At that point, we'll need
            // a single cluster manager to be able to manage all Vaults.

            var vaultUri        = Environment.GetEnvironmentVariable("VAULT_DIRECT_ADDR");
            var lastVaultStatus = (VaultStatus)null;

            // We're going to periodically log Vault status even
            // when there is no status changes.

            var statusUpdateTimeUtc  = DateTime.UtcNow;
            var statusUpdateInterval = TimeSpan.FromMinutes(30);

            log.Debug(() => "Opening Vault");

            using (var vault = VaultClient.OpenWithToken(new Uri(vaultUri)))
            {
                vault.AllowSelfSignedCertificates = true;

                while (true)
                {
                    try
                    {
                        log.Debug(() => "VaultPolling: Polling");

                        if (ctsTerminate.Token.IsCancellationRequested)
                        {
                            log.Debug(() => "VaultPolling: Terminating");
                            return;
                        }

                        // Monitor Vault for status changes and handle unsealing if enabled.

                        log.Debug(() => $"VaultPoller: Querying status from [{vaultUri}]");

                        var newVaultStatus = await vault.GetHealthAsync(ctsTerminate.Token);
                        var changed        = false;

                        if (lastVaultStatus == null)
                        {
                            changed = true;
                        }
                        else
                        {
                            changed = !lastVaultStatus.Equals(newVaultStatus);
                        }

                        if (changed)
                        {
                            if (!newVaultStatus.IsInitialized || newVaultStatus.IsSealed)
                            {
                                log.Error(() => $"Vault Status: CHANGED");
                            }
                            else
                            {
                                log.Info(() => $"Vault Status: CHANGED");
                            }

                            statusUpdateTimeUtc = DateTime.UtcNow; // Force logging status below
                        }

                        if (DateTime.UtcNow >= statusUpdateTimeUtc)
                        {
                            if (!newVaultStatus.IsInitialized || newVaultStatus.IsSealed)
                            {
                                log.Error(() => $"Vault Status: {newVaultStatus}");
                            }
                            else
                            {
                                log.Info(() => $"Vault Status: {newVaultStatus}");
                            }

                            statusUpdateTimeUtc = DateTime.UtcNow + statusUpdateInterval;
                        }

                        lastVaultStatus = newVaultStatus;

                        // Attempt to unseal the Vault if it's sealed and we have the keys.

                        if (newVaultStatus.IsSealed && vaultCredentials != null)
                        {
                            try
                            {
                                log.Info(() => "Unsealing Vault");
                                await vault.UnsealAsync(vaultCredentials, ctsTerminate.Token);
                                log.Info(() => "Vault UNSEALED");

                                // Schedule a status update on the next loop
                                // and then loop immediately so we'll log the
                                // updated status.

                                statusUpdateTimeUtc = DateTime.UtcNow;
                                continue;
                            }
                            catch (Exception e)
                            {
                                log.Error("Vault unseal failed", e);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        log.Debug(() => "VaultPolling: Terminating");
                        return;
                    }
                    catch (Exception e)
                    {
                        log.Error("VaultPoller", e);
                    }

                    await Task.Delay(vaultPollInterval, ctsTerminate.Token);
                }
            }
        }
    }
}
