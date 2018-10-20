//-----------------------------------------------------------------------------
// FILE:	    Program.HAProxShim.cs
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
        private const string NotDeployedHash = "NOT-DEPLOYED";

        private static string deployedHash = NotDeployedHash;

        /// <summary>
        /// Indicates whether HAProxy has been deployed.
        /// </summary>
        private static bool IsHAProxyDeployed => deployedHash != NotDeployedHash;

        /// <summary>
        /// Retrieves the HAProxy process.
        /// </summary>
        /// <returns>The <see cref="Process"/> or <c>null</c> if its not running.</returns>
        private static Process GetHAProxyProcess()
        {
            return Process.GetProcessesByName("haproxy").SingleOrDefault();
        }

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
        /// This class uses <see cref="cts"/> to detect a pending service termination and
        /// then exit gracefully.
        /// </para>
        /// </remarks>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async static Task HAProxShim()
        {
            // We need to use HiveMQ bootstrap settings to avoid chicken-and-the-egg
            // dependency issues.

            // $todo(jeff.lill):
            //
            // We'll need to restart the [neon-proxy] instances whenever the
            // HiveMQ node topology changes.

            // This call ensures that HAProxy is started immediately.

            await ConfigureHAProxy(initialDeploy: true);

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
                        log.LogInfo(() => $"HAPROXY-SHIM: Received but ignorning: {message}");
                        return;
                    }

                    log.LogInfo(() => $"HAPROXY-SHIM: Received: {message}");
                    await ConfigureHAProxy();
                });

            // Spin quietly while waiting for a cancellation indicating that
            // the service is stopping.

            var task = new AsyncPeriodicTask(
                TimeSpan.FromMinutes(5),
                onTaskAsync: async () => await Task.FromResult(false),
                onTerminateAsync:
                    async () =>
                    {
                        log.LogInfo(() => "HAPROXY-SHIM: Terminating");
                        await Task.CompletedTask;
                    },
                cancellationTokenSource: cts);

            await task.Run();
        }

        /// <summary>
        /// Configures HAProxy based on the current load balancer configuration.
        /// </summary>
        /// <param name="initialDeploy">Pass <c>true</c> to indicate that this is the initial HAProxy deployment.</param>
        /// <remarks>
        /// This method will terminate the service if HAProxy could not be started
        /// for the first call.
        /// </remarks>
        public async static Task ConfigureHAProxy(bool initialDeploy = false)
        {
            if (!initialDeploy && !IsHAProxyDeployed)
            {
                // If this isn't the initial HAProxy call and HAProxy isn't already
                // deployed then we're going to ignore this method call because
                // we're presumably already in the process of doing the initial
                // deploy.  This will prevent a [proxy-notify] message from interferring
                // with the initial deployment.

                return;
            }

            try
            {
                // Retrieve the configuration HASH and compare that with what 
                // we have already deployed.

                log.LogInfo(() => $"HAPROXY-SHIM: Retrieving configuration HASH from Consul path [{configHashKey}].");

                string configHash;

                try
                {
                    configHash = await consul.KV.GetString(configHashKey, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    SetErrorTime();
                    log.LogError($"HAPROXY-SHIM: Cannot retrieve [{configHashKey}] from Consul.", e);
                    return;
                }

                if (configHash == deployedHash)
                {
                    log.LogInfo(() => $"HAPROXY-SHIM: Configuration with [hash={configHash}] is already deployed.");
                    return;
                }
                else
                {
                    log.LogInfo(() => $"HAPROXY-SHIM: Configuration hash has changed from [{deployedHash}] to [{configHash}].");
                }

                // Download the configuration archive from Consul and extract it to
                // the new configuration directory (after ensuring that the directory
                // has been cleared).

                log.LogInfo(() => $"HAPROXY-SHIM: Retrieving configuration ZIP archive from Consul path [{configKey}].");

                byte[] zipBytes;

                try
                {
                    zipBytes = await consul.KV.GetBytes(configKey, cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch (Exception e)
                {
                    SetErrorTime();
                    log.LogError($"HAPROXY-SHIM: Cannot retrieve [{configKey}] from Consul.", e);
                    return;
                }

                if (configHash == deployedHash)
                {
                    log.LogInfo(() => $"HAPROXY-SHIM: Configuration with [hash={configHash}] is already deployed.");
                    return;
                }

                var zipPath = Path.Combine(configUpdateFolder, "haproxy.zip");

                log.LogInfo(() => $"HAPROXY-SHIM: Extracting ZIP archive to [{configUpdateFolder}].");

                // Ensure that we have a fresh update folder.

                NeonHelper.DeleteFolder(configUpdateFolder);

                Directory.CreateDirectory(configUpdateFolder);

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
                                log.LogWarn(() => $"HAPROXY-SHIM: Bridge cannot process unexpected TLS certificate reference: {line}");
                                return;
                            }

                            var fields = line.Split(' ');
                            var certKey = fields[0];
                            var certDir = Path.Combine(configUpdateFolder, fields[1]);
                            var certFile = fields[2];

                            Directory.CreateDirectory(certDir);

                            var cert = await vault.ReadJsonAsync<TlsCertificate>(certKey, cts.Token);

                            File.WriteAllText(Path.Combine(certDir, certFile), cert.CombinedPemNormalized);
                        }
                    }
                }

                // Get the running HAProxy process (if there is one).

                var haProxyProcess = GetHAProxyProcess();

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

                        log.LogInfo(() => "HAPROXY-SHIM: Configuration is OK.");
                        break;

                    case 2:

                        log.LogInfo(() => "HAPROXY-SHIM: Configuration is valid but specifies no routes.");

                        // Ensure that any existing HAProxy instance is stopped and that
                        // the configuration folders are cleared (for non-DebugMode) and
                        // then return so we won't try to spin up another HAProxy.

                        if (haProxyProcess != null)
                        {
                            haProxyProcess.Dispose();
                            haProxyProcess = null;

                            if (!debugMode)
                            {
                                NeonHelper.DeleteFolder(configFolder);
                                NeonHelper.DeleteFolder(configUpdateFolder);
                            }
                        }
                        return;

                    default:

                        SetErrorTime();
                        log.LogCritical(() => "HAPROXY-SHIM: Invalid HAProxy configuration.");
                        throw new Exception("HAPROXY-SHIM: Invalid HAProxy configuration.");
                }

                // Purge the contents of the [configFolder] and copy the contents
                // of [configNewFolder] into it.

                NeonHelper.DeleteFolder(configFolder);
                Directory.CreateDirectory(configFolder);
                NeonHelper.CopyFolder(configUpdateFolder, configFolder);

                // Start HAProxy if it's not already running.
                //
                // ...or we'll generally do a soft stop when HAProxy is already running,
                // which means that HAProxy will try hard to maintain existing connections
                // as it reloads its config.  The presence of a [.hardstop] file in the
                // configuration folder will enable a hard stop.

                // $todo(jeff.lill):
                //
                // I don't believe that [neon-proxy-manager] ever generates a
                // [.hardstop] file.  This was a future feature idea.  This would
                // probably be better replaced by a boolean [HardStop] oroperty
                // passed with the notification message.

                var stopType   = string.Empty;
                var stopOption = string.Empty;
                var restart    = false;

                if (haProxyProcess != null)
                {
                    restart = true;

                    if (File.Exists(Path.Combine(configFolder, ".hardstop")))
                    {
                        stopType   = "(hard stop)";
                        stopOption = $"-st {haProxyProcess.Id}";
                    }
                    else
                    {
                        stopType   = "(soft stop)";
                        stopOption = $"-sf {haProxyProcess.Id}";
                    }

                    log.LogInfo(() => $"HAPROXY-SHIM: Restarting HAProxy {stopType}.");
                }
                else
                {
                    restart = false;
                    log.LogInfo(() => $"HAPROXY-SHIM: Starting HAProxy.");
                }

                // Enable HAProxy debugging mode to get a better idea of why health
                // checks are failing.

                var debugOption = string.Empty;

                if (debugMode)
                {
                    debugOption = "-d";
                }

                // Execute HAProxy.  If we're not running in DEBUG mode then HAProxy
                // will fork another HAProxy process that will handle the traffic and
                // return.  For DEBUG mode, this HAProxy call will ignore daemon mode
                // and run in the forground.  In that case, we need to fork HAProxy
                // so we won't block here forever.

                Environment.SetEnvironmentVariable("HAPROXY_CONFIG_FOLDER", configFolder);

                if (!debugMode)
                {
                    // Regular mode.

                    response = NeonHelper.ExecuteCapture("haproxy",
                        new object[]
                        {
                            "-f", configPath,
                            stopOption,
                            debugOption,
                            "-V"
                        });

                    if (response.ExitCode != 0)
                    {
                        SetErrorTime();
                        log.LogError(() => $"HAPROXY-SHIM: HAProxy failure: {response.ErrorText}");
                        return;
                    }
                }
                else
                {
                    // DEBUG mode steps:
                    //
                    //      1: Kill any existing HAProxy process.
                    //      2: Fork the new one to pick up the latest config.

                    if (haProxyProcess != null)
                    {
                        haProxyProcess.Kill();
                        haProxyProcess = null;
                    }

                    NeonHelper.Fork("haproxy",
                        new object[]
                        {
                            "-f", configPath,
                            stopOption,
                            debugOption,
                            "-V"
                        });
                }

                // Give HAProxy a chance to start/restart cleanly.

                await Task.Delay(startDelay, cts.Token);

                if (restart)
                {
                    log.LogInfo(() => "HAPROXY-SHIM: HAProxy has been updated.");
                }
                else
                {
                    log.LogInfo(() => "HAPROXY-SHIM: HAProxy has started.");
                }

                // Update the deployed hash so we won't try to update the same 
                // configuration again.

                deployedHash = configHash;

                // HAProxy was updated successfully so we can reset the error time
                // so to ensure that periodic error reporting will stop.
                ResetErrorTime();
            }
            catch (OperationCanceledException)
            {
                log.LogInfo(() => "HAPROXY-SHIM: Terminating");
                throw;
            }
            catch (Exception e)
            {
                if (GetHAProxyProcess() == null)
                {
                    log.LogCritical("HAPROXY-SHIM: Terminating because we cannot launch HAProxy.", e);
                    Program.Exit(1);
                }
                else
                {
                    log.LogError("HAPROXY-SHIM: Unable to reconfigure HAProxy.  Using the old configuration as a fail-safe.", e);
                }

                SetErrorTime();
            }
            finally
            {
                // When DEBUG mode is not disabled, we're going to clear the
                // both the old and new configuration folders so we don't leave
                // secrets like TLS private keys lying around in a file system.
                //
                // We'll leave these intact for DEBUG mode so we can manually
                // poke around the config.

                if (!debugMode)
                {
                    NeonHelper.DeleteFolder(configFolder);
                    NeonHelper.DeleteFolder(configUpdateFolder);
                }
            }
        }

        /// <summary>
        /// Sets the error time if it's not already set.  Errors will be 
        /// logged periodically when this is set.
        /// </summary>
        private static void SetErrorTime()
        {
            if (errorTimeUtc != DateTime.MinValue)
            {
                errorTimeUtc = DateTime.UtcNow;
            }
        }

        /// <summary>
        /// Clears the error time, disabling periodic error logging.
        /// </summary>
        private static void ResetErrorTime()
        {
            errorTimeUtc = DateTime.MinValue;
        }
    }
}
