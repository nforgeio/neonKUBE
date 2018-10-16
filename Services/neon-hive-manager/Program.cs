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
using Neon.Tasks;

namespace NeonHiveManager
{
    /// <summary>
    /// Implements the <b>neon-hive-manager</b> service.  See 
    /// <a href="https://hub.docker.com/r/nhive/neon-hive-manager/">nhive/neon-hive-manager</a>
    /// for more information.
    /// </summary>
    public static partial class Program
    {
        private static readonly string serviceName               = $"neon-hive-manager:{GitVersion}";
        private static readonly string hiveDefinitionKey         = $"{HiveConst.GlobalKey}/{HiveGlobals.DefinitionDeflate}";

        private static readonly string serviceRootKey            = "neon/service/neon-hive-manager";
        private static readonly string hivemqMaintainSecondsKey  = $"{serviceRootKey}/hivemq_maintain_seconds";
        private static readonly string logPurgeSecondsKey        = $"{serviceRootKey}/log_purge_seconds";
        private static readonly string managerTopologySecondsKey = $"{serviceRootKey}/manager_topology_seconds";
        private static readonly string proxyNotifySecondsKey     = $"{serviceRootKey}/proxy_notify_seconds";
        private static readonly string secretPurgeSecondsKey     = $"{serviceRootKey}/secret_purge_seconds";
        private static readonly string swarmPollSecondsKey       = $"{serviceRootKey}/swarm_poll_seconds";
        private static readonly string vaultUnsealSecondsKey     = $"{serviceRootKey}/vault_unseal_seconds";

        private static ProcessTerminator        terminator;
        private static INeonLogger              log;
        private static HiveProxy                hive;
        private static ConsulClient             consul;
        private static DockerClient             docker;
        private static VaultCredentials         vaultCredentials;

        private static BroadcastChannel         proxyNotifyChannel;
        private static TimeSpan                 hivemqMantainInterval;
        private static TimeSpan                 logPurgerInterval;
        private static TimeSpan                 managerTopologyInterval;
        private static TimeSpan                 proxyNotifyInterval;
        private static TimeSpan                 secretPurgeInterval;
        private static TimeSpan                 swarmPollInterval;
        private static TimeSpan                 vaultUnsealInterval;

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

            // Create process terminator to handle process termination signals.

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

            if (!await consul.KV.Exists(hivemqMaintainSecondsKey))
            {
                log.LogInfo($"Persisting setting [{hivemqMaintainSecondsKey}=60.0]");
                await consul.KV.PutDouble(hivemqMaintainSecondsKey, 60);
            }

            if (!await consul.KV.Exists(logPurgeSecondsKey))
            {
                log.LogInfo($"Persisting setting [{logPurgeSecondsKey}=300.0]");
                await consul.KV.PutDouble(logPurgeSecondsKey, 300);
            }

            if (!await consul.KV.Exists(managerTopologySecondsKey))
            {
                log.LogInfo($"Persisting setting [{managerTopologySecondsKey}=300.0]");
                await consul.KV.PutDouble(managerTopologySecondsKey, 1800);
            }

            if (!await consul.KV.Exists(proxyNotifySecondsKey))
            {
                log.LogInfo($"Persisting setting [{proxyNotifySecondsKey}=300.0]");
                await consul.KV.PutDouble(proxyNotifySecondsKey, 300);
            }

            if (!await consul.KV.Exists(secretPurgeSecondsKey))
            {
                log.LogInfo($"Persisting setting [{secretPurgeSecondsKey}=300.0]");
                await consul.KV.PutDouble(secretPurgeSecondsKey, 300);
            }

            if (!await consul.KV.Exists(swarmPollSecondsKey))
            {
                log.LogInfo($"Persisting setting [{swarmPollSecondsKey}=30.0]");
                await consul.KV.PutDouble(swarmPollSecondsKey, 30.0);
            }

            if (!await consul.KV.Exists(vaultUnsealSecondsKey))
            {
                log.LogInfo($"Persisting setting [{vaultUnsealSecondsKey}=30.0]");
                await consul.KV.PutDouble(vaultUnsealSecondsKey, 30.0);
            }

            hivemqMantainInterval   = TimeSpan.FromSeconds(await consul.KV.GetDouble(hivemqMaintainSecondsKey));
            logPurgerInterval       = TimeSpan.FromSeconds(await consul.KV.GetDouble(logPurgeSecondsKey));
            managerTopologyInterval = TimeSpan.FromSeconds(await consul.KV.GetDouble(managerTopologySecondsKey));
            proxyNotifyInterval     = TimeSpan.FromSeconds(await consul.KV.GetDouble(proxyNotifySecondsKey));
            secretPurgeInterval     = TimeSpan.FromSeconds(await consul.KV.GetDouble(secretPurgeSecondsKey));
            swarmPollInterval       = TimeSpan.FromSeconds(await consul.KV.GetDouble(swarmPollSecondsKey));
            vaultUnsealInterval     = TimeSpan.FromSeconds(await consul.KV.GetDouble(vaultUnsealSecondsKey));

            log.LogInfo(() => $"Using setting [{hivemqMaintainSecondsKey}={hivemqMantainInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{logPurgeSecondsKey}={logPurgerInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{managerTopologySecondsKey}={managerTopologyInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{proxyNotifySecondsKey}={proxyNotifyInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{secretPurgeSecondsKey}={secretPurgeInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{swarmPollSecondsKey}={swarmPollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{vaultUnsealSecondsKey}={vaultUnsealInterval.TotalSeconds}]");

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

            // Start a task that handles HiveMQ related activities like ensuring that
            // the [sysadmin] account has full permissions for all virtual hosts.

            tasks.Add(HiveMQMaintainerAsync());

            // Start a task that checks for Elasticsearch [logstash] and [metricbeat] indexes
            // that are older than the number of retention days.

            tasks.Add(LogPurgerAsync());

            // Start a task that periodically checks for changes to the set of hive managers 
            // (e.g. if a manager is added or removed).  This task will cause the service to exit
            // so it can be restarted automatically by Docker to respond to the change.

            tasks.Add(ManagerWatcherAsync());

            // Start a task that checks for old [neon-secret-retriever-*] service instances
            // as well as old persisted secrets and removes them.

            tasks.Add(SecretPurgerAsync());

            // Start a task that polls current hive state to update the hive definition in Consul, etc.

            tasks.Add(SwarmPollerAsync());

            // Start a task that periodically notifies the [neon-proxy-manager] service
            // that it should proactively rebuild the proxy configurations.

            tasks.Add(ProxyUpdaterAsync());

            // We need to start a vault poller for the Vault instance running on each manager
            // node.  We're going to construct the direct Vault URIs by querying Docker for
            // the current hive nodes and looking for the managers.

            vaultUris = await GetVaultUrisAsync();

            foreach (var uri in vaultUris)
            {
                tasks.Add(VaultUnsealerAsync(uri));
            }

            // Wait for all tasks to exit cleanly for a normal shutdown.

            await NeonHelper.WaitAllAsync(tasks);

            terminator.ReadyToExit();
        }
    }
}