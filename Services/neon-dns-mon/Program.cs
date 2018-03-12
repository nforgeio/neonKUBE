//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
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

namespace NeonDnsHealth
{
    /// <summary>
    /// Implements the <b>neon-dns-mon</b> service.  See 
    /// <a href="https://hub.docker.com/r/neoncluster/neon-dns-mon/">neoncluster/neon-dns-mon</a>
    /// for more information.
    /// </summary>
    public static class Program
    {
        private const string serviceName = "neon-dns-mon";

        private static ProcessTerminator    terminator;
        private static INeonLogger          log;
        private static ConsulClient         consul;
        private static TimeSpan             pollInterval;

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

            pollInterval = environment.Get("POLL_INTERVAL", TimeSpan.FromSeconds(15), validator: v => v > TimeSpan.Zero);

            // Create process terminator that handles process termination signals.

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

                // Open the cluster data services and then start the main service task.

                log.LogDebug(() => $"Opening Consul");

                using (consul = NeonClusterHelper.OpenConsul())
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
            var clusterDefinition = (ClusterDefinition)null;

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

                    // Retrieve the current cluster definition from Consul if we don't already
                    // have it or if it's different from what we've cached.

                    clusterDefinition = await NeonClusterHelper.GetDefinitionAsync(clusterDefinition, terminator.CancellationToken);

                    log.LogDebug(() => $"Cluster has [{clusterDefinition.NodeDefinitions.Count}] nodes.");

                    // Add the [NAME.node.cluster] definitions for each cluster node.

                    foreach (var node in clusterDefinition.Nodes)
                    {
                        hostAddresses.Add($"{node.Name}.node.cluster", IPAddress.Parse(node.PrivateAddress));
                    }

                    // Read the DNS target definitions from Consul.

                    IEnumerable<DnsTarget> targets;

                    try
                    {
                        targets = await consul.KV.List<DnsTarget>(NeonClusterConst.DnsConsulTargetsKey + "/", terminator.CancellationToken);
                    }
                    catch (KeyNotFoundException)
                    {
                        // The targets key wasn't found in Consul, so we're
                        // going to assume that there are no targets.

                        targets = new List<DnsTarget>();
                    }

                    log.LogDebug(() => $"Consul has [{targets.Count()}] DNS targets.");

                    // Generate a canonical [hosts.txt] file by sorting host entries by 
                    // hostname and then by IP address.

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

                    // Compute the MD5 hash and compare it to the hash persisted to
                    // Consul (if any) to determine whether we need to update the
                    // answers in Consul.

                    var hostsTxt   = sbHosts.ToString();
                    var hostsMD5   = NeonHelper.ComputeMD5(hostsTxt);
                    var currentMD5 = (string)null;

                    try
                    {
                        currentMD5 = await consul.KV.GetString(NeonClusterConst.DnsConsulHostsMd5Key, terminator.CancellationToken);
                    }
                    catch (KeyNotFoundException)
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
                        new KVTxnOp(NeonClusterConst.DnsConsulHostsMd5Key, KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(hostsMD5) },
                        new KVTxnOp(NeonClusterConst.DnsConsulHostsKey, KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(hostsTxt) }
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
    }
}
