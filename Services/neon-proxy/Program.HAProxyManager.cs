//-----------------------------------------------------------------------------
// FILE:	    Program.HAProxyManager.cs
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
        private async static Task HAProxyManager()
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
                        log.LogInfo(() => $"Received but ignorning: {message}");
                        return;
                    }

                    log.LogInfo(() => $"Received: {message}");
                    await ConfigureHAProxy();
                });
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
log.LogInfo(() => $"*** CONFIGURE 0:");

            if (!initialDeploy && !IsHAProxyDeployed)
            {
                // If this isn't the initial HAProxy call and HAProxy isn't already
                // deployed then we're going to ignore this method call because
                // we're presumably already in the process of doing the initial
                // deploy.  This will prevent a [proxy-notify] message from interferring
                // with the initial deployment.

                return;
            }

log.LogInfo(() => $"*** CONFIGURE 1:");
            try
            {
                // Retrieve the configuration HASH and compare that with what 
                // we have already deployed.

                log.LogInfo(() => $"CONFIGURE: Retrieving configuration HASH from Consul path [{configHashKey}].");

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
                    log.LogError($"CONFIGURE: Cannot retrieve [{configHashKey}] from Consul.", e);
                    return;
                }
log.LogInfo(() => $"*** CONFIGURE 2:");

                if (configHash == deployedHash)
                {
                    log.LogInfo(() => $"CONFIGURE: Configuration with [hash={configHash}] is already deployed.");
                    return;
                }
                else
                {
                    log.LogInfo(() => $"CONFIGURE: Configuration hash has changed from [{deployedHash}] to [{configHash}].");
                }

                // Download the configuration archive from Consul and extract it to
                // the new configuration directory (after ensuring that the directory
                // has been cleared).

                log.LogInfo(() => $"CONFIGURE: Retrieving configuration ZIP archive from Consul path [{configKey}].");

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
                    log.LogError($"CONFIGURE: Cannot retrieve [{configKey}] from Consul.", e);
                    return;
                }
log.LogInfo(() => $"*** CONFIGURE 3:");

                if (configHash == deployedHash)
                {
                    log.LogInfo(() => $"CONFIGURE: Configuration with [hash={configHash}] is already deployed.");
                    return;
                }

                var zipPath = Path.Combine(configUpdateFolder, "haproxy.zip");

                log.LogInfo(() => $"CONFIGURE: Extracting ZIP archive to [{configUpdateFolder}].");

                if (Directory.Exists(configUpdateFolder))
                {
                    Directory.Delete(configUpdateFolder, recursive: true);
                    Directory.CreateDirectory(configUpdateFolder);
                }
log.LogInfo(() => $"*** CONFIGURE 4:");

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
log.LogInfo(() => $"*** CONFIGURE 5:");

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
log.LogInfo(() => $"*** CONFIGURE 6:");

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

                        log.LogInfo(() => "CONFIGURE: Configuration is OK.");
                        break;

                    case 2:

                        log.LogInfo(() => "CONFIGURE: Configuration is valid but specifies no routes.");

                        // Ensure that any existing HAProxy instance is stopped and that
                        // the configuration folders are cleared (for non-DebugMode) and
                        // then return so we won't try to spin up another HAProxy.

                        if (haProxyProcess != null)
                        {
                            haProxyProcess.Dispose();
                            haProxyProcess = null;

                            if (!debugMode)
                            {
                                Directory.Delete(configFolder);
                                Directory.Delete(configUpdateFolder);
                            }
                        }
                        return;

                    default:

                        SetErrorTime();
                        log.LogCritical(() => "CONFIGURE: Invalid HAProxy configuration.");
                        throw new Exception("CONFIGURE: Invalid HAProxy configuration.");
                }
log.LogInfo(() => $"*** CONFIGURE 7:");

                // Purge the contents of the [configFolder] and copy the contents
                // of [configNewFolder] into it.

                Directory.Delete(configFolder, recursive: true);
                Directory.CreateDirectory(configFolder);
                NeonHelper.CopyFolder(configUpdateFolder, configFolder);
log.LogInfo(() => $"*** CONFIGURE 8:");

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
                        stopType = "(hard stop)";
                        stopOption = $"-st {haProxyProcess.Id}";
                    }
                    else
                    {
                        stopType = "(soft stop)";
                        stopOption = $"-sf {haProxyProcess.Id})";
                    }

                    log.LogInfo(() => $"HAProxy is restarting {stopType}.");
                }
                else
                {
                    restart = false;
                    log.LogInfo(() => $"CONFIGURE: HAProxy is starting.");
                }
log.LogInfo(() => $"*** CONFIGURE 9:");

                // Enable HAProxy debugging mode to get a better idea of why health
                // checks are failing.

                var debugOption = string.Empty;

                if (debugMode)
                {
                    debugOption = "-d";
                }

                // Execute HAProxy.  Note that since the HAProxy configuration specifies
                // daemon mode, the program we're running will actually fork a new instance
                // of HAProxy.
                //
                // NOTE:
                //
                // We're assuming that the command will not return until it has completed
                // starting or restarting the HAProxy instance that will actually be 
                // doing all of the work.  If this wasn't true, [GetHAProxyProcess()]
                // might return the wrong process if more than one are running.

                Environment.SetEnvironmentVariable("HAPROXY_CONFIG_FOLDER", configFolder);

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
                    log.LogError(() => $"CONFIGURE: HAProxy failure: {response.ErrorText}");
                    return;
                }
log.LogInfo(() => $"*** CONFIGURE 10:");

                // Give HAProxy a chance to start/restart cleanly.

log.LogInfo(() => $"*** CONFIGURE 10-A:");
                await Task.Delay(startDelay, cts.Token);
log.LogInfo(() => $"*** CONFIGURE 10-B:");

                if (restart)
                {
log.LogInfo(() => $"*** CONFIGURE 10-C:");
                    log.LogInfo(() => "CONFIGURE: HAProxy has been updated.");
log.LogInfo(() => $"*** CONFIGURE 10-D:");
                }
                else
                {
log.LogInfo(() => $"*** CONFIGURE 10-E:");
                    log.LogInfo(() => "CONFIGURE: HAProxy has started.");
log.LogInfo(() => $"*** CONFIGURE 10-F:");
                }
log.LogInfo(() => $"*** CONFIGURE 10-G:");

                // Update the deployed hash so we won't try to update the same 
                // configuration again.

                deployedHash = configHash;
log.LogInfo(() => $"*** CONFIGURE 11:");

                // HAProxy was updated successfully so we can reset the error time
                // so to ensure that periodic error reporting will stop.
                ResetErrorTime();
            }
            catch (OperationCanceledException)
            {
                log.LogInfo(() => "CONFIGURE: Terminating");
                throw;
            }
            catch (Exception e)
            {
                if (GetHAProxyProcess() == null)
                {
                    log.LogCritical("CONFIGURE: Terminating because we cannot launch HAProxy.", e);
                    Program.Exit(1);
                }
                else
                {
                    log.LogError("CONFIGURE: Unable to reconfigure HAProxy.  Using the old configuration as a fail-safe.", e);
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
                    Directory.Delete(configFolder, recursive: true);
                    Directory.Delete(configUpdateFolder, recursive: true);
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
