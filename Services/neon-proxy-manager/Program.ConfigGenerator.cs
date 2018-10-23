//-----------------------------------------------------------------------------
// FILE:	    Program.ConfigGenerator.cs
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

namespace NeonProxyManager
{
    public static partial class Program
    {

        /// <summary>
        /// Generates the HAProxy and Vault configurations.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task ConfigGeneratorAsync()
        {
            // The implementation is pretty straightforward:
            //
            // We're simply going to listen for [ProxyRegenerateMessage] messages
            // broadcast on the [proxy-notify] channel when changes are made to
            // the proxy configuration or periodicaly broadcast by [neon-hive-manager] 
            // signalling that the proxy configurations should be proactively
            // regenerated to recover from a potential loss of update notification
            // messages as well as to check for configuration problems like expired
            // or expiring TLS certificates.
            //
            // Note that we're going to broadcast a single [ProxyRegenerateMessage] 
            // to ourself so we'll perform an immediate regeneration when the 
            // service starts.

            proxyNotifyChannel.Publish(new ProxyRegenerateMessage() { Reason="[neon-proxy-manager]: Startup" });

            await Task.Run(
                () =>
                {
                    processingConfigs = false;

                    try
                    {
                        log.LogInfo(() => "MONITOR: Starting");
                        log.LogInfo(() => $"MONITOR: Listening for [{nameof(ProxyRegenerateMessage)}] messages on [{proxyNotifyChannel.Name}].");

                        proxyNotifyChannel.Consume<ProxyRegenerateMessage>(
                            async notifyMessage =>
                            {
                                try
                                {
                                    log.LogInfo(() => $"MONITOR: Received [{nameof(ProxyRegenerateMessage)}({notifyMessage.Reason})]");

                                    if (processingConfigs)
                                    {
                                        log.LogInfo(() => $"MONITOR: Ignorning [{nameof(ProxyRegenerateMessage)}] because we're already generating proxy configurations.");
                                        return;
                                    }

                                    processingConfigs = true;

                                    // Load and check the hive certificates.

                                    var hiveCerts = new HiveCerts();
                                    var utcNow    = DateTime.UtcNow;

                                    log.LogInfo(() => "Reading hive certificates.");

                                    try
                                    {
                                        foreach (var certName in await vault.ListAsync(vaultCertPrefix, terminator.CancellationToken))
                                        {
                                            var certificate = await vault.ReadJsonAsync<TlsCertificate>($"{vaultCertPrefix}/{certName}", terminator.CancellationToken);
                                            var certInfo    = new CertInfo(certName, certificate);

                                            if (!certInfo.Certificate.IsValidDate(utcNow))
                                            {
                                                log.LogError(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] expired at [{certInfo.Certificate.ValidUntil.Value}].");
                                            }
                                            else if (!certInfo.Certificate.IsValidDate(utcNow + certWarnTime))
                                            {
                                                log.LogWarn(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] will expire in [{(certInfo.Certificate.ValidUntil.Value - utcNow).TotalDays}] days at [{certInfo.Certificate.ValidUntil}].");
                                            }

                                            hiveCerts.Add(certInfo);
                                        }
                                    }
                                    catch (Exception e)
                                    {
                                        log.LogError("Unable to load certificates from Vault.", e);
                                        log.LogError(() => "Aborting load balancer configuration.");
                                    }

                                    // Fetch the hive definition and detect whether it changed since the
                                    // previous run.

                                    var currentHiveDefinition = await HiveHelper.GetDefinitionAsync(hiveDefinition, terminator.CancellationToken);

                                    hiveDefinitionChanged = hiveDefinition == null || !NeonHelper.JsonEquals(hiveDefinition, currentHiveDefinition);
                                    hiveDefinition        = currentHiveDefinition;

                                    // Fetch the list of active Docker Swarm nodes.  We'll need this to generate the
                                    // proxy bridge configurations.

                                    swarmNodes = await docker.NodeListAsync(terminator.CancellationToken);

                                    // Rebuild the proxy configurations and write the captured status to
                                    // Consul to make it available for the [neon proxy public|private status]
                                    // command.  Note that we're going to build the [neon-proxy-public-bridge]
                                    // and [neon-proxy-private-bridge] configurations as well for use by any 
                                    // hive pet nodes.

                                    var publicBuildStatus = await BuildProxyConfigAsync("public", hiveCerts);
                                    var publicProxyStatus = new LoadBalancerStatus() { Status = publicBuildStatus.Status };

                                    await consul.KV.PutString($"{proxyStatusKey}/public", NeonHelper.JsonSerialize(publicProxyStatus), terminator.CancellationToken);

                                    var privateBuildStatus = await BuildProxyConfigAsync("private", hiveCerts);
                                    var privateProxyStatus = new LoadBalancerStatus() { Status = privateBuildStatus.Status };

                                    await consul.KV.PutString($"{proxyStatusKey}/private", NeonHelper.JsonSerialize(privateProxyStatus), terminator.CancellationToken);

                                    // We need to ensure that the deployment's load balancer and security
                                    // rules are updated to match changes to the public load balancer rules.
                                    // Note that we're going to call this even if the PUBLIC load balancer
                                    // hasn't changed to ensure that the load balancer doesn't get
                                    // out of sync.

                                    await UpdateHiveNetwork(publicBuildStatus.Rules);
                                }
                                finally
                                {
                                    processingConfigs = false;
                                }
                            });
                    }
                    catch (OperationCanceledException)
                    {
                        log.LogInfo(() => "MONITOR: Terminating.");
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
                });
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
        /// Returns the <c>option</c> argument for a <see cref="LoadBalancerRule"/> that has
        /// enables HTTP health checks.
        /// </summary>
        /// <param name="rule">The load balancer rule.</param>
        /// <returns>The option argument string.</returns>
        private static string GetHttpCheckOptionArgs(LoadBalancerRule rule)
        {
            Covenant.Requires<ArgumentNullException>(rule != null);

            if (rule.UseHttpCheckMode)
            {
                // Generate the request headers.  We're going to append the [CheckHost] if present first, 
                // followed by any custom check headers.

                var headers = string.Empty;

                if (!string.IsNullOrWhiteSpace(rule.CheckHost))
                {
                    headers += $@"Host: {rule.CheckHost}\r\n";
                }

                foreach (var header in rule.CheckHeaders)
                {
                    headers += $@"{header.Name}: {header.Value}\r\n";
                }

                var optionArgs = $"{rule.CheckMethod.ToUpperInvariant()} {rule.CheckUri} HTTP/{rule.CheckVersion}";

                if (!string.IsNullOrEmpty(headers))
                {
                    optionArgs += $@"\r\n{headers}";
                }

                return optionArgs;
            }
            else
            {
                throw new InvalidOperationException($"Rule [{rule.Name}] does not perform HTTP health checks.");
            }
        }

        /// <summary>
        /// Rebuilds the configurations for a public or private load balancers and 
        /// persists them to Consul if they differ from the previous version.
        /// </summary>
        /// <param name="loadBalancerName">The load balancer name: <b>public</b> or <b>private</b>.</param>
        /// <param name="hiveCerts">The hive certificate information.</param>
        /// <returns>
        /// A tuple including the load balancer's rule dictionary and publication status details.
        /// </returns>
        private static async Task<(Dictionary<string, LoadBalancerRule> Rules, string Status)> 
            BuildProxyConfigAsync(string loadBalancerName, HiveCerts hiveCerts)
        {
            var proxyDisplayName       = loadBalancerName.ToUpperInvariant();
            var proxyBridgeName        = $"{loadBalancerName}-bridge";
            var proxyBridgeDisplayName = proxyBridgeName.ToUpperInvariant();
            var isPublic               = loadBalancerName.Equals("public", StringComparison.InvariantCultureIgnoreCase);
            var configError            = false;
            var log                    = new LogRecorder(Program.log);

            log.LogInfo(() => $"Rebuilding load balancer [{proxyDisplayName}].");

            // We need to track which certificates are actually referenced by load balancer rules.

            hiveCerts.ClearReferences();

            // Load the load balancer's settings and rules.

            string                  proxyPrefix = $"{proxyConfKey}/{loadBalancerName}";
            var                     rules       = new Dictionary<string, LoadBalancerRule>();
            var                     hostGroups  = hiveDefinition.GetHostGroups(excludeAllGroup: false);
            LoadBalancerSettings    settings;

            try
            {
                settings = await consul.KV.GetObjectOrDefault<LoadBalancerSettings>($"{proxyPrefix}/settings", terminator.CancellationToken);

                if (settings == null)
                {
                    // Initialize default settings for the load balancer if they aren't written to Consul yet.

                    HiveProxyPorts proxyPorts;

                    switch (loadBalancerName)
                    {
                        case "public":

                            proxyPorts = HiveConst.PublicProxyPorts;
                            break;

                        case "private":

                            proxyPorts = HiveConst.PrivateProxyPorts;
                            break;

                        default:

                            throw new NotImplementedException();
                    }

                    settings = new LoadBalancerSettings()
                    {
                        ProxyPorts = proxyPorts
                    };

                    log.LogInfo(() => $"Updating proxy [{proxyDisplayName}] settings.");
                    await consul.KV.PutString($"{proxyPrefix}/settings", NeonHelper.JsonSerialize(settings, Formatting.None), terminator.CancellationToken);
                }

                log.LogInfo(() => $"Reading [{proxyDisplayName}] rules.");

                var result = await consul.KV.List($"{proxyPrefix}/rules/", terminator.CancellationToken);

                if (result.Response != null)
                {
                    foreach (var ruleKey in result.Response)
                    {
                        var rule = LoadBalancerRule.ParseJson(Encoding.UTF8.GetString(ruleKey.Value));

                        rules.Add(rule.Name, rule);
                    }
                }
            }
            catch (Exception e)
            {
                // Warn and exit for (presumably transient) Consul errors.

                log.LogWarn($"Consul request failure for load balancer [{proxyDisplayName}].", e);
                return (Rules: rules, Status: log.ToString());
            }

            log.Record();

            // Record some details about the rules.

            var httpRuleCount = rules.Values.Count(r => r.Mode == LoadBalancerMode.Http);
            var tcpRuleCount  = rules.Values.Count(r => r.Mode == LoadBalancerMode.Tcp);

            // Record HTTP rule summaries.

            if (httpRuleCount == 0 && tcpRuleCount == 0)
            {
                log.Record("*** No load balancer rules defined.");
            }

            if (httpRuleCount > 0)
            {
                log.Record($"HTTP Rules [count={httpRuleCount}]");
                log.Record("------------------------------");

                foreach (LoadBalancerHttpRule rule in rules.Values
                    .Where(r => r.Mode == LoadBalancerMode.Http)
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                {
                    log.Record($"{rule.Name}:");

                    foreach (var frontend in rule.Frontends)
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

                    foreach (var backend in rule.SelectBackends(hostGroups))
                    {
                        log.Record($"    backend:         {backend.Server}:{backend.Port}");
                    }

                    log.Record();
                }
            }

            log.Record();

            // Record TCP rule summaries.

            if (tcpRuleCount > 0)
            {
                log.Record($"TCP Rules [count={tcpRuleCount}]");
                log.Record("------------------------------");

                foreach (LoadBalancerTcpRule rule in rules.Values
                    .Where(r => r.Mode == LoadBalancerMode.Tcp)
                    .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase))
                {
                    var maxconn = rule.MaxConnections == 0 ? "unlimited" : rule.MaxConnections.ToString();

                    log.Record($"{rule.Name}:");

                    foreach (var frontend in rule.Frontends)
                    {
                        log.Record($"    frontend:");
                        log.Record($"        public-port: {frontend.PublicPort}");
                        log.Record($"        proxy-port:  {frontend.ProxyPort}");
                    }

                    foreach (var backend in rule.SelectBackends(hostGroups))
                    {
                        log.Record($"    backend:         {backend.Server}:{backend.Port}");
                    }

                    log.Record();   
                }
            }

            log.Record();

            // Verify the configuration.

            var loadBalancerDefinition = new LoadBalancerDefinition()
            {
                Name     = loadBalancerName,
                Settings = settings,
                Rules    = rules
            };

            var validationContext = loadBalancerDefinition.Validate(hiveCerts.ToTlsCertificateDictionary(), addImplicitFrontends: true);

            if (validationContext.HasErrors)
            {
                log.LogError(validationContext.GetErrors());
                return (Rules: rules, log.ToString());
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
# Documentation:    http://cbonte.github.io/haproxy-dconv/1.8/configuration.html

global
    daemon

# Specify the maximum number of connections allowed for a proxy instance.

    maxconn             {settings.MaxConnections}

# Enable logging to syslog on the local Docker host under the
# [HiveSysLogFacility_ProxyPublic] facility.

    log                 ""${{NEON_NODE_IP}}:{HiveHostPorts.LogHostSysLog}"" len 65535 {HiveSysLogFacility.ProxyName}

# Certificate Authority and Certificate file locations:

    ca-base             ""${{HAPROXY_CONFIG_FOLDER}}""
    crt-base            ""${{HAPROXY_CONFIG_FOLDER}}""

# Disable TLS certificate verification for backend health checks:

    ssl-server-verify   none

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
            // [HiveConst.HAProxyStatsPort] port on the [neon-public] or
            // [neon-private] network the proxy serves.
            //
            // HAProxy statistics pages are not intended to be viewed directly by
            // by hive operators.  Instead, the statistics from multiple HAProxy
            // instances will be aggregated by the hive Dashboard.

            sbHaProxy.AppendLine($@"
#------------------------------------------------------------------------------
# Enable HAProxy statistics pages.

frontend haproxy_stats
    bind                *:{HiveConst.HAProxyStatsPort}
    mode                http
    log                 global
    option              httplog
    option              http-keep-alive
    use_backend         haproxy_stats

backend haproxy_stats
    mode                http
    stats               enable
    stats               scope .
    stats               uri {HiveConst.HaProxyStatsUri}
    stats               refresh 5s
");
            //-----------------------------------------------------------------
            // Verify that the rules don't conflict.

            // Verify that TCP rules don't have conflicting publically facing ports.

            var publicTcpPortToRule = new Dictionary<int, LoadBalancerRule>();

            foreach (LoadBalancerTcpRule rule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Tcp))
            {
                foreach (var frontend in rule.Frontends)
                {
                    if (frontend.PublicPort <= 0)
                    {
                        continue;
                    }

                    if (publicTcpPortToRule.TryGetValue(frontend.PublicPort, out LoadBalancerRule conflictRule))
                    {
                        log.LogError(() => $"TCP rule [{rule.Name}] has a public Internet facing port [{frontend.PublicPort}] conflict with TCP rule [{conflictRule.Name}].");
                        configError = true;
                    }
                    else
                    {
                        publicTcpPortToRule.Add(frontend.PublicPort, rule);
                    }
                }
            }

            // Verify that HTTP rules don't have conflicting publically facing ports and path
            // prefix combinations.  To pass, an HTTP frontend public port can't already be assigned
            // to a TCP rule and the hostname/port/path combination can't already be assigned to
            // another frontend.
            //
            // The wrinkle here is that we need ensure that rules with a path prefix don't conflict
            // with rules that don't.

            var publicHttpHostPortPathToRules = new Dictionary<string, LoadBalancerRule>();

            // Check rules without path prefixes first.

            foreach (LoadBalancerHttpRule rule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Http))
            {
                foreach (var frontend in rule.Frontends)
                {
                    if (frontend.PublicPort <= 0 || !string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        continue;
                    }

                    if (publicTcpPortToRule.TryGetValue(frontend.PublicPort, out LoadBalancerRule conflictRule))
                    {
                        log.LogError(() => $"HTTP rule [{rule.Name}] has a public Internet facing port [{frontend.PublicPort}] conflict with TCP rule [{conflictRule.Name}].");
                        configError = true;
                        continue;
                    }

                    var hostPort = $"{frontend.Host}:{frontend.ProxyPort}";

                    if (publicHttpHostPortPathToRules.TryGetValue(hostPort, out conflictRule))
                    {
                        log.LogError(() => $"HTTP rule [{rule.Name}] has a public Internet facing hostname/port [{hostPort}] conflict with HTTP rule [{conflictRule.Name}].");
                        configError = true;
                        continue;
                    }
                    else
                    {
                        publicHttpHostPortPathToRules.Add($"{hostPort}:{allPrefix}", rule);
                    }
                }
            }

            // Now check the rules with path prefixes.

            foreach (LoadBalancerHttpRule rule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Http))
            {
                foreach (var frontend in rule.Frontends)
                {
                    if (frontend.PublicPort <= 0 || string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        continue;
                    }

                    if (publicTcpPortToRule.TryGetValue(frontend.PublicPort, out LoadBalancerRule conflictRule))
                    {
                        log.LogError(() => $"HTTP rule [{rule.Name}] has a public Internet facing port [{frontend.PublicPort}] conflict with TCP rule [{conflictRule.Name}].");
                        configError = true;
                        continue;
                    }

                    var pathPrefix   = NormalizePathPrefix(frontend.PathPrefix);
                    var hostPortPath = $"{frontend.Host}:{frontend.ProxyPort}:{pathPrefix}";

                    if (publicHttpHostPortPathToRules.TryGetValue($"{frontend.Host}:{frontend.ProxyPort}:{allPrefix}", out conflictRule) ||
                        publicHttpHostPortPathToRules.TryGetValue(hostPortPath, out conflictRule))
                    {
                        log.LogError(() => $"HTTP rule [{rule.Name}] has a public Internet facing hostname/port/path [{hostPortPath}] conflict with HTTP rule [{conflictRule.Name}].");
                        configError = true;
                        continue;
                    }
                    else
                    {
                        publicHttpHostPortPathToRules.Add(hostPortPath, rule);
                    }
                }
            }

            // Verify that TCP rules don't have conflicting HAProxy frontends.  For
            // TCP, this means that a port can have only one assigned frontend.

            var haTcpProxyPortToRule = new Dictionary<int, LoadBalancerRule>();

            foreach (LoadBalancerTcpRule rule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Tcp))
            {
                foreach (var frontend in rule.Frontends)
                {
                    if (haTcpProxyPortToRule.TryGetValue(frontend.PublicPort, out LoadBalancerRule conflictRule))
                    {
                        log.LogError(() => $"TCP rule [{rule.Name}] has an HAProxy frontend port [{frontend.ProxyPort}] conflict with TCP rule [{conflictRule.Name}].");
                        configError = true;
                    }
                    else
                    {
                        haTcpProxyPortToRule.Add(frontend.ProxyPort, rule);
                    }
                }
            }

            // Verify that HTTP rules don't have conflicting HAProxy frontend ports.  For
            // HTTP, we need to make sure that there isn't already a TCP frontend on the
            // port and then ensure that only one HTTP frontend maps to a hostname/port/path
            // combination.

            var haHttpProxyHostPortPathToRule = new Dictionary<string, LoadBalancerRule>(StringComparer.OrdinalIgnoreCase);

            // Check rules without path prefixes first.

            foreach (LoadBalancerHttpRule rule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Http))
            {
                foreach (var frontend in rule.Frontends)
                {
                    if (!string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        continue;
                    }

                    if (haTcpProxyPortToRule.TryGetValue(frontend.PublicPort, out LoadBalancerRule conflictRule))
                    {
                        log.LogError(() => $"HTTP rule [{rule.Name}] has an HAProxy frontend port [{frontend.ProxyPort}] conflict with TCP rule [{conflictRule.Name}].");
                        configError = true;
                        continue;
                    }

                    var pathPrefix   = allPrefix;
                    var hostPortPath = $"{frontend.Host}:{frontend.ProxyPort}:{pathPrefix}";

                    if (haHttpProxyHostPortPathToRule.TryGetValue(hostPortPath, out conflictRule))
                    {
                        log.LogError(() => $"HTTP rule [{rule.Name}] has an HAProxy frontend hostname/port/path [{hostPortPath}] conflict with HTTP rule [{conflictRule.Name}].");
                        configError = true;
                    }
                    else
                    {
                        haHttpProxyHostPortPathToRule.Add(hostPortPath, rule);
                    }
                }
            }

            // Now check the rules with path prefixes.

            foreach (LoadBalancerHttpRule rule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Http))
            {
                foreach (var frontend in rule.Frontends)
                {
                    if (string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        continue;
                    }

                    if (haTcpProxyPortToRule.TryGetValue(frontend.PublicPort, out LoadBalancerRule conflictRule))
                    {
                        log.LogError(() => $"HTTP rule [{rule.Name}] has an HAProxy frontend port [{frontend.ProxyPort}] conflict with TCP rule [{conflictRule.Name}].");
                        configError = true;
                        continue;
                    }

                    var pathPrefix   = frontend.PathPrefix;
                    var hostPortPath = $"{frontend.Host}:{frontend.ProxyPort}:{pathPrefix}";

                    if (haHttpProxyHostPortPathToRule.TryGetValue($"{frontend.Host}:{frontend.ProxyPort}:{allPrefix}", out conflictRule) ||
                        haHttpProxyHostPortPathToRule.TryGetValue(hostPortPath, out conflictRule))
                    {
                        log.LogError(() => $"HTTP rule [{rule.Name}] has an HAProxy frontend hostname/port/path [{hostPortPath}] conflict with HTTP rule [{conflictRule.Name}].");
                        configError = true;
                    }
                    else
                    {
                        haHttpProxyHostPortPathToRule.Add(hostPortPath, rule);
                    }
                }
            }

            //-----------------------------------------------------------------
            // Generate the TCP rules.

            var hasTcpRules = false;

            if (rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Tcp)
                .Count() > 0)
            {
                hasTcpRules = true;

                sbHaProxy.AppendLine("#------------------------------------------------------------------------------");
                sbHaProxy.AppendLine("# TCP Rules");
            }

            foreach (LoadBalancerTcpRule tcpRule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Tcp))
            {
                // Generate the resolvers argument to be used to locate the
                // backend servers.

                var initAddrArg  = " init-addr last,libc,none";
                var resolversArg = string.Empty;

                if (!string.IsNullOrEmpty(tcpRule.Resolver))
                {
                    resolversArg = $" resolvers {tcpRule.Resolver}";
                }

                // Generate the frontend and it's associated backend servers.

                foreach (var frontend in tcpRule.Frontends)
                {
                    sbHaProxy.Append(
$@"
listen tcp:{tcpRule.Name}-port-{frontend.ProxyPort}
    mode                tcp
    bind                *:{frontend.ProxyPort}
");

                    if (tcpRule.MaxConnections > 0)
                    {
                        sbHaProxy.AppendLine($"    maxconn             {tcpRule.MaxConnections}");
                    }

                    if (tcpRule.Log)
                    {
                        sbHaProxy.AppendLine($"    log                 global");
                        sbHaProxy.AppendLine($"    log-format          {HiveHelper.GetProxyLogFormat("neon-proxy-" + loadBalancerName, tcp: true)}");
                    }

                    if (tcpRule.CheckMode != LoadBalancerCheckMode.Disabled && tcpRule.LogChecks)
                    {
                        sbHaProxy.AppendLine($"    option              log-health-checks");
                    }

                    if (tcpRule.UseHttpCheckMode)
                    {
                        sbHaProxy.AppendLine($"    option              httpchk {GetHttpCheckOptionArgs(tcpRule)}");

                        if (!string.IsNullOrEmpty(tcpRule.CheckExpect))
                        {
                            sbHaProxy.AppendLine($"    http-check          expect {tcpRule.CheckExpect.Trim()}");
                        }
                    }
                }

                var checkArg    = tcpRule.CheckMode != LoadBalancerCheckMode.Disabled ? " check" : " no-check";
                var checkSslArg = tcpRule.CheckTls ? " check-ssl" : string.Empty;
                var serverIndex = 0;

                if (tcpRule.CheckMode == LoadBalancerCheckMode.Disabled)
                {
                    checkSslArg = string.Empty;
                }

                foreach (var backend in tcpRule.SelectBackends(hostGroups))
                {
                    var backendName = $"server-{serverIndex++}";

                    if (!string.IsNullOrEmpty(backend.Name))
                    {
                        backendName = backend.Name;
                    }

                    sbHaProxy.AppendLine($"    server              {backendName} {backend.Server}:{backend.Port}{checkArg}{checkSslArg}{initAddrArg}{resolversArg}");
                }
            }

            //-----------------------------------------------------------------
            // HTTP rules are tricker:
            //
            //      1. We need to generate an HAProxy frontend for each IP/port combination 
            //         and then use HOST header or SNI rules in addition to an optional path
            //         prefix to map the correct backend.   This means that neonHIVE proxy
            //         frontends don't map directly to HAProxy frontends.
            //
            //      2. We need to generate an HAProxy backend for each neonHIVE proxy backend.
            //
            //      3. For TLS frontends, we're going to persist all of the referenced certificates 
            //         into frontend specific folders and then reference the folder in the bind
            //         statement.  HAProxy will use SNI to present the correct certificate to clients.

            var haProxyFrontends = new Dictionary<int, HAProxyHttpFrontend>();

            if (rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Http)
                .Count() > 0)
            {
                if (hasTcpRules)
                {
                    sbHaProxy.AppendLine();
                }

                sbHaProxy.AppendLine("#------------------------------------------------------------------------------");
                sbHaProxy.AppendLine("# HTTP Rules");

                // Enumerate all of the rules and build a dictionary with information about
                // the HAProxy frontends we'll need to generate.  This dictionary will be
                // keyed by the host/path.

                foreach (LoadBalancerHttpRule httpRule in rules.Values
                    .Where(r => r.Mode == LoadBalancerMode.Http)
                    .OrderBy(r => r.Name))
                {
                    foreach (var frontend in httpRule.Frontends)
                    {
                        if (!haProxyFrontends.TryGetValue(frontend.ProxyPort, out HAProxyHttpFrontend haProxyFrontend))
                        {
                            haProxyFrontend = new HAProxyHttpFrontend()
                            {
                                Port       = frontend.ProxyPort,
                                PathPrefix = NormalizePathPrefix(frontend.PathPrefix)
                            };

                            haProxyFrontends.Add(frontend.ProxyPort, haProxyFrontend);
                        }

                        var hostPath = $"{frontend.Host ?? string.Empty}:{NormalizePathPrefix(frontend.PathPrefix)}";

                        if (haProxyFrontend.HostPathMappings.ContainsKey(hostPath))
                        {
                            // It's possible to incorrectly define multiple HTTP rules with the same 
                            // host/path mapping to the same HAProxy frontend port.  This code will
                            // simply choose a winner with a warning.

                            // $todo(jeff.lill): 
                            //
                            // I'm not entirely sure that this check is really necessary.

                            LoadBalancerHttpRule conflictRule = null;

                            foreach (LoadBalancerHttpRule checkRule in rules.Values
                                .Where(r => r.Mode == LoadBalancerMode.Http && r != httpRule))
                            {
                                if (checkRule.Frontends.Count(fe => fe.ProxyPort == frontend.ProxyPort && fe.Host.Equals(frontend.Host, StringComparison.CurrentCultureIgnoreCase)) > 0)
                                {
                                    conflictRule = checkRule;
                                }
                            }

                            if (conflictRule != null)
                            {
                                log.LogWarn(() => $"HTTP rule [{httpRule.Name}] defines a frontend for host/port [{frontend.Host}/{frontend.ProxyPort}] which conflicts with rule [{conflictRule.Name}].  This frontend will be ignored.");
                            }
                        }
                        else
                        {
                            haProxyFrontend.HostPathMappings[hostPath] = $"http:{httpRule.Name}";
                        }

                        if (httpRule.Log)
                        {
                            // If any of the rules on this port require logging we'll have to
                            // enable logging for all of the rules, since they'll end up sharing
                            // the same proxy frontend.

                            haProxyFrontend.Log = true;
                        }

                        if (frontend.Tls)
                        {
                            if (!hiveCerts.TryGetValue(frontend.CertName, out CertInfo certInfo))
                            {
                                log.LogError(() => $"Rule [{httpRule.Name}] references certificate [{frontend.CertName}] which does not exist or could not be loaded.");
                                configError = true;
                                continue;
                            }

                            if (!certInfo.Certificate.IsValidHost(frontend.Host))
                            {
                                log.LogError(() => $"Certificate [{certInfo.Name}] for [hosts={certInfo.Certificate.HostNames}] does not cover host [{frontend.Host}] for a rule [{httpRule.Name}] frontend.");
                            }

                            certInfo.WasReferenced = true;
                            haProxyFrontend.Certificates[certInfo.Name] = certInfo.Certificate;
                        }

                        if (frontend.Tls != haProxyFrontend.Tls)
                        {
                            if (frontend.Tls)
                            {
                                log.LogError(() => $"Rule [{httpRule.Name}] specifies a TLS frontend on port [{frontend.ProxyPort}] that conflict with non-TLS frontends on the same port.");
                                configError = true;
                            }
                            else
                            {
                                log.LogError(() => $"Rule [{httpRule.Name}] specifies a non-TLS frontend on port [{frontend.ProxyPort}] that conflict with TLS frontends on the same port.");
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
    unique-id-format    {HiveConst.HAProxyUidFormat}
    option              forwardfor
    option              http-keep-alive
    http-request        set-header X-Forwarded-Proto https if {{ ssl_fc }}
");

                    if (haProxyFrontend.Log)
                    {
                        sbHaProxy.AppendLine($"    capture             request header Host len 255");
                        sbHaProxy.AppendLine($"    capture             request header User-Agent len 2048");
                        sbHaProxy.AppendLine($"    log                 global");
                        sbHaProxy.AppendLine($"    log-format          {HiveHelper.GetProxyLogFormat("neon-proxy-" + loadBalancerName, tcp: false)}");
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
                        else if (!string.IsNullOrEmpty(host))
                        {
                            var hostAclName = $"is-{host.Replace('.', '-')}";

                            sbHaProxy.AppendLine($"    acl                 {hostAclName} hdr_reg(host) -i {host}(:\\d+)?");
                            sbHaProxy.AppendLine($"    use_backend         {hostPathMapping.Value} if {hostAclName}");
                        }
                        else
                        {
                            // The frontend does not specify a host so we'll always use the backend.

                            sbHaProxy.AppendLine($"    use_backend         {hostPathMapping.Value}");
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
                        else if (!string.IsNullOrEmpty(host))
                        {
                            sbHaProxy.AppendLine($"    acl                 {pathAclName} path_beg {path}");
                            sbHaProxy.AppendLine($"    acl                 {hostAclName} hdr_reg(host) -i {host}(:\\d+)?");
                            sbHaProxy.AppendLine($"    use_backend         {hostPathMapping.Value} if {hostAclName} {pathAclName}");
                        }
                        else
                        {
                            // The frontend does not specify a host so we'll use the backend
                            // if only the path matches.

                            sbHaProxy.AppendLine($"    acl                 {pathAclName} path_beg {path}");
                            sbHaProxy.AppendLine($"    use_backend         {hostPathMapping.Value} if {pathAclName}");
                        }
                    }
                }

                // Generate the HTTP backends

                foreach (LoadBalancerHttpRule httpRule in rules.Values
                    .Where(r => r.Mode == LoadBalancerMode.Http)
                    .OrderBy(r => r.Name))
                {
                    // Generate the resolvers argument to be used to locate the
                    // backend servers.

                    var resolversArg    = string.Empty;

                    if (!string.IsNullOrEmpty(httpRule.Resolver))
                    {
                        resolversArg = $" resolvers {httpRule.Resolver}";
                    }

                    var checkArg    = httpRule.CheckMode != LoadBalancerCheckMode.Disabled ? " check" : " no-check";
                    var checkSslArg = httpRule.CheckTls ? " check-ssl" : string.Empty;
                    var initAddrArg = " init-addr last,libc,none";

                    if (httpRule.CheckMode == LoadBalancerCheckMode.Disabled)
                    {
                        checkSslArg = string.Empty;
                    }

                    sbHaProxy.Append(
$@"
backend http:{httpRule.Name}
    mode                http
");

                    if (httpRule.HttpsRedirect)
                    {
                        sbHaProxy.AppendLine($"    redirect            scheme https if !{{ ssl_fc }}");
                    }

                    if (httpRule.UseHttpCheckMode)
                    {
                        sbHaProxy.AppendLine($"    option              httpchk {GetHttpCheckOptionArgs(httpRule)}");

                        if (!string.IsNullOrEmpty(httpRule.CheckExpect))
                        {
                            sbHaProxy.AppendLine($"    http-check          expect {httpRule.CheckExpect.Trim()}");
                        }
                    }
                    else if (httpRule.CheckMode == LoadBalancerCheckMode.Tcp)
                    {
                        sbHaProxy.AppendLine($"    option              tcp-check");
                    }

                    if (httpRule.CheckMode != LoadBalancerCheckMode.Disabled && httpRule.LogChecks)
                    {
                        sbHaProxy.AppendLine($"    option              log-health-checks");
                    }

                    if (httpRule.Log)
                    {
                        sbHaProxy.AppendLine($"    log                 global");
                    }

                    var serverIndex = 0;

                    foreach (var backend in httpRule.SelectBackends(hostGroups))
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

                        sbHaProxy.AppendLine($"    server              {serverName} {backend.Server}:{backend.Port}{sslArg}{checkArg}{checkSslArg}{initAddrArg}{resolversArg}");
                    }
                }
            }

            if (configError)
            {
                log.LogError("Proxy configuration aborted due to one or more errors.");
                return (Rules: rules, log.ToString());
            }

            // Generate the contents of the [certs.list] file.

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
                    zip.Add(new StaticBytesDataSource(NeonHelper.ToLinuxLineEndings(sbCerts.ToString())), "certs.list");

                    zip.CommitUpdate();
                }

                zipBytes = ms.ToArray();
            }

            // Compute the MD5 hash for the combined configuration ZIP and the referenced certificates.

            var     hasher = MD5.Create();
            string  combinedHash;

            if (hiveCerts.HasReferences)
            {
                using (var ms = new MemoryStream())
                {
                    ms.Write(hasher.ComputeHash(zipBytes));
                    ms.Write(hiveCerts.HashReferenced());

                    ms.Position  = 0;
                    combinedHash = Convert.ToBase64String(hasher.ComputeHash(ms));
                }
            }
            else
            {
                combinedHash = Convert.ToBase64String(hasher.ComputeHash(zipBytes));
            }

            // Compare the combined hash against what's already published to Consul
            // for the load balancer and update these keys if the hashes differ.

            var publish = false;

            try
            {
                if (!await consul.KV.Exists($"{consulPrefix}/proxies/{loadBalancerName}/proxy-hash", terminator.CancellationToken) || 
                    !await consul.KV.Exists($"{consulPrefix}/proxies/{loadBalancerName}/proxy-conf", terminator.CancellationToken))
                {
                    publish = true; // Nothing published yet.
                }
                else
                {
                    publish = combinedHash != await consul.KV.GetString($"{consulPrefix}/proxies/{loadBalancerName}/proxy-hash", terminator.CancellationToken);
                }

                if (publish)
                {
                    log.LogInfo(() => $"Updating load balancer [{proxyDisplayName}] configuration: [rules={rules.Count}] [hash={combinedHash}]");

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
                        new KVTxnOp($"{consulPrefix}/proxies/{loadBalancerName}/proxy-hash", KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(combinedHash) },
                        new KVTxnOp($"{consulPrefix}/proxies/{loadBalancerName}/proxy-conf", KVTxnVerb.Set) { Value = zipBytes }
                    };

                    await consul.KV.Txn(operations, terminator.CancellationToken);

                    // Notify the proxy instances that they should reconfigure.

                    await proxyNotifyChannel.PublishAsync(
                        new ProxyUpdateMessage()
                        {
                            Reason       = "proxy update",
                            PublicProxy  = isPublic,
                            PrivateProxy = !isPublic
                        });
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
                return (Rules: rules, log.ToString());
            }

            //-----------------------------------------------------------------
            // Generate the HAProxy bridge configuration.  This configuration is  pretty simple.  
            // All we need to do is forward all endpoints as TCP connections to the proxy we just
            // generated above.  We won't treat HTTP/S specially and we don't need to worry 
            // about TLS termination or generate fancy health checks.
            //
            // The bridge proxy is typically deployed as a standalone Docker container on hive
            // pet nodes and will expose internal hive services on the pets using the same
            // ports where they are deployed on the internal Swarm nodes.  This means that containersl
            // running on pet nodes can consume hive services the same way as they do on the manager
            // and worker nodes.
            //
            // This was initially used as a way to route pet node logging traffic from the
            // [neon-log-host] containers to the [neon-log-collector] service to handle
            // upstream log processing and persistance to the Elasticsearch cluster but
            // the bridges could also be used in the future to access any hive service
            // with a public or private load balancer rule defined.
            //
            // The code below generally assumes that the bridge target proxy is exposed 
            // on all Swarm manager or worker nodes (via the Docker ingress/mesh network 
            // or because the proxy is running in global mode).  Exactly which nodes will
            // be configured to handle forwarded traffic is determined by the proxy
            // settings.
            //
            // The code below starts by examining the list of active Docker Swarm nodes captured
            // before we started generating proxy configs.  If there are 5 (the default) or fewer 
            // worker nodes, the configuration will use all hive nodes (including managers) as 
            // target endpoints.  If there are more than 5 worker nodes, the code will randomly 
            // select 5 of them as endpoints.
            //
            // This approach balances the need for simplicity and reliability in the face of 
            // node failure while trying to avoid an explosion of health check traffic.
            //
            // This may become a problem for hives with a large number of pet nodes
            // banging away with health checks.  One way to mitigate this is to target
            // specific Swarm nodes in the [ProxySettings] by IP address.

            // Determine which hive Swarm nodes will be targeted by the bridge.  The
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
                        log.LogWarn(() => $"Proxy bridge target [{targetAddress}] does not reference a known hive Swarm node.");
                    }
                }
            }
            else
            {
                // We're going to automatically select the target nodes.
                //
                // We're going to try to favor worker nodes that report being
                // reachable and we're going to sort candidates by the hash of
                // their node names, to try to select a consistent list of target
                // swarm nodes that will be used to relay the traffic bridged
                // from the pets.  This will avoid unnecessary bridge HAProxy
                // configuration changes, as decribed here:
                //
                //      https://github.com/jefflill/NeonForge/issues/349
                //
                // The sort-by-hash is a way to consistently "randomize" the targets
                // without sorting by name which may tend to concentrate traffic to 
                // nodes having the same function or network subnets (depending on the
                // hive node naming conventions).  Sorting by hash will tend to spread 
                // traffic around better.
                //
                // We'll fall back to selecting random swarm nodes if none are 
                // reachable (which should never happen because the [neon-proxy-manager]
                // has to be running somewhere.

                var targetCandidates = new List<DockerNode>();

                swarmNodes = swarmNodes.Where(n => n.State == "ready").ToList();    // We want only READY Swarm nodes.

                var workers = swarmNodes.Where(n => n.Role == "worker").ToList();

                if (workers.Count >= settings.BridgeTargetCount)
                {
                    // There are enough workers to select targets from, so we'll just do that.
                    // The idea here is to try to keep the managers from doing as much routing 
                    // work as possible because they may be busy handling global hive activities,
                    // especially for large hives.

                    foreach (var worker in workers)
                    {
                        targetCandidates.Add(worker);
                    }
                }
                else
                {
                    // Otherwise for small hives, we'll select targets from both
                    // managers and workers.

                    foreach (var node in swarmNodes)
                    {
                        targetCandidates.Add(node);
                    }
                }

                if (targetCandidates.Count == 0)
                {
                    // No swarm nodes appear to be ready (which should never happen because this
                    // program needs to be running somewhere) but we'll select all swarm nodes
                    // as target candidates just to be safe.

                    foreach (var node in swarmNodes)
                    {
                        targetCandidates.Add(node);
                    }
                }

                // Semi-randomly select the swarm target nodes by sorting by the hash of the
                // node name and then taking nodes from the front of the sorted list.

                foreach (var node in targetCandidates.OrderBy(n => NeonHelper.ComputeMD5(n.Hostname)).Take(Math.Min(targetCandidates.Count, settings.BridgeTargetCount)))
                {
                    bridgeTargets.Add(node.Addr);
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

# Specify the maximum number of connections allowed for a proxy instance.

    maxconn             {settings.MaxConnections}

# We're going to disable bridge proxy logging for now because I'm not entirely 
# sure that this will be useful.  If we decide to enable this in the future, we
# should probably specify a different SYSLOG facility so we can distinguish 
# between problems with bridges and normal proxies. 

# log                 ""${{NEON_NODE_IP}}:{HiveHostPorts.LogHostSysLog}"" len 65535 {HiveSysLogFacility.ProxyName}

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
            // [HiverConst.HAProxyStatsPort] port on the [neon-public] or
            // [neon-private] network the proxy serves.
            //
            // HAProxy statistics pages are not intended to be viewed directly by
            // by hive operators.  Instead, the statistics from multiple HAProxy
            // instances will be aggregated by the hive Dashboard.

            sbHaProxy.AppendLine($@"
#------------------------------------------------------------------------------
# Enable HAProxy statistics pages.

frontend haproxy_stats
    bind                *:{HiveConst.HAProxyStatsPort}
    mode                http
    option              http-keep-alive
    use_backend         haproxy_stats

backend haproxy_stats
    mode                http
    stats               enable
    stats               scope .
    stats               uri {HiveConst.HaProxyStatsUri}
    stats               refresh 5s
");
            // Generate the TCP bridge rules.

            sbHaProxy.AppendLine("#------------------------------------------------------------------------------");
            sbHaProxy.AppendLine("# TCP Rules");

            var bridgePorts = new HashSet<int>();

            foreach (LoadBalancerTcpRule tcpRule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Tcp))
            {
                foreach (var frontEnd in tcpRule.Frontends)
                {
                    if (!bridgePorts.Contains(frontEnd.ProxyPort))
                    {
                        bridgePorts.Add(frontEnd.ProxyPort);
                    }
                }
            }

            foreach (LoadBalancerHttpRule httpRule in rules.Values
                .Where(r => r.Mode == LoadBalancerMode.Http))
            {
                foreach (var frontEnd in httpRule.Frontends)
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
                //sbHaProxy.AppendLine($"    log-format          {HiveHelper.GetProxyLogFormat("neon-proxy-" + proxyName, tcp: true)}");
                //sbHaProxy.AppendLine($"    option              log-health-checks");

                var checkArg    = " check";
                var initAddrArg = " init-addr last,libc,none";
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
                if (!await consul.KV.Exists($"{consulPrefix}/proxies/{proxyBridgeName}/proxy-hash", terminator.CancellationToken) ||
                    !await consul.KV.Exists($"{consulPrefix}/proxies/{proxyBridgeName}/proxy-conf", terminator.CancellationToken))
                {
                    publish = true; // Nothing published yet.
                }
                else
                {
                    publish = combinedHash != await consul.KV.GetString($"{consulPrefix}/proxies/{proxyBridgeName}/proxy-hash", terminator.CancellationToken);
                }

                if (publish)
                {
                    log.LogInfo(() => $"Updating proxy [{proxyBridgeDisplayName}] configuration: [rules={bridgePorts.Count}] [hash={combinedHash}]");

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
                        new KVTxnOp($"{consulPrefix}/proxies/{proxyBridgeName}/proxy-hash", KVTxnVerb.Set) { Value = Encoding.UTF8.GetBytes(combinedHash) },
                        new KVTxnOp($"{consulPrefix}/proxies/{proxyBridgeName}/proxy-conf", KVTxnVerb.Set) { Value = zipBytes }
                    };

                    await consul.KV.Txn(operations, terminator.CancellationToken);

                    // Notify the proxy-bridge instances that they should reconfigure.

                    await proxyNotifyChannel.PublishAsync(
                        new ProxyUpdateMessage()
                        {
                            Reason        = "bridge update",
                            PublicBridge  = isPublic,
                            PrivateBridge = !isPublic
                        });
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
                return (Rules: rules, log.ToString());
            }
            
            //-----------------------------------------------------------------
            // We're done

            return (Rules: rules, Status: log.ToString());
        }

        /// <summary>
        /// Updates the hive's public load balancer and network security rules so they
        /// are consistent with the public load balancer rules passed.
        /// </summary>
        /// <param name="publicRules">The public proxy rules.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private static async Task UpdateHiveNetwork(Dictionary<string, LoadBalancerRule> publicRules)
        {
            try
            {
                // Clone the cached hive definition and add the hosting options
                // acquired from Vault to create a hive proxy.

                var clonedHiveDefinition = NeonHelper.JsonClone<HiveDefinition>(hiveDefinition);

                clonedHiveDefinition.Hosting = await vault.ReadJsonAsync<HostingOptions>("neon-secret/hosting/options", cancellationToken: terminator.CancellationToken);

                var hive = new HiveProxy(clonedHiveDefinition);

                // Retrieve the current public load balancer rules and then compare
                // these with the public rules defined for the hive to determine
                // whether we need to update the load balancer and network security
                // rules.

                // $note(jeff.lill):
                //
                // It's possible for the load balancer and network security rules
                // to be out of sync if the operator modifies the security rules
                // manually (e.g. via the cloud portal).  This code won't detect
                // this situation and the security rules won't be brought back
                // into sync until the public rules changes enough to actually 
                // change the external load balanced port set.
                //
                // This could be considered a feature.  For example, this allows
                // the operator temporarily block a port manually.

                var hostingManager = new HostingManagerFactory().GetManager(hive);

                if (hostingManager == null)
                {
                    Console.Error.WriteLine($"*** ERROR: No hosting manager for the [{hive.Definition.Hosting.Environment}] hosting environment could be located.");
                    Program.Exit(1, immediate: true);
                }

                if (!hostingManager.CanUpdatePublicEndpoints)
                {
                    return; // Operators need to maintain the load balancer manually for this environment.
                }

                var publicEndpoints = hostingManager.GetPublicEndpoints();
                var latestEndpoints = new List<HostedEndpoint>();

                // Build a dictionary mapping the load balancer frontend ports to 
                // internal HAProxy frontend ports for the latest rules.

                foreach (LoadBalancerTcpRule rule in publicRules.Values
                    .Where(r => r.Mode == LoadBalancerMode.Tcp))
                {
                    foreach (var frontend in rule.Frontends)
                    {
                        if (frontend.PublicPort > 0)
                        {
                            latestEndpoints.Add(new HostedEndpoint(HostedEndpointProtocol.Tcp, frontend.PublicPort, frontend.ProxyPort));
                        }
                    }
                }

                foreach (LoadBalancerHttpRule rule in publicRules.Values
                    .Where(r => r.Mode == LoadBalancerMode.Http))
                {
                    foreach (var frontend in rule.Frontends)
                    {
                        if (frontend.PublicPort > 0)
                        {
                            latestEndpoints.Add(new HostedEndpoint(HostedEndpointProtocol.Tcp, frontend.PublicPort, frontend.ProxyPort));
                        }
                    }
                }

                // Determine if the latest rule port mappings differ from the current
                // hive load balancer rules.

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
                    log.LogInfo(() => $"Public hive load balancer configuration matches current rules. [endpoint-count={publicEndpoints.Count}]");
                    return;
                }

                // The endpoints have changed so update the hive.

                log.LogInfo(() => $"Updating: public hive load balancer and security. [endpoint-count={publicEndpoints.Count}]");
                hostingManager.UpdatePublicEndpoints(latestEndpoints);
                log.LogInfo(() => $"Update Completed: public hive load balancer and security. [endpoint-count={publicEndpoints.Count}]");
            }
            catch (Exception e)
            {
                log.LogError($"Unable to update hive load balancer and/or network security configuration.", e);
            }
        }
    }
}
