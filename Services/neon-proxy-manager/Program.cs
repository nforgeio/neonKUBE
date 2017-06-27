//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

// $todo(jeff.lill):
//
// Temporarily disabling leader locking because of [https://github.com/jefflill/NeonForge/issues/80].
// This shouldn't reall be a problem since we're deploying only one service replica and the changes
// are committed to Consul via a transaction.

#define NO_LOCK

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
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

namespace NeonProxyManager
{
    /// <summary>
    /// Implements the <b>neon-proxy-manager</b> service.  See 
    /// <a href="https://hub.docker.com/r/neoncluster/neon-proxy-manager/">neoncluster/neon-proxy-manager</a>  
    /// and <a href="https://hub.docker.com/r/neoncluster/neon-proxy/">neoncluster/neon-proxy</a> for more information.
    /// </summary>
    public static class Program
    {
        private const string serviceName         = "neon-proxy-manager";
        private const string consulPrefix        = "neon/service/neon-proxy-manager";
        private const string leaderKey           = consulPrefix + "/leader";
        private const string leaderTTLSecondsKey = consulPrefix + "/leader-ttl-seconds";
        private const string pollSecondsKey      = consulPrefix + "/poll-seconds";
        private const string certWarnDaysKey     = consulPrefix + "/cert-warn-days";
        private const string proxyConf           = consulPrefix + "/conf";
        private const string proxyStatus         = consulPrefix + "/status";
        private const string vaultCertPrefix     = "neon-secret/cert";

        private static TimeSpan                 delayTime = TimeSpan.FromSeconds(5);
        private static ProcessTerminator        terminator;
        private static ILog                     log;
        private static VaultClient              vault;
        private static ConsulClient             consul;
        private static TimeSpan                 leaderTTL;
        private static TimeSpan                 pollInterval;
        private static TimeSpan                 certWarnTime;
        private static ClusterDefinition        cachedClusterDefinition;
        private static Task                     monitorTask;

        /// <summary>
        /// Application entry point.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static void Main(string[] args)
        {
            LogManager.SetLogLevel(Environment.GetEnvironmentVariable("LOG_LEVEL"));
            log = LogManager.GetLogger(serviceName);
            log.Info(() => $"Starting [{serviceName}]");

            terminator = new ProcessTerminator(log);

            // Establish the cluster connections.

            if (NeonHelper.IsDevWorkstation)
            {
                var vaultCredentialsSecret = "neon-proxy-manager-credentials";

                Environment.SetEnvironmentVariable("VAULT_CREDENTIALS", vaultCredentialsSecret);

                NeonClusterHelper.OpenRemoteCluster(
                    new DebugSecrets()
                        .VaultAppRole(vaultCredentialsSecret, "neon-proxy-manager"));
            }
            else
            {
                NeonClusterHelper.OpenCluster();
            }

            try
            {
                // Log into Vault using a Docker secret.

                var vaultCredentialsSecret = Environment.GetEnvironmentVariable("VAULT_CREDENTIALS");

                if (string.IsNullOrEmpty(vaultCredentialsSecret))
                {
                    log.Fatal("[VAULT_CREDENTIALS] environment variable does not exist.");
                    Program.Exit(1);
                }

                var vaultCredentials = ClusterCredentials.ParseJson(NeonClusterHelper.GetSecret(vaultCredentialsSecret));

                if (vaultCredentials == null)
                {
                    log.Fatal($"Cannot read Docker secret [{vaultCredentialsSecret}].");
                    Program.Exit(1);
                }

                // Open the cluster data services and then start the main service task.

                log.Debug(() => $"Opening Vault");

                using (vault = NeonClusterHelper.OpenVault(vaultCredentials))
                {
                    log.Debug(() => $"Opening Consul");

                    using (consul = NeonClusterHelper.OpenConsul())
                    {
                        Task.Run(
                            async () =>
                            {
                                await RunAsync();

                            }).Wait();

                        terminator.ReadyToExit();
                    }
                }
            }
            catch (Exception e)
            {
                log.Fatal(e);
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
        /// Exits the service with an exit code.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        public static void Exit(int exitCode)
        {
            log.Info(() => $"Exiting: [{serviceName}]");
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

            if (!await consul.KV.Exists(leaderTTLSecondsKey))
            {
                log.Info($"Persisting setting [{leaderTTLSecondsKey}=60.0]");
                await consul.KV.PutDouble(leaderTTLSecondsKey, 60.0);
            }

            if (!await consul.KV.Exists(pollSecondsKey))
            {
                log.Info($"Persisting setting [{pollSecondsKey}=300.0]");
                await consul.KV.PutDouble(pollSecondsKey, 300.0);
            }

            if (!await consul.KV.Exists(certWarnDaysKey))
            {
                log.Info($"Persisting setting [{certWarnDaysKey}=30.0]");
                await consul.KV.PutDouble(certWarnDaysKey, 30.0);
            }

            leaderTTL    = TimeSpan.FromSeconds(await consul.KV.GetDouble(leaderTTLSecondsKey));
            pollInterval = TimeSpan.FromSeconds(await consul.KV.GetDouble(pollSecondsKey));
            certWarnTime = TimeSpan.FromDays(await consul.KV.GetDouble(certWarnDaysKey));

            log.Info(() => $"Using setting [{leaderTTLSecondsKey}={leaderTTL}]");
            log.Info(() => $"Using setting [{pollSecondsKey}={pollSecondsKey}]");
            log.Info(() => $"Using setting [{certWarnDaysKey}={certWarnTime}]");

#if !NO_LOCK
            // We're going to use a Consul lock to prevent more than
            // one proxy manager from generating proxy configurations.

            var lockOpts = new LockOptions(leaderKey)
            {
                SessionName = Guid.NewGuid().ToString(),
                SessionTTL  = leaderTTL
            };

            var leaderLock = consul.CreateLock(lockOpts);

            log.Info("Starting as FOLLOWER");
#endif

            while (true)
            {
#if !NO_LOCK
                if (!leaderLock.IsHeld)
                {
                    await leaderLock.Acquire();
                }

                log.Info("Promoted to LEADER");
#endif

                // The implementation is pretty simple: We're going to watch
                // the [neon/service/neon-proxy-manager/conf/] prefix for changes 
                // with the timeout set to [pollTime].  The watch will fire 
                // whenever [neon-cli] modifies a cluster certificate or any of
                // the routes or settings for a proxy.
                //
                // Whenever the watch fires, the code will rebuild the proxy
                // configurations and also update the deployment's public load balancer
                // and network security as required.
                //
                // We're also going to monitor the leader lock and cancel the task
                // if we lose leadership status.

                var cts  = new CancellationTokenSource();
                var ct   = cts.Token;
                var exit = false;

                // Gracefully exit when the application is being terminated (e.g. via a [SIGTERM]).

                terminator.AddHandler(
                    () =>
                    {
                        exit = true;

                        cts.Cancel();

                        if (monitorTask != null)
                        {
                            if (monitorTask.Wait(terminator.Timeout))
                            {
                                log.Info(() => "Tasks stopped gracefully.");
                            }
                            else
                            {
                                log.Warn(() => $"Tasks did not stop within [{terminator.Timeout}].");
                            }
                        }
                    });

                var loadContext = AssemblyLoadContext.GetLoadContext(Assembly.GetEntryAssembly());

                // Monitor Consul for configuration changes and update the proxy configs.

                monitorTask = Task.Run(
                    async () =>
                    {
                        log.Debug("Starting [Monitor] task.");

                        var initialPoll = true;

                        while (true)
                        {
                            log.Debug(() => "Polling");

                            if (terminator.CancellationToken.IsCancellationRequested)
                            {
                                log.Debug(() => "Poll Terminating");
                                return;
                            }

                            var utcNow = DateTime.UtcNow;

                            try
                                {
                                await consul.KV.WatchPrefix(proxyConf + "/",
                                    async () =>
                                    {
                                        if (initialPoll)
                                        {
                                            log.Info("Initial proxy configuration poll.");
                                            initialPoll = false;
                                        }
                                        else
                                        {
                                            log.Info("Possible proxy or certificate change detected.");
                                        }

                                        // Load and check the cluster certificates.

                                        var clusterCerts = new ClusterCerts();

                                        log.Debug("Reading cluster certificates.");

                                        try
                                        {
                                            foreach (var certName in await vault.ListAsync(vaultCertPrefix))
                                            {
                                                var certJson    = (await vault.ReadDynamicAsync($"{vaultCertPrefix}/{certName}")).ToString();
                                                var certificate = NeonHelper.JsonDeserialize<TlsCertificate>(certJson);
                                                var certInfo    = new CertInfo(certName, certificate);

                                                if (!certInfo.Certificate.IsValidDate(utcNow))
                                                {
                                                    log.Error(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] expired at [{certInfo.Certificate.ValidUntil.Value}].");
                                                }
                                                else if (!certInfo.Certificate.IsValidDate(utcNow + certWarnTime))
                                                {
                                                    log.Warn(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] will expire in [{(certInfo.Certificate.ValidUntil.Value - utcNow).TotalDays}] days at [{certInfo.Certificate.ValidUntil}].");
                                                }

                                                clusterCerts.Add(certInfo);
                                            }
                                        }
                                        catch (Exception e)
                                        {
                                            log.Error("Unable to load certificates from Vault.", e);
                                            log.Error("Aborting proxy configuration.");
                                            return;
                                        }

                                        // Rebuild the proxy configurations and write the captured status to
                                        // Consul to make it available for the [neon proxy public|private status]
                                        // command.

                                        var publicBuildStatus = await BuildProxyConfigAsync("public", clusterCerts, ct);
                                        var publicProxyStatus = new ProxyStatus() { Status = publicBuildStatus.Status };

                                    await consul.KV.PutString($"{proxyStatus}/public", NeonHelper.JsonSerialize(publicProxyStatus), ct);

                                        var privateBuildStatus = await BuildProxyConfigAsync("private", clusterCerts, ct);
                                        var privateProxyStatus = new ProxyStatus() { Status = privateBuildStatus.Status };

                                        await consul.KV.PutString($"{proxyStatus}/private", NeonHelper.JsonSerialize(privateProxyStatus), ct);

                                        // We need to ensure that the deployment's load balancer and security
                                        // rules are updated to match changes to the public proxy routes.
                                        // Note that we're going to call this even if the PUBLIC proxy
                                        // hasn't changed to ensure that the load balancer doesn't get
                                        // out of sync.

                                        await UpdateClusterNetwork(publicBuildStatus.Routes, cts.Token);
                                    },
                                    timeout: pollInterval,
                                    cancellationToken: terminator.CancellationToken);
                            }
                            catch (TaskCanceledException)
                            {
                                log.Debug("Cancelling [Monitor] task.");
                                return;
                            }
                            catch (Exception e)
                            {
                                log.Error(e);
                            }

                            if (exit)
                            {
                                return;
                            }

                            await Task.Delay(delayTime);
                        }
                    });

#if !NO_LOCK
                // Monitor the leader lock to detect when we are demoted.

                while (leaderLock.IsHeld)
                {
                    await Task.Delay(delayTime);

                    // The monitor task will complete only when there's an error.

                    if (monitorTask.IsCompleted)
                    {
                        await leaderLock.Release();

                        log.Info(() => $"Exiting: [{serviceName}]");
                        Environment.Exit(1);
                    }
                }

                log.Info("Demoted to FOLLOWER");

                // Cancel the tasks then wait for them to stop.

                cts.Cancel();

                var timeout = TimeSpan.FromSeconds(30);

                if (!monitorTask.Wait(timeout))
                {
                    throw new TimeoutException($"Unable to stop poll and/or monitor tasks within [{timeout}].");
                }
#endif

#if !NO_LOCK
                await leaderLock.Release();
#endif
            }
        }

        /// <summary>
        /// Rebuilds the configuration for a public or private proxy and persists it
        /// to Consul if it differs from the previous version.
        /// </summary>
        /// <param name="proxyName">The proxy name: <b>public</b> or <b>private</b>.</param>
        /// <param name="clusterCerts">The cluster certificate information.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A tuple including the proxy's route dictionary as well as a flag indicating whether changes were published to Consul.</returns>
        private static async Task<(Dictionary<string, ProxyRoute> Routes, string Status, bool Published)> BuildProxyConfigAsync(string proxyName, ClusterCerts clusterCerts, CancellationToken cancellationToken)
        {
            var configError = false;
            var log         = new LogRecorder(Program.log);

            log.Debug(() => $"Rebuilding proxy [{proxyName.ToUpperInvariant()}].");

            // We need to track which certificates are actually referenced by proxy routes.

            clusterCerts.ClearReferences();

            // Load the proxy's settings and routes.

            string          proxyPrefix = $"{proxyConf}/{proxyName}";
            var             routes      = new Dictionary<string, ProxyRoute>();
            ProxySettings   settings;

            try
            {
                try
                {
                    settings = await consul.KV.GetObject<ProxySettings>($"{proxyPrefix}/settings", cancellationToken);
                }
                catch (KeyNotFoundException)
                {
                    // Initialize default settings for the proxy if they aren't written to Consul yet.

                    int firstProxyPort;
                    int lastProxyPort;

                    switch (proxyName)
                    {
                        case "public":

                            firstProxyPort = NeonHostPorts.ProxyPublicFirst;
                            lastProxyPort  = NeonHostPorts.ProxyPublicLast;
                            break;

                        case "private":

                            firstProxyPort = NeonHostPorts.ProxyPrivateFirst;
                            lastProxyPort  = NeonHostPorts.ProxyPrivateLast;
                            break;

                        default:

                            throw new NotImplementedException();
                    }

                    settings = new ProxySettings()
                    {
                        FirstPort = firstProxyPort,
                        LastPort  = lastProxyPort
                    };

                    await consul.KV.PutString($"{proxyPrefix}/settings", NeonHelper.JsonSerialize(settings, Formatting.None), cancellationToken);
                }

                var result = await consul.KV.List($"{proxyPrefix}/routes/", cancellationToken);

                if (result.Response != null)
                {
                    foreach (var routeKey in result.Response)
                    {
                        var route = ProxyRoute.Parse(Encoding.UTF8.GetString(routeKey.Value));

                        routes.Add(route.Name, route);
                    }
                }
            }
            catch (Exception e)
            {
                // Warn and exit for (presumably transient) Consul errors.

                log.Warn("Consul request failure.", e);
                return (Routes: routes, Status: log.ToString(), Published: false);
            }

            // Record some details about the routes.

            var httpRouteCount = routes.Values.Count(r => r.Mode == ProxyMode.Http);
            var tcpRouteCount  = routes.Values.Count(r => r.Mode == ProxyMode.Tcp);

            // Record HTTP route summaries.

            if (httpRouteCount == 0 && tcpRouteCount == 0)
            {
                log.Record("*** No proxy routes defined.");
            }

            if (httpRouteCount > 0)
            {
                log.Record($"HTTP/S Routes [{httpRouteCount}]");
                log.Record("------------------------------");

                foreach (ProxyHttpRoute route in routes.Values
                    .Where(r => r.Mode == ProxyMode.Http)
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                {
                    log.Record($"{route.Name}:");

                    foreach (var frontend in route.Frontends)
                    {
                        log.Record($"    frontend:");

                        log.Record($"        host:        {frontend.Host}");

                        if (frontend.Tls)
                        {
                            log.Record($"        certificate: {frontend.CertName}");
                        }

                        log.Record($"        public-port: {frontend.PublicPort}");
                        log.Record($"        proxy-port:  {frontend.ProxyPort}");
                    }

                    foreach (var backend in route.Backends)
                    {
                        log.Record($"    backend:         {backend.Server}:{backend.Port}");
                    }

                    log.Record();
                }
            }

            log.Record();

            // Record TCP route summaries.

            if (tcpRouteCount > 0)
            {
                log.Record($"TCP Routes [{tcpRouteCount}]");
                log.Record("------------------------------");

                foreach (ProxyTcpRoute route in routes.Values
                    .Where(r => r.Mode == ProxyMode.Tcp)
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var maxconn = route.MaxConnections == 0 ? "unlimited" : route.MaxConnections.ToString();

                    log.Record($"{route.Name}:");

                    foreach (var frontend in route.Frontends)
                    {
                        log.Record($"    frontend:");
                        log.Record($"        public-port: {frontend.PublicPort}");
                        log.Record($"        proxy-port:  {frontend.ProxyPort}");
                    }

                    foreach (var backend in route.Backends)
                    {
                        log.Record($"    backend:         {backend.Server}:{backend.Port}");
                    }

                    log.Record();   
                }
            }

            log.Record();

            // Verify the configuration.

            var proxyDefinition = new ProxyDefinition()
            {
                Name     = proxyName,
                Settings = settings,
                Routes   = routes
            };

            var validationContext = proxyDefinition.Validate(clusterCerts.ToTlsCertificateDictionary());

            if (validationContext.HasErrors)
            {
                log.Error(validationContext.GetErrors());
                return (Routes: routes, log.ToString(), Published: false);
            }

            // Generate the contents of the [haproxy.cfg] file.
            //
            // Note that the [neon-log-collector] depends on the format of the proxy frontend
            // and backend names, so don't change these.

            var sbHaProxy = new StringBuilder();

            sbHaProxy.Append(
$@"#------------------------------------------------------------------------------
# {proxyName.ToUpper()} HAProxy configuration file.
#
# Generated by:     {serviceName}
# Documentation:    http://cbonte.github.io/haproxy-dconv/1.7/configuration.html#7.1.4

global
    daemon

# Specifiy the maximum number of connections allowed by the proxy.

    maxconn             {settings.MaxConnections}

# Enable logging to syslog on the local Docker host under the
# NeonSysLogFacility_ProxyPublic facility.

    log                 ""${{NEON_NODE_IP}}:{NeonHostPorts.LogHostSysLog}"" len 65535 {NeonSysLogFacility.ProxyName}

# Certificate Authority and Certificate file locations:

    ca-base             ""${{HAPROXY_CONFIG_FOLDER}}""
    crt-base            ""${{HAPROXY_CONFIG_FOLDER}}""

# Other settings

    tune.ssl.default-dh-param   {settings.MaxDHParamBits}

defaults
    balance             roundrobin
    retries             2
    timeout connect     {settings.Timeouts.ConnectSeconds}s
    timeout client      {settings.Timeouts.ClientSeconds}s
    timeout server      {settings.Timeouts.ServerSeconds}s
    timeout check       {settings.Timeouts.CheckSeconds}s
");

            if (settings.Resolvers.Count > 0)
            {
                foreach (var resolver in settings.Resolvers)
                {
                    sbHaProxy.Append(
$@"
resolvers {resolver.Name}
    resolve_retries     {resolver.ResolveRetries}
    timeout retry       {resolver.RetrySeconds}s
    hold valid          {resolver.HoldSeconds}s
");
                    foreach (var nameserver in resolver.NameServers)
                    {
                        sbHaProxy.AppendLine($@"    nameserver          {nameserver.Name} {nameserver.Endpoint}");
                    }
                }
            }

            // Enable the HAProxy statistics pages.  These will be available on the 
            // [NeonClusterConst.HAProxyStatsPort] port on the [neon-cluster-public] or
            // [neon-cluster-private] network the proxy is serving.
            //
            // HAProxy statistics pages are not intended to be viewed directly by
            // by cluster operators.  Instead, the statistics from multiple HAProxy
            // instances will be aggregated by the cluster Dashboard.

            sbHaProxy.AppendLine($@"
#------------------------------------------------------------------------------
# Enable HAProxy statistics pages.

frontend haproxy_stats
    bind                *:{NeonClusterConst.HAProxyStatsPort}
    mode                http
    log                 global
    option              httplog
    option              http-server-close
    use_backend         haproxy_stats

backend haproxy_stats
    mode                http
    stats               enable
    stats               scope .
    stats               uri {NeonClusterConst.HaProxyStatsUri}
    stats               refresh 5s
");
            //-----------------------------------------------------------------
            // Verify that the routes don't conflict.

            // Verify that routes don't have conflicting publically facing ports.

            var publicPortToRoute = new Dictionary<int, ProxyRoute>();

            foreach (ProxyTcpRoute route in routes.Values.Where(r => r.Mode == ProxyMode.Tcp))
            {
                foreach (var frontend in route.Frontends)
                {
                    if (frontend.PublicPort <= 0)
                    {
                        continue;
                    }

                    ProxyRoute conflictRoute;

                    if (publicPortToRoute.TryGetValue(frontend.PublicPort, out conflictRoute))
                    {
                        log.Error(() => $"Route [{route.Name}] has public Internet facing port [{frontend.PublicPort}] conflicts with route [{conflictRoute.Name}].");
                        configError = true;
                    }
                    else
                    {
                        publicPortToRoute.Add(frontend.PublicPort, route);
                    }
                }
            }

            foreach (ProxyHttpRoute route in routes.Values.Where(r => r.Mode == ProxyMode.Http))
            {
                foreach (var frontend in route.Frontends)
                {
                    if (frontend.PublicPort <= 0)
                    {
                        continue;
                    }

                    ProxyRoute conflictRoute;

                    if (publicPortToRoute.TryGetValue(frontend.PublicPort, out conflictRoute))
                    {
                        log.Error(() => $"Route [{route.Name}] has public Internet facing port [{frontend.PublicPort}] conflicts with route [{conflictRoute.Name}].");
                        configError = true;
                    }
                    else
                    {
                        publicPortToRoute.Add(frontend.PublicPort, route);
                    }
                }
            }

            // Verify that routes don't have conflicting HAProxy frontend ports.

            var haProxyPortToRoute = new Dictionary<int, ProxyRoute>();

            foreach (ProxyTcpRoute route in routes.Values.Where(r => r.Mode == ProxyMode.Tcp))
            {
                foreach (var frontend in route.Frontends)
                {
                    ProxyRoute conflictRoute;

                    if (haProxyPortToRoute.TryGetValue(frontend.PublicPort, out conflictRoute))
                    {
                        log.Error(() => $"Route [{route.Name}] has HAProxy frontend port [{frontend.ProxyPort}] conflicts with route [{conflictRoute.Name}].");
                        configError = true;
                    }
                    else
                    {
                        haProxyPortToRoute.Add(frontend.ProxyPort, route);
                    }
                }
            }

            foreach (ProxyHttpRoute route in routes.Values.Where(r => r.Mode == ProxyMode.Http))
            {
                foreach (var frontend in route.Frontends)
                {
                    ProxyRoute conflictRoute;

                    if (haProxyPortToRoute.TryGetValue(frontend.PublicPort, out conflictRoute))
                    {
                        log.Error(() => $"Route [{route.Name}] has HAProxy frontend port [{frontend.ProxyPort}] conflicts with route [{conflictRoute.Name}].");
                        configError = true;
                    }
                    else
                    {
                        haProxyPortToRoute.Add(frontend.ProxyPort, route);
                    }
                }
            }

            //-----------------------------------------------------------------
            // Generate the TCP routes.

            if (routes.Values.Where(r => r.Mode == ProxyMode.Tcp).Count() > 0)
            {
                sbHaProxy.AppendLine("#------------------------------------------------------------------------------");
                sbHaProxy.AppendLine("# TCP Routes");
            }

            foreach (ProxyTcpRoute tcpRoute in routes.Values.Where(r => r.Mode == ProxyMode.Tcp))
            {
                // Generate the resolvers argument to be used to locate the
                // backend servers.

                var checkArg     = tcpRoute.Check ? " check" : string.Empty;
                var initAddrArg  = " init-addr none";
                var resolversArg = string.Empty;

                if (!string.IsNullOrEmpty(tcpRoute.Resolver))
                {
                    resolversArg = $" resolvers {tcpRoute.Resolver}";
                }

                // Generate the frontend with integrated backend servers.

                foreach (var frontend in tcpRoute.Frontends)
                {
                    sbHaProxy.Append(
$@"
listen tcp:{tcpRoute.Name}-port-{frontend.ProxyPort}
    mode                tcp
    bind                *:{frontend.ProxyPort}
");

                    if (tcpRoute.MaxConnections > 0)
                    {
                        sbHaProxy.AppendLine($"    maxconn             {tcpRoute.MaxConnections}");
                    }

                    if (tcpRoute.Log)
                    {
                        sbHaProxy.AppendLine($"    log                 global");
                        sbHaProxy.AppendLine($"    log-format          {NeonClusterHelper.GetProxyLogFormat("neon-proxy-" + proxyName, tcp: true)}");
                    }

                    if (tcpRoute.LogChecks)
                    {
                        sbHaProxy.AppendLine($"    option              log-health-checks");
                    }
                }

                var serverIndex = 0;

                foreach (var backend in tcpRoute.Backends)
                {
                    var backendName = $"server-{serverIndex++}";

                    if (!string.IsNullOrEmpty(backend.Name))
                    {
                        backendName = backend.Name;
                    }

                    sbHaProxy.AppendLine($"    server            {backendName} {backend.Server}:{backend.Port}{checkArg}{initAddrArg}{resolversArg}");
                }
            }

            //-----------------------------------------------------------------
            // HTTP routes are a bit tricker:
            //
            //      1. We need to generate an HAProxy frontend for each IP/port combination 
            //         and then use HOST header or SNI rules to the correct backend.  NeonCluster
            //         proxy frontends don't map directly to HAProxy frontends.
            //
            //      2. We need to generate an HAProxy backend for each NeonCluster proxy backend.
            //
            //      3. For TLS frontends, we're going to persist all of the referenced certificates 
            //         into frontend specific folders and then reference the folder in the bind
            //         statement.  HAProxy will use SNI to present the correct certificate to clients.

            var haProxyFrontends = new Dictionary<int, HAProxyHttpFrontend>();

            if (routes.Values.Where(r => r.Mode == ProxyMode.Http).Count() > 0)
            {
                sbHaProxy.AppendLine("#------------------------------------------------------------------------------");
                sbHaProxy.AppendLine("# HTTP Routes");

                // Enumerate all of the routes and build a dictionary with information about
                // the HAProxy frontends we'll need to generate.  This dictionary will be
                // keyed by the frontend PORT.

                foreach (ProxyHttpRoute httpRoute in routes.Values.Where(r => r.Mode == ProxyMode.Http).OrderBy(r => r.Name))
                {
                    foreach (var frontend in httpRoute.Frontends)
                    {
                        HAProxyHttpFrontend haProxyFrontend;

                        if (!haProxyFrontends.TryGetValue(frontend.ProxyPort, out haProxyFrontend))
                        {
                            haProxyFrontend = new HAProxyHttpFrontend()
                            {
                                Port = frontend.ProxyPort
                            };

                            haProxyFrontends.Add(frontend.ProxyPort, haProxyFrontend);
                        }

                        if (haProxyFrontend.HostMappings.ContainsKey(frontend.Host))
                        {
                            // It's possible to incorrectly define multiple HTTP routes with the same 
                            // host name mapping to the same HAProxy frontend port.  This code will
                            // simply choose a winner without reporting an error or warning.  It would
                            // be better to have an explicit check for this situation.

                            ProxyHttpRoute conflictRoute = null;

                            foreach (ProxyHttpRoute checkRoute in routes.Values.Where(r => r.Mode == ProxyMode.Http && r != httpRoute))
                            {
                                if (checkRoute.Frontends.Count(fe => fe.ProxyPort == frontend.ProxyPort && fe.Host.Equals(frontend.Host, StringComparison.CurrentCultureIgnoreCase)) > 0)
                                {
                                    conflictRoute = checkRoute;
                                }
                            }

                            log.Warn(() => $"HTTP/S route [{httpRoute.Name}] defines a frontend for hostname [{frontend.Host}] and port [{frontend.ProxyPort}] which conflicts with route [{conflictRoute.Name}].  This frontend will be ignored.");
                        }
                        else
                        {
                            haProxyFrontend.HostMappings[frontend.Host] = $"http:{httpRoute.Name}";
                        }

                        if (httpRoute.Log)
                        {
                            // If any of the routes on this port require logging we'll have to
                            // enable logging for all of the routes, since they'll end up sharing
                            // share the same proxy frontend.

                            haProxyFrontend.Log = true;
                        }

                        if (frontend.Tls)
                        {
                            CertInfo certInfo;

                            if (!clusterCerts.TryGetValue(frontend.CertName, out certInfo))
                            {
                                log.Error(() => $"Route [{httpRoute.Name}] references [{frontend.CertName}] which does not exist.");
                                configError = true;
                                continue;
                            }

                            if (!certInfo.Certificate.IsValidHost(frontend.Host))
                            {
                                log.Error(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] does not cover host [{frontend.Host}] for a route [{httpRoute.Name}] frontend.");
                            }

                            certInfo.WasReferenced = true;
                            haProxyFrontend.Certificates[certInfo.Name] = certInfo.Certificate;
                        }

                        if (frontend.Tls != haProxyFrontend.Tls)
                        {
                            if (frontend.Tls)
                            {
                                log.Error(() => $"Route [{httpRoute.Name}] specifies a TLS frontend on port [{frontend.ProxyPort}] that conflicts with non-TLS frontends on the same port.");
                                configError = true;
                            }
                            else
                            {
                                log.Error(() => $"Route [{httpRoute.Name}] specifies a non-TLS frontend on port [{frontend.ProxyPort}] that conflicts with TLS frontends on the same port.");
                                configError = true;
                            }
                        }
                    }
                }

                // We can generate the HAProxy HTTP frontends now.

                foreach (var haProxyFrontend in haProxyFrontends.Values.OrderBy(f => f.Port))
                {
                    var certArg = string.Empty;

                    if (haProxyFrontend.Tls)
                    {
                        certArg = $" ssl strict-sni crt certs-{haProxyFrontend.Name.Replace(':', '-')}";
                    }

                    var scheme = haProxyFrontend.Tls ? "https" : "http";

                    sbHaProxy.Append(
$@"
frontend {haProxyFrontend.Name}
    mode                http
    bind                *:{haProxyFrontend.Port}{certArg}
    unique-id-header    {LogActivity.HttpHeader}
    unique-id-format    {NeonClusterConst.HAProxyUidFormat}
    capture             request header Host len 255
    capture             request header User-Agent len 2048
    option              forwardfor
    option              http-server-close
");

                    if (haProxyFrontend.Log)
                    {
                        sbHaProxy.AppendLine($"    log                 global");
                        sbHaProxy.AppendLine($"    log-format          {NeonClusterHelper.GetProxyLogFormat("neon-proxy-" + proxyName, tcp: false)}");
                    }

                    foreach (var hostMapping in haProxyFrontend.HostMappings.OrderBy(m => m.Key))
                    {
                        if (haProxyFrontend.Tls)
                        {
                            sbHaProxy.AppendLine($"    use_backend         {hostMapping.Value} if {{ ssl_fc_sni {hostMapping.Key} }}");
                        }
                        else
                        {
                            var aclName = $"is-{hostMapping.Key.Replace('.', '-')}";

                            sbHaProxy.AppendLine();
                            sbHaProxy.AppendLine($"    acl                 {aclName} hdr_reg(host) -i {hostMapping.Key}(:\\d+)?");
                            sbHaProxy.AppendLine($"    use_backend         {hostMapping.Value} if {aclName}");
                        }
                    }
                }

                // Generate the HTTP backends

                foreach (ProxyHttpRoute httpRoute in routes.Values.Where(r => r.Mode == ProxyMode.Http).OrderBy(r => r.Name))
                {
                    // Generate the resolvers argument to be used to locate the
                    // backend servers.

                    var checkArg        = httpRoute.Check ? " check" : string.Empty;
                    var initAddrArg     = " init-addr none";
                    var resolversArg    = string.Empty;
                    var checkVersionArg = string.Empty;

                    if (!string.IsNullOrEmpty(httpRoute.Resolver))
                    {
                        resolversArg = $" resolvers {httpRoute.Resolver}";
                    }

                    if (!string.IsNullOrEmpty(httpRoute.CheckHost))
                    {
                        checkVersionArg = $"\"HTTP/{httpRoute.CheckVersion}\\r\\nHost: {httpRoute.CheckHost}\"";
                    }
                    else
                    {
                        checkVersionArg = $"HTTP/{httpRoute.CheckVersion}";
                    }

                    sbHaProxy.Append(
$@"
backend http:{httpRoute.Name}
    mode                http
");

                    if (httpRoute.HttpsRedirect)
                    {
                        sbHaProxy.AppendLine($"    redirect            scheme https if !{{ ssl_fc }}");
                    }

                    if (httpRoute.Check && !string.IsNullOrEmpty(httpRoute.CheckExpect))
                    {
                        sbHaProxy.AppendLine($"    http-check          expect {httpRoute.CheckExpect.Trim()}");
                    }

                    if (httpRoute.Check && !string.IsNullOrEmpty(httpRoute.CheckUri))
                    {
                        sbHaProxy.AppendLine($"    option              httpchk {httpRoute.CheckMethod.ToUpper()} {httpRoute.CheckUri} {checkVersionArg}");
                    }

                    if (httpRoute.Check && httpRoute.LogChecks)
                    {
                        sbHaProxy.AppendLine($"    option              log-health-checks");
                    }

                    if (httpRoute.Log)
                    {
                        sbHaProxy.AppendLine($"    log                 global");
                    }

                    var serverIndex = 0;

                    foreach (var backend in httpRoute.Backends)
                    {
                        var serverName = $"server-{serverIndex++}";

                        if (!string.IsNullOrEmpty(backend.Name))
                        {
                            serverName = backend.Name;
                        }

                        var sslArg = string.Empty;

                        if (backend.Tls)
                        {
                            sslArg = $" ssl verify required";
                        }

                        sbHaProxy.AppendLine($"    server              {serverName} {backend.Server}:{backend.Port}{sslArg}{checkArg}{initAddrArg}{resolversArg}");
                    }
                }
            }

            if (configError)
            {
                log.Error("Proxy configuration aborted due to one or more errors.");
                return (Routes: routes, log.ToString(), Published: false);
            }

            // Generate the contents of the [.certs] file.

            var sbCerts = new StringBuilder();

            foreach (var haProxyFrontend in haProxyFrontends.Values.OrderBy(f => f.Port))
            {
                var certFolder = $"certs-{haProxyFrontend.Name.Replace(':', '-')}";

                foreach (var item in haProxyFrontend.Certificates.OrderBy(certItem => certItem.Key))
                {
                    sbCerts.AppendLine($"{vaultCertPrefix}/{item.Key} {certFolder} {item.Key}.pem");
                }
            }

            // Generate the [neon-proxy] service compatible configuration ZIP archive.

            byte[] zipBytes;

            using (var ms = new MemoryStream())
            {
                using (var zip = ZipFile.Create(ms))
                {
                    // We need all archive entries to have fixed dates so we'll be able
                    // to compare configuration archives for changes using MD5 hashes.

                    zip.EntryFactory = new ZipEntryFactory(new DateTime(2000, 1, 1));

                    // NOTE: We're converting text to Linux style line endings.

                    zip.BeginUpdate();
                    zip.Add(new StaticBytesDataSource(NeonHelper.ToLinuxLineEndings(sbHaProxy.ToString())), "haproxy.cfg");

                    if (sbCerts.Length > 0)
                    {
                        zip.Add(new StaticBytesDataSource(NeonHelper.ToLinuxLineEndings(sbCerts.ToString())), ".certs");
                        zip.GetEntry(".certs").DateTime = DateTime.MinValue;
                    }

                    zip.CommitUpdate();
                }

                zipBytes = ms.ToArray();
            }

            // Compute the MD5 hash for the combined configuration ZIP and the referenced certificates.

            var     hasher = MD5.Create();
            string  combinedHash;

            if (clusterCerts.HasReferences)
            {
                using (var ms = new MemoryStream())
                {
                    ms.Write(hasher.ComputeHash(zipBytes));
                    ms.Write(clusterCerts.HashReferenced());

                    ms.Position  = 0;
                    combinedHash = Convert.ToBase64String(hasher.ComputeHash(ms));
                }
            }
            else
            {
                combinedHash = Convert.ToBase64String(hasher.ComputeHash(zipBytes));
            }

            // Compare the combined hash against what's already published to Consul
            // for the proxy and update these keys if the hashes differ.

            var publish = false;

            try
            {
                if (!await consul.KV.Exists($"{consulPrefix}/proxies/{proxyName}/hash", cancellationToken) || 
                    !await consul.KV.Exists($"{consulPrefix}/proxies/{proxyName}/conf", cancellationToken))
                {
                    publish = true; // Nothing published yet.
                }
                else
                {
                    publish = combinedHash != await consul.KV.GetString($"{consulPrefix}/proxies/{proxyName}/hash", cancellationToken);
                }

                if (publish)
                {
                    log.Info(() => $"Updating proxy [{proxyName.ToUpperInvariant()}] configuration: [routes={routes.Count}] [hash={combinedHash}]");

                    // Write the hash and configuration out as a transaction so we'll 
                    // be sure they match (don't get out of sync).  We don't need to
                    // do CAS here because only one proxy manager will own the lock
                    // and even if multiple instances happened to update this with 
                    // different values for some reason, the most recent updates would
                    // be applied the next time the proxy manager polled the config.

                    var operations = new List<KVTxnOp>()
                    {
                        new KVTxnOp($"{consulPrefix}/proxies/{proxyName}/hash", KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(combinedHash) },
                        new KVTxnOp($"{consulPrefix}/proxies/{proxyName}/conf", KVTxnVerb.Set) { Value = zipBytes }
                    };

                    await consul.KV.Txn(operations, cancellationToken);
                }
                else
                {
                    log.Info(() => $"No changes detected for proxy [{proxyName.ToUpperInvariant()}].");
                }
            }
            catch (Exception e)
            {
                // Warn and exit for Consul errors.

                log.Warn("Consul request failure.", e);
                return (Routes: routes, log.ToString(), Published: false);
            }

            return (Routes: routes, Status: log.ToString(), Published: publish);
        }

        /// <summary>
        /// Updates the cluster's public load balancer and network security rules so they
        /// are consistent with the public proxy routes passed.
        /// </summary>
        /// <param name="publicRoutes">The public proxy routes.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task UpdateClusterNetwork(Dictionary<string, ProxyRoute> publicRoutes, CancellationToken cancellationToken)
        {
            try
            {
                // Read the cluster hosting options from Vault and this combine 
                // this with the cluster definition loaded from Consul to create
                // a cluster proxy.

                cachedClusterDefinition         = await NeonClusterHelper.GetClusterDefinitionAsync(cachedClusterDefinition, cancellationToken);
                cachedClusterDefinition.Hosting = await vault.ReadJsonAsync<HostingOptions>("neon-secret/hosting/options", cancellationToken);

                var cluster = new ClusterProxy(cachedClusterDefinition);

                // Retrieve the current public load balancer rules and then compare
                // these with the public routes defined for the cluster to determine
                // whether we need to update the load balancer and network security
                // rules.

                // $note(jeff.lill):
                //
                // It's possible for the load balancer and network security rules
                // to be out of sync if the operator modifies the security rules
                // manually (e.g. via the cloud portal).  This code won't detect
                // this situation and the security rules won't be brought back
                // int sync until the public routes changes enough to actually 
                // change the external load balanced port set.
                //
                // This could be considered a feature.  For example, this allows
                // the operator temporarily block a port manually.

                var hostingManager  = HostingManager.GetManager(cachedClusterDefinition.Hosting.Environment, cluster);

                if (!hostingManager.CanUpdatePublicEndpoints)
                {
                    return; // Operators need to maintain the load balancer manually for this environment.
                }

                var publicEndpoints = hostingManager.GetPublicEndpoints();
                var latestEndpoints = new List<HostedEndpoint>();

                // Build a dictionary mapping the load balancer frontend ports to 
                // internal HAProxy frontend ports for the latest routes.

                foreach (ProxyTcpRoute route in publicRoutes.Values.Where(r => r.Mode == ProxyMode.Tcp))
                {
                    foreach (var frontend in route.Frontends)
                    {
                        if (frontend.PublicPort > 0)
                        {
                            latestEndpoints.Add(new HostedEndpoint(HostedEndpointProtocol.Tcp, frontend.PublicPort, frontend.ProxyPort));
                        }
                    }
                }

                foreach (ProxyHttpRoute route in publicRoutes.Values.Where(r => r.Mode == ProxyMode.Http))
                {
                    foreach (var frontend in route.Frontends)
                    {
                        if (frontend.PublicPort > 0)
                        {
                            latestEndpoints.Add(new HostedEndpoint(HostedEndpointProtocol.Tcp, frontend.PublicPort, frontend.ProxyPort));
                        }
                    }
                }

                // Determine if the latest route port mappings differ from the current
                // cluster load balancer rules.

                var changed = false;

                if (latestEndpoints.Count != publicEndpoints.Count)
                {
                    changed = true;
                }
                else
                {
                    // We're going to compare the endpoints in sorted order (by public load balancer port).

                    publicEndpoints = publicEndpoints.OrderBy(ep => ep.FrontendPort).ToList();
                    latestEndpoints = latestEndpoints.OrderBy(ep => ep.FrontendPort).ToList();

                    for (int i = 0; i < publicEndpoints.Count; i++)
                    {
                        if (publicEndpoints[i].FrontendPort != latestEndpoints[i].FrontendPort ||
                            publicEndpoints[i].BackendPort != latestEndpoints[i].BackendPort)
                        {
                            changed = true;
                            break;
                        }
                    }
                }

                if (!changed)
                {
                    log.Info(() => $"Public cluster load balancer configuration matches current routes. [endpoint-count={publicEndpoints.Count}]");
                    return;
                }

                // The endpoints have changed so update the cluster.

                log.Info(() => $"Updating: public cluster load balancer and security. [endpoint-count={publicEndpoints.Count}]");
                hostingManager.UpdatePublicEndpoints(latestEndpoints);
                log.Info(() => $"Update Completed: public cluster load balancer and security. [endpoint-count={publicEndpoints.Count}]");
            }
            catch (Exception e)
            {
                log.Error($"Unable to update cluster load balancer and/or network security configuration.", e);
            }
        }
    }
}
