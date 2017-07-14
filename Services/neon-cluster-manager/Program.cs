//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
using Neon.Net;

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

        private static ProcessTerminator        terminator;
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
            log = LogManager.GetLogger("main");
            log.Info(() => $"Starting [{serviceName}]");

            terminator = new ProcessTerminator(log);

            try
            {
                // Establish the cluster connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    NeonClusterHelper.OpenRemoteCluster();
                }
                else
                {
                    NeonClusterHelper.OpenCluster();
                }

                // Ensure that we're running on a manager node.  We won't be able
                // to query swarm status otherwise.

                var nodeRole = Environment.GetEnvironmentVariable("NEON_NODE_ROLE");

                if (string.IsNullOrEmpty(nodeRole))
                {
                    log.Fatal(() => "Container does not appear to be running on a neonCLUSTER.");
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
                NeonClusterHelper.CloseCluster();
                terminator.ReadyToExit();
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
            terminator.ReadyToExit();
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

            var tasks = new List<Task>();

            tasks.Add(NodePoller());

            // $hack(jeff.lill):
            //
            // We need to start a vault poller for the vault instance running on each manager
            // node.  We're going to construct the direct Vault URIs by parsing the Vault
            // host names from the [hosts] file.  The Vault host names will look like:
            //
            //      *.neon-vault.cluster

            var vaultUris = new List<string>();

            if (NeonHelper.IsWindows)
            {
                // Assume that we're running in development mode if we're on Windows.

                vaultUris.Add(Environment.GetEnvironmentVariable("VAULT_DIRECT"));
            }
            else
            {
                using (var reader = new StreamReader(new FileStream("/etc/hosts", FileMode.Open, FileAccess.Read)))
                {
                    foreach (var line in reader.Lines())
                    {
                        var extract = line.Trim();

                        // Strip out any comments.

                        var commentPos = line.IndexOf('#');

                        if (commentPos != -1)
                        {
                            extract = extract.Substring(0, commentPos).Trim();
                        }

                        if (string.IsNullOrEmpty(extract))
                        {
                            continue;   // Ignore blank lines
                        }

                        // Extract the hostname

                        var hostPos = 0;

                        while (hostPos < extract.Length && !char.IsLetter(extract[hostPos]))
                        {
                            hostPos++;
                        }

                        if (hostPos >= extract.Length)
                        {
                            continue;   // Ignore malformed DNS entries.
                        }

                        var hostname = extract.Substring(hostPos);

                        if (hostname.EndsWith(".neon-vault.cluster", StringComparison.OrdinalIgnoreCase))
                        {
                            vaultUris.Add($"https://{hostname}:{NetworkPorts.Vault}");
                        }
                    }
                }
            }

            foreach (var uri in vaultUris)
            {
                tasks.Add(VaultPoller(uri));
            }

            await NeonHelper.WaitAllAsync(tasks);

            terminator.ReadyToExit();
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

                    if (terminator.CancellationToken.IsCancellationRequested)
                    {
                        log.Debug(() => "NodePoller: Terminating");
                        return;
                    }

                    // Retrieve the current cluster definition from Consul if we don't already
                    // have it or if it's changed from what we've cached.

                    cachedClusterDefinition = await NeonClusterHelper.GetClusterDefinitionAsync(cachedClusterDefinition, terminator.CancellationToken);

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

                        await NeonClusterHelper.PutClusterDefinitionAsync(currentClusterDefinition, terminator.CancellationToken);

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

                await Task.Delay(nodePollInterval, terminator.CancellationToken);
            }
        }

        /// <summary>
        /// Handles polling of Vault seal status and automatic unsealing if enabled.
        /// </summary>
        /// <param name="vaultUri">The URI for the Vault instance being managed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task VaultPoller(string vaultUri)
        {
            // Each cluster manager instance is only going to manage the Vault instance
            // running on the same host manager node.

            var lastVaultStatus = (VaultStatus)null;

            // We're going to periodically log Vault status even
            // when there is no status changes.

            var statusUpdateTimeUtc  = DateTime.UtcNow;
            var statusUpdateInterval = TimeSpan.FromMinutes(30);

            log.Debug(() => $"Vault: opening [{vaultUri}]");

            using (var vault = VaultClient.OpenWithToken(new Uri(vaultUri)))
            {
                vault.AllowSelfSignedCertificates = true;

                while (true)
                {
                    try
                    {
                        log.Debug(() => $"Vault: polling [{vaultUri}]");

                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.Debug(() => $"Vault: terminating [{vaultUri}]");
                            return;
                        }

                        // Monitor Vault for status changes and handle unsealing if enabled.

                        log.Debug(() => $"Vault: querying [{vaultUri}]");

                        var newVaultStatus = await vault.GetHealthAsync(terminator.CancellationToken);
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
                                log.Error(() => $"Vault: status CHANGED [{vaultUri}]");
                            }
                            else
                            {
                                log.Info(() => $"Vault: status CHANGED [{vaultUri}]");
                            }

                            statusUpdateTimeUtc = DateTime.UtcNow; // Force logging status below
                        }

                        if (DateTime.UtcNow >= statusUpdateTimeUtc)
                        {
                            if (!newVaultStatus.IsInitialized || newVaultStatus.IsSealed)
                            {
                                log.Error(() => $"Vault: status={newVaultStatus} [{vaultUri}]");
                            }
                            else
                            {
                                log.Info(() => $"Vault: status={newVaultStatus} [{vaultUri}]");
                            }

                            statusUpdateTimeUtc = DateTime.UtcNow + statusUpdateInterval;
                        }

                        lastVaultStatus = newVaultStatus;

                        // Attempt to unseal the Vault if it's sealed and we have the keys.

                        if (newVaultStatus.IsSealed && vaultCredentials != null)
                        {
                            try
                            {
                                log.Info(() => $"Vault: unsealing [{vaultUri}]");
                                await vault.UnsealAsync(vaultCredentials, terminator.CancellationToken);
                                log.Info(() => $"Vault: UNSEALED [{vaultUri}]");

                                // Schedule a status update on the next loop
                                // and then loop immediately so we'll log the
                                // updated status.

                                statusUpdateTimeUtc = DateTime.UtcNow;
                                continue;
                            }
                            catch (Exception e)
                            {
                                log.Error($"Vault: unseal failed [{vaultUri}]", e);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        log.Debug(() => $"Vault: terminating [{vaultUri}]");
                        return;
                    }
                    catch (Exception e)
                    {
                        log.Error($"Vault: [{vaultUri}]", e);
                    }

                    await Task.Delay(vaultPollInterval, terminator.CancellationToken);
                }
            }
        }
    }
}
