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

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Net;
using Neon.Retry;

namespace NeonDnsHealth
{
    /// <summary>
    /// Implements the <b>neon-dns</b> service.  See 
    /// <a href="https://hub.docker.com/r/neoncluster/neon-dns/">neoncluster/neon-dns</a>
    /// for more information.
    /// </summary>
    public static class Program
    {
        private const string serviceName       = "neon-dns";
        private const string powerDnsHostsPath = "/etc/powerdns/hosts";

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

                // Ensure that we're running on a manager node.  This is required because
                // we need to be able to update the [/etc/powerdns/hosts] files deployed
                // on the managers.

                var nodeRole = Environment.GetEnvironmentVariable("NEON_NODE_ROLE");

                if (string.IsNullOrEmpty(nodeRole))
                {
                    log.LogCritical(() => "Container does not appear to be running on a neonCLUSTER.");
                    Program.Exit(1);
                }

                if (!string.Equals(nodeRole, NodeRole.Manager, StringComparison.OrdinalIgnoreCase))
                {
                    log.LogCritical(() => $"[neon-dns] service is running on a [{nodeRole}] cluster node.  Only [{NodeRole.Manager}] nodes are supported.");
                    Program.Exit(1);
                }

                // Ensure that the [/etc/powerdns/hosts] file was mapped into the container.

                if (File.Exists(powerDnsHostsPath))
                {
                    log.LogCritical(() => $"[neon-dns] service cannot locate [{powerDnsHostsPath}] on the host manager.  Was this mounted to the container as read/write?");
                    Program.Exit(1);
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
            var localMD5  = string.Empty;
            var remoteMD5 = "[unknown]";

            while (true)
            {
                log.LogDebug(() => "Starting poll");

                if (terminator.CancellationToken.IsCancellationRequested)
                {
                    log.LogDebug(() => "Terminating");
                    break;
                }

                try
                {
                    log.LogDebug(() => "Fetching DNS answers MD5 from Consul.");

                    try
                    {
                        remoteMD5 = await consul.KV.GetString(NeonClusterConst.DnsConsulHostsMd5Key, terminator.CancellationToken);
                    }
                    catch (KeyNotFoundException)
                    {
                        remoteMD5 = "[unknown]";
                    }

                    if (localMD5 == remoteMD5)
                    {
                        log.LogDebug(() => "DNS answers are unchanged.");
                    }
                    else
                    {
                        log.LogDebug(() => "DNS answers have changed.");
                        log.LogDebug(() => "Fetching DNS answers.");

                        try
                        {
                            var hostsTxt = await consul.KV.GetString(NeonClusterConst.DnsConsulHostsKey, terminator.CancellationToken);
                            var marker   = "# ---DYNAMIC-HOSTS---";

                            // We have the host entries from Consul.  We need to add these onto the
                            // end [/etc/powserdns/hosts], replacing any host entries written during
                            // a previous run.
                            //
                            // We're going to use the special marker line:
                            //
                            //  # ---DYNAMIC-HOSTS---
                            //
                            // to separate the built-in hosts (above the line) from the dynamic hosts
                            // we're generating here (which will be below the line).  Note that this
                            // line won't exist the first time this service runs, so we'll just add it.
                            //
                            // Note that it's possible that the PowerDNS Recursor might be reading this
                            // file while we're trying to write it.  We're going to treat these as a
                            // transient errors and retry.

                            var retry = new LinearRetryPolicy(e => e is IOException, maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(1));

                            await retry.InvokeAsync(
                                async () =>
                                {
                                    using (var stream = new FileStream(powerDnsHostsPath, FileMode.Open, FileAccess.ReadWrite))
                                    {
                                        var sbHosts = new StringBuilder();

                                        // Read the hosts file up to but not including the special marker
                                        // line (if it's present).

                                        using (var reader = new StreamReader(stream))
                                        {
                                            foreach (var line in reader.Lines())
                                            {
                                                if (line.StartsWith(marker))
                                                {
                                                    break;
                                                }

                                                sbHosts.AppendLineLinux(line);
                                            }
                                        }

                                        // Append the marker line, followed by the dynamic host
                                        // entries we downloaded from Consul.

                                        sbHosts.AppendLineLinux(marker);
                                        sbHosts.AppendLineLinux();
                                        sbHosts.Append(hostsTxt);

                                        // Update the hosts file, taking extra care to ensure that 
                                        // we're using only Linux style line endings.

                                        var hosts = sbHosts.ToString();

                                        hosts = NeonHelper.ToLinuxLineEndings(hosts);

                                        stream.Position = 0;
                                        stream.SetLength(0);
                                        stream.Write(Encoding.UTF8.GetBytes(hosts));
                                    }

                                    await Task.CompletedTask;
                                });
                        }
                        catch (KeyNotFoundException)
                        {
                            log.LogWarn(() => "DNS answers do not exist on Consul.");
                        }
                    }
                }
                catch (Exception e)
                {
                    log.LogWarn(e);
                }

                await Task.Delay(pollInterval);
            }

            log.LogDebug(() => "Finished poll");
            terminator.ReadyToExit();
        }
    }
}