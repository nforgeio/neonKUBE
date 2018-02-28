//-----------------------------------------------------------------------------
// FILE:	    Program.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

// $todo(jeff.lill):
//
// Temporarily disabling leader locking because of [https://github.com/jefflill/NeonForge/issues/80].
// This shouldn't really be a problem since we're deploying only one service replica and the changes
// are committed to Consul via a transaction.

using System;
using System.Collections.Generic;
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

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.Diagnostics;
using Neon.Docker;

namespace NeonProxyManager
{
    /// <summary>
    /// Implements the <b>neon-proxy-manager</b> service which is responsible for dynamically generating the HAProxy 
    /// configurations for the <c>neon-proxy-public</c>, <c>neon-proxy-private</c>, <c>neon-proxy-public-bridge</c>,
    /// and <c>neon-proxy-private-bridge</c> services from the proxy routes persisted in Consul and the TLS certificates
    /// persisted in Vault.  See <a href="https://hub.docker.com/r/neoncluster/neon-proxy-manager/">neoncluster/neon-proxy-manager</a>  
    /// and <a href="https://hub.docker.com/r/neoncluster/neon-proxy/">neoncluster/neon-proxy</a> for more information.
    /// </summary>
    public static class Program
    {
        private const string serviceName     = "neon-proxy-manager";
        private const string consulPrefix    = "neon/service/neon-proxy-manager";
        private const string pollSecondsKey  = consulPrefix + "/poll-seconds";
        private const string certWarnDaysKey = consulPrefix + "/cert-warn-days";
        private const string proxyConf       = consulPrefix + "/conf";
        private const string proxyStatus     = consulPrefix + "/status";
        private const string vaultCertPrefix = "neon-secret/cert";
        private const string allPrefix       = "~all~";   // Special path prefix indicating that all paths should be matched.

        private static TimeSpan                 delayTime = TimeSpan.FromSeconds(5);
        private static ProcessTerminator        terminator;
        private static INeonLogger              log;
        private static VaultClient              vault;
        private static ConsulClient             consul;
        private static DockerClient             docker;
        private static TimeSpan                 pollInterval;
        private static TimeSpan                 certWarnTime;
        private static ClusterDefinition        cachedClusterDefinition;
        private static Task                     monitorTask;
        private static List<DockerNode>         swarmNodes;

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

            // Create process terminator that handles process termination signals.

            terminator = new ProcessTerminator(log);

            // Establish the cluster connections.

            if (NeonHelper.IsDevWorkstation)
            {
                var vaultCredentialsSecret = "neon-proxy-manager-credentials";

                Environment.SetEnvironmentVariable("VAULT_CREDENTIALS", vaultCredentialsSecret);

                NeonClusterHelper.OpenRemoteCluster(
                    new DebugSecrets().VaultAppRole(vaultCredentialsSecret, "neon-proxy-manager"));
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
                    log.LogCritical("[VAULT_CREDENTIALS] environment variable does not exist.");
                    Program.Exit(1);
                }

                var vaultCredentials = ClusterCredentials.ParseJson(NeonClusterHelper.GetSecret(vaultCredentialsSecret));

                if (vaultCredentials == null)
                {
                    log.LogCritical($"Cannot read Docker secret [{vaultCredentialsSecret}].");
                    Program.Exit(1);
                }

                // Open the cluster data services and then start the main service task.

                log.LogInfo(() => $"Opening Vault");

                using (vault = NeonClusterHelper.OpenVault(vaultCredentials))
                {
                    log.LogInfo(() => $"Opening Consul");

                    using (consul = NeonClusterHelper.OpenConsul())
                    {
                        using (docker = NeonClusterHelper.OpenDocker())
                        {
                            await RunAsync();
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
            // Load the settings.
            //
            // Initialize the proxy manager settings to their default values
            // if they don't already exist.

            if (!await consul.KV.Exists(pollSecondsKey))
            {
                log.LogInfo($"Persisting setting [{pollSecondsKey}=120.0]");
                await consul.KV.PutDouble(pollSecondsKey, 300.0);
            }

            if (!await consul.KV.Exists(certWarnDaysKey))
            {
                log.LogInfo($"Persisting setting [{certWarnDaysKey}=30.0]");
                await consul.KV.PutDouble(certWarnDaysKey, 30.0);
            }

            pollInterval = TimeSpan.FromSeconds(await consul.KV.GetDouble(pollSecondsKey));
            certWarnTime = TimeSpan.FromDays(await consul.KV.GetDouble(certWarnDaysKey));

            log.LogInfo(() => $"Using setting [{pollSecondsKey}={pollInterval.TotalSeconds}]");
            log.LogInfo(() => $"Using setting [{certWarnDaysKey}={certWarnTime.TotalSeconds}]");

            // The implementation is pretty straight forward: We're going to watch
            // the [neon/service/neon-proxy-manager/conf/] prefix for changes 
            // with the timeout set to [pollTime].  The watch will fire 
            // whenever [neon-cli] modifies a cluster certificate or any of
            // the routes or settings for a proxy.
            //
            // Whenever the watch fires, the code will rebuild the proxy
            // configurations and also update the deployment's public load balancer
            // and network security as required.

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
                            log.LogInfo(() => "Tasks stopped gracefully.");
                        }
                        else
                        {
                            log.LogWarn(() => $"Tasks did not stop within [{terminator.Timeout}].");
                        }
                    }
                });

            // Monitor Consul for configuration changes and update the proxy configs.

            monitorTask = Task.Run(
                async () =>
                {
                    log.LogInfo("Starting [Monitor] task.");

                    var initialPoll = true;

                    while (true)
                    {
                        if (terminator.CancellationToken.IsCancellationRequested)
                        {
                            log.LogInfo(() => "Poll Terminating");
                            return;
                        }

                        var utcNow = DateTime.UtcNow;

                        try
                        {
                            log.LogInfo(() => "Watching for routing changes.");

                            await consul.KV.WatchPrefix(proxyConf + "/",
                                async () =>
                                {
                                    if (initialPoll)
                                    {
                                        log.LogInfo("Proxy startup poll.");
                                        initialPoll = false;
                                    }
                                    else
                                    {
                                        log.LogInfo("Potential proxy or certificate change detected.");
                                    }

                                    // Load and check the cluster certificates.

                                    var clusterCerts = new ClusterCerts();

                                    log.LogInfo(() => "Reading cluster certificates.");

                                    try
                                    {
                                        foreach (var certName in await vault.ListAsync(vaultCertPrefix))
                                        {
                                            var certJson    = (await vault.ReadDynamicAsync($"{vaultCertPrefix}/{certName}")).ToString();
                                            var certificate = NeonHelper.JsonDeserialize<TlsCertificate>(certJson);
                                            var certInfo    = new CertInfo(certName, certificate);

                                            if (!certInfo.Certificate.IsValidDate(utcNow))
                                            {
                                                log.LogError(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] expired at [{certInfo.Certificate.ValidUntil.Value}].");
                                            }
                                            else if (!certInfo.Certificate.IsValidDate(utcNow + certWarnTime))
                                            {
                                                log.LogWarn(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] will expire in [{(certInfo.Certificate.ValidUntil.Value - utcNow).TotalDays}] days at [{certInfo.Certificate.ValidUntil}].");
                                            }

                                            clusterCerts.Add(certInfo);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        log.LogError("Unable to load certificates from Vault.", e);
                                        log.LogError("Aborting proxy configuration.");
                                        return;
                                    }

                                    // Fetch the list of active Docker Swarm nodes.  We'll need this to generate the
                                    // proxy bridge configurations.

                                    swarmNodes = await docker.NodeListAsync();

                                    // Rebuild the proxy configurations and write the captured status to
                                    // Consul to make it available for the [neon proxy public|private status]
                                    // command.  Note that we're going to build the [neon-proxy-public-bridge]
                                    // and [neon-proxy-private-bridge] configurations as well for use by any 
                                    // cluster pet nodes.

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
                            log.LogInfo(() => "Cancelling [Monitor] task.");
                            return;
                        }
                        catch (Exception e)
                        {
                            log.LogError(e);
                        }

                        if (exit)
                        {
                            return;
                        }

                        await Task.Delay(delayTime);
                    }
                });

            // Just spin and let the monitor task run.

            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        }

        /// <summary>
        /// Normalizes a path prefix by converting <c>null</c> or empty prefixes to <see cref="allPrefix"/>.
        /// </summary>
        /// <param name="pathPrefix">The input prefix.</param>
        /// <returns>The prefix or <see cref="allPrefix"/>.</returns>
        private static string NormalizePathPrefix(string pathPrefix)
        {
            if (string.IsNullOrEmpty(pathPrefix))
            {
                return allPrefix;
            }
            else
            {
                return pathPrefix;
            }
        }

        /// <summary>
        /// Rebuilds the configurations for a public or private proxy and persists them
        /// to Consul if they differ from the previous version.
        /// </summary>
        /// <param name="proxyName">The proxy name: <b>public</b> or <b>private</b>.</param>
        /// <param name="clusterCerts">The cluster certificate information.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>
        /// A tuple including the proxy's route dictionary and publication status details.
        /// </returns>
        private static async Task<(Dictionary<string, ProxyRoute> Routes, string Status)> 
            BuildProxyConfigAsync(string proxyName, ClusterCerts clusterCerts, CancellationToken cancellationToken)
        {
            var proxyDisplayName       = proxyName.ToUpperInvariant();
            var proxyBridgeName        = $"{proxyName}-bridge";
            var proxyBridgeDisplayName = proxyBridgeName.ToUpperInvariant();
            var configError            = false;
            var log                    = new LogRecorder(Program.log);

            log.LogInfo(() => $"Rebuilding proxy [{proxyDisplayName}].");

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

                    log.LogInfo(() => $"Updating proxy [{proxyDisplayName}] settings.");
                    await consul.KV.PutString($"{proxyPrefix}/settings", NeonHelper.JsonSerialize(settings, Formatting.None), cancellationToken);
                }

                log.LogInfo(() => $"Reading [{proxyDisplayName}] routes.");

                var result = await consul.KV.List($"{proxyPrefix}/routes/", cancellationToken);

                if (result.Response != null)
                {
                    foreach (var routeKey in result.Response)
                    {
                        var route = ProxyRoute.ParseJson(Encoding.UTF8.GetString(routeKey.Value));

                        routes.Add(route.Name, route);
                    }
                }
            }
            catch (Exception e)
            {
                // Warn and exit for (presumably transient) Consul errors.

                log.LogWarn($"Consul request failure for proxy [{proxyDisplayName}].", e);
                return (Routes: routes, Status: log.ToString());
            }

            log.Record();

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
                log.Record($"HTTP Routes [count={httpRouteCount}]");
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
                log.Record($"TCP Routes [count={tcpRouteCount}]");
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
                log.LogError(validationContext.GetErrors());
                return (Routes: routes, log.ToString());
            }

            // Generate the contents of the [haproxy.cfg] file.
            //
            // Note that the [neon-log-collector] depends on the format of the proxy frontend
            // and backend names, so don't change these.

            var sbHaProxy = new StringBuilder();

            sbHaProxy.Append(
$@"#------------------------------------------------------------------------------
# {proxyDisplayName} HAProxy configuration file.
#
# Generated by:     {serviceName}
# Documentation:    http://cbonte.github.io/haproxy-dconv/1.7/configuration.html#7.1.4

global
    daemon

# Specifiy the maximum number of connections allowed for a proxy instance.

    maxconn             {settings.MaxConnections}

# Enable logging to syslog on the local Docker host under the
# [NeonSysLogFacility_ProxyPublic] facility.

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
            // [NeonClusterConst.HAProxyStatsPort] port on the [neon-public] or
            // [neon-private] network the proxy serves.
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

            // Verify that TCP routes don't have conflicting publically facing ports.

            var publicTcpPortToRoute = new Dictionary<int, ProxyRoute>();

            foreach (ProxyTcpRoute route in routes.Values
                .Where(r => r.Mode == ProxyMode.Tcp))
            {
                foreach (var frontend in route.Frontends)
                {
                    if (frontend.PublicPort <= 0)
                    {
                        continue;
                    }

                    if (publicTcpPortToRoute.TryGetValue(frontend.PublicPort, out ProxyRoute conflictRoute))
                    {
                        log.LogError(() => $"TCP route [{route.Name}] has a public Internet facing port [{frontend.PublicPort}] conflict with TCP route [{conflictRoute.Name}].");
                        configError = true;
                    }
                    else
                    {
                        publicTcpPortToRoute.Add(frontend.PublicPort, route);
                    }
                }
            }

            // Verify that HTTP routes don't have conflicting publically facing ports and path
            // prefix combinations.  To pass, an HTTP frontend public port can't already be assigned
            // to a TCP route and the hostname/port/path combination can't already be assigned to
            // another frontend.
            //
            // The wrinkle here is that we need ensure that routes with a path prefix don't conflict
            // with routes that don't.

            var publicHttpHostPortPathToRoute = new Dictionary<string, ProxyRoute>();

            // Check routes without path prefixes first.

            foreach (ProxyHttpRoute route in routes.Values
                .Where(r => r.Mode == ProxyMode.Http))
            {
                foreach (var frontend in route.Frontends)
                {
                    if (frontend.PublicPort <= 0 || !string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        continue;
                    }

                    if (publicTcpPortToRoute.TryGetValue(frontend.PublicPort, out ProxyRoute conflictRoute))
                    {
                        log.LogError(() => $"HTTP route [{route.Name}] has a public Internet facing port [{frontend.PublicPort}] conflict with TCP route [{conflictRoute.Name}].");
                        configError = true;
                        continue;
                    }

                    var hostPort = $"{frontend.Host}:{frontend.ProxyPort}";

                    if (publicHttpHostPortPathToRoute.TryGetValue(hostPort, out conflictRoute))
                    {
                        log.LogError(() => $"HTTP route [{route.Name}] has a public Internet facing hostname/port [{hostPort}] conflict with HTTP route [{conflictRoute.Name}].");
                        configError = true;
                        continue;
                    }
                    else
                    {
                        publicHttpHostPortPathToRoute.Add($"{hostPort}:{allPrefix}", route);
                    }
                }
            }

            // Now check the routes with path prefixes.

            foreach (ProxyHttpRoute route in routes.Values
                .Where(r => r.Mode == ProxyMode.Http))
            {
                foreach (var frontend in route.Frontends)
                {
                    if (frontend.PublicPort <= 0 || string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        continue;
                    }

                    if (publicTcpPortToRoute.TryGetValue(frontend.PublicPort, out ProxyRoute conflictRoute))
                    {
                        log.LogError(() => $"HTTP route [{route.Name}] has a public Internet facing port [{frontend.PublicPort}] conflict with TCP route [{conflictRoute.Name}].");
                        configError = true;
                        continue;
                    }

                    var pathPrefix   = NormalizePathPrefix(frontend.PathPrefix);
                    var hostPortPath = $"{frontend.Host}:{frontend.ProxyPort}:{pathPrefix}";

                    if (publicHttpHostPortPathToRoute.TryGetValue($"{frontend.Host}:{frontend.ProxyPort}:{allPrefix}", out conflictRoute) ||
                        publicHttpHostPortPathToRoute.TryGetValue(hostPortPath, out conflictRoute))
                    {
                        log.LogError(() => $"HTTP route [{route.Name}] has a public Internet facing hostname/port/path [{hostPortPath}] conflict with HTTP route [{conflictRoute.Name}].");
                        configError = true;
                        continue;
                    }
                    else
                    {
                        publicHttpHostPortPathToRoute.Add(hostPortPath, route);
                    }
                }
            }

            // Verify that TCP routes don't have conflicting HAProxy frontends.  For
            // TCP, this means that a port can have only one assigned frontend.

            var haTcpProxyPortToRoute = new Dictionary<int, ProxyRoute>();

            foreach (ProxyTcpRoute route in routes.Values
                .Where(r => r.Mode == ProxyMode.Tcp))
            {
                foreach (var frontend in route.Frontends)
                {
                    if (haTcpProxyPortToRoute.TryGetValue(frontend.PublicPort, out ProxyRoute conflictRoute))
                    {
                        log.LogError(() => $"TCP route [{route.Name}] has an HAProxy frontend port [{frontend.ProxyPort}] conflict with TCP route [{conflictRoute.Name}].");
                        configError = true;
                    }
                    else
                    {
                        haTcpProxyPortToRoute.Add(frontend.ProxyPort, route);
                    }
                }
            }

            // Verify that HTTP routes don't have conflicting HAProxy frontend ports.  For
            // HTTP, we need to make sure that there isn't already a TCP frontend on the
            // port and then ensure that only one HTTP frontend maps to a hostname/port/path
            // combination.

            var haHttpProxyHostPortPathToRoute = new Dictionary<string, ProxyRoute>(StringComparer.OrdinalIgnoreCase);

            // Check routes without path prefixes first.

            foreach (ProxyHttpRoute route in routes.Values
                .Where(r => r.Mode == ProxyMode.Http))
            {
                foreach (var frontend in route.Frontends)
                {
                    if (!string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        continue;
                    }

                    if (haTcpProxyPortToRoute.TryGetValue(frontend.PublicPort, out ProxyRoute conflictRoute))
                    {
                        log.LogError(() => $"HTTP route [{route.Name}] has an HAProxy frontend port [{frontend.ProxyPort}] conflict with TCP route [{conflictRoute.Name}].");
                        configError = true;
                        continue;
                    }

                    var pathPrefix   = allPrefix;
                    var hostPortPath = $"{frontend.Host}:{frontend.ProxyPort}:{pathPrefix}";

                    if (haHttpProxyHostPortPathToRoute.TryGetValue(hostPortPath, out conflictRoute))
                    {
                        log.LogError(() => $"HTTP route [{route.Name}] has an HAProxy frontend hostname/port/path [{hostPortPath}] conflict with HTTP route [{conflictRoute.Name}].");
                        configError = true;
                    }
                    else
                    {
                        haHttpProxyHostPortPathToRoute.Add(hostPortPath, route);
                    }
                }
            }

            // Now check the routes with path prefixes.

            foreach (ProxyHttpRoute route in routes.Values
                .Where(r => r.Mode == ProxyMode.Http))
            {
                foreach (var frontend in route.Frontends)
                {
                    if (string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        continue;
                    }

                    if (haTcpProxyPortToRoute.TryGetValue(frontend.PublicPort, out ProxyRoute conflictRoute))
                    {
                        log.LogError(() => $"HTTP route [{route.Name}] has an HAProxy frontend port [{frontend.ProxyPort}] conflict with TCP route [{conflictRoute.Name}].");
                        configError = true;
                        continue;
                    }

                    var pathPrefix   = frontend.PathPrefix;
                    var hostPortPath = $"{frontend.Host}:{frontend.ProxyPort}:{pathPrefix}";

                    if (haHttpProxyHostPortPathToRoute.TryGetValue($"{frontend.Host}:{frontend.ProxyPort}:{allPrefix}", out conflictRoute) ||
                        haHttpProxyHostPortPathToRoute.TryGetValue(hostPortPath, out conflictRoute))
                    {
                        log.LogError(() => $"HTTP route [{route.Name}] has an HAProxy frontend hostname/port/path [{hostPortPath}] conflict with HTTP route [{conflictRoute.Name}].");
                        configError = true;
                    }
                    else
                    {
                        haHttpProxyHostPortPathToRoute.Add(hostPortPath, route);
                    }
                }
            }

            //-----------------------------------------------------------------
            // Generate the TCP routes.

            var hasTcpRoutes = false;

            if (routes.Values
                .Where(r => r.Mode == ProxyMode.Tcp)
                .Count() > 0)
            {
                hasTcpRoutes = true;

                sbHaProxy.AppendLine("#------------------------------------------------------------------------------");
                sbHaProxy.AppendLine("# TCP Routes");
            }

            foreach (ProxyTcpRoute tcpRoute in routes.Values
                .Where(r => r.Mode == ProxyMode.Tcp))
            {
                // Generate the resolvers argument to be used to locate the
                // backend servers.

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

                var checkArg    = tcpRoute.Check ? " check" : string.Empty;
                var serverIndex = 0;

                foreach (var backend in tcpRoute.Backends)
                {
                    var backendName = $"server-{serverIndex++}";

                    if (!string.IsNullOrEmpty(backend.Name))
                    {
                        backendName = backend.Name;
                    }

                    sbHaProxy.AppendLine($"    server              {backendName} {backend.Server}:{backend.Port}{checkArg}{initAddrArg}{resolversArg}");
                }
            }

            //-----------------------------------------------------------------
            // HTTP routes are tricker:
            //
            //      1. We need to generate an HAProxy frontend for each IP/port combination 
            //         and then use HOST header or SNI rules in addition to an optional path
            //         prefix to map the correct backend.   This means that neonCLUSTER proxy
            //         frontends don't map directly to HAProxy frontends.
            //
            //      2. We need to generate an HAProxy backend for each neonCLUSTER proxy backend.
            //
            //      3. For TLS frontends, we're going to persist all of the referenced certificates 
            //         into frontend specific folders and then reference the folder in the bind
            //         statement.  HAProxy will use SNI to present the correct certificate to clients.

            var haProxyFrontends = new Dictionary<int, HAProxyHttpFrontend>();

            if (routes.Values
                .Where(r => r.Mode == ProxyMode.Http)
                .Count() > 0)
            {
                if (hasTcpRoutes)
                {
                    sbHaProxy.AppendLine();
                }

                sbHaProxy.AppendLine("#------------------------------------------------------------------------------");
                sbHaProxy.AppendLine("# HTTP Routes");

                // Enumerate all of the routes and build a dictionary with information about
                // the HAProxy frontends we'll need to generate.  This dictionary will be
                // keyed by the host/path.

                foreach (ProxyHttpRoute httpRoute in routes.Values
                    .Where(r => r.Mode == ProxyMode.Http)
                    .OrderBy(r => r.Name))
                {
                    foreach (var frontend in httpRoute.Frontends)
                    {
                        if (!haProxyFrontends.TryGetValue(frontend.ProxyPort, out HAProxyHttpFrontend haProxyFrontend))
                        {
                            haProxyFrontend = new HAProxyHttpFrontend()
                            {
                                Port = frontend.ProxyPort,
                                PathPrefix = NormalizePathPrefix(frontend.PathPrefix)
                            };

                            haProxyFrontends.Add(frontend.ProxyPort, haProxyFrontend);
                        }

                        var hostPath = $"{frontend.Host}:{NormalizePathPrefix(frontend.PathPrefix)}";

                        if (haProxyFrontend.HostPathMappings.ContainsKey(hostPath))
                        {
                            // It's possible to incorrectly define multiple HTTP routes with the same 
                            // host/path mapping to the same HAProxy frontend port.  This code will
                            // simply choose a winner with a warning.

                            // $todo(jeff.lill): 
                            //
                            // I'm not entirely sure that this check is really necessary.

                            ProxyHttpRoute conflictRoute = null;

                            foreach (ProxyHttpRoute checkRoute in routes.Values
                                .Where(r => r.Mode == ProxyMode.Http && r != httpRoute))
                            {
                                if (checkRoute.Frontends.Count(fe => fe.ProxyPort == frontend.ProxyPort && fe.Host.Equals(frontend.Host, StringComparison.CurrentCultureIgnoreCase)) > 0)
                                {
                                    conflictRoute = checkRoute;
                                }
                            }

                            if (conflictRoute != null)
                            {
                                log.LogWarn(() => $"HTTP route [{httpRoute.Name}] defines a frontend for host/port [{frontend.Host}/{frontend.ProxyPort}] which conflicts with route [{conflictRoute.Name}].  This frontend will be ignored.");
                            }
                        }
                        else
                        {
                            haProxyFrontend.HostPathMappings[hostPath] = $"http:{httpRoute.Name}";
                        }

                        if (httpRoute.Log)
                        {
                            // If any of the routes on this port require logging we'll have to
                            // enable logging for all of the routes, since they'll end up sharing
                            // the same proxy frontend.

                            haProxyFrontend.Log = true;
                        }

                        if (frontend.Tls)
                        {
                            if (!clusterCerts.TryGetValue(frontend.CertName, out CertInfo certInfo))
                            {
                                log.LogError(() => $"Route [{httpRoute.Name}] references [{frontend.CertName}] which does not exist.");
                                configError = true;
                                continue;
                            }

                            if (!certInfo.Certificate.IsValidHost(frontend.Host))
                            {
                                log.LogError(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] does not cover host [{frontend.Host}] for a route [{httpRoute.Name}] frontend.");
                            }

                            certInfo.WasReferenced = true;
                            haProxyFrontend.Certificates[certInfo.Name] = certInfo.Certificate;
                        }

                        if (frontend.Tls != haProxyFrontend.Tls)
                        {
                            if (frontend.Tls)
                            {
                                log.LogError(() => $"Route [{httpRoute.Name}] specifies a TLS frontend on port [{frontend.ProxyPort}] that conflict with non-TLS frontends on the same port.");
                                configError = true;
                            }
                            else
                            {
                                log.LogError(() => $"Route [{httpRoute.Name}] specifies a non-TLS frontend on port [{frontend.ProxyPort}] that conflict with TLS frontends on the same port.");
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
    option              forwardfor
    option              http-server-close
");

                    if (haProxyFrontend.Log)
                    {
                        sbHaProxy.AppendLine($"    capture             request header Host len 255");
                        sbHaProxy.AppendLine($"    capture             request header User-Agent len 2048");
                        sbHaProxy.AppendLine($"    log                 global");
                        sbHaProxy.AppendLine($"    log-format          {NeonClusterHelper.GetProxyLogFormat("neon-proxy-" + proxyName, tcp: false)}");
                    }

                    // Generate the backend mappings for frontends without path prefixes first.
                    // This code is a bit of a hack.  It depends on the host/path mapping key
                    // being formatted as $"{host}:{path}" with [path] being normalized.

                    foreach (var hostPathMapping in haProxyFrontend.HostPathMappings
                        .Where(m => HAProxyHttpFrontend.GetPath(m.Key) == allPrefix)
                        .OrderBy(m => HAProxyHttpFrontend.GetHost(m.Key)))
                    {
                        var host = HAProxyHttpFrontend.GetHost(hostPathMapping.Key);

                        sbHaProxy.AppendLine();

                        if (haProxyFrontend.Tls)
                        {
                            sbHaProxy.AppendLine($"    use_backend         {hostPathMapping.Value} if {{ ssl_fc_sni {host} }}");
                        }
                        else
                        {
                            var hostAclName = $"is-{host.Replace('.', '-')}";

                            sbHaProxy.AppendLine($"    acl                 {hostAclName} hdr_reg(host) -i {host}(:\\d+)?");
                            sbHaProxy.AppendLine($"    use_backend         {hostPathMapping.Value} if {hostAclName}");
                        }
                    }

                    // Generate the backend mappings for frontends with path prefixes.
                    // Note that we're going to generate mappings for the longest 
                    // paths first and we're also going to sort by path so the
                    // HAProxy config file will be a bit easier to read.

                    var pathAclCount = 0;

                    foreach (var hostPathMapping in haProxyFrontend.HostPathMappings
                        .Where(m => HAProxyHttpFrontend.GetPath(m.Key) != allPrefix)
                        .OrderByDescending(m => HAProxyHttpFrontend.GetPath(m.Key).Length)
                        .ThenBy(m => HAProxyHttpFrontend.GetPath(m.Key)))
                    {
                        var host        = HAProxyHttpFrontend.GetHost(hostPathMapping.Key);
                        var path        = HAProxyHttpFrontend.GetPath(hostPathMapping.Key);
                        var hostAclName = $"is-{host.Replace('.', '-')}";
                        var pathAclName = $"is-path-{pathAclCount++}";

                        sbHaProxy.AppendLine();

                        if (haProxyFrontend.Tls)
                        {
                            sbHaProxy.AppendLine($"    acl                 {pathAclName} path_beg {path}");
                            sbHaProxy.AppendLine($"    use_backend         {hostPathMapping.Value} if {{ ssl_fc_sni {host} }} {pathAclName}");
                        }
                        else
                        {
                            sbHaProxy.AppendLine($"    acl                 {pathAclName} path_beg {path}");
                            sbHaProxy.AppendLine($"    acl                 {hostAclName} hdr_reg(host) -i {host}(:\\d+)?");
                            sbHaProxy.AppendLine($"    use_backend         {hostPathMapping.Value} if {hostAclName} {pathAclName}");
                        }
                    }
                }

                // Generate the HTTP backends

                foreach (ProxyHttpRoute httpRoute in routes.Values
                    .Where(r => r.Mode == ProxyMode.Http)
                    .OrderBy(r => r.Name))
                {
                    // Generate the resolvers argument to be used to locate the
                    // backend servers.

                    var resolversArg    = string.Empty;

                    if (!string.IsNullOrEmpty(httpRoute.Resolver))
                    {
                        resolversArg = $" resolvers {httpRoute.Resolver}";
                    }

                    var checkArg        = httpRoute.Check ? " check" : string.Empty;
                    var initAddrArg     = " init-addr none";
                    var checkVersionArg = string.Empty;

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
                log.LogError("Proxy configuration aborted due to one or more errors.");
                return (Routes: routes, log.ToString());
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
                    log.LogInfo(() => $"Updating proxy [{proxyDisplayName}] configuration: [routes={routes.Count}] [hash={combinedHash}]");

                    // Write the hash and configuration out as a transaction so we'll 
                    // be sure they match (don't get out of sync).  We don't need to
                    // do CAS here because only one proxy manager will be running
                    // most of the time and even if multiple instances happened to
                    // update this with different values for some reason, the most 
                    // recent updates would be applied the next time a proxy manager
                    // polled the config and then the instances will remain in sync
                    // until the next routing change is detected.

                    var operations = new List<KVTxnOp>()
                    {
                        new KVTxnOp($"{consulPrefix}/proxies/{proxyName}/hash", KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(combinedHash) },
                        new KVTxnOp($"{consulPrefix}/proxies/{proxyName}/conf", KVTxnVerb.Set) { Value = zipBytes }
                    };

                    await consul.KV.Txn(operations, cancellationToken);
                }
                else
                {
                    log.LogInfo(() => $"No changes detected for proxy [{proxyDisplayName}].");
                }
            }
            catch (Exception e)
            {
                // Warn and exit for Consul errors.

                log.LogWarn("Consul request failure.", e);
                return (Routes: routes, log.ToString());
            }

            //-----------------------------------------------------------------
            // Generate the HAProxy bridge configuration.  This configuration is  pretty simple.  
            // All we need to do is forward all endpoints as TCP connections to the proxy we just
            // generated above.  We won't treat HTTP/S specially and we don't need to worry 
            // about TLS termination or generate fancy health checks.
            //
            // The bridge proxy is typically deployed as a standalone Docker container on cluster
            // pet nodes and will expose internal cluster services on the pets using the same
            // ports where they are deployed on the internal Swarm nodes.  This means that containers
            // running on pet nodes can consume cluster services the same way as they do on the manager
            // and worker nodes.
            //
            // This was initially used as a way to route pet node logging traffic from the
            // [neon-log-host] containers to the [neon-log-collector] service to handle
            // upstream log processing and persistance to the Elasticsearch cluster but
            // the bridges could also be used in the future to access any cluster service
            // with a public or private proxy route defined.
            //
            // The code below generally assumes that the bridge target proxy is exposed 
            // on all Swarm manager or worker nodes (via the Docker ingress/mesh network 
            // or because the proxy is running in global mode).  Exactly which nodes will
            // be configured to handle forwarded traffic is determined by the proxy
            // settings.
            //
            // The code below starts by examining the list of active Docker Swarm nodes captured
            // before we started generating proxy configs.  If there are 5 (the default) or fewer 
            // worker nodes, the configuration will use all cluster nodes (including managers) as 
            // target endpoints.  If there are more than 5 worker nodes, the code will randomly 
            // select 5 of them as endpoints.
            //
            // This approach balances the need for simplicity and reliability in the face of 
            // node failure while trying to avoid an explosion of health check traffic.
            //
            // This may become a problem for clusters with a large number of pet nodes
            // banging away with health checks.  One way to mitigate this is to target
            // specific Swarm nodes in the [ProxySettings] by IP address.

            // Determine which cluster Swarm nodes will be targeted by the bridge.  The
            // target nodes may have been specified explicitly by IP address in the 
            // proxy settings or we may need to select them here.
            // 
            // Start out by querying Docker for the current Swarm nodes.

            Dictionary<string, DockerNode>  addressToSwarmNode = new Dictionary<string, DockerNode>();

            foreach (var node in swarmNodes)
            {
                addressToSwarmNode.Add(node.Addr, node);
            }

            var bridgeTargets = new List<string>();

            if (settings.BridgeTargetAddresses.Count > 0)
            {
                // Specific Swarm nodes have been targeted.

                foreach (var targetAddress in settings.BridgeTargetAddresses)
                {
                    bridgeTargets.Add(targetAddress.ToString());

                    if (!addressToSwarmNode.ContainsKey(targetAddress.ToString()))
                    {
                        log.LogWarn(() => $"Proxy bridge target [{targetAddress}] does not reference a known cluster Swarm node.");
                    }
                }
            }
            else
            {
                // We're going to automatically select the target nodes.

                swarmNodes = swarmNodes.Where(n => n.State == "ready").ToList();    // We want only READY Swarm nodes.

                var workers = swarmNodes.Where(n => n.Role == "worker").ToList();

                if (workers.Count >= settings.BridgeTargetCount)
                {
                    // There are enough workers to select targets from, so we'll just do that.
                    // The idea here is to try to keep the managers from doing as much routing 
                    // work as possible because they may be busy handling global cluster activities,
                    // especially for large clusters.

                    foreach (var worker in workers.SelectRandom(settings.BridgeTargetCount))
                    {
                        bridgeTargets.Add(worker.Addr);
                    }
                }
                else
                {
                    // Otherwise for small clusters, we'll select targets from managers
                    // and workers.

                    foreach (var node in swarmNodes.SelectRandom(Math.Min(settings.BridgeTargetCount, swarmNodes.Count)))
                    {
                        bridgeTargets.Add(node.Addr);
                    }
                }
            }

            if (bridgeTargets.Count == 0)
            {
                log.LogWarn(() => $"No bridge targets were specified or are ready.");
            }

            // Generate the contents of the [haproxy.cfg] file.

            sbHaProxy = new StringBuilder();

            sbHaProxy.Append(
$@"#------------------------------------------------------------------------------
# {proxyBridgeDisplayName} HAProxy configuration file.
#
# Generated by:     {serviceName}
# Documentation:    http://cbonte.github.io/haproxy-dconv/1.7/configuration.html#7.1.4

global
    daemon

# Specifiy the maximum number of connections allowed for a proxy instance.

    maxconn             {settings.MaxConnections}

# We're going to disable bridge proxy logging for now because I'm not entirely 
# sure that this will be useful.  If we decide to enable this in the future, we
# should probably specify a different SYSLOG facility so we can distinguish 
# between problems with bridges and normal proxies. 

#   log                 ""${{NEON_NODE_IP}}:{NeonHostPorts.LogHostSysLog}"" len 65535 {NeonSysLogFacility.ProxyName}

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
            // Enable the HAProxy statistics pages.  These will be available on the 
            // [NeonClusterConst.HAProxyStatsPort] port on the [neon-public] or
            // [neon-private] network the proxy serves.
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
            // Generate the TCP bridge routes.

            sbHaProxy.AppendLine("#------------------------------------------------------------------------------");
            sbHaProxy.AppendLine("# TCP Routes");

            var bridgePorts = new HashSet<int>();

            foreach (ProxyTcpRoute tcpRoute in routes.Values
                .Where(r => r.Mode == ProxyMode.Tcp))
            {
                foreach (var frontEnd in tcpRoute.Frontends)
                {
                    if (!bridgePorts.Contains(frontEnd.ProxyPort))
                    {
                        bridgePorts.Add(frontEnd.ProxyPort);
                    }
                }
            }

            foreach (ProxyHttpRoute httpRoute in routes.Values
                .Where(r => r.Mode == ProxyMode.Http))
            {
                foreach (var frontEnd in httpRoute.Frontends)
                {
                    if (!bridgePorts.Contains(frontEnd.ProxyPort))
                    {
                        bridgePorts.Add(frontEnd.ProxyPort);
                    }
                }
            }

            foreach (var port in bridgePorts)
            {
                    sbHaProxy.Append(
$@"
listen tcp:port-{port}
    mode                tcp
    bind                *:{port}
");

                // Bridge logging is disabled for now.

                // $todo(jeff.lill):
                //
                // I wonder if it's possible to have HAProxy log ONLY health checks.  It seems like this
                // is the main thing we'd really want to log for bridge proxies.

                //sbHaProxy.AppendLine($"    log                 global");
                //sbHaProxy.AppendLine($"    log-format          {NeonClusterHelper.GetProxyLogFormat("neon-proxy-" + proxyName, tcp: true)}");
                //sbHaProxy.AppendLine($"    option              log-health-checks");

                var checkArg    = " check";
                var initAddrArg = " init-addr none";
                var serverIndex = 0;

                foreach (var targetAddress in bridgeTargets)
                {
                    var backendName = $"server-{serverIndex++}";

                    sbHaProxy.AppendLine($"    server              {backendName} {targetAddress}:{port}{checkArg}{initAddrArg}");
                }
            }

            // Generate the [neon-proxy] service compatible configuration ZIP archive for the bridge.
            // Note that the bridge forwards only TCP traffic so there are no TLS certificates.

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
                    zip.CommitUpdate();
                }

                zipBytes = ms.ToArray();
            }

            // Compute the MD5 hash for the configuration ZIP.

            hasher       = MD5.Create();
            combinedHash = Convert.ToBase64String(hasher.ComputeHash(zipBytes));

            // Compare the hash against what's already published to Consul
            // for the proxy and update these keys if the hashes differ.

            publish = false;

            try
            {
                if (!await consul.KV.Exists($"{consulPrefix}/proxies/{proxyBridgeName}/hash", cancellationToken) ||
                    !await consul.KV.Exists($"{consulPrefix}/proxies/{proxyBridgeName}/conf", cancellationToken))
                {
                    publish = true; // Nothing published yet.
                }
                else
                {
                    publish = combinedHash != await consul.KV.GetString($"{consulPrefix}/proxies/{proxyBridgeName}/hash", cancellationToken);
                }

                if (publish)
                {
                    log.LogInfo(() => $"Updating proxy [{proxyBridgeDisplayName}] configuration: [routes={bridgePorts.Count}] [hash={combinedHash}]");

                    // Write the hash and configuration out as a transaction so we'll 
                    // be sure they match (don't get out of sync).  We don't need to
                    // do CAS here because only one proxy manager will be running
                    // most of the time and even if multiple instances happened to
                    // update this with different values for some reason, the most 
                    // recent updates would be applied the next time a proxy manager
                    // polled the config and then the instances will remain in sync
                    // until the next routing change is detected.

                    var operations = new List<KVTxnOp>()
                    {
                        new KVTxnOp($"{consulPrefix}/proxies/{proxyBridgeName}/hash", KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(combinedHash) },
                        new KVTxnOp($"{consulPrefix}/proxies/{proxyBridgeName}/conf", KVTxnVerb.Set) { Value = zipBytes }
                    };

                    await consul.KV.Txn(operations, cancellationToken);
                }
                else
                {
                    log.LogInfo(() => $"No changes detected for proxy [{proxyBridgeDisplayName}].");
                }
            }
            catch (Exception e)
            {
                // Warn and exit for Consul/Docker errors.

                log.LogWarn("Consul or Docker request failure.", e);
                return (Routes: routes, log.ToString());
            }
            
            //-----------------------------------------------------------------
            // We're done

            return (Routes: routes, Status: log.ToString());
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

                cachedClusterDefinition         = await NeonClusterHelper.GetDefinitionAsync(cachedClusterDefinition, cancellationToken);
                cachedClusterDefinition.Hosting = await vault.ReadJsonAsync<HostingOptions>("neon-secret/hosting/options", cancellationToken: cancellationToken);

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

                var hostingManager = HostingManager.GetManager(cluster);

                if (!hostingManager.CanUpdatePublicEndpoints)
                {
                    return; // Operators need to maintain the load balancer manually for this environment.
                }

                var publicEndpoints = hostingManager.GetPublicEndpoints();
                var latestEndpoints = new List<HostedEndpoint>();

                // Build a dictionary mapping the load balancer frontend ports to 
                // internal HAProxy frontend ports for the latest routes.

                foreach (ProxyTcpRoute route in publicRoutes.Values
                    .Where(r => r.Mode == ProxyMode.Tcp))
                {
                    foreach (var frontend in route.Frontends)
                    {
                        if (frontend.PublicPort > 0)
                        {
                            latestEndpoints.Add(new HostedEndpoint(HostedEndpointProtocol.Tcp, frontend.PublicPort, frontend.ProxyPort));
                        }
                    }
                }

                foreach (ProxyHttpRoute route in publicRoutes.Values
                    .Where(r => r.Mode == ProxyMode.Http))
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
                    log.LogInfo(() => $"Public cluster load balancer configuration matches current routes. [endpoint-count={publicEndpoints.Count}]");
                    return;
                }

                // The endpoints have changed so update the cluster.

                log.LogInfo(() => $"Updating: public cluster load balancer and security. [endpoint-count={publicEndpoints.Count}]");
                hostingManager.UpdatePublicEndpoints(latestEndpoints);
                log.LogInfo(() => $"Update Completed: public cluster load balancer and security. [endpoint-count={publicEndpoints.Count}]");
            }
            catch (Exception e)
            {
                log.LogError($"Unable to update cluster load balancer and/or network security configuration.", e);
            }
        }
    }
}
