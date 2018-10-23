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

using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;
using Neon.Hive;
using Neon.Net;
using Neon.Retry;
using Neon.Tasks;
using Neon.Time;

namespace NeonDns
{
    /// <summary>
    /// Implements the <b>neon-dns</b> service.  See 
    /// <a href="https://hub.docker.com/r/nhive/neon-dns/">nhive/neon-dns</a>
    /// for more information.
    /// </summary>
    public static class Program
    {
        private static readonly string      serviceName       = $"neon-dns:{GitVersion}";
        private static string               powerDnsHostsPath = "/etc/powerdns/hosts";
        private static string               reloadSignalPath  = "/neon-dns/reload";
        private static ProcessTerminator    terminator;
        private static INeonLogger          log;
        private static HiveProxy            hive;
        private static ConsulClient         consul;
        private static TimeSpan             pollInterval;
        private static TimeSpan             verifyInterval;

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

            // Parse the environment variable settings.

            var environment = new EnvironmentParser(log);

            pollInterval   = environment.Get("POLL_INTERVAL", TimeSpan.FromSeconds(5), validator: v => v > TimeSpan.Zero);
            verifyInterval = environment.Get("VERIFY_INTERVAL", TimeSpan.FromMinutes(5), validator: v => v > TimeSpan.Zero);

            // Create process terminator to handle process termination signals.

            terminator = new ProcessTerminator(log);

            try
            {
                // Establish the hive connections.

                if (NeonHelper.IsDevWorkstation)
                {
                    hive = HiveHelper.OpenHiveRemote();

                    // For testing and development, we're going to write a test
                    // hosts file to [%NF_TEMP\neon-dns-hosts.txt] so we can see
                    // what's happening outside of a hive.

                    powerDnsHostsPath = Environment.ExpandEnvironmentVariables("%NF_TEMP%\\neon-dns-hosts.txt");

                    File.WriteAllText(powerDnsHostsPath,
$@"# PowerDNS Recursor authoritatively answers for [*.HIVENAME.nhive.io] hostnames.
# on the local node using these mappings.

10.0.0.30       {HiveHelper.Hive.Definition.Hostnames.Consul}

# Internal hive Vault mappings:

10.0.0.30       {HiveHelper.Hive.Definition.Hostnames.Vault}
10.0.0.30       {HiveHelper.Hive.FirstManager.Name}.{HiveHelper.Hive.Definition.Hostnames.Vault}

# Internal hive registry cache related mappings:

10.0.0.30       {HiveHelper.Hive.FirstManager.Name}.{HiveHelper.Hive.Definition.Hostnames.RegistryCache}

# Internal hive log pipeline related mappings:

10.0.0.30       {HiveHelper.Hive.Definition.Hostnames.LogEsData}
");
                    // We're also going to create a temporary folder for the reload signal.

                    reloadSignalPath = Environment.ExpandEnvironmentVariables("%NF_TEMP%\\neon-dns\\reload");

                    Directory.CreateDirectory(Path.GetDirectoryName(reloadSignalPath));
                }
                else
                {
                    hive = HiveHelper.OpenHive();
                }

                // Ensure that we're running on a manager node.  This is required because
                // we need to be able to update the [/etc/powerdns/hosts] files deployed
                // on the managers.

                var nodeRole = Environment.GetEnvironmentVariable("NEON_NODE_ROLE");

                if (string.IsNullOrEmpty(nodeRole))
                {
                    log.LogCritical(() => "Service does not appear to be running on a neonHIVE.");
                    Program.Exit(1, immediate: true);
                }

                if (!string.Equals(nodeRole, NodeRole.Manager, StringComparison.OrdinalIgnoreCase))
                {
                    log.LogCritical(() => $"[neon-dns] service is running on a [{nodeRole}] hive node.  Only [{NodeRole.Manager}] nodes are supported.");
                    Program.Exit(1, immediate: true);
                }

                // Ensure that the [/etc/powerdns/hosts] file was mapped into the container.

                if (!File.Exists(powerDnsHostsPath))
                {
                    log.LogCritical(() => $"[neon-dns] service cannot locate [{powerDnsHostsPath}] on the host manager.  Was this mounted to the container as read/write?");
                    Program.Exit(1, immediate: true);
                }

                // Open Consul and then start the main service task.

                log.LogDebug(() => $"Connecting: Consul");

                using (consul = HiveHelper.OpenConsul())
                {
                    await RunAsync();
                }
            }
            catch (Exception e)
            {
                log.LogCritical(e);
                Program.Exit(1);
                return;
            }
            finally
            {
                HiveHelper.CloseHive();
                terminator.ReadyToExit();
            }

            Program.Exit(0);
            return;
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
        /// <para>
        /// Exits the service with an exit code.  This method defaults to using
        /// the <see cref="ProcessTerminator"/> if there is one to gracefully exit 
        /// the program.  The program will be exited immediately by passing 
        /// <paramref name="immediate"/><c>=true</c> or when there is no process
        /// terminator.
        /// </para>
        /// <note>
        /// You should always ensure that you exit the current operation
        /// context after calling this method.  This will ensure that the
        /// <see cref="ProcessTerminator"/> will have a chance to determine
        /// that the process was able to be stopped cleanly.
        /// </note>
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        /// <param name="immediate">Forces an immediate ungraceful exit.</param>
        public static void Exit(int exitCode, bool immediate = false)
        {
            log.LogInfo(() => $"Exiting: [{serviceName}]");

            if (terminator == null || immediate)
            {
                Environment.Exit(exitCode);
            }
            else
            {
                // Signal the terminator to stop on another thread
                // so this method can return and the caller will be
                // able to return from its operation code.

                var threadStart = new ThreadStart(() => terminator.Exit(exitCode));
                var thread      = new Thread(threadStart);

                thread.Start();
            }
        }

        /// <summary>
        /// Implements the service as a <see cref="Task"/>.
        /// </summary>
        /// <returns>The <see cref="Task"/>.</returns>
        private static async Task RunAsync()
        {
            var localMD5     = string.Empty;
            var remoteMD5    = "[unknown]";
            var verifyTimer  = new PolledTimer(verifyInterval, autoReset: true);

            var periodicTask = 
                new AsyncPeriodicTask(
                    pollInterval,
                    onTaskAsync:
                        async () =>
                        {
                            log.LogDebug(() => "Starting poll");
                            log.LogDebug(() => "Fetching DNS answers MD5 from Consul.");

                            remoteMD5 = await consul.KV.GetStringOrDefault(HiveConst.ConsulDnsHostsMd5Key, terminator.CancellationToken);

                            if (remoteMD5 == null)
                            {
                                remoteMD5 = "[unknown]";
                            }

                            var verify = verifyTimer.HasFired;

                            if (verify)
                            {
                                // Under normal circumstances, we should never see the reload signal file
                                // here because the [neon-dns-loader] service should have deleted it after
                                // handling the last change signal.
                                //
                                // This probably means that [neon-dns-loader] is not running or if this service
                                // is configured with POLL_INTERVAL being so short that [neon-dns-loader]
                                // hasn't had a chance to handle the previous signal.

                                if (File.Exists(reloadSignalPath))
                                {
                                    log.LogWarn("[neon-dns-loader] service doesn't appear to be running because the reload signal file is present.");
                                }
                            }

                            if (!verify && localMD5 == remoteMD5)
                            {
                                log.LogDebug(() => "DNS answers are unchanged.");
                            }
                            else
                            {
                                if (localMD5 == remoteMD5)
                                {
                                    log.LogDebug(() => "DNS answers have not changed but we're going to verify that we have the correct hosts anyway.");
                                }
                                else
                                {
                                    log.LogDebug(() => "DNS answers have changed.");
                                }

                                log.LogDebug(() => "Fetching DNS answers.");

                                var hostsTxt = await consul.KV.GetStringOrDefault(HiveConst.ConsulDnsHostsKey, terminator.CancellationToken);

                                if (hostsTxt == null)
                                {
                                    log.LogWarn(() => "DNS answers do not exist on Consul.  Is [neon-dns-mon] functioning properly?");
                                }
                                else
                                {
                                    var marker = "# -------- NEON-DNS --------";

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

                                    var retry = new LinearRetryPolicy(typeof(IOException), maxAttempts: 5, retryInterval: TimeSpan.FromSeconds(1));

                                    await retry.InvokeAsync(
                                        async () =>
                                        {
                                            using (var stream = new FileStream(powerDnsHostsPath, FileMode.Open, FileAccess.ReadWrite))
                                            {
                                                // Read a copy of the hosts file as bytes so we can compare
                                                // the old version with the new one generated below for changes.

                                                var orgHostBytes = stream.ReadToEnd();

                                                stream.Position = 0;

                                                // Generate the new hosts file.

                                                var sbHosts = new StringBuilder();

                                                // Read the hosts file up to but not including the special marker
                                                // line (if it's present).

                                                using (var reader = new StreamReader(stream, Encoding.UTF8, true, 32 * 1024, leaveOpen: true))
                                                {
                                                    foreach (var line in reader.Lines())
                                                    {
                                                        if (line.StartsWith(marker))
                                                        {
                                                            break;
                                                        }

                                                        sbHosts.AppendLine(line);
                                                    }
                                                }

                                                // Strip any trailing whitespace from the hosts file so we'll
                                                // be able to leave a nice blank line between the end of the
                                                // original file and the special marker line.

                                                var text = sbHosts.ToString().TrimEnd();

                                                sbHosts.Clear();
                                                sbHosts.AppendLine(text);

                                                // Append the marker line, followed by dynamic host
                                                // entries we downloaded from Consul.

                                                sbHosts.AppendLine();
                                                sbHosts.AppendLine(marker);
                                                sbHosts.AppendLine();
                                                sbHosts.Append(hostsTxt);

                                                // Generate the new host file bytes, taking care to ensure that
                                                // we're using Linux style line endings and then update the
                                                // hosts file if anything changed.

                                                var hostsText    = NeonHelper.ToLinuxLineEndings(sbHosts.ToString());
                                                var newHostBytes = Encoding.UTF8.GetBytes(hostsText);

                                                if (NeonHelper.ArrayEquals(orgHostBytes, newHostBytes))
                                                {
                                                    log.LogDebug(() => $"[{powerDnsHostsPath}] file is up-to-date.");
                                                }
                                                else
                                                {
                                                    log.LogDebug(() => $"[{powerDnsHostsPath}] is being updated.");

                                                    stream.Position = 0;
                                                    stream.SetLength(0);
                                                    stream.Write(newHostBytes);

                                                    // Signal to the local [neon-dns-loader] systemd service that it needs
                                                    // to have PowerDNS Recursor reload the hosts file.

                                                    File.WriteAllText(reloadSignalPath, "reload now");
                                                }
                                            }

                                            log.LogDebug(() => "Finished poll");
                                            await Task.CompletedTask;
                                        });

                                    // We've successfully synchronized the local hosts file with
                                    // the Consul DNS settings.

                                    localMD5 = remoteMD5;
                                }
                            }

                            return await Task.FromResult(false);
                        },
                    onExceptionAsync:
                        async e =>
                        {
                            log.LogError(e);
                            return await Task.FromResult(false);
                        },
                    onTerminateAsync:
                        async () =>
                        {
                            log.LogInfo(() => "Terminating");
                            await Task.CompletedTask;
                        });

            terminator.AddDisposable(periodicTask);
            await periodicTask.Run();
        }
    }
}
