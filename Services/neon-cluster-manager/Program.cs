//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using Newtonsoft.Json.Linq;

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
        private static readonly string serviceName           = $"neon-cluster-manager:{GitVersion}";
        private static readonly string serviceRootKey        = "neon/service/neon-cluster-manager";
        private static readonly string nodePollSecondsKey    = $"{serviceRootKey}/node_poll_seconds";
        private static readonly string vaultPollSecondsKey   = $"{serviceRootKey}/vault_poll_seconds";
        private static readonly string managerPollSecondsKey = $"{serviceRootKey}/manager_poll_seconds";
        private static readonly string logPollSecondsKey     = $"{serviceRootKey}/log_poll_seconds";
        private static readonly string clusterDefKey         = $"neon/cluster/{NeonClusterGlobals.DefinitionDeflate}";

        private static ProcessTerminator        terminator;
        private static INeonLogger              log;
        private static ClusterProxy             cluster;
        private static ConsulClient             consul;
        private static DockerClient             docker;
        private static VaultCredentials         vaultCredentials;
        private static TimeSpan                 nodePollInterval;
        private static TimeSpan                 vaultPollInterval;
        private static TimeSpan                 managerPollInterval;
        private static TimeSpan                 logPollInterval;
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
            log.LogInfo(() => $"LOG_LEVEL={LogManager.Default.LogLevel.ToString().ToUpper()}");

            // Create process terminator that handles process termination signals.

            terminator = new ProcessTerminator(log);

            try
            {
                // Establish the cluster connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    var secrets = new DebugSecrets();

                    // NOTE: 
                    //
                    // Add your target cluster's Vault credentials here for 
                    // manual debugging.  Take care not to commit sensitive
                    // credentials for production clusters.
                    //
                    // You'll find this information in the ROOT cluster login
                    // for the target cluster.

                    secrets.Add("neon-cluster-manager-vaultkeys",
                        new VaultCredentials()
                        {
                            RootToken    = "876d0814-76c0-1d6b-c044-e586c5b41dd7",
                            KeyThreshold = 1,
                            UnsealKeys   = new List<string>()
                            {
                                "VzHweeYWWOk7hklPmSDNctySSRjFU8rm7Ao3HOhci8k="
                            }
                        });

                    NeonClusterHelper.OpenRemoteCluster(secrets);
                }
                else
                {
                    NeonClusterHelper.OpenCluster();
                }

                cluster = NeonClusterHelper.Cluster;

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

                //if (ThisAssembly.Git.IsDirty)
                //{
                //    version += "-DIRTY";
                //}

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

            if (!await consul.KV.Exists(logPollSecondsKey))
            {
                log.LogInfo($"Persisting setting [{logPollSecondsKey}=300.0]");
                await consul.KV.PutDouble(logPollSecondsKey, 300);
            }

            nodePollInterval    = TimeSpan.FromSeconds(await consul.KV.GetDouble(nodePollSecondsKey));
            vaultPollInterval   = TimeSpan.FromSeconds(await consul.KV.GetDouble(vaultPollSecondsKey));
            managerPollInterval = TimeSpan.FromSeconds(await consul.KV.GetDouble(managerPollSecondsKey));
            logPollInterval     = TimeSpan.FromSeconds(await consul.KV.GetDouble(logPollSecondsKey));

            log.LogInfo(() => $"Using setting [{nodePollSecondsKey}={nodePollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{vaultPollSecondsKey}={vaultPollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{managerPollSecondsKey}={managerPollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{logPollSecondsKey}={logPollInterval.TotalSeconds}]");

            // Parse the Vault credentials from the [neon-cluster-manager-vaultkeys] 
            // secret, if it exists.

            var vaultCredentialsJson = NeonClusterHelper.GetSecret("neon-cluster-manager-vaultkeys");

            if (string.IsNullOrWhiteSpace(vaultCredentialsJson))
            {
                log.LogInfo(() => "Vault AUTO-UNSEAL is DISABLED because [neon-cluster-manager-vaultkeys] Docker secret is not specified.");
            }
            else
            {
                try
                {
                    vaultCredentials = NeonHelper.JsonDeserialize<VaultCredentials>(vaultCredentialsJson);

                    log.LogInfo(() => "Vault AUTO-UNSEAL is ENABLED.");
                }
                catch (Exception e)
                {
                    log.LogError("Vault AUTO-UNSEAL is DISABLED because the [neon-cluster-manager-vaultkeys] Docker secret could not be parsed.", e);
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

            // Start a task that checks for Elasticsearch [logstash] and [metricbeat] indexes
            // that are older than the number of retention days.

            tasks.Add(LogPurgerAsync());

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

            // Vault runs on the cluster managers so add a URI for each manager.
            // Note that we also need to ensure that each Vault manager hostname
            // has an entry in [/etc/hosts].

            var clusterNodes = await docker.NodeListAsync();
            var hosts        = File.ReadAllText("/etc/hosts");

            foreach (var managerNode in clusterNodes.Where(n => n.Role == "manager")
                .OrderBy(n => n.Hostname))
            {
                var vaultHostname = $"{managerNode.Hostname}.neon-vault.cluster";

                vaultUris.Add($"https://{vaultHostname}:{NeonHostPorts.ProxyVault}");

                if (!hosts.Contains($"{vaultHostname} "))
                {
                    File.AppendAllText("/etc/hosts", $"{vaultHostname} {managerNode.Addr}\n");
                }
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
                    log.LogDebug(() => "NODE-POLLER Polling");

                    if (terminator.CancellationToken.IsCancellationRequested)
                    {
                        log.LogDebug(() => "NODE-POLLER Terminating");
                        return;
                    }

                    // Retrieve the current cluster definition from Consul if we don't already
                    // have it or if it's different from what we've cached.

                    cachedClusterDefinition = await NeonClusterHelper.GetDefinitionAsync(cachedClusterDefinition, terminator.CancellationToken);

                    // Retrieve the swarm nodes from Docker.

                    log.LogDebug(() => $"NODE-POLLER Querying [{docker.Settings.Uri}]");

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

                    log.LogDebug(() => $"NODE-POLLER [{currentClusterDefinition.Managers.Count()}] managers and [{currentClusterDefinition.Workers.Count()}] workers in current cluster definition.");

                    // Cluster pets are not part of the Swarm, so Docker won't return any information
                    // about them.  We'll read the pet definitions from [neon/cluster/pets-definition] in 
                    // Consul.  We'll assume that there are no pets if this key doesn't exist for
                    // backwards compatibility and robustness.

                    var petsJson = await NeonClusterHelper.Consul.KV.GetStringOrDefault($"neon/cluster/{NeonClusterGlobals.PetsDefinition}", terminator.CancellationToken);

                    if (petsJson == null)
                    {
                        log.LogDebug(() => $"NODE-POLLER [neon/cluster/{NeonClusterGlobals.PetsDefinition}] Consul key not found.  Assuming no pets.");
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(petsJson))
                        {
                            // Parse the pet node definitions and add them to the cluster definition.

                            var petDefinitions = NeonHelper.JsonDeserialize<Dictionary<string, NodeDefinition>>(petsJson);

                            foreach (var item in petDefinitions)
                            {
                                currentClusterDefinition.NodeDefinitions.Add(item.Key, item.Value);
                            }

                            log.LogDebug(() => $"NODE-POLLER [neon/cluster/{NeonClusterGlobals.PetsDefinition}] defines [{petDefinitions.Count}] pets.");
                        }
                        else
                        {
                            log.LogDebug(() => $"NODE-POLLER [neon/cluster/{NeonClusterGlobals.PetsDefinition}] is empty.");
                        }
                    }

                    // Determine if the definition has changed.

                    currentClusterDefinition.ComputeHash();

                    if (currentClusterDefinition.Hash != cachedClusterDefinition.Hash)
                    {
                        log.LogInfo(() => "NODE-POLLER Changed cluster definition.  Updating Consul.");

                        await NeonClusterHelper.PutDefinitionAsync(currentClusterDefinition, cancellationToken: terminator.CancellationToken);

                        cachedClusterDefinition = currentClusterDefinition;
                    }
                    else
                    {
                        log.LogDebug(() => "NODE-POLLER Unchanged cluster definition.");
                    }
                }
                catch (OperationCanceledException)
                {
                    log.LogDebug(() => "NODE-POLLER Terminating");
                    return;
                }
                catch (KeyNotFoundException)
                {
                    // We'll see this when no cluster definition has been persisted to the
                    // cluster.  This is a serious problem.  This is configured during setup
                    // and there should always be a definition in Consul.

                    log.LogError(() => $"NODE-POLLER No cluster definition has been found at [{clusterDefKey}] in Consul.  This is a serious error that will have to be corrected manually.");
                }
                catch (Exception e)
                {
                    log.LogError("NODE-POLLER", e);
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
            var lastVaultStatus = (VaultHealthStatus)null;

            // We're going to periodically log Vault status even
            // when there is no status changes.

            var statusUpdateTimeUtc  = DateTime.UtcNow;
            var statusUpdateInterval = TimeSpan.FromMinutes(30);

            log.LogInfo(() => $"VAULT: Opening [{vaultUri}]");

            using (var vault = VaultClient.OpenWithToken(new Uri(vaultUri)))
            {
                while (true)
                {
                    await Task.Delay(vaultPollInterval, terminator.CancellationToken);

                    try
                    {
                        log.LogDebug(() => $"VAULT: Polling [{vaultUri}]");

                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.LogDebug(() => $"VAULT: Terminating [{vaultUri}]");
                            return;
                        }

                        // Monitor Vault for status changes and handle unsealing if enabled.

                        log.LogDebug(() => $"VAULT: Querying [{vaultUri}]");

                        var newVaultStatus     = await vault.GetHealthAsync(terminator.CancellationToken);
                        var autoUnsealDisabled = consul.KV.GetBoolOrDefault($"{NeonClusterConst.ClusterGlobalsKey}/{NeonClusterGlobals.DisableAutoUnseal}").Result;
                        var changed            = false;

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
                                log.LogError(() => $"VAULT: status CHANGED [{vaultUri}]");
                            }
                            else
                            {
                                log.LogInfo(() => $"VAULT: status CHANGED [{vaultUri}]");
                            }

                            statusUpdateTimeUtc = DateTime.UtcNow; // Force logging status below
                        }

                        if (DateTime.UtcNow >= statusUpdateTimeUtc)
                        {
                            if (!newVaultStatus.IsInitialized || newVaultStatus.IsSealed)
                            {
                                log.LogError(() => $"VAULT: status={newVaultStatus} [{vaultUri}]");
                            }
                            else
                            {
                                log.LogInfo(() => $"VAULT: status={newVaultStatus} [{vaultUri}]");
                            }

                            if (newVaultStatus.IsSealed && autoUnsealDisabled)
                            {
                                log.LogInfo(() => $"Vault AUTO-UNSEAL is temporarily DISABLED because Consul [{NeonClusterConst.ClusterGlobalsKey}/{NeonClusterGlobals.DisableAutoUnseal}=true].");
                            }

                            statusUpdateTimeUtc = DateTime.UtcNow + statusUpdateInterval;
                        }

                        lastVaultStatus = newVaultStatus;

                        // Attempt to unseal the Vault if it's sealed and we have the keys.

                        if (newVaultStatus.IsSealed && vaultCredentials != null)
                        {
                            if (autoUnsealDisabled)
                            {
                                continue;   // Don't unseal.
                            }

                            try
                            {
                                log.LogInfo(() => $"VAULT: UNSEALING [{vaultUri}]");
                                await vault.UnsealAsync(vaultCredentials, terminator.CancellationToken);
                                log.LogInfo(() => $"VAULT: UNSEALED [{vaultUri}]");

                                // Schedule a status update on the next loop
                                // and then loop immediately so we'll log the
                                // updated status.

                                statusUpdateTimeUtc = DateTime.UtcNow;
                                continue;
                            }
                            catch (Exception e)
                            {
                                log.LogError($"VAULT: Unseal failed [{vaultUri}]", e);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        log.LogDebug(() => $"VAULT: Terminating [{vaultUri}]");
                        return;
                    }
                    catch (Exception e)
                    {
                        log.LogError($"VAULT: [{vaultUri}]", e);
                    }
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
                        log.LogDebug(() => "MANAGER: Terminating.");
                        return;
                    }

                    log.LogDebug(() => "MANAGER: Polling for cluster manager changes.");

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
                        log.LogInfo("MANAGER: Detected one or more cluster manager node changes.");
                        log.LogInfo("MANAGER: Exiting the service so that Docker will restart it to pick up the manager node changes.");
                        terminator.Exit();
                    }
                    else
                    {
                        log.LogDebug(() => "MANAGER: No manager changes detected.");
                    }
                }
                catch (OperationCanceledException)
                {
                    log.LogDebug(() => "MANAGER: Terminating.");
                    return;
                }
                catch (Exception e)
                {
                    log.LogError($"MANAGER", e);
                }

                await Task.Delay(vaultPollInterval, terminator.CancellationToken);
            }
        }

        /// <summary>
        /// Handles purging of old <b>logstash</b> and <b>metricbeat</b> Elasticsearch indexes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task LogPurgerAsync()
        {
            using (var jsonClient = new JsonClient())
            {
                while (true)
                {
                    await Task.Delay(logPollInterval);

                    var manager = cluster.GetHealthyManager();

                    try
                    {
                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.LogDebug(() => "LOG-PURGE: Terminating.");
                            return;
                        }

                        log.LogDebug(() => "LOG-PURGE: Scanning for old Elasticsearch indexes ready for removal.");

                        // We're going to list the indexes and look for [logstash]
                        // and [metricbeat] indexes that encode the index date like:
                        //
                        //      logstash-2018.06.06
                        //      metricbeat-6.1.1-2018.06.06
                        //
                        // The date is simply encodes the day covered by the index.
                        // We're going to remove any indexes that are at least one
                        // day older than the current date so that we won't delete
                        // partial days logs.

                        var utcNow           = DateTime.UtcNow;
                        var deleteBeforeDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day) - TimeSpan.FromDays(1);

                        var indexList = await jsonClient.GetAsync<JObject>($"http://{manager.PrivateAddress}:{NeonHostPorts.ProxyPrivateHttpLogEsData}/_aliases");

                        foreach (var indexProperty in indexList.Properties())
                        {
                            var indexName = indexProperty.Name;

                            // We're only purging [logstash] and [metricbeat] indexes.

                            if (!indexName.StartsWith("logstash-") && !indexName.StartsWith("metricbeat-"))
                            {
                                continue;
                            }

                            // Extract the date from the index name.

                            var pos = indexName.LastIndexOf('-');

                            if (pos == -1)
                            {
                                log.LogWarn(() => $"LOG-PURGE: Cannot extract date from index named [{indexName}].");
                                continue;
                            }

                            var date      = indexName.Substring(pos + 1);
                            var fields    = date.Split('.');
                            var indexDate = default(DateTime); ;

                            try
                            {
                                indexDate = new DateTime(int.Parse(fields[0]), int.Parse(fields[1]), int.Parse(fields[2]));
                            }
                            catch
                            {
                                log.LogWarn(() => $"LOG-PURGE: Cannot extract date from index named [{indexName}].");
                                continue;
                            }

                            if (indexDate < deleteBeforeDate)
                            {
                                log.LogInfo(() => $"LOG-PURGE: Deleting index [{indexName}].");
                                await jsonClient.DeleteAsync<JObject>($"http://{manager.PrivateAddress}:{NeonHostPorts.ProxyPrivateHttpLogEsData}/{indexName}");
                                log.LogInfo(() => $"LOG-PURGE: [{indexName}] was deleted.");
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        log.LogDebug(() => "LOG-PURGE: Terminating.");
                        return;
                    }
                    catch (Exception e)
                    {
                        log.LogError($"LOG-PURGE", e);
                    }

                    await Task.Delay(vaultPollInterval, terminator.CancellationToken);
                }
            }
        }
    }
}