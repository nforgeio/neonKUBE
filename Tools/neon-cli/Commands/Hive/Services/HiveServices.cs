//-----------------------------------------------------------------------------
// FILE:	    HiveServices.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Handles the provisioning of the global hive proxy services including: 
    /// <b>neon-hive-manager</b>, <b>neon-proxy-manager</b>, <b>neon-proxy-public</b>,
    /// <b>neon-proxy-public-cache</b>, <b>neon-proxy-private-cache</b>,
    /// and <b>neon-proxy-private</b>, <b>neon-dns</b>, <b>neon-dns-mon</b> as
    /// well as the <b>neon-proxy-public-bridge</b> and <b>neon-proxy-private-bridge</b> 
    /// containers on any pet nodes.
    /// </summary>
    public class HiveServices
    {
        private HiveProxy hive;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        public HiveServices(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive = hive;
        }

        /// <summary>
        /// Configures the hive services.
        /// </summary>
        /// <param name="firstManager">The first hive proxy manager.</param>
        public void Configure(SshProxy<NodeDefinition> firstManager)
        {
            firstManager.InvokeIdempotentAction("setup/hive-services",
                () =>
                {
                    // Ensure that Vault has been initialized.

                    if (!hive.HiveLogin.HasVaultRootCredentials)
                    {
                        throw new InvalidOperationException("Vault has not been initialized yet.");
                    }

                    //---------------------------------------------------------
                    // Persist the proxy settings.

                    // Obtain the AppRole credentials from Vault for the proxy manager as well as the
                    // public and private proxy services and persist these as Docker secrets.

                    firstManager.Status = "secrets: proxy services";

                    hive.Docker.Secret.Set("neon-proxy-manager-credentials", NeonHelper.JsonSerialize(hive.Vault.Client.GetAppRoleCredentialsAsync("neon-proxy-manager").Result, Formatting.Indented));
                    hive.Docker.Secret.Set("neon-proxy-public-credentials", NeonHelper.JsonSerialize(hive.Vault.Client.GetAppRoleCredentialsAsync("neon-proxy-public").Result, Formatting.Indented));
                    hive.Docker.Secret.Set("neon-proxy-private-credentials", NeonHelper.JsonSerialize(hive.Vault.Client.GetAppRoleCredentialsAsync("neon-proxy-private").Result, Formatting.Indented));

                    //---------------------------------------------------------
                    // Deploy the HiveMQ cluster.

                    hive.FirstManager.InvokeIdempotentAction("setup/hivemq-cluster",
                        () =>
                        {
                            // We're going to list the hive nodes that will host the
                            // RabbitMQ cluster and sort them by node name.  Then we're
                            // going to ensure that the first RabbitMQ node/container
                            // is started and ready before configuring the rest of the
                            // cluster so that it will bootstrap properly.

                            var hiveMQNodes = hive.Nodes
                                .Where(n => n.Metadata.Labels.HiveMQ)
                                .OrderBy(n => n.Name)
                                .ToList();

                            DeployHiveMQ(hiveMQNodes.First());

                            // Start the remaining nodes in parallel.

                            var actions = new List<Action>();

                            foreach (var node in hiveMQNodes.Skip(1))
                            {
                                actions.Add(() => DeployHiveMQ(node));
                            }

                            NeonHelper.WaitForParallel(actions);

                            // The RabbitMQ cluster is created with the [/] vhost and the
                            // [sysadmin] user by default.  We need to create the [neon]
                            // and [app] vhosts along with the [neon] and [app] users
                            // and then set the appropriate permissions.
                            //
                            // We're going to run [rabbitmqctl] within the first RabbitMQ
                            // to accomplish this.

                            var hiveMQNode = hiveMQNodes.First();

                            // Create the vhosts.

                            hive.FirstManager.InvokeIdempotentAction("setup/hivemq-cluster-vhost-app", () => hiveMQNode.SudoCommand($"docker exec neon-hivemq rabbitmqctl add_vhost {HiveConst.HiveMQAppVHost}"));
                            hive.FirstManager.InvokeIdempotentAction("setup/hivemq-cluster-vhost-neon", () => hiveMQNode.SudoCommand($"docker exec neon-hivemq rabbitmqctl add_vhost {HiveConst.HiveMQNeonVHost}"));

                            // Create the users.

                            hive.FirstManager.InvokeIdempotentAction("setup/hivemq-cluster-user-app", () => hiveMQNode.SudoCommand($"docker exec neon-hivemq rabbitmqctl add_user {HiveConst.HiveMQAppUser} {hive.Definition.HiveMQ.AppPassword}"));
                            hive.FirstManager.InvokeIdempotentAction("setup/hivemq-cluster-user-neon", () => hiveMQNode.SudoCommand($"docker exec neon-hivemq rabbitmqctl add_user {HiveConst.HiveMQNeonUser} {hive.Definition.HiveMQ.NeonPassword}"));

                            // Grant the [app] account full access to the [app] vhost, the [neon] account full
                            // access to the [neon] vhost.  Note that this doesn't need to be idempotent.

                            hiveMQNode.SudoCommand($"docker exec neon-hivemq rabbitmqctl set_permissions -p {HiveConst.HiveMQAppVHost} {HiveConst.HiveMQAppUser} \".*\" \".*\" \".*\"");
                            hiveMQNode.SudoCommand($"docker exec neon-hivemq rabbitmqctl set_permissions -p {HiveConst.HiveMQNeonVHost} {HiveConst.HiveMQNeonUser} \".*\" \".*\" \".*\"");

                            // Clear the UX status for the HiveMQ nodes.

                            foreach (var node in hiveMQNodes)
                            {
                                node.Status = string.Empty;
                            }

                            // Set the RabbitMQ cluster name to the name of the hive.

                            hiveMQNode.InvokeIdempotentAction("setup/hivemq-cluster-name", () => hiveMQNode.SudoCommand($"docker exec neon-hivemq rabbitmqctl set_cluster_name {hive.Definition.Name}"));
                        });

                    //---------------------------------------------------------
                    // Initialize the public and private traffic manager managers.

                    hive.PublicTraffic.UpdateSettings(
                        new TrafficSettings()
                        {
                            ProxyPorts = HiveConst.PublicProxyPorts
                        });

                    hive.PrivateTraffic.UpdateSettings(
                        new TrafficSettings()
                        {
                            ProxyPorts = HiveConst.PrivateProxyPorts
                        });

                    //---------------------------------------------------------
                    // Deploy the HiveMQ traffic manager rules.

                    hive.FirstManager.InvokeIdempotentAction("setup/hivemq-traffic-manager-rules",
                        () =>
                        {
                            // Deploy private traffic manager for the AMQP endpoints.

                            var amqpRule = new TrafficTcpRule()
                            {
                                Name     = "neon-hivemq-amqp",
                                System   = true,
                                Resolver = null
                            };

                            // We're going to set this up to allow idle connections for up to
                            // five minutes.  In theory, AMQP connections should never be idle
                            // this long because we've enabled level 7 keep-alive.
                            //
                            //      https://github.com/jefflill/NeonForge/issues/new

                            amqpRule.Timeouts = new TrafficTimeouts()
                            {
                                ClientSeconds = 0,
                                ServerSeconds = 0
                            };

                            amqpRule.Frontends.Add(
                                new TrafficTcpFrontend()
                                {
                                    ProxyPort = HiveHostPorts.ProxyPrivateHiveMQAMQP
                                });

                            foreach (var ampqNode in hive.Nodes.Where(n => n.Metadata.Labels.HiveMQ))
                            {
                                amqpRule.Backends.Add(
                                    new TrafficTcpBackend()
                                    {
                                         Server = ampqNode.PrivateAddress.ToString(),
                                         Port   = HiveHostPorts.HiveMQAMQP
                                    });
                            }

                            hive.PrivateTraffic.SetRule(amqpRule);

                            // Deploy private traffic manager for the management endpoints.

                            var adminRule = new TrafficHttpRule()
                            {
                                Name     = "neon-hivemq-management",
                                System   = true,
                                Resolver = null
                            };

                            // Initialize the frontends and backends.

                            adminRule.Frontends.Add(
                                new TrafficHttpFrontend()
                                {
                                    ProxyPort = HiveHostPorts.ProxyPrivateHiveMQAdmin
                                });

                            adminRule.Backends.Add(
                                new TrafficHttpBackend()
                                {
                                    Group      = HiveHostGroups.HiveMQManagers,
                                    GroupLimit = 5,
                                    Port       = HiveHostPorts.HiveMQManagement
                                });

                            hive.PrivateTraffic.SetRule(adminRule);
                        });

                    //---------------------------------------------------------
                    // Deploy DNS related services.

                    // Deploy: neon-dns-mon

                    ServiceHelper.StartService(hive, "neon-dns-mon", hive.Definition.Image.DnsMon,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-dns-mon",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--env", "POLL_INTERVAL=5s",
                            "--env", "LOG_LEVEL=INFO",
                            "--constraint", "node.role==manager",
                            "--replicas", "1",
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            ServiceHelper.ImagePlaceholderArg));

                    // Deploy: neon-dns

                    ServiceHelper.StartService(hive, "neon-dns", hive.Definition.Image.Dns,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-dns",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--mount", "type=bind,src=/etc/powerdns/hosts,dst=/etc/powerdns/hosts",
                            "--mount", "type=bind,src=/dev/shm/neon-dns,dst=/neon-dns",
                            "--env", "POLL_INTERVAL=5s",
                            "--env", "VERIFY_INTERVAL=5m",
                            "--env", "LOG_LEVEL=INFO",
                            "--constraint", "node.role==manager",
                            "--mode", "global",
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            ServiceHelper.ImagePlaceholderArg));

                    //---------------------------------------------------------
                    // Deploy [neon-hive-manager] as a service constrained to manager nodes.

                    string unsealSecretOption = null;

                    if (hive.Definition.Vault.AutoUnseal)
                    {
                        var vaultCredentials = NeonHelper.JsonClone<VaultCredentials>(hive.HiveLogin.VaultCredentials);

                        // We really don't want to include the root token in the credentials
                        // passed to [neon-hive-manager], which needs the unseal keys so 
                        // we'll clear that here.

                        vaultCredentials.RootToken = null;

                        hive.Docker.Secret.Set("neon-hive-manager-vaultkeys", Encoding.UTF8.GetBytes(NeonHelper.JsonSerialize(vaultCredentials, Formatting.Indented)));

                        unsealSecretOption = "--secret=neon-hive-manager-vaultkeys";
                    }

                    ServiceHelper.StartService(hive, "neon-hive-manager", hive.Definition.Image.HiveManager,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-hive-manager",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--mount", "type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock",
                            "--env", "LOG_LEVEL=INFO",
                            "--secret", "neon-ssh-credentials",
                            unsealSecretOption,
                            "--constraint", "node.role==manager",
                            "--replicas", 1,
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            ServiceHelper.ImagePlaceholderArg
                        ),
                        hive.SecureRunOptions | RunOptions.FaultOnError);

                    //---------------------------------------------------------
                    // Deploy proxy related services.

                    // Deploy the proxy manager service.

                    ServiceHelper.StartService(hive, "neon-proxy-manager", hive.Definition.Image.ProxyManager,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-proxy-manager",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--mount", "type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-manager-credentials",
                            "--env", "LOG_LEVEL=INFO",
                            "--secret", "neon-proxy-manager-credentials",
                            "--constraint", "node.role==manager",
                            "--replicas", 1,
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            ServiceHelper.ImagePlaceholderArg));

                    // Docker mesh routing seemed unstable on versions so we're going
                    // to provide an option to work around this by running the PUBLIC, 
                    // PRIVATE and VAULT proxies on all nodes and  publishing the ports
                    // to the host (not the mesh).
                    //
                    //      https://github.com/jefflill/NeonForge/issues/104
                    //
                    // Note that this mode feature is documented (somewhat poorly) here:
                    //
                    //      https://docs.docker.com/engine/swarm/services/#publish-ports

                    var publicPublishArgs   = new List<string>();
                    var privatePublishArgs  = new List<string>();
                    var proxyConstraintArgs = new List<string>();
                    var proxyReplicasArgs   = new List<string>();
                    var proxyModeArgs       = new List<string>();

                    if (hive.Definition.Docker.GetAvoidIngressNetwork(hive.Definition))
                    {
                        // The parameterized [docker service create --publish] option doesn't handle port ranges so we need to 
                        // specify multiple publish options.

                        foreach (var port in HiveConst.PublicProxyPorts.Ports)
                        {
                            publicPublishArgs.Add($"--publish");
                            publicPublishArgs.Add($"mode=host,published={port},target={port}");
                        }

                        for (int port = HiveConst.PublicProxyPorts.PortRange.FirstPort; port <= HiveConst.PublicProxyPorts.PortRange.LastPort; port++)
                        {
                            publicPublishArgs.Add($"--publish");
                            publicPublishArgs.Add($"mode=host,published={port},target={port}");
                        }

                        foreach (var port in HiveConst.PrivateProxyPorts.Ports)
                        {
                            privatePublishArgs.Add($"--publish");
                            privatePublishArgs.Add($"mode=host,published={port},target={port}");
                        }

                        for (int port = HiveConst.PrivateProxyPorts.PortRange.FirstPort; port <= HiveConst.PrivateProxyPorts.PortRange.LastPort; port++)
                        {
                            privatePublishArgs.Add($"--publish");
                            privatePublishArgs.Add($"mode=host,published={port},target={port}");
                        }

                        proxyModeArgs.Add("--mode");
                        proxyModeArgs.Add("global");
                    }
                    else
                    {
                        // The parameterized [docker run --publish] option doesn't handle port ranges so we need to 
                        // specify multiple publish options.

                        foreach (var port in HiveConst.PublicProxyPorts.Ports)
                        {
                            publicPublishArgs.Add($"--publish");
                            publicPublishArgs.Add($"{port}:{port}");
                        }

                        publicPublishArgs.Add($"--publish");
                        publicPublishArgs.Add($"{HiveConst.PublicProxyPorts.PortRange.FirstPort}-{HiveConst.PublicProxyPorts.PortRange.LastPort}:{HiveConst.PublicProxyPorts.PortRange.FirstPort}-{HiveConst.PublicProxyPorts.PortRange.LastPort}");

                        foreach (var port in HiveConst.PrivateProxyPorts.Ports)
                        {
                            privatePublishArgs.Add($"--publish");
                            privatePublishArgs.Add($"{port}:{port}");
                        }

                        privatePublishArgs.Add($"--publish");
                        privatePublishArgs.Add($"{HiveConst.PrivateProxyPorts.PortRange.FirstPort}-{HiveConst.PrivateProxyPorts.PortRange.LastPort}:{HiveConst.PrivateProxyPorts.PortRange.FirstPort}-{HiveConst.PrivateProxyPorts.PortRange.LastPort}");

                        proxyConstraintArgs.Add($"--constraint");
                        proxyReplicasArgs.Add("--replicas");

                        if (hive.Definition.Workers.Count() > 0)
                        {
                            // Constrain proxies to worker nodes if there are any.

                            proxyConstraintArgs.Add($"node.role!=manager");

                            if (hive.Definition.Workers.Count() == 1)
                            {
                                proxyReplicasArgs.Add("1");
                            }
                            else
                            {
                                proxyReplicasArgs.Add("2");
                            }
                        }
                        else
                        {
                            // Constrain proxies to manager nodes nodes if there are no workers.

                            proxyConstraintArgs.Add($"node.role==manager");

                            if (hive.Definition.Managers.Count() == 1)
                            {
                                proxyReplicasArgs.Add("1");
                            }
                            else
                            {
                                proxyReplicasArgs.Add("2");
                            }
                        }

                        proxyModeArgs.Add("--mode");
                        proxyModeArgs.Add("replicated");
                    }

                    // Deploy: neon-proxy-public

                    ServiceHelper.StartService(hive, "neon-proxy-public", hive.Definition.Image.Proxy,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-proxy-public",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-conf",
                            "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-hash",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-public-credentials",
                            "--env", "WARN_SECONDS=300",
                            "--env", "START_SECONDS=10",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--secret", "neon-proxy-public-credentials",
                            publicPublishArgs,
                            proxyConstraintArgs,
                            proxyReplicasArgs,
                            proxyModeArgs,
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            "--network", HiveConst.PublicNetwork,
                            ServiceHelper.ImagePlaceholderArg));

                    // Deploy: neon-proxy-private

                    ServiceHelper.StartService(hive, "neon-proxy-private", hive.Definition.Image.Proxy,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-proxy-private",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-conf",
                            "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-hash",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-private-credentials",
                            "--env", "WARN_SECONDS=300",
                            "--env", "START_SECONDS=10",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--secret", "neon-proxy-private-credentials",
                            privatePublishArgs,
                            proxyConstraintArgs,
                            proxyReplicasArgs,
                            proxyModeArgs,
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            "--network", HiveConst.PrivateNetwork,
                            ServiceHelper.ImagePlaceholderArg));

                    // Deploy: neon-proxy-public-cache

                    var publicCacheConstraintArgs = new List<string>();
                    var publicCacheReplicaArgs    = new List<string>();

                    if (hive.Definition.Proxy.PublicCacheReplicas <= hive.Definition.Workers.Count())
                    {
                        publicCacheConstraintArgs.Add("--constraint");
                        publicCacheConstraintArgs.Add("node.role==worker");
                    }

                    publicCacheReplicaArgs.Add("--replicas");
                    publicCacheReplicaArgs.Add($"{hive.Definition.Proxy.PublicCacheReplicas}");

                    ServiceHelper.StartService(hive, "neon-proxy-public-cache", hive.Definition.Image.ProxyCache,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-proxy-public-cache",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--mount", "type=tmpfs,dst=/var/lib/varnish/_.vsm_mgt,tmpfs-size=90M,tmpfs-mode=755",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-conf",
                            "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-hash",
                            "--env", "WARN_SECONDS=300",
                            "--env", $"MEMORY-LIMIT={hive.Definition.Proxy.PublicCacheSize}",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--secret", "neon-proxy-public-credentials",
                            publicCacheConstraintArgs,
                            publicCacheReplicaArgs,
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            "--network", HiveConst.PublicNetwork,
                            ServiceHelper.ImagePlaceholderArg));

                    // Deploy: neon-proxy-private-cache

                    var privateCacheConstraintArgs = new List<string>();
                    var privateCacheReplicaArgs    = new List<string>();

                    if (hive.Definition.Proxy.PrivateCacheReplicas <= hive.Definition.Workers.Count())
                    {
                        privateCacheConstraintArgs.Add("--constraint");
                        privateCacheConstraintArgs.Add("node.role==worker");
                    }

                    privateCacheReplicaArgs.Add("--replicas");
                    privateCacheReplicaArgs.Add($"{hive.Definition.Proxy.PrivateCacheReplicas}");

                    ServiceHelper.StartService(hive, "neon-proxy-private-cache", hive.Definition.Image.ProxyCache,
                        new CommandBundle(
                            "docker service create",
                            "--name", "neon-proxy-private-cache",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                            "--mount", "type=tmpfs,dst=/var/lib/varnish/_.vsm_mgt,tmpfs-size=90M,tmpfs-mode=755",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-conf",
                            "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-hash",
                            "--env", "WARN_SECONDS=300",
                            "--env", $"MEMORY-LIMIT={hive.Definition.Proxy.PrivateCacheSize}",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--secret", "neon-proxy-private-credentials",
                            privateCacheConstraintArgs,
                            privateCacheReplicaArgs,
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            "--network", HiveConst.PrivateNetwork,
                            ServiceHelper.ImagePlaceholderArg));
                });

            // Log the hive into any Docker registries with credentials.

            firstManager.InvokeIdempotentAction("setup/registry-login",
                () =>
                {
                    foreach (var credential in hive.Definition.Docker.Registries
                        .Where(r => !string.IsNullOrEmpty(r.Username)))
                    {
                        hive.Registry.Login(credential.Registry, credential.Username, credential.Password);
                    }
                });
        }

        /// <summary>
        /// Deploys RabbitMQ to a cluster node as a container.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        private void DeployHiveMQ(SshProxy<NodeDefinition> node)
        {
            // Deploy RabbitMQ only on the labeled nodes.

            if (node.Metadata.Labels.HiveMQ)
            {
                // Build a comma separated list of fully qualified RabbitMQ hostnames so we
                // can pass them as the CLUSTER environment variable.

                var rabbitNodes = hive.Definition.SortedNodes.Where(n => n.Labels.HiveMQ).ToList();
                var sbCluster   = new StringBuilder();

                foreach (var rabbitNode in rabbitNodes)
                {
                    sbCluster.AppendWithSeparator($"{rabbitNode.Name}@{rabbitNode.Name}.{hive.Definition.Hostnames.HiveMQ}", ",");
                }

                var hipeCompileArgs = new List<string>();

                if (hive.Definition.HiveMQ.Precompile)
                {
                    hipeCompileArgs.Add("--env");
                    hipeCompileArgs.Add("RABBITMQ_HIPE_COMPILE=1");
                }

                var managementPluginArgs = new List<string>();

                if (node.Metadata.Labels.HiveMQManager)
                {
                    hipeCompileArgs.Add("--env");
                    hipeCompileArgs.Add("MANAGEMENT_PLUGIN=true");
                }

                // $todo(jeff.lill):
                //
                // I was unable to get TLS working correctly for RabbitMQ.  I'll come back
                // and revisit this later:
                //
                //      https://github.com/jefflill/NeonForge/issues/319

                ServiceHelper.StartContainer(node, "neon-hivemq", hive.Definition.Image.HiveMQ, RunOptions.FaultOnError,
                    new CommandBundle(
                        "docker run",
                        "--detach",
                        "--name", "neon-hivemq",
                        "--env", $"CLUSTER_NAME={hive.Definition.Name}",
                        "--env", $"CLUSTER_NODES={sbCluster}",
                        "--env", $"CLUSTER_PARTITION_MODE=autoheal",
                        "--env", $"NODENAME={node.Name}@{node.Name}.{hive.Definition.Hostnames.HiveMQ}",
                        "--env", $"RABBITMQ_USE_LONGNAME=true",
                        "--env", $"RABBITMQ_DEFAULT_USER=sysadmin",
                        "--env", $"RABBITMQ_DEFAULT_PASS=password",
                        "--env", $"RABBITMQ_NODE_PORT={HiveHostPorts.HiveMQAMQP}",
                        "--env", $"RABBITMQ_DIST_PORT={HiveHostPorts.HiveMQDIST}",
                        "--env", $"RABBITMQ_MANAGEMENT_PORT={HiveHostPorts.HiveMQManagement}",
                        "--env", $"RABBITMQ_ERLANG_COOKIE={hive.Definition.HiveMQ.ErlangCookie}",
                        "--env", $"RABBITMQ_VM_MEMORY_HIGH_WATERMARK={hive.Definition.HiveMQ.RamHighWatermark}",
                        hipeCompileArgs,
                        managementPluginArgs,
                        "--env", $"RABBITMQ_DISK_FREE_LIMIT={HiveDefinition.ValidateSize(hive.Definition.HiveMQ.DiskFreeLimit, typeof(HiveMQOptions), nameof(hive.Definition.HiveMQ.DiskFreeLimit))}",
                        //"--env", $"RABBITMQ_SSL_CERTFILE=/etc/neon/certs/hive.crt",
                        //"--env", $"RABBITMQ_SSL_KEYFILE=/etc/neon/certs/hive.key",
                        "--env", $"ERL_EPMD_PORT={HiveHostPorts.HiveMQEPMD}",
                        "--mount", "type=volume,source=neon-hivemq,target=/var/lib/rabbitmq",
                        "--mount", "type=bind,source=/etc/neon/certs,target=/etc/neon/certs,readonly",
                        "--publish", $"{HiveHostPorts.HiveMQEPMD}:{HiveHostPorts.HiveMQEPMD}",
                        "--publish", $"{HiveHostPorts.HiveMQAMQP}:{HiveHostPorts.HiveMQAMQP}",
                        "--publish", $"{HiveHostPorts.HiveMQDIST}:{HiveHostPorts.HiveMQDIST}",
                        "--publish", $"{HiveHostPorts.HiveMQManagement}:{HiveHostPorts.HiveMQManagement}",
                        "--memory", HiveDefinition.ValidateSize(hive.Definition.HiveMQ.RamLimit, typeof(HiveMQOptions), nameof(hive.Definition.HiveMQ.RamLimit)),
                        "--restart", "always",
                        ServiceHelper.ImagePlaceholderArg));

                // Wait for the RabbitMQ node to report that it's ready.

                var timeout  = TimeSpan.FromMinutes(4);
                var pollTime = TimeSpan.FromSeconds(2);

                node.Status = "hivemq: waiting";

                try
                {
                    NeonHelper.WaitFor(
                    () =>
                    {
                        var readyReponse = node.SudoCommand($"docker exec neon-hivemq rabbitmqctl node_health_check -n {node.Name}@{node.Name}.{hive.Definition.Hostnames.HiveMQ}", node.DefaultRunOptions & ~RunOptions.FaultOnError);

                        return readyReponse.ExitCode == 0;
                    },
                    timeout: timeout,
                    pollTime: pollTime);
                }
                catch (TimeoutException)
                {
                    node.Fault($"RabbitMQ not ready after waiting [{timeout}].");
                    return;
                }

                node.Status = "hivemq: ready";
            }
        }

        /// <summary>
        /// Deploys hive containers to a node.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        public void DeployContainers(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            Thread.Sleep(stepDelay);

            // NOTE: We only need to deploy the proxy bridges to the pet nodes, 
            //       because these will be deployed as global services on the 
            //       swarm nodes.

            if (node.Metadata.IsPet)
            {
                ServiceHelper.StartContainer(node, "neon-proxy-public-bridge", hive.Definition.Image.Proxy, RunOptions.FaultOnError,
                    new CommandBundle(
                        "docker run",
                        "--detach",
                        "--name", "neon-proxy-public-bridge",
                        "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                        "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                        "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public-bridge/proxy-conf",
                        "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/public-bridge/proxy-hash",
                        "--env", "WARN_SECONDS=300",
                        "--env", "POLL_SECONDS=15",
                        "--env", "START_SECONDS=10",
                        "--env", "LOG_LEVEL=INFO",
                        "--env", "DEBUG=false",
                        "--env", "VAULT_SKIP_VERIFY=true",
                        "--network", "host",
                        "--restart", "always",
                        ServiceHelper.ImagePlaceholderArg));

                ServiceHelper.StartContainer(node, "neon-proxy-private-bridge", hive.Definition.Image.Proxy, RunOptions.FaultOnError,
                    new CommandBundle(
                        "docker run",
                        "--detach",
                        "--name", "neon-proxy-private-bridge",
                        "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                        "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                        "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private-bridge/proxy-conf",
                        "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/private-bridge/proxy-hash",
                        "--env", "WARN_SECONDS=300",
                        "--env", "POLL_SECONDS=15",
                        "--env", "START_SECONDS=10",
                        "--env", "LOG_LEVEL=INFO",
                        "--env", "DEBUG=false",
                        "--env", "VAULT_SKIP_VERIFY=true",
                        "--network", "host",
                        "--restart", "always",
                        ServiceHelper.ImagePlaceholderArg));
            }
        }
    }
}
