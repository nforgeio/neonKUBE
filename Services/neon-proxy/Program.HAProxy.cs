//-----------------------------------------------------------------------------
// FILE:	    Program.HAProxy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Loader;
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
using Neon.HiveMQ;
using Neon.IO;
using Neon.Tasks;
using Neon.Time;

namespace NeonProxy
{
    public static partial class Program
    {
        private static object   haProxySyncLock = new object();
        private static bool     haProxyRunning  = false;
        private static Process  haProxyProcess  = null;
        private static string   deployedHash    = "NOT-DEPLOYED";

        /// <summary>
        /// Manages the HAProxy initial configuration from Consul and Vault settings and
        /// then listens for <see cref="ProxyUpdateMessage"/> messages on the <see cref="HiveMQChannels.ProxyNotify"/>
        /// broadcast by <b>neon-proxy-manager</b> signalling that the configuration has been
        /// updated.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method will terminate the service with an error if the configuration could 
        /// not be retrieved or applied for the first attempt since this very likely indicates
        /// a larger problem with the hive (e.g. Consul or Vault are down).
        /// </para>
        /// <para>
        /// If HAProxy was configured successfully on the first attempt, subsequent failures
        /// will be logged as warnings but the service will continue running with the out-of-date
        /// configuration to provide some resilience for running hive services.
        /// </para>
        /// <para>
        /// This method uses <see cref="cts"/> to detect a pending service termination and
        /// then exit gracefully.
        /// </para>
        /// </remarks>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async static Task HAProxyManager()
        {
            // We need to use HiveMQ bootstrap settings to avoid chicken-and-the-egg
            // dependency issues.

            // $todo(jeff.lill):
            //
            // We'll need to restart the [neon-proxy] instances whenever the
            // HiveMQ node topology changes.

            using (var proxyNotifyChannel = hive.HiveMQ.Internal.GetProxyNotifyChannel(useBootstrap: true))
            {
                // Register a handler for [ProxyUpdateMessage] messages that determines
                // whether the message is meant for this service instance and handle it.

                proxyNotifyChannel.ConsumeAsync<ProxyUpdateMessage>(
                    async message =>
                    {
                        // Determine whether the broadcast notification applies to
                        // this instance.

                        var forThisInstance = false;

                        if (isPublic)
                        {
                            forThisInstance = message.PublicProxy && !isBridge ||
                                              message.PublicBridge && isBridge;
                        }
                        else
                        {
                            forThisInstance = message.PrivateProxy && !isBridge ||
                                              message.PrivateBridge && isBridge;
                        }

                        if (!forThisInstance)
                        {
                            log.LogInfo(() => $"Received but ignorning: {message}");
                            return;
                        }

                        log.LogInfo(() => $"Received: {message}");
                        await ConfigureHAProxy();
                    });

                proxyNotifyChannel.Open();

                // This call ensures that HAProxy is started immediately.

                await ConfigureHAProxy();
            }
        }

        /// <summary>
        /// Configures HAProxy based on the current load balancer configuration.
        /// </summary>
        /// <remarks>
        /// This method will terminate the service if HAProxy could not be started
        /// for the first call.
        /// </remarks>
        public async static Task ConfigureHAProxy()
        {
            // We need to protect this with a lock because it might be possible for
            // the initial [ConfigureHAProxy()] call and a notification message to
            // happen at the same time.  HAProxy will be launched by whichever call
            // happens first and then the subsequent call will perform an update.

            lock (haProxySyncLock)
            {
                try
                {
                    // Retrieve the configuration HASH and compare that with what 
                    // we already have deployed.

                    log.LogInfo(() => $"CONFIGURE: Retrieving configuration HASH from Consul path [{configHashKey}].");

                    var configHash = consul.KV.GetString(configHashKey).Result;

                    if (configHash == deployedHash)
                    {
                        log.LogInfo(() => $"CONFIGURE: Configuration with [hash={configHash}] is already deployed.");
                        return;
                    }

                    // Download the configuration archive from Consul and extract it to
                    // the new configuration directory.

                    log.LogInfo(() => $"CONFIGURE: Retrieving configuration ZIP archive from Consul path [{configKey}].");

                    var zipPath  = Path.Combine(configUpdateFolder, "haproxy.zip");
                    var zipBytes = consul.KV.GetBytes(configKey).Result;

                    log.LogInfo(() => $"CONFIGURE: Extracting ZIP archive to [{configUpdateFolder}].");

                    if (Directory.Exists(configUpdateFolder))
                    {
                        Directory.Delete(configUpdateFolder, recursive: true);
                        Directory.CreateDirectory(configUpdateFolder);
                    }

                    File.WriteAllBytes(zipPath, zipBytes);

                    var response = NeonHelper.ExecuteCapture("unzip",
                        new object[]
                        {
                            "-o",
                            zipPath,
                            "-d",
                            configUpdateFolder
                        });

                    response.EnsureSuccess();

                    // The [.certs] file (if present) describes the certificates 
                    // to be downloaded from Vault.
                    //
                    // Each line contains three fields separated by a space:
                    // the Vault object path, the relative destination folder 
                    // path and the file name.
                    //
                    // Note that certificates are stored in Vault as JSON using
                    // the [TlsCertificate] schema, so we'll need to extract and
                    // combine the [cert] and [key] properties.

                    var certsPath = Path.Combine(configUpdateFolder, ".certs");

                    if (File.Exists(certsPath))
                    {
                        using (var reader = new StreamReader(certsPath))
                        {
                            foreach (var line in reader.Lines())
                            {
                                if (string.IsNullOrWhiteSpace(line))
                                {
                                    continue;   // Ignore blank lines
                                }

                                if (isBridge)
                                {
                                    log.LogWarn(() => $"CONFIGURE: Bridge cannot process unexpected TLS certificate reference: {line}");
                                    return;
                                }

                                var fields   = line.Split(' ');
                                var certKey  = fields[0];
                                var certDir  = Path.Combine(configUpdateFolder, fields[1]);
                                var certFile = fields[2];

                                Directory.CreateDirectory(certDir);

                                var cert = vault.ReadJsonAsync<TlsCertificate>(certKey).Result;

                                File.WriteAllText(certFile, cert.CombinedPemNormalized);
                            }
                        }
                    }

                    // Verify the configuration.  Note that HAProxy will return a
                    // 0 error code if the configuration is OK and specifies at
                    // least one route.  It will return 2 if the configuration is
                    // OK but there are no routes.  In this case, HAProxy won't
                    // actually launch.  Any other exit code indicates that the
                    // configuration is not valid.

                    log.LogInfo(() => "Verifying HAProxy configuration.");

                    Environment.SetEnvironmentVariable("HAPROXY_CONFIG_FOLDER", configUpdateFolder);

                    response = NeonHelper.ExecuteCapture("haproxy",
                        new object[]
                        {
                            "-c", "-q", "-f", configUpdateFolder
                        });

                    switch (response.ExitCode)
                    {
                        case 0:

                            log.LogInfo(() => "Configuration is OK.");
                            break;

                        case 2:

                            log.LogInfo(() => "Configuration is valid but specifies no routes.");
                            break;

                        default:

                            log.LogCritical(() => "Invalid HAProxy configuration.");
                            throw new Exception("Invalid HAProxy configuration.");
                    }

                    // Purge the contents of the [configFolder] and copy the contents
                    // of [configNewFolder] into it.

                    Directory.Delete(configFolder, recursive: true);
                    Directory.CreateDirectory(configFolder);
                    NeonHelper.CopyFolder(configUpdateFolder, configFolder);

                    // Start HAProxy if it's not already running or do a soft restart
                    // when it's already running.

                    var stopType   = string.Empty;
                    var stopOption = string.Empty;

                    if (haProxyRunning)
                    {
                        if (File.Exists(Path.Combine(configFolder, ".hardstop")))
                        {
                            stopType   = "(hard stop)";
                            stopOption = $"-st {haProxyProcess.Id}";
                        }
                        else
                        {
                            stopType   = "(soft stop)";
                            stopOption = $"-sf {haProxyProcess.Id})";
                        }

                        log.LogInfo(() => $"HAProxy is restarting {stopType}.");
                    }
                    else
                    {
                        log.LogInfo(() => $"HAProxy is starting.");
                    }

                    // Enable HAProxy debugging mode to get a better idea of why health
                    // checks are failing.

                    var debugOption = string.Empty;

                    if (debugMode)
                    {
                        debugOption = "-d";
                    }

                    Environment.SetEnvironmentVariable("HAPROXY_CONFIG_FOLDER", configFolder);

                    haProxyProcess = NeonHelper.Fork("haproxy",
                        new object[]
                        {
                            "-f", configPath,
                            stopOption,
                            debugOption,
                            "-V"
                        });

                    // Give HAProxy a chance to start/restart cleanly.

                    Thread.Sleep(startDelay);

                    if (!haProxyRunning)
                    {
                        if (haProxyProcess.HasExited)
                        {
                            log.LogCritical("HAProxy terminated unexpectedly.");
                            Program.Exit(1);
                        }
                        else
                        {
                            log.LogInfo(() => "HAProxy has started.");
                        }
                    }
                    else
                    {
                        log.LogInfo(() => "HAProxy has been updated.");
                    }

                    // When DEBUG mode is not disabled, we're going to clear the
                    // both the old and new configuration folders so we don't leave
                    // secrets like TLSk private keys lying around in a file system.
                    //
                    // We'll leave these intact for DEBUG mode so we can manually
                    // poke around the config.

                    if (!debugMode)
                    {
                        Directory.Delete(configFolder, recursive: true);
                        Directory.Delete(configUpdateFolder, recursive: true);
                    }
                }
                catch (OperationCanceledException)
                {
                    log.LogInfo(() => "CONFIGURE: Terminating");
                    throw;
                }
                catch (Exception e)
                {
                    if (!haProxyRunning)
                    {
                        log.LogCritical("CONFIGURE: Terminating because we cannot launch HAProxy.", e);
                        Program.Exit(1);
                    }
                    else
                    {
                        log.LogError("CONFIGURE: Unable to reconfigure HAProxy.  Continuing with the old configuration as a fail-safe.", e);
                    }
                }
            }

            await Task.CompletedTask;
        }
    }
}
