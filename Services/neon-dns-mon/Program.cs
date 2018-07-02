//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using DNS.Client;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft.Json;

using Neon.DnsTools;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.Net;
using Neon.Time;

namespace NeonDnsMon
{
    /// <summary>
    /// Implements the <b>neon-dns-mon</b> service.  See 
    /// <a href="https://hub.docker.com/r/nhive/neon-dns-mon/">nhive/neon-dns-mon</a>
    /// for more information.
    /// </summary>
    public static class Program
    {
        private static readonly string serviceName = $"neon-dns-mon:{GitVersion}";

        private static ProcessTerminator    terminator;
        private static INeonLogger          log;
        private static ConsulClient         consul;
        private static string[]             nameservers;
        private static TimeSpan             pingTimeout;
        private static TimeSpan             pollInterval;
        private static TimeSpan             warnInterval;
        private static HiveDefinition       hiveDefinition;
        private static PolledTimer          warnTimer;
        private static HealthResolver       healthResolver;

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

            // Parse the environment variable settings.

            var environment = new EnvironmentParser(log);

            nameservers  = environment.Get("NAMESERVERS", "8.8.8.8,8.8.4.4").Split(',');
            pingTimeout  = environment.Get("PING_TIMEOUT", TimeSpan.FromSeconds(1.5), validator: v => v > TimeSpan.Zero);
            pollInterval = environment.Get("POLL_INTERVAL", TimeSpan.FromSeconds(5), validator: v => v > TimeSpan.Zero);
            warnInterval = environment.Get("WARN_INTERVAL", TimeSpan.FromMinutes(5), validator: v => v > TimeSpan.Zero);

            // Create a timer so we'll avoid spamming the logs with warnings.

            warnTimer = new PolledTimer(warnInterval, autoReset: true);
            warnTimer.FireNow();    // Set so that the first warnings detected will be reported immediately.

            // Create the object that will actually perform the hostname lookups
            // and health pings.  This object caches things to improve performance.

            healthResolver = new HealthResolver(nameservers);

            // Create process terminator that handles process termination signals.

            terminator = new ProcessTerminator(log);

            try
            {
                // Establish the hive connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    HiveHelper.OpenHiveRemote();
                }
                else
                {
                    HiveHelper.OpenHive();
                }

                // Open Consul and then start the main service task.

                log.LogDebug(() => $"Connecting Consul");

                using (consul = HiveHelper.OpenConsul())
                {
                    await RunAsync();
                }
            }
            catch (Exception e)
            {
                log.LogCritical(e);
                Program.Exit(1);
            }
            finally
            {
                HiveHelper.CloseCluster();
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
            while (true)
            {
                try
                {
                    log.LogDebug(() => "Starting poll");

                    if (terminator.CancellationToken.IsCancellationRequested)
                    {
                        log.LogDebug(() => "Terminating");
                        break;
                    }

                    // We're going to collect the hostname --> address mappings into
                    // a specialized (semi-threadsafe) dictionary.

                    var hostAddresses = new HostAddresses();

                    // Retrieve the current hive definition from Consul if we don't already
                    // have it or if it's different from what we've cached.

                    hiveDefinition = await HiveHelper.GetDefinitionAsync(hiveDefinition, terminator.CancellationToken);

                    log.LogDebug(() => $"Hive has [{hiveDefinition.NodeDefinitions.Count}] nodes.");

                    // Add the [NAME.hive] definitions for each cluster node.

                    foreach (var node in hiveDefinition.Nodes)
                    {
                        hostAddresses.Add($"{node.Name}.hive", IPAddress.Parse(node.PrivateAddress));
                    }

                    // Read the DNS entry definitions from Consul and add the appropriate 
                    // host/addresses based on health checks, etc.

                    var targetsResult = (await consul.KV.ListOrDefault<DnsEntry>(HiveConst.ConsulDnsEntriesKey + "/", terminator.CancellationToken));

                    List<DnsEntry> targets;

                    if (targetsResult == null)
                    {
                        // The targets key wasn't found in Consul, so we're
                        // going to assume that there are no targets.

                        targets = new List<DnsEntry>();
                    }
                    else
                    {
                        targets = targetsResult.ToList();
                    }

                    log.LogDebug(() => $"Consul has [{targets.Count()}] DNS targets.");

                    await ResolveTargetsAsync(hostAddresses, targets);

                    // Generate a canonical [hosts.txt] file by sorting host entries by 
                    // hostname and then by IP address followed by comment lines describing
                    // any hosts that don't have any healthy endpoints.  These error
                    // lines will sorted by hostname and will be formatted like:
                    //
                    //      # unhealthy: HOSTNAME

                    var sbHosts      = new StringBuilder();
                    var mappingCount = 0;

                    foreach (var host in hostAddresses.OrderBy(h => h.Key))
                    {
                        foreach (var address in host.Value.OrderBy(a => a.ToString()))
                        {
                            sbHosts.AppendLineLinux($"{address,-15} {host.Key}");
                            mappingCount++;
                        }
                    }

                    var unhealthyTargets = targets.Where(t => !hostAddresses.ContainsKey(t.Hostname) || hostAddresses[t.Hostname].Count == 0).ToList();

                    if (unhealthyTargets.Count > 0)
                    {
                        sbHosts.AppendLine();
                        sbHosts.AppendLine($"# [{unhealthyTargets.Count}] hosts without healthy endpoints:");
                        sbHosts.AppendLine($"#");

                        foreach (var target in unhealthyTargets.OrderBy(h => h))
                        {
                            sbHosts.AppendLine($"# unhealthy: {target.Hostname}");
                        }
                    }

                    // Compute the MD5 hash and compare it to the hash persisted to
                    // Consul (if any) to determine whether we need to update the
                    // answers in Consul.

                    var hostsTxt   = sbHosts.ToString();
                    var hostsMD5   = NeonHelper.ComputeMD5(hostsTxt);
                    var currentMD5 = await consul.KV.GetStringOrDefault(HiveConst.ConsulDnsHostsMd5Key, terminator.CancellationToken);

                    if (currentMD5 == null)
                    {
                        currentMD5 = string.Empty;
                    }

                    if (hostsMD5 != currentMD5)
                    {
                        log.LogDebug(() => $"DNS answers have changed.");
                        log.LogDebug(() => $"Writing [{mappingCount}] DNS answers to Consul.");

                        // Update the Consul keys using a transaction.

                        var operations = new List<KVTxnOp>()
                    {
                        new KVTxnOp(HiveConst.ConsulDnsHostsMd5Key, KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(hostsMD5) },
                        new KVTxnOp(HiveConst.ConsulDnsHostsKey, KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(hostsTxt) }
                    };

                        await consul.KV.Txn(operations, terminator.CancellationToken);
                    }
                }
                catch (Exception e)
                {
                    log.LogWarn(e);
                }

                log.LogDebug(() => "Finished poll");
                await Task.Delay(pollInterval);
            }

            terminator.ReadyToExit();
        }

        /// <summary>
        /// Resolves the <paramref name="targets"/> into healthy host addresses, 
        /// adding the results to <paramref name="hostAddresses"/>.
        /// </summary>
        /// <param name="hostAddresses">The host addresses.</param>
        /// <param name="targets">The DNS targets.</param>
        private static async Task ResolveTargetsAsync(HostAddresses hostAddresses, List<DnsEntry> targets)
        {
            // $todo(jeff.lill): 
            //
            // I'm keeping this implementation super simple for now, by performing 
            // all of the health checks during the poll.  This probably won't scale
            // well when there are 100s of target endpoints.  This will tend to 
            // blast endpoints all at once.
            //
            // It would probably be better to do health checking continuously in
            // another task and have this method resolve the hosts from that data.
            // That would also allow health checks to use a target TTL as a hint
            // for how often endpoint health should be checked.

            // Implementation Note:
            // --------------------
            // We're going to create a task for each DNS host entry and then
            // each of those tasks will create a task for each endpoint that
            // requires a health check.

            var nodeGroups = hiveDefinition.GetNodeGroups();
            var entryTasks = new List<Task>();
            var warnings   = new List<string>();

            foreach (var target in targets)
            {
                var targetWarnings = target.Validate(hiveDefinition, nodeGroups);

                if (targetWarnings.Count > 0)
                {
                    // We skip generating DNS entries for targets with warnings.
                    
                    foreach (var warning in warnings)
                    {
                        warnings.Add(warning);
                    }

                    continue;
                }

                // Clear the resolver at the beginning of each health check pass
                // to purge any cached state from the previous pass.

                healthResolver.Clear();

                // Kick off the endpoint health checks.

                var healthyAddresses = new HashSet<string>();

                entryTasks.Add(Task.Run(
                    async () =>
                    {
                        var healthTasks = new List<Task>();

                        foreach (var endpoint in target.Endpoints)
                        {
                            //-------------------------------------------------
                            // Handle node group endpoints.

                            var groupName = endpoint.GetGroupName();

                            if (groupName != null)
                            {
                                if (nodeGroups.TryGetValue(groupName, out var group))
                                {
                                    foreach (var node in group)
                                    {
                                        healthTasks.Add(Task.Run(
                                            async () =>
                                            {
                                                var nodeAddresses = await CheckEndpointAsync(endpoint, node.PrivateAddress);

                                                foreach (var nodeAddress in nodeAddresses)
                                                {
                                                    hostAddresses.Add(target.Hostname, nodeAddress);
                                                }
                                            }));
                                    }
                                }

                                continue;
                            }

                            //-------------------------------------------------
                            // Handle normal endpoints.

                            var addresses = await CheckEndpointAsync(endpoint);

                            if (addresses != null)
                            {
                                foreach (var address in addresses)
                                {
                                    hostAddresses.Add(target.Hostname, address);
                                }
                            }
                        }

                        await NeonHelper.WaitAllAsync(healthTasks);
                    },
                    cancellationToken: terminator.CancellationToken));
            }

            await NeonHelper.WaitAllAsync(entryTasks);

            // Log any detected configuration warnings.  Note that we're going to throttle
            // warning reports to once every 5 minutes, so we won't spam the logs.

            if (warnTimer.HasFired)
            {
                foreach (var warning in warnings)
                {
                    log.LogWarn(warning);
                }
            }
        }

        /// <summary>
        /// Performs an endpoint health check.
        /// </summary>
        /// <param name="endpoint">The endpoint being tested.</param>
        /// <param name="targetOverride">
        /// Optionally overrides the <see cref="DnsEndpoint.Target"/> property when we're
        /// testing node groups.
        /// </param>
        /// <returns>
        /// The list of healthy endpoint IP addresses for the endpoint or 
        /// an empty list if there are no healthy addresses.
        /// </returns>
        private static async Task<List<IPAddress>> CheckEndpointAsync(DnsEndpoint endpoint, string targetOverride = null)
        {
            // Resolve the target DNS name, if required.

            var addresses = await healthResolver.LookupAsync(targetOverride ?? endpoint.Target);

            // Perform health checking if required.

            var healthyAddresses = new List<IPAddress>();

            if (endpoint.Check)
            {
                var pingTasks = new List<Task>();

                foreach (var address in addresses)
                {
                    pingTasks.Add(Task.Run(
                        async () =>
                        {
                            var isHealthy = await healthResolver.SendPingAsync(address, pingTimeout);

                            if (isHealthy)
                            {
                                lock (healthyAddresses)
                                {
                                    healthyAddresses.Add(address);
                                }
                            }
                            else
                            {
                                // $todo(jeff.lill):
                                //
                                // Consider logging health transitions:
                                //
                                //      HEALTHY   --> UNHEALTHY
                                //      UNHEALTHY --> HEALTHY
                                //
                                // We'll need to keep some state to manage this.
                            }
                        }));
                }

                await NeonHelper.WaitAllAsync(pingTasks);
            }
            else
            {
                healthyAddresses = addresses.ToList();
            }

            return healthyAddresses;
        }
    }
}
