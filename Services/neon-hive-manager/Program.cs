//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using EasyNetQ.Management.Client.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.HiveMQ;
using Neon.Net;

namespace NeonHiveManager
{
    /// <summary>
    /// Implements the <b>neon-hive-manager</b> service.  See 
    /// <a href="https://hub.docker.com/r/nhive/neon-hive-manager/">nhive/neon-hive-manager</a>
    /// for more information.
    /// </summary>
    public static class Program
    {
        private static readonly string serviceName           = $"neon-hive-manager:{GitVersion}";
        private static readonly string serviceRootKey        = "neon/service/neon-hive-manager";
        private static readonly string nodePollSecondsKey    = $"{serviceRootKey}/node_poll_seconds";
        private static readonly string vaultPollSecondsKey   = $"{serviceRootKey}/vault_poll_seconds";
        private static readonly string managerPollSecondsKey = $"{serviceRootKey}/manager_poll_seconds";
        private static readonly string secretPollSecondsKey  = $"{serviceRootKey}/secret_poll_seconds";
        private static readonly string logPollSecondsKey     = $"{serviceRootKey}/log_poll_seconds";
        private static readonly string proxyUpdateSecondsKey = $"{serviceRootKey}/proxy_update_seconds";
        private static readonly string hivemqPollSecondsKey  = $"{serviceRootKey}/hivemq_poll_seconds";
        private static readonly string hiveDefinitionKey     = $"{HiveConst.GlobalKey}/{HiveGlobals.DefinitionDeflate}";

        private static ProcessTerminator        terminator;
        private static INeonLogger              log;
        private static HiveProxy                hive;
        private static ConsulClient             consul;
        private static DockerClient             docker;
        private static VaultCredentials         vaultCredentials;
        private static BroadcastChannel         proxyNotifyChannel;
        private static TimeSpan                 nodePollInterval;
        private static TimeSpan                 vaultPollInterval;
        private static TimeSpan                 managerPollInterval;
        private static TimeSpan                 logPollInterval;
        private static TimeSpan                 secretPollInterval;
        private static TimeSpan                 proxyUpdateInterval;
        private static TimeSpan                 hivemqPollInterval;
        private static HiveDefinition           cachedHiveDefinition;
        private static List<string>             vaultUris;

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));
            log.LogInfo(() => $"Starting [{serviceName}]");
            log.LogInfo(() => $"LOG_LEVEL={LogManager.Default.LogLevel.ToString().ToUpper()}");

            // Create process terminator that handles process termination signals.

            terminator = new ProcessTerminator(log);

            try
            {
                // Establish the hive connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    var secrets = new DebugSecrets();

                    // NOTE: 
                    //
                    // Add your target hive's Vault credentials here for 
                    // manual debugging.  Take care not to commit sensitive
                    // credentials for production hives.
                    //
                    // You'll find this information in the ROOT hive login
                    // for the target hive.

                    secrets.Add("neon-hive-manager-vaultkeys",
                        new VaultCredentials()
                        {
                            RootToken    = "cd5831fa-86ec-cc22-b1f3-051f88147382",
                            KeyThreshold = 1,
                            UnsealKeys   = new List<string>()
                            {
                                "8SgwdO/GwqJ7nyxT2tK2n1CCR3084kQVh7gEy8jNQh8="
                            }
                        });

                    hive = HiveHelper.OpenHiveRemote(secrets);
                }
                else
                {
                    hive = HiveHelper.OpenHive(sshCredentialsSecret: "neon-ssh-credentials");
                }

                // Ensure that we're running on a manager node.  We won't be able
                // to query swarm status otherwise.

                var nodeRole = Environment.GetEnvironmentVariable("NEON_NODE_ROLE");

                if (string.IsNullOrEmpty(nodeRole))
                {
                    log.LogCritical(() => "Service does not appear to be running on a neonHIVE.");
                    Program.Exit(1);
                }

                if (!string.Equals(nodeRole, NodeRole.Manager, StringComparison.OrdinalIgnoreCase))
                {
                    log.LogCritical(() => $"[neon-hive-manager] service is running on a [{nodeRole}] hive node.  Running on only [{NodeRole.Manager}] nodes are supported.");
                    Program.Exit(1);
                }

                // Open the hive data services and then start the main service task.

                log.LogDebug(() => $"Connecting: Consul");

                using (consul = HiveHelper.OpenConsul())
                {
                    log.LogDebug(() => $"Connecting: Docker");

                    using (docker = HiveHelper.OpenDocker())
                    {
                        log.LogInfo(() => $"Connecting: {HiveMQChannels.ProxyNotify} channel");

                        // NOTE:
                        //
                        // We're passing [useBootstrap=true] here so that the HiveMQ client will
                        // connect directly to the HiveMQ cluster nodes as opposed to routing
                        // traffic through the private load balancer.  This is necessary because
                        // the load balancers rely on HiveMQ to broadcast update notifications.
                        //
                        // One consequence of this is that this service will need to be restarted
                        // whenever HiveMQ instances are relocated to different hive hosts.

                        // $todo(jeff.lill):
                        //
                        // This service will need to be restarted whenever future code provides
                        // for relocating HiveMQ instances or when hive nodes hosting HiveMQ
                        // are added or removed.
                        //
                        //      https://github.com/jefflill/NeonForge/issues/337

                        using (proxyNotifyChannel = hive.HiveMQ.Internal.GetProxyNotifyChannel(useBootstrap: true))
                        {
                            await RunAsync();
                        }
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
                HiveHelper.CloseHive();
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

            if (!await consul.KV.Exists(secretPollSecondsKey))
            {
                log.LogInfo($"Persisting setting [{secretPollSecondsKey}=300.0]");
                await consul.KV.PutDouble(secretPollSecondsKey, 300);
            }

            if (!await consul.KV.Exists(proxyUpdateSecondsKey))
            {
                log.LogInfo($"Persisting setting [{proxyUpdateSecondsKey}=300.0]");
                await consul.KV.PutDouble(proxyUpdateSecondsKey, 300);
            }

            if (!await consul.KV.Exists(hivemqPollSecondsKey))
            {
                log.LogInfo($"Persisting setting [{hivemqPollSecondsKey}=60.0]");
                await consul.KV.PutDouble(hivemqPollSecondsKey, 60);
            }

            nodePollInterval    = TimeSpan.FromSeconds(await consul.KV.GetDouble(nodePollSecondsKey));
            vaultPollInterval   = TimeSpan.FromSeconds(await consul.KV.GetDouble(vaultPollSecondsKey));
            managerPollInterval = TimeSpan.FromSeconds(await consul.KV.GetDouble(managerPollSecondsKey));
            logPollInterval     = TimeSpan.FromSeconds(await consul.KV.GetDouble(logPollSecondsKey));
            secretPollInterval  = TimeSpan.FromSeconds(await consul.KV.GetDouble(secretPollSecondsKey));
            proxyUpdateInterval = TimeSpan.FromSeconds(await consul.KV.GetDouble(proxyUpdateSecondsKey));
            hivemqPollInterval  = TimeSpan.FromSeconds(await consul.KV.GetDouble(hivemqPollSecondsKey));

            log.LogInfo(() => $"Using setting [{nodePollSecondsKey}={nodePollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{vaultPollSecondsKey}={vaultPollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{managerPollSecondsKey}={managerPollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{logPollSecondsKey}={logPollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{secretPollSecondsKey}={secretPollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{proxyUpdateSecondsKey}={proxyUpdateInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{hivemqPollSecondsKey}={hivemqPollInterval.TotalSeconds}]");

            // Parse the Vault credentials from the [neon-hive-manager-vaultkeys] 
            // secret, if it exists.

            var vaultCredentialsJson = HiveHelper.GetSecret("neon-hive-manager-vaultkeys");

            if (string.IsNullOrWhiteSpace(vaultCredentialsJson))
            {
                log.LogInfo(() => "Vault AUTO-UNSEAL is DISABLED because [neon-hive-manager-vaultkeys] Docker secret is not specified.");
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
                    log.LogError("Vault AUTO-UNSEAL is DISABLED because the [neon-hive-manager-vaultkeys] Docker secret could not be parsed.", e);
                }
            }

            // Launch the sub-tasks.  These will run until the service is terminated.

            var tasks = new List<Task>();

            // Start a task that polls current hive state to update the hive definition in Consul, etc.

            tasks.Add(StatePollerAsync());

            // We need to start a vault poller for the Vault instance running on each manager
            // node.  We're going to construct the direct Vault URIs by querying Docker for
            // the current hive nodes and looking for the managers.

            vaultUris = await GetVaultUrisAsync();

            foreach (var uri in vaultUris)
            {
                tasks.Add(VaultPollerAsync(uri));
            }

            // Start a task that periodically checks for changes to the set of hive managers 
            // (e.g. if a manager is added or removed).  This task will cause the service to exit
            // so it can be restarted automatically by Docker to respond to the change.

            tasks.Add(ManagerPollerAsync());

            // Start a task that checks for Elasticsearch [logstash] and [metricbeat] indexes
            // that are older than the number of retention days.

            tasks.Add(LogPurgerAsync());

            // Start a task that checks for old [neon-secret-retriever-*] service instances
            // as well as old persisted secrets and removes them.

            tasks.Add(SecretPurgerAsync());

            // Start a task that periodically notifies the [neon-proxy-manager] service
            // that it should proactively rebuild the proxy configurations.

            tasks.Add(ProxyUpdaterAsync());

            // Start a task that handles HiveMQ related activities like ensuring that
            // the [sysadmin] account has full permissions for all virtual hosts.

            tasks.Add(HiveMQMaintenanceAsync());

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

            // Vault runs on the hive managers so add a URI for each manager.
            // Note that we also need to ensure that each Vault manager hostname
            // has an entry in [/etc/hosts].
            //
            // Note that we need to use the direct Vault port rather than the 
            // Vault proxy port because we need to be able to address these
            // individually.

            var swarmNodes = await docker.NodeListAsync();
            var hosts      = File.ReadAllText("/etc/hosts");

            foreach (var managerNode in swarmNodes.Where(n => n.Role == "manager")
                .OrderBy(n => n.Hostname))
            {
                var vaultHostname = $"{managerNode.Hostname}.{hive.Definition.Hostnames.Vault}";

                vaultUris.Add($"https://{vaultHostname}:{NetworkPorts.Vault}");

                if (!hosts.Contains($"{vaultHostname} "))
                {
                    File.AppendAllText("/etc/hosts", $"{vaultHostname} {managerNode.Addr}\n");
                }
            }

            return vaultUris;
        }

        /// <summary>
        /// Handles polling of Docker swarm about the hive nodes and updating the hive
        /// definition and hash when changes are detected.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task StatePollerAsync()
        {
            while (true)
            {
                try
                {
                    try
                    {
                        log.LogDebug(() => "STATE-POLLER: Polling");

                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.LogDebug(() => "STATE-POLLER: Terminating");
                            return;
                        }

                        // Retrieve the current hive definition from Consul if we don't already
                        // have it or if it's different from what we've cached.

                        cachedHiveDefinition = await HiveHelper.GetDefinitionAsync(cachedHiveDefinition, terminator.CancellationToken);

                        // Retrieve the swarm nodes from Docker.

                        log.LogDebug(() => $"STATE-POLLER: Querying [{docker.Settings.Uri}]");

                        var swarmNodes = await docker.NodeListAsync();

                        // Parse the node definitions from the swarm nodes and build a new definition with
                        // using the new nodes.  Then compare the hashes of the cached and new hive definitions
                        // and then update Consul if they're different.

                        var currentHiveDefinition = NeonHelper.JsonClone<HiveDefinition>(cachedHiveDefinition);

                        currentHiveDefinition.NodeDefinitions.Clear();

                        foreach (var swarmNode in swarmNodes)
                        {
                            var nodeDefinition = NodeDefinition.ParseFromLabels(swarmNode.Labels);

                            nodeDefinition.Name = swarmNode.Hostname;

                            currentHiveDefinition.NodeDefinitions.Add(nodeDefinition.Name, nodeDefinition);
                        }

                        log.LogDebug(() => $"STATE-POLLER: [{currentHiveDefinition.Managers.Count()}] managers and [{currentHiveDefinition.Workers.Count()}] workers in current hive definition.");

                        // Hive pets are not part of the Swarm, so Docker won't return any information
                        // about them.  We'll read the pet definitions from [neon/global/pets-definition] in 
                        // Consul.  We'll assume that there are no pets if this key doesn't exist for
                        // backwards compatibility and robustness.

                        var petsJson = await HiveHelper.Consul.KV.GetStringOrDefault($"{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}", terminator.CancellationToken);

                        if (petsJson == null)
                        {
                            log.LogDebug(() => $"STATE-POLLER: [{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}] Consul key not found.  Assuming no pets.");
                        }
                        else
                        {
                            if (!string.IsNullOrWhiteSpace(petsJson))
                            {
                                // Parse the pet node definitions and add them to the hive definition.

                                var petDefinitions = NeonHelper.JsonDeserialize<Dictionary<string, NodeDefinition>>(petsJson);

                                foreach (var item in petDefinitions)
                                {
                                    currentHiveDefinition.NodeDefinitions.Add(item.Key, item.Value);
                                }

                                log.LogDebug(() => $"STATE-POLLER: [{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}] defines [{petDefinitions.Count}] pets.");
                            }
                            else
                            {
                                log.LogDebug(() => $"STATE-POLLER: [{HiveConst.GlobalKey}/{HiveGlobals.PetsDefinition}] is empty.");
                            }
                        }

                        // Fetch the hive summary and add it to the hive definition.

                        currentHiveDefinition.Summary = HiveSummary.FromHive(hive, currentHiveDefinition);

                        // Determine if the definition has changed.

                        currentHiveDefinition.ComputeHash();

                        if (currentHiveDefinition.Hash != cachedHiveDefinition.Hash)
                        {
                            log.LogInfo(() => "STATE-POLLER: Hive definition has CHANGED.  Updating Consul.");

                            await HiveHelper.PutDefinitionAsync(currentHiveDefinition, cancellationToken: terminator.CancellationToken);

                            cachedHiveDefinition = currentHiveDefinition;
                        }
                        else
                        {
                            log.LogDebug(() => "STATE-POLLER: Hive definition is UNCHANGED.");
                        }
                    }
                    catch (KeyNotFoundException)
                    {
                        // We'll see this when no hive definition has been persisted to the
                        // hive.  This is a serious problem.  This is configured during setup
                        // and there should always be a definition in Consul.

                        log.LogError(() => $"STATE-POLLER: No hive definition has been found at [{hiveDefinitionKey}] in Consul.  This is a serious error that will have to be corrected manually.");
                    }
                    catch (Exception e)
                    {
                        if (!(e is OperationCanceledException))
                        {
                            log.LogError("STATE-POLLER", e);
                        }
                    }

                    await Task.Delay(nodePollInterval, terminator.CancellationToken);
                 }
                catch (OperationCanceledException)
                {
                    log.LogDebug(() => "STATE-POLLER: Terminating");
                    return;
                }
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

            log.LogInfo(() => $"VAULT-POLLER: Opening [{vaultUri}]");

            using (var vault = VaultClient.OpenWithToken(new Uri(vaultUri)))
            {
                while (true)
                {
                    try
                    {
                        await Task.Delay(vaultPollInterval, terminator.CancellationToken);

                        try
                        {
                            log.LogDebug(() => $"VAULT-POLLER: Polling [{vaultUri}]");

                            if (terminator.CancellationToken.IsCancellationRequested)
                            {
                                log.LogDebug(() => $"VAULT: Terminating [{vaultUri}]");
                                return;
                            }

                            // Monitor Vault for status changes and handle unsealing if enabled.

                            log.LogDebug(() => $"VAULT-POLLER: Querying [{vaultUri}]");

                            var newVaultStatus     = await vault.GetHealthAsync(terminator.CancellationToken);
                            var autoUnsealDisabled = consul.KV.GetBoolOrDefault($"{HiveConst.GlobalKey}/{HiveGlobals.UserDisableAutoUnseal}").Result;
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
                                    log.LogError(() => $"VAULT-POLLER: status CHANGED [{vaultUri}]");
                                }
                                else
                                {
                                    log.LogInfo(() => $"VAULT-POLLER: status CHANGED [{vaultUri}]");
                                }

                                statusUpdateTimeUtc = DateTime.UtcNow; // Force logging status below
                            }

                            if (DateTime.UtcNow >= statusUpdateTimeUtc)
                            {
                                if (!newVaultStatus.IsInitialized || newVaultStatus.IsSealed)
                                {
                                    log.LogError(() => $"VAULT-POLLER: status={newVaultStatus} [{vaultUri}]");
                                }
                                else
                                {
                                    log.LogInfo(() => $"VAULT-POLLER: status={newVaultStatus} [{vaultUri}]");
                                }

                                if (newVaultStatus.IsSealed && autoUnsealDisabled)
                                {
                                    log.LogInfo(() => $"VAULT-POLLER: AUTO-UNSEAL is temporarily DISABLED because Consul [{HiveConst.GlobalKey}/{HiveGlobals.UserDisableAutoUnseal}=true].");
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
                                    log.LogInfo(() => $"VAULT-POLLER: UNSEALING [{vaultUri}]");
                                    await vault.UnsealAsync(vaultCredentials, terminator.CancellationToken);
                                    log.LogInfo(() => $"VAULT-POLLER: UNSEALED [{vaultUri}]");

                                    // Schedule a status update on the next loop
                                    // and then loop immediately so we'll log the
                                    // updated status.

                                    statusUpdateTimeUtc = DateTime.UtcNow;
                                    continue;
                                }
                                catch (Exception e)
                                {
                                    if (!(e is OperationCanceledException))
                                    {
                                        log.LogError($"VAULT-POLLER: Unseal failed [{vaultUri}]", e);
                                    }
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            if (!(e is OperationCanceledException))
                            {
                                log.LogError($"VAULT-POLLER: [{vaultUri}]", e);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        log.LogDebug(() => $"VAULT-POLLER: Terminating [{vaultUri}]");
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Handles detection of changes to the hive's manager nodes.  The process will
        /// be terminated when manager nodes are added or removed so that Docker will restart
        /// the service to begin handling the changes.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ManagerPollerAsync()
        {
            while (true)
            {
                try
                {
                    try
                    {
                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.LogDebug(() => "MANAGER-POLLER: Terminating.");
                            return;
                        }

                        log.LogDebug(() => "MANAGER-POLLER: Polling for hive manager changes.");

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
                            log.LogInfo("MANAGER-POLLER: Detected one or more hive manager node changes.");
                            log.LogInfo("MANAGER-POLLER: Exiting the service so that Docker will restart it to pick up the manager node changes.");
                            terminator.Exit();
                        }
                        else
                        {
                            log.LogDebug(() => "MANAGER-POLLER: No manager changes detected.");
                        }
                    }
                    catch (Exception e)
                    {
                        if (!(e is OperationCanceledException))
                        {
                            log.LogError($"MANAGER-POLLER", e);
                        }
                    }

                    // We don't need to poll that often because hive managers
                    // will rarely change.

                    await Task.Delay(managerPollInterval, terminator.CancellationToken);
                }
                catch (OperationCanceledException)
                {
                    log.LogDebug(() => "MANAGER-POLLER: Terminating.");
                    return;
                }
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
                    var manager = hive.GetReachableManager();

                    try
                    {
                        try
                        {
                            if (terminator.CancellationToken.IsCancellationRequested)
                            {
                                log.LogDebug(() => "LOG-PURGER: Terminating.");
                                return;
                            }

                            log.LogDebug(() => "LOG-PURGER: Scanning for old Elasticsearch indexes ready for removal.");

                            // We're going to list the indexes and look for [logstash]
                            // and [metricbeat] indexes that encode the index date like:
                            //
                            //      logstash-2018.06.06
                            //      metricbeat-6.1.1-2018.06.06
                            //
                            // The date is simply encodes the day covered by the index.

                            if (!hive.Globals.TryGetInt(HiveGlobals.UserLogRetentionDays, out var retentionDays))
                            {
                                retentionDays = 14;
                            }

                            var utcNow           = DateTime.UtcNow;
                            var deleteBeforeDate = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day) - TimeSpan.FromDays(retentionDays);

                            var indexList = await jsonClient.GetAsync<JObject>($"http://{manager.PrivateAddress}:{HiveHostPorts.ProxyPrivateHttpLogEsData}/_aliases");

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
                                    log.LogWarn(() => $"LOG-PURGER: Cannot extract date from index named [{indexName}].");
                                    continue;
                                }

                                var date      = indexName.Substring(pos + 1);
                                var fields    = date.Split('.');
                                var indexDate = default(DateTime);

                                try
                                {
                                    indexDate = new DateTime(int.Parse(fields[0]), int.Parse(fields[1]), int.Parse(fields[2]));
                                }
                                catch
                                {
                                    log.LogWarn(() => $"LOG-PURGER: Cannot extract date from index named [{indexName}].");
                                    continue;
                                }

                                if (indexDate < deleteBeforeDate)
                                {
                                    log.LogInfo(() => $"LOG-PURGER: Deleting index [{indexName}].");
                                    await jsonClient.DeleteAsync<JObject>($"http://{manager.PrivateAddress}:{HiveHostPorts.ProxyPrivateHttpLogEsData}/{indexName}");
                                    log.LogInfo(() => $"LOG-PURGER: [{indexName}] was deleted.");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            if (!(e is OperationCanceledException))
                            {
                                log.LogError($"LOG-PURGER", e);
                            }
                        }

                        await Task.Delay(logPollInterval);
                    }
                    catch (OperationCanceledException)
                    {
                        log.LogDebug(() => "LOG-PURGER: Terminating.");
                        return;
                    }
                }
            }
        }

        /// <summary>
        /// Handles purging of old <b>neon-secret-retriever-*</b> service instances as well
        /// as any persisted secrets.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task SecretPurgerAsync()
        {
            while (true)
            {
                try
                {
                    try
                    {
                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.LogDebug(() => "SECRET-PURGER: Terminating.");
                            return;
                        }

                        // Commpute the minimum creation time for the retriever service and
                        // the retrieved Consul key.  We're hardcoding the maximum age to
                        // 30 minutes.

                        var utcNow        = DateTime.UtcNow;
                        var minCreateTime = utcNow - TimeSpan.FromMinutes(30);

                        // Scan for and remove old [neon-service-retriever] services.

                        log.LogDebug(() => "SECRET-PURGER: Scanning for old [neon-secret-retriver] services ready for removal.");

                        var retrieverServices = hive.Docker.ListServices()
                            .Where(l => l.StartsWith("neon-secret-retriever-"))
                            .ToList();

                        if (retrieverServices.Count > 0)
                        {
                            log.LogInfo($"SECRET-PURGER: Discovered [{retrieverServices.Count}] services named like [neon-secret-retriever-*].");

                            foreach (var service in retrieverServices)
                            {
                                // Inspect the service to obtain its creation date.

                                var serviceDetails = hive.Docker.InspectService(service);

                                if (serviceDetails.CreatedAtUtc < minCreateTime)
                                {
                                    log.LogInfo($"Removing service [service].");

                                    var response = hive.GetReachableManager().SudoCommand($"docker service rm {service}");

                                    if (response.ExitCode != 0)
                                    {
                                        throw new HiveException(response.ErrorSummary);
                                    }
                                }
                            }
                        }

                        // Scan for and remove old retrieved secrets persisted as Consul
                        // keys under [neon/service/neon-secret-retriever].

                        log.LogDebug(() => "SECRET-PURGER: Scanning for old [neon-secret-retriver] secrets persisted to Consul.");

                        var secretKeyPaths = consul.KV.ListKeys("neon/service/neon-secret-retriever").Result
                            .Where(k => k.Contains('~'))    // Secret keys use "~" to separate the timestamp and GUID
                            .ToList();

                        if (secretKeyPaths.Count > 0)
                        {
                            log.LogInfo($"SECRET-PURGER: Discovered [{secretKeyPaths.Count}] keys under [neon/service/neon-secret-retriever].");

                            foreach (var keyPath in secretKeyPaths)
                            {
                                // Strip off the leading path to leave only the key.

                                var key          = keyPath;
                                var lastSlashPos = key.LastIndexOf('/');

                                if (lastSlashPos == -1)
                                {
                                    continue;
                                }

                                key = key.Substring(lastSlashPos + 1);

                                // Split the key on the '~' character and parse the first
                                // field as the timestamp.  We're going to ignore keys that
                                // can't be parsed for resilience.
                                //
                                // NOTE: 
                                //
                                // The timestamp replaced colon (:) characters with underscore (_) to
                                // prevent Consul from escaping these so they'll be easier to read.
                                // We need to reverse this before parsing the timestamp.

                                var timestampPart = key.Split('~')[0];

                                timestampPart = timestampPart.Replace('_', ':');

                                if (DateTime.TryParseExact(timestampPart, NeonHelper.DateFormatTZ, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp) &&
                                    timestamp < minCreateTime)
                                {
                                    log.LogInfo($"SECRET-PURGER: Removing Consul key [{keyPath}].");
                                    consul.KV.Delete(keyPath).Wait();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        if (!(e is OperationCanceledException))
                        {
                            log.LogError($"SECRET-PURGER", e);
                        }
                    }

                    await Task.Delay(secretPollInterval);
                }
                catch (OperationCanceledException)
                {
                    log.LogDebug(() => "SECRET-PURGER: Terminating.");
                    return;
                }
            }
        }

        /// <summary>
        /// Periodically broadcasts a <see cref="ProxyRegenerateMessage"/> to the <b>neon-proxy-manager</b>
        /// service which will then regenerate the public and private proxy related configurations.  This
        /// is a fail-safe that ensures that the proxy configurations will eventually converge, even when
        /// proxy change notifications may have been lost somehow.  This also provides an opportunity for
        /// <b>neon-proxy-manager</b> to verify the load balancer rules for correctness and also to check
        /// for expired or expiring TLS certificates so that warnings can be logged.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ProxyUpdaterAsync()
        {
            while (true)
            {
                try
                {
                    try
                    {
                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.LogDebug(() => "PROXY-UPDATER: Terminating.");
                            return;
                        }

                        log.LogInfo(() => $"PROXY-UPDATER: Publish: [{nameof(ProxyRegenerateMessage)}(\"fail-safe\") --> {proxyNotifyChannel.Name}]");
                        proxyNotifyChannel.Publish(new ProxyRegenerateMessage() { Reason = "[neon-hive-manager]: fail-safe" });
                    }
                    catch (Exception e)
                    {
                        if (!(e is OperationCanceledException))
                        {
                            log.LogError($"PROXY-UPDATER", e);
                        }
                    }

                    await Task.Delay(proxyUpdateInterval);
                }
                catch (OperationCanceledException)
                {
                    log.LogDebug(() => "PROXY-UPDATER: Terminating.");
                    return;
                }
            }
        }

        /// <summary>
        /// Periodically performs HiveMQ related maintenance activities such as ensuring
        /// that the [sysadmin] account has full permissions for all virtual hosts.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task HiveMQMaintenanceAsync()
        {
            using (var hivemqManager = hive.HiveMQ.ConnectHiveMQManager())
            {
                while (true)
                {
                    try
                    {
                        try
                        {
                            if (terminator.CancellationToken.IsCancellationRequested)
                            {
                                log.LogDebug(() => "HIVEMQ-MAINTENANCE: Terminating.");
                                return;
                            }

                            log.LogDebug(() => $"HIVEMQ-MAINTENANCE: Checking [{HiveConst.RootUser}] permissions.");

                            // Build the set of virtual hostt names where [sysadmin] already has
                            // full permissions.

                            var sysadminVHosts = new HashSet<string>();

                            foreach (var permissions in await hivemqManager.GetPermissionsAsync())
                            {
                                if (permissions.User == HiveConst.RootUser &&
                                    permissions.Configure == ".*" &&
                                    permissions.Read == ".*" &&
                                    permissions.Write == ".*")
                                {
                                    sysadminVHosts.Add(permissions.Vhost);
                                }
                            }

                            // List the vhosts and set full permissions for [sysadmin] for any
                            // virtual hosts where [sysadmin] doesn't already have full permissions.

                            var sysadminUser          = await hivemqManager.GetUserAsync(HiveConst.RootUser);
                            var addedVHostPermissions = new List<string>();

                            foreach (var vhost in await hivemqManager.GetVHostsAsync())
                            {
                                if (!sysadminVHosts.Contains(vhost.Name))
                                {
                                    addedVHostPermissions.Add(vhost.Name);
                                    await hivemqManager.CreatePermissionAsync(new PermissionInfo(sysadminUser, vhost));
                                }
                            }

                            if (addedVHostPermissions.Count > 0)
                            {
                                var sbVHostList = new StringBuilder();

                                foreach (var vhost in addedVHostPermissions)
                                {
                                    sbVHostList.AppendWithSeparator(vhost, ", ");
                                }

                                log.LogInfo(() => $"Granted [{HiveConst.RootUser}] full permissions for vhosts: {sbVHostList}");
                            }
                        }
                        catch (Exception e)
                        {
                            if (!(e is OperationCanceledException))
                            {
                                log.LogError($"HIVEMQ-MAINTENANCE", e);
                            }
                        }

                        await Task.Delay(hivemqPollInterval);
                    }
                    catch (OperationCanceledException)
                    {
                        log.LogDebug(() => "HIVEMQ-MAINTENANCE: Terminating.");
                        return;
                    }
                }
            }
        }
    }
}