//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

        private static readonly string serviceRootKey        = "neon/service/neon-cluster-manager";
        private static readonly string nodePollSecondsKey    = $"{serviceRootKey}/node_poll_seconds";
        private static readonly string vaultPollSecondsKey   = $"{serviceRootKey}/vault_poll_seconds";
        private static readonly string managerPollSecondsKey = $"{serviceRootKey}/manager_poll_seconds";
        private static readonly string clusterDefKey         = "neon/cluster/definition.deflate";

        private static ProcessTerminator        terminator;
        private static INeonLogger              log;
        private static ConsulClient             consul;
        private static DockerClient             docker;
        private static VaultCredentials         vaultCredentials;
        private static TimeSpan                 nodePollInterval;
        private static TimeSpan                 vaultPollInterval;
        private static TimeSpan                 managerPollInterval;
        private static ClusterDefinition        cachedClusterDefinition;
        private static List<string>             vaultUris;

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));
            log.LogInfo(() => $"Starting [{serviceName}:{Program.GitVersion}]");

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
                    log.LogCritical(() => "Container does not appear to be running on a neonCLUSTER.");
                    Program.Exit(1);
                }

                if (!string.Equals(nodeRole, NodeRole.Manager, StringComparison.OrdinalIgnoreCase))
                {
                    log.LogCritical(() => $"[neon-cluster-manager] service is running on a [{nodeRole}] cluster node.  Running on only [{NodeRole.Manager}] nodes are supported.");
                    Program.Exit(1);
                }

                // Open the cluster data services and then start the main service task.

                log.LogDebug(() => $"Opening Consul");

                using (consul = NeonClusterHelper.OpenConsul())
                {
                    log.LogDebug(() => $"Opening Docker");

                    using (docker = NeonClusterHelper.OpenDocker())
                    {
                        await RunAsync();
                    }
                }
            }
            catch (Exception e)
            {
                log.LogCritical(e);
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
        /// Returns the program version as the Git branch and commit and an optional
        /// indication of whether the program was build from a dirty branch.
        /// </summary>
        public static string GitVersion
        {
            get
            {
                var version = $"{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}";

#pragma warning disable 162 // Unreachable code

                if (ThisAssembly.Git.IsDirty)
                {
                    version += "-DIRTY";
                }

#pragma warning restore 162 // Unreachable code

                return version;
            }
        }

        /// <summary>
        /// Exits the service with an exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            log.LogInfo(() => $"Exiting: [{serviceName}]");
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
                log.LogInfo($"Persisting setting [{nodePollSecondsKey}=30.0]");
                await consul.KV.PutDouble(nodePollSecondsKey, 30.0);
            }

            if (!await consul.KV.Exists(vaultPollSecondsKey))
            {
                log.LogInfo($"Persisting setting [{vaultPollSecondsKey}=30.0]");
                await consul.KV.PutDouble(vaultPollSecondsKey, 30.0);
            }

            if (!await consul.KV.Exists(managerPollSecondsKey))
            {
                log.LogInfo($"Persisting setting [{managerPollSecondsKey}=1800.0]");
                await consul.KV.PutDouble(managerPollSecondsKey, 1800);
            }

            nodePollInterval    = TimeSpan.FromSeconds(await consul.KV.GetDouble(nodePollSecondsKey));
            vaultPollInterval   = TimeSpan.FromSeconds(await consul.KV.GetDouble(vaultPollSecondsKey));
            managerPollInterval = TimeSpan.FromSeconds(await consul.KV.GetDouble(managerPollSecondsKey));

            log.LogInfo(() => $"Using setting [{nodePollSecondsKey}={nodePollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{vaultPollSecondsKey}={vaultPollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{managerPollSecondsKey}={managerPollInterval.TotalSeconds}]");

            // Parse the Vault credentials from the [neon-cluster-manager-vaultkeys] 
            // secret, if it exists.

            var vaultCredentialsJson = NeonClusterHelper.GetSecret("neon-cluster-manager-vaultkeys");

            if (string.IsNullOrWhiteSpace(vaultCredentialsJson))
            {
                log.LogInfo(() => "Vault unsealing is DISABLED because [neon-cluster-manager-vaultkeys] Docker secret is not specified.");
            }
            else
            {
                try
                {
                    vaultCredentials = NeonHelper.JsonDeserialize<VaultCredentials>(vaultCredentialsJson);

                    log.LogInfo(() => "Vault unsealing is ENABLED.");
                }
                catch (Exception e)
                {
                    log.LogError("Vault unsealing is DISABLED because the [neon-cluster-manager-vaultkeys] Docker secret could not be parsed.", e);
                }
            }

            // Launch the sub-tasks.  These will run until the service is terminated.

            var tasks = new List<Task>();

            tasks.Add(NodePollerAsync());

            // We need to start a vault poller for the Vault instance running on each manager
            // node.  We're going to construct the direct Vault URIs by querying Docker for
            // the current cluster nodes and looking for the managers.

            vaultUris = await GetVaultUrisAsync();

            foreach (var uri in vaultUris)
            {
                tasks.Add(VaultPollerAsync(uri));
            }

            // Start a task that periodically checks for changes to the set of cluster managers 
            // (e.g. if a manager is added or removed).  This task will cause the service to exit
            // so it can be restarted automatically by Docker to respond to the change.

            tasks.Add(ManagerPollerAsync());

            // Wait for all tasks to exit cleanly for a normal shutdown.

            await NeonHelper.WaitAllAsync(tasks);

            terminator.ReadyToExit();
        }

        /// <summary>
        /// Returns the list of URIs targeting Vault on each current manager node.
        /// </summary>
        /// <returns>The Vault URIs.</returns>
        private static async Task<List<string>> GetVaultUrisAsync()
        {
            var vaultUris = new List<string>();

            if (NeonHelper.IsWindows)
            {
                // Assume that we're running in development mode if we're on Windows.

                vaultUris.Add(Environment.GetEnvironmentVariable("VAULT_DIRECT_ADDR"));
                return vaultUris;
            }

            var clusterNodes = await docker.NodeListAsync();

            foreach (var managerNode in clusterNodes.Where(n => n.Role == "manager")
                .OrderBy(n => n.Hostname))
            {
                vaultUris.Add($"https://{managerNode.Hostname}.neon-vault.cluster:{NetworkPorts.Vault}");
            }

            return vaultUris;
        }

        /// <summary>
        /// Handles polling of Docker swarm about the cluster nodes and updating the cluster
        /// definition and hash when changes are detected.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task NodePollerAsync()
        {
            while (true)
            {
                try
                {
                    log.LogDebug(() => "NodePoller: Polling");

                    if (terminator.CancellationToken.IsCancellationRequested)
                    {
                        log.LogDebug(() => "NodePoller: Terminating");
                        return;
                    }

                    // Retrieve the current cluster definition from Consul if we don't already
                    // have it or if it's different from what we've cached.

                    cachedClusterDefinition = await NeonClusterHelper.GetDefinitionAsync(cachedClusterDefinition, terminator.CancellationToken);

                    // Retrieve the swarm nodes from Docker.

                    log.LogDebug(() => $"NodePoller: Querying [{docker.Settings.Uri}]");

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

                    // Cluster pets are not part of the Swarm, so Docker won't return any information
                    // about them.  We'll read the pet definitions from [neon/cluster/pets.json] in 
                    // Consul.  We'll assume that there are no pets if this key doesn't exist for
                    // backwards compatibility and robustness.

                    string petsJson = null;

                    try
                    {
                        petsJson = await NeonClusterHelper.Consul.KV.GetString("neon/cluster/pets.json", terminator.CancellationToken);
                    }
                    catch (KeyNotFoundException)
                    {
                        // Assume that there are no cluster pets.
                    }

                    if (!string.IsNullOrWhiteSpace(petsJson))
                    {
                        // Parse the pet node definitions and add them to the cluster definition.

                        var petDefinitions = NeonHelper.JsonDeserialize<Dictionary<string, NodeDefinition>>(petsJson);

                        foreach (var item in petDefinitions)
                        {
                            currentClusterDefinition.NodeDefinitions.Add(item.Key, item.Value);
                        }
                    }

                    // Determine if the definition has changed.

                    currentClusterDefinition.ComputeHash();

                    if (currentClusterDefinition.Hash != cachedClusterDefinition.Hash)
                    {
                        log.LogInfo(() => "NodePoller: Changed cluster definition.  Updating Consul.");

                        await NeonClusterHelper.PutDefinitionAsync(currentClusterDefinition, cancellationToken: terminator.CancellationToken);

                        cachedClusterDefinition = currentClusterDefinition;
                    }
                    else
                    {
                        log.LogDebug(() => "NodePoller: Unchanged cluster definition.");
                    }
                }
                catch (OperationCanceledException)
                {
                    log.LogDebug(() => "NodePoller: Terminating");
                    return;
                }
                catch (KeyNotFoundException)
                {
                    // We'll see this when no cluster definition has been persisted to the
                    // cluster.  This is a serious problem.  This is configured during setup
                    // and there should always be a definition in Consul.

                    log.LogError(() => $"NodePoller: No cluster definition has been found at [{clusterDefKey}] in Consul.  This is a serious error that will have to be corrected manually.");
                }
                catch (Exception e)
                {
                    log.LogError("NodePoller", e);
                }

                await Task.Delay(nodePollInterval, terminator.CancellationToken);
            }
        }

        /// <summary>
        /// Handles polling of Vault seal status and automatic unsealing if enabled.
        /// </summary>
        /// <param name="vaultUri">The URI for the Vault instance being managed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task VaultPollerAsync(string vaultUri)
        {
            // Each cluster manager instance is only going to manage the Vault instance
            // running on the same host manager node.

            var lastVaultStatus = (VaultStatus)null;

            // We're going to periodically log Vault status even
            // when there is no status changes.

            var statusUpdateTimeUtc  = DateTime.UtcNow;
            var statusUpdateInterval = TimeSpan.FromMinutes(30);

            log.LogDebug(() => $"Vault: opening [{vaultUri}]");

            using (var vault = VaultClient.OpenWithToken(new Uri(vaultUri)))
            {
                vault.AllowSelfSignedCertificates = true;

                while (true)
                {
                    try
                    {
                        log.LogDebug(() => $"Vault: polling [{vaultUri}]");

                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.LogDebug(() => $"Vault: terminating [{vaultUri}]");
                            return;
                        }

                        // Monitor Vault for status changes and handle unsealing if enabled.

                        log.LogDebug(() => $"Vault: querying [{vaultUri}]");

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
                                log.LogError(() => $"Vault: status CHANGED [{vaultUri}]");
                            }
                            else
                            {
                                log.LogInfo(() => $"Vault: status CHANGED [{vaultUri}]");
                            }

                            statusUpdateTimeUtc = DateTime.UtcNow; // Force logging status below
                        }

                        if (DateTime.UtcNow >= statusUpdateTimeUtc)
                        {
                            if (!newVaultStatus.IsInitialized || newVaultStatus.IsSealed)
                            {
                                log.LogError(() => $"Vault: status={newVaultStatus} [{vaultUri}]");
                            }
                            else
                            {
                                log.LogInfo(() => $"Vault: status={newVaultStatus} [{vaultUri}]");
                            }

                            statusUpdateTimeUtc = DateTime.UtcNow + statusUpdateInterval;
                        }

                        lastVaultStatus = newVaultStatus;

                        // Attempt to unseal the Vault if it's sealed and we have the keys.

                        if (newVaultStatus.IsSealed && vaultCredentials != null)
                        {
                            try
                            {
                                log.LogInfo(() => $"Vault: unsealing [{vaultUri}]");
                                await vault.UnsealAsync(vaultCredentials, terminator.CancellationToken);
                                log.LogInfo(() => $"Vault: UNSEALED [{vaultUri}]");

                                // Schedule a status update on the next loop
                                // and then loop immediately so we'll log the
                                // updated status.

                                statusUpdateTimeUtc = DateTime.UtcNow;
                                continue;
                            }
                            catch (Exception e)
                            {
                                log.LogError($"Vault: unseal failed [{vaultUri}]", e);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        log.LogDebug(() => $"Vault: terminating [{vaultUri}]");
                        return;
                    }
                    catch (Exception e)
                    {
                        log.LogError($"Vault: [{vaultUri}]", e);
                    }

                    await Task.Delay(vaultPollInterval, terminator.CancellationToken);
                }
            }
        }

        /// <summary>
        /// Handles detection of changes to the cluster's manager nodes.  The process will
        /// be terminated when manager nodes are added or removed so that Docker will restart
        /// the service to begin handling the changes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ManagerPollerAsync()
        {
            while (true)
            {
                // We don't need to poll that often because cluster managers
                // will rarely change.

                await Task.Delay(managerPollInterval);

                try
                {
                    if (terminator.CancellationToken.IsCancellationRequested)
                    {
                        log.LogDebug(() => "Manager: terminating.");
                        return;
                    }

                    log.LogDebug(() => "Manager: polling for cluster manager changes.");

                    var latestVaultUris = await GetVaultUrisAsync();
                    var changed         = vaultUris.Count != latestVaultUris.Count;

                    if (!changed)
                    {
                        for (int i = 0; i < vaultUris.Count; i++)
                        {
                            if (vaultUris[i] != latestVaultUris[i])
                            {
                                changed = true;
                                break;
                            }
                        }
                    }

                    if (changed)
                    {
                        log.LogInfo("Manager: detected one or more cluster manager node changes.");
                        log.LogInfo("Manager: exiting the service so that Docker will restart it to pick up the manager node changes.");
                        terminator.Exit();
                    }
                    else
                    {
                        log.LogDebug(() => "Manager: no manager changes detected.");
                    }
                }
                catch (OperationCanceledException)
                {
                    log.LogDebug(() => "Manager: terminating.");
                    return;
                }
                catch (Exception e)
                {
                    log.LogError($"Manager", e);
                }

                await Task.Delay(vaultPollInterval, terminator.CancellationToken);
            }
        }
    }
}