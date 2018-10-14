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
using Neon.Time;

#if TODO

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
    public static class Program
    {
        /// <summary>
        /// TODO
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
        }
    }
}

#else

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
    public static class Program
    {
        private const string vaultCertPrefix = "neon-secret/cert";

        // Environment variables:

        private static string                   proxyName;
        private static string                   configKey;
        private static string                   configHashKey;
        private static string                   vaultCredentialsName;
        private static TimeSpan                 warnInterval;
        private static TimeSpan                 startDelay;
        private static bool                     debug;
        private static bool                     vaultSkipVerify;

        // Service state:

        private static string                   serviceName;
        private static ProcessTerminator        terminator;
        private static INeonLogger              log;
        private static HiveProxy                hive;
        private static VaultClient              vault;
        private static ConsulClient             consul;
        private static BroadcastChannel         proxyNotifyChannel;
        private static DateTime                 warnTimeUtc = DateTime.MinValue;
        private static CancellationTokenSource  cts         = new CancellationTokenSource();

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static async Task Main(string[] args)
        {
            LogManager.Default.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.Default.GetLogger(typeof(Program));

            // Read the environment variables.

            proxyName = Environment.GetEnvironmentVariable("PROXY_NAME");

            if (string.IsNullOrEmpty(proxyName))
            {
                log.LogError("[PROXY_NAME] environment variable is required.");
                Program.Exit(1);
            }

            serviceName = $"neon-proxy-{proxyName.ToLowerInvariant()}:{GitVersion}";

            log.LogInfo(() => $"Starting [{serviceName}]");

            configKey = Environment.GetEnvironmentVariable("CONFIG_KEY");

            if (string.IsNullOrEmpty(configKey))
            {
                log.LogError("[CONFIG_KEY] environment variable is required.");
                Program.Exit(1);
            }

            configHashKey = Environment.GetEnvironmentVariable("CONFIG_HASH_KEY");

            if (string.IsNullOrEmpty(configHashKey))
            {
                log.LogError("[CONFIG_HASH_KEY] environment variable is required.");
                Program.Exit(1);
            }

            vaultCredentialsName = Environment.GetEnvironmentVariable("VAULT_CREDENTIALS");

            if (string.IsNullOrEmpty(vaultCredentialsName))
            {
                log.LogError("[VAULT_CREDENTIALS] environment variable is required.");
                Program.Exit(1);
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

            debug = "true".Equals(Environment.GetEnvironmentVariable("DEBUG"), StringComparison.InvariantCultureIgnoreCase);

            vaultSkipVerify = Environment.GetEnvironmentVariable("VAULT_SKIP_VERIFY") != null;

            log.LogInfo(() => $"LOG_LEVEL={LogManager.Default.LogLevel.ToString().ToUpper()}");
            log.LogInfo(() => $"PROXY_NAME={proxyName}");
            log.LogInfo(() => $"CONFIG_KEY={configKey}");
            log.LogInfo(() => $"CONFIG_HASH_KEY={configHashKey}");
            log.LogInfo(() => $"VAULT_CREDENTIALS={vaultCredentialsName}");
            log.LogInfo(() => $"WARN_SECONDS={warnInterval}");
            log.LogInfo(() => $"START_SECONDS={startDelay}");
            log.LogInfo(() => $"DEBUG={debug}");
            log.LogInfo(() => $"VAULT_SKIP_VERIFY={vaultSkipVerify}");

            // Create process terminator that to handle termination signals.

            terminator = new ProcessTerminator(log);
            terminator.AddHandler(() => cts.Cancel());

            // Establish the hive connections.

            if (NeonHelper.IsDevWorkstation)
            {
                var vaultCredentialsSecret = "neon-proxy-manager-credentials";

                Environment.SetEnvironmentVariable("VAULT_CREDENTIALS", vaultCredentialsSecret);

                hive = HiveHelper.OpenHiveRemote(new DebugSecrets().VaultAppRole(vaultCredentialsSecret, $"neon-proxy-{proxyName}"));
            }
            else
            {
                hive = HiveHelper.OpenHive();
            }

            try
            {
                // Log into Vault using a Docker secret.

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

                // Open the hive data services and then start the main service task.

                log.LogInfo(() => $"Connecting: Vault");

                using (vault = HiveHelper.OpenVault(vaultCredentials))
                {
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
                            // Crank up the service tasks.

                            await NeonHelper.WaitAllAsync(
                                RunAsync(),
                                MonitorAsync());

                            terminator.ReadyToExit();
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
        /// Starts HAProxy and then monitors HiveMQ for update notifications and then
        /// updates HAProxy as required.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task RunAsync()
        {
            await Task.CompletedTask;
        }

        /// <summary>
        /// Perodically logs warnings when the HAProxy configuration could not be loaded.
        /// </summary>
        /// <returns>The tacking <see cref="Task"/>.</returns>
        private static async Task MonitorAsync()
        {
            await Task.CompletedTask;
        }
    }
}
#endif