//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
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
using Neon.Tasks;
using Neon.Time;

namespace NeonProxy
{
    /// <summary>
    /// <para>
    /// Implements the <b>neon-proxy-public</b> and <b>neon-proxy-private</b> services by launching and then managing 
    /// an HAProxy subprocess.  This  service listens for HiveMQ notifications from <b>neon-proxy-manager</b>, indicating 
    /// that the HAProxy/Varnish may have changed and that the Varnish process should be notified of the changes.  This 
    /// is built into the <a href="https://hub.docker.com/r/nhive/neon-proxy/">nhive/neon-proxy</a> image and will run
    /// as the main container process.
    /// </para>
    /// <para>
    /// This service handles cache warming by perodically retrieving designated pages and files from the target services
    /// and the service also handles HiveMQ notifications commanding that items be purged from the caches.
    /// </para>
    /// </summary>
    public static partial class Program
    {
        // Environment variables:

        private static string                   configKey;
        private static string                   configHashKey;
        private static string                   vaultCredentialsName;
        private static TimeSpan                 warnInterval;
        private static TimeSpan                 startDelay;
        private static bool                     debugMode;

        // File system paths:

        private const string vaultCertPrefix    = "neon-secret/cert";
        private const string secretsFolder      = "/dev/shm/secrets";
        private const string configFolder       = secretsFolder + "/haproxy";
        private const string configPath         = configFolder + "/haproxy.cfg";
        private const string configUpdateFolder = secretsFolder + "/haproxy-update";
        private const string configNewPath      = configUpdateFolder + "/haproxy.cfg";

        // Service state:

        private static string                   serviceName;
        private static ProcessTerminator        terminator;
        private static bool                     isPublic = false;
        private static bool                     isBridge = false;
        private static INeonLogger              log;
        private static HiveProxy                hive;
        private static VaultClient              vault;
        private static ConsulClient             consul;
        private static BroadcastChannel         proxyNotifyChannel;
        private static DateTime                 errorTimeUtc = DateTime.MinValue;
        private static CancellationTokenSource  cts          = new CancellationTokenSource();

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));

            // Create process terminator to handle termination signals.

            terminator = new ProcessTerminator(log);
            terminator.AddHandler(() => cts.Cancel());

            // Read the environment variables.

            // $hack(jeff.lill:
            //
            // We're going to scan the Consul configuration key to determine whether this
            // instance is managing the public or private proxy (or bridges) so we'll
            // be completely compatible with existing deployments.
            //
            // In theory, we could have passed a new environment variable but that's not
            // worth the trouble.

            configKey = Environment.GetEnvironmentVariable("CONFIG_KEY");

            if (string.IsNullOrEmpty(configKey))
            {
                log.LogError("[CONFIG_KEY] environment variable is required.");
                Program.Exit(1);
            }

            isPublic = configKey.Contains("/public/");

            var proxyName = isPublic ? "public" : "private";

            serviceName = $"neon-proxy-{proxyName}:{GitVersion}";

            log.LogInfo(() => $"Starting [{serviceName}]");

            configHashKey = Environment.GetEnvironmentVariable("CONFIG_HASH_KEY");

            if (string.IsNullOrEmpty(configHashKey))
            {
                log.LogError("[CONFIG_HASH_KEY] environment variable is required.");
                Program.Exit(1);
            }

            vaultCredentialsName = Environment.GetEnvironmentVariable("VAULT_CREDENTIALS");

            if (string.IsNullOrEmpty(vaultCredentialsName))
            {
                log.LogWarn("HTTPS routes are not supported because VAULT_CREDENTIALS is not specified or blank.");
            }

            var warnSeconds = Environment.GetEnvironmentVariable("WARN_SECONDS");

            if (string.IsNullOrEmpty(warnSeconds) || !double.TryParse(warnSeconds, out var warnSecondsValue))
            {
                warnInterval = TimeSpan.FromSeconds(300);
            }
            else
            {
                warnInterval = TimeSpan.FromSeconds(warnSecondsValue);
            }

            var startSeconds = Environment.GetEnvironmentVariable("START_SECONDS");

            if (string.IsNullOrEmpty(startSeconds) || !double.TryParse(warnSeconds, out var startSecondsValue))
            {
                startDelay = TimeSpan.FromSeconds(10);
            }
            else
            {
                startDelay = TimeSpan.FromSeconds(startSecondsValue);
            }

            debugMode = "true".Equals(Environment.GetEnvironmentVariable("DEBUG"), StringComparison.InvariantCultureIgnoreCase);

            log.LogInfo(() => $"LOG_LEVEL={LogManager.Default.LogLevel.ToString().ToUpper()}");
            log.LogInfo(() => $"CONFIG_KEY={configKey}");
            log.LogInfo(() => $"CONFIG_HASH_KEY={configHashKey}");
            log.LogInfo(() => $"VAULT_CREDENTIALS={vaultCredentialsName}");
            log.LogInfo(() => $"WARN_SECONDS={warnInterval}");
            log.LogInfo(() => $"START_SECONDS={startDelay}");
            log.LogInfo(() => $"DEBUG={debugMode}");

            // Ensure that the required directories exist.

            Directory.CreateDirectory(secretsFolder);
            Directory.CreateDirectory(configFolder);
            Directory.CreateDirectory(configUpdateFolder);

            // Establish the hive connections.

            if (NeonHelper.IsDevWorkstation)
            {
                throw new NotImplementedException("This service works only within a Linux container with HAProxy installed.");

                //var vaultCredentialsSecret = "neon-proxy-manager-credentials";

                //Environment.SetEnvironmentVariable("VAULT_CREDENTIALS", vaultCredentialsSecret);

                //hive = HiveHelper.OpenHiveRemote(new DebugSecrets().VaultAppRole(vaultCredentialsSecret, $"neon-proxy-{proxyName}"));
            }
            else
            {
                hive = HiveHelper.OpenHive();
            }

            try
            {
                // Log into Vault using the Vault credentials persisted as a Docker
                // secret, if one was specified.  We won't open Vault otherwise.

                if (!string.IsNullOrEmpty(vaultCredentialsName))
                {
                    var vaultSecret = HiveHelper.GetSecret(vaultCredentialsName);

                    if (string.IsNullOrEmpty(vaultSecret))
                    {
                        log.LogCritical($"Cannot read Docker secret [{vaultCredentialsName}].");
                        Program.Exit(1);
                    }

                    var vaultCredentials = HiveCredentials.ParseJson(vaultSecret);

                    if (vaultCredentials == null)
                    {
                        log.LogCritical($"Cannot parse Docker secret [{vaultCredentialsName}].");
                        Program.Exit(1);
                    }

                    log.LogInfo(() => $"Connecting: Vault");
                    vault = HiveHelper.OpenVault(vaultCredentials);
                }
                else
                {
                    vault = null;

                    // $hack(jeff.lill):
                    //
                    // This is a bit of backwards compatible hack.  Instances started without
                    // the VAULT_CREDENTIALS environment variable are proxy bridges.

                    isBridge = true;
                }

                // Open Consul and then start the service tasks.

                log.LogInfo(() => $"Connecting: Consul");

                using (consul = HiveHelper.OpenConsul())
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
                        // Verify that the required Consul keys exist or loop to wait until they
                        // are created.  This will allow the service wait for pending hive setup
                        // operations to be completed.

                        while (!await consul.KV.Exists(configKey))
                        {
                            log.LogWarn(() => $"Waiting for [{configKey}] key to be present in Consul.");
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }

                        while (!await consul.KV.Exists(configHashKey))
                        {
                            log.LogWarn(() => $"Waiting for [{configHashKey}] key to be present in Consul.");
                            await Task.Delay(TimeSpan.FromSeconds(5));
                        }

                        // Crank up the service tasks.

                        await NeonHelper.WaitAllAsync(
                            ErrorPollerAsync(),
                            HAProxyManager());

                        terminator.ReadyToExit();
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
    }
}
