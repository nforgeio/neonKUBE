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
    /// <b>neon-hive-manager</b>, <b>neon-proxy-manager</b>, <b>neon-varnish</b>,
    /// <b>neon-proxy-public</b> and <b>neon-proxy-private</b>, <b>neon-dns</b>, 
    /// <b>neon-dns-mon</b> as well as the <b>neon-proxy-public-bridge</b> and
    /// <b>neon-proxy-private-bridge</b> containers on any pet nodes.
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
                    // Deploy DNS related services.
                    
                    // Deploy: neon-dns-mon

                    firstManager.Status = "start: neon-dns-mon";

                    firstManager.IdempotentDockerCommand("setup/neon-dns-mon",
                        response =>
                        {
                            foreach (var manager in hive.Managers)
                            {
                                manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-dns-mon.sh"), response.BashCommand);
                            }

                            if (response.ExitCode != 0)
                            {
                                firstManager.Fault(response.ErrorSummary);
                            }
                        },
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
                        Program.ResolveDockerImage(hive.Definition.DnsMonImage));

                    firstManager.Status = string.Empty;

                    // Deploy: neon-dns

                    firstManager.Status = "start: neon-dns";

                    firstManager.IdempotentDockerCommand("setup/neon-dns",
                        response =>
                        {
                            foreach (var manager in hive.Managers)
                            {
                                manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-dns.sh"), response.BashCommand);
                            }

                            if (response.ExitCode != 0)
                            {
                                firstManager.Fault(response.ErrorSummary);
                            }
                        },
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
                        Program.ResolveDockerImage(hive.Definition.DnsImage));

                    //---------------------------------------------------------
                    // Deploy the RabbitMQ cluster.

                    hive.FirstManager.InvokeIdempotentAction("setup/rabbitmq-cluster",
                        () =>
                        {
                            // We're going to start list the hive nodes that will host the
                            // RabbitMQ cluster and sort them by node name.  Then we're
                            // going to ensure that the first node is started and ready
                            // before configuring the rest of the cluster.

                            var rabbitMQNodes = hive.Nodes
                                .Where(n => n.Metadata.Labels.RabbitMQ)
                                .OrderBy(n => n.Name)
                                .ToList();

                            DeployRabbitMQ(rabbitMQNodes.First());
                            Thread.Sleep(TimeSpan.FromSeconds(30));     // Give the first node a chance to initialize

                            // Start the remaining nodes in parallel.

                            var actions = new List<Action>();

                            foreach (var node in rabbitMQNodes.Skip(1))
                            {
                                actions.Add(() => DeployRabbitMQ(node));
                            }

                            NeonHelper.WaitForParallel(actions);
                        });

                    //---------------------------------------------------------
                    // Deploy [neon-hive-manager] as a service constrained to manager nodes.

                    string unsealSecretOption = null;

                    if (hive.Definition.Vault.AutoUnseal)
                    {
                        var vaultCredentials = NeonHelper.JsonClone<VaultCredentials>(hive.HiveLogin.VaultCredentials);

                        // We really don't want to include the root token in the credentials
                        // passed to [neon-hive-manager], which needs the unseal keys.

                        vaultCredentials.RootToken = null;

                        hive.Docker.Secret.Set("neon-hive-manager-vaultkeys", Encoding.UTF8.GetBytes(NeonHelper.JsonSerialize(vaultCredentials, Formatting.Indented)));

                        unsealSecretOption = "--secret=neon-hive-manager-vaultkeys";
                    }

                    hive.FirstManager.Status = "start: neon-hive-manager";

                    hive.FirstManager.IdempotentDockerCommand("setup/neon-hive-manager",
                        response =>
                        {
                            foreach (var manager in hive.Managers)
                            {
                                manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-hive-manager.sh"), response.BashCommand);
                            }

                            if (response.ExitCode != 0)
                            {
                                firstManager.Fault(response.ErrorSummary);
                            }
                        },
                        hive.SecureRunOptions | RunOptions.FaultOnError,
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
                        Program.ResolveDockerImage(hive.Definition.HiveManagerImage));

                    //---------------------------------------------------------
                    // Deploy the Varnish HTTP caching service

#if TODO
                    if (hive.Definition.Varnish.Enabled)
                    {
                        firstManager.Status = "start: neon-varnish";

                        var constraint = new List<string>();

                        if (hive.Workers.Count() > 0)
                        {
                            constraint.Add("--constraint");
                            constraint.Add("node.role!=manager");
                        }

                        firstManager.IdempotentDockerCommand("setup/neon-varnish",
                            response =>
                            {
                                foreach (var manager in hive.Managers)
                                {
                                    manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-varnish.sh"), response.BashCommand);
                                }

                                if (response.ExitCode != 0)
                                {
                                    firstManager.Fault(response.ErrorSummary);
                                }
                            },
                            "docker service create",
                            "--name", "neon-varnish",
                            "--detach=false",
                            "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                            "--env", "LOG_LEVEL=INFO",
                            constraint,
                            "--replicas", 1,
                            "--restart-delay", hive.Definition.Docker.RestartDelay,
                            Program.ResolveDockerImage(hive.Definition.VarnishImage));
                    }
#endif

                    //---------------------------------------------------------
                    // Deploy proxy related services

                    // Obtain the AppRole credentials from Vault for the proxy manager as well as the
                    // public and private proxy services and persist these as Docker secrets.

                    firstManager.Status = "secrets: proxy services";

                    hive.Docker.Secret.Set("neon-proxy-manager-credentials", NeonHelper.JsonSerialize(hive.Vault.Client.GetAppRoleCredentialsAsync("neon-proxy-manager").Result, Formatting.Indented));
                    hive.Docker.Secret.Set("neon-proxy-public-credentials", NeonHelper.JsonSerialize(hive.Vault.Client.GetAppRoleCredentialsAsync("neon-proxy-public").Result, Formatting.Indented));
                    hive.Docker.Secret.Set("neon-proxy-private-credentials", NeonHelper.JsonSerialize(hive.Vault.Client.GetAppRoleCredentialsAsync("neon-proxy-private").Result, Formatting.Indented));

                    // Initialize the public and private proxies.

                    hive.PublicLoadBalancer.UpdateSettings(
                        new LoadBalancerSettings()
                        {
                            ProxyPorts = HiveConst.PublicProxyPorts
                        });

                    hive.PrivateLoadBalancer.UpdateSettings(
                        new LoadBalancerSettings()
                        {
                            ProxyPorts = HiveConst.PrivateProxyPorts
                        });

                    // Deploy the proxy manager service.

                    firstManager.Status = "start: neon-proxy-manager";

                    firstManager.IdempotentDockerCommand("setup/neon-proxy-manager",
                        response =>
                        {
                            foreach (var manager in hive.Managers)
                            {
                                manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-proxy-manager.sh"), response.BashCommand);
                            }

                            if (response.ExitCode != 0)
                            {
                                firstManager.Fault(response.ErrorSummary);
                            }
                        },
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
                        Program.ResolveDockerImage(hive.Definition.ProxyManagerImage));

                    // Docker mesh routing seemed unstable on versions 17.03.0-ce
                    // thru 17.06.0-ce so we're going to provide an option to work
                    // around this by running the PUBLIC, PRIVATE and VAULT proxies 
                    // on all nodes and  publishing the ports to the host (not the mesh).
                    //
                    //      https://github.com/jefflill/NeonForge/issues/104
                    //
                    // Note that this mode feature is documented (somewhat poorly) here:
                    //
                    //      https://docs.docker.com/engine/swarm/services/#publish-ports

                    var publicPublish   = new List<string>();
                    var privatePublish  = new List<string>();
                    var proxyConstraint = new List<string>();
                    var proxyReplicas   = new List<string>();
                    var proxyMode       = new List<string>();

                    if (hive.Definition.Docker.GetAvoidIngressNetwork(hive.Definition))
                    {
                        // The parameterized [service create --publish] option doesn't handle port ranges so we need to 
                        // specify multiple publish options.

                        foreach (var port in HiveConst.PublicProxyPorts.Ports)
                        {
                            publicPublish.Add($"--publish");
                            publicPublish.Add($"mode=host,published={port},target={port}");
                        }

                        for (int port = HiveConst.PublicProxyPorts.PortRange.FirstPort; port <= HiveConst.PublicProxyPorts.PortRange.LastPort; port++)
                        {
                            publicPublish.Add($"--publish");
                            publicPublish.Add($"mode=host,published={port},target={port}");
                        }

                        foreach (var port in HiveConst.PrivateProxyPorts.Ports)
                        {
                            privatePublish.Add($"--publish");
                            privatePublish.Add($"mode=host,published={port},target={port}");
                        }

                        for (int port = HiveConst.PrivateProxyPorts.PortRange.FirstPort; port <= HiveConst.PrivateProxyPorts.PortRange.LastPort; port++)
                        {
                            privatePublish.Add($"--publish");
                            privatePublish.Add($"mode=host,published={port},target={port}");
                        }

                        proxyMode.Add("--mode");
                        proxyMode.Add("global");
                    }
                    else
                    {
                        foreach (var port in HiveConst.PublicProxyPorts.Ports)
                        {
                            publicPublish.Add($"--publish");
                            publicPublish.Add($"{port}:{port}");
                        }

                        publicPublish.Add($"--publish");
                        publicPublish.Add($"{HiveConst.PublicProxyPorts.PortRange.FirstPort}-{HiveConst.PublicProxyPorts.PortRange.LastPort}:{HiveConst.PublicProxyPorts.PortRange.FirstPort}-{HiveConst.PublicProxyPorts.PortRange.LastPort}");

                        foreach (var port in HiveConst.PrivateProxyPorts.Ports)
                        {
                            privatePublish.Add($"--publish");
                            privatePublish.Add($"{port}:{port}");
                        }

                        privatePublish.Add($"--publish");
                        privatePublish.Add($"{HiveConst.PrivateProxyPorts.PortRange.FirstPort}-{HiveConst.PrivateProxyPorts.PortRange.LastPort}:{HiveConst.PrivateProxyPorts.PortRange.FirstPort}-{HiveConst.PrivateProxyPorts.PortRange.LastPort}");

                        proxyConstraint.Add($"--constraint");
                        proxyReplicas.Add("--replicas");

                        if (hive.Definition.Workers.Count() > 0)
                        {
                            // Constrain proxies to worker nodes if there are any.

                            proxyConstraint.Add($"node.role!=manager");

                            if (hive.Definition.Workers.Count() == 1)
                            {
                                proxyReplicas.Add("1");
                            }
                            else
                            {
                                proxyReplicas.Add("2");
                            }
                        }
                        else
                        {
                            // Constrain proxies to manager nodes nodes if there are no workers.

                            proxyConstraint.Add($"node.role==manager");

                            if (hive.Definition.Managers.Count() == 1)
                            {
                                proxyReplicas.Add("1");
                            }
                            else
                            {
                                proxyReplicas.Add("2");
                            }
                        }

                        proxyMode.Add("--mode");
                        proxyMode.Add("replicated");
                    }

                    // Deploy: neon-proxy-public

                    firstManager.Status = "start: neon-proxy-public";

                    firstManager.IdempotentDockerCommand("setup/neon-proxy-public",
                        response =>
                        {
                            foreach (var manager in hive.Managers)
                            {
                                manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-proxy-public.sh"), response.BashCommand);
                            }

                            if (response.ExitCode != 0)
                            {
                                firstManager.Fault(response.ErrorSummary);
                            }
                        },
                        "docker service create",
                        "--name", "neon-proxy-public",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                        "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                        "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-conf",
                        "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-hash",
                        "--env", "VAULT_CREDENTIALS=neon-proxy-public-credentials",
                        "--env", "WARN_SECONDS=300",
                        "--env", "POLL_SECONDS=15",
                        "--env", "START_SECONDS=10",
                        "--env", "LOG_LEVEL=INFO",
                        "--env", "DEBUG=false",
                        "--env", "VAULT_SKIP_VERIFY=true",
                        "--secret", "neon-proxy-public-credentials",
                        publicPublish,
                        proxyConstraint,
                        proxyReplicas,
                        proxyMode,
                        "--restart-delay", hive.Definition.Docker.RestartDelay,
                        "--network", HiveConst.PublicNetwork,
                        Program.ResolveDockerImage(hive.Definition.ProxyImage));

                    // Deploy: neon-proxy-private

                    firstManager.Status = "start: neon-proxy-private";

                    firstManager.IdempotentDockerCommand("setup/neon-proxy-private",
                        response =>
                        {
                            foreach (var manager in hive.Managers)
                            {
                                manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-proxy-private.sh"), response.BashCommand);
                            }

                            if (response.ExitCode != 0)
                            {
                                firstManager.Fault(response.ErrorSummary);
                            }
                        },
                        "docker service create",
                        "--name", "neon-proxy-private",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/host-env,dst=/etc/neon/host-env,readonly=true",
                        "--mount", "type=bind,src=/usr/local/share/ca-certificates,dst=/mnt/host/ca-certificates,readonly=true",
                        "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-conf",
                        "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-hash",
                        "--env", "VAULT_CREDENTIALS=neon-proxy-private-credentials",
                        "--env", "WARN_SECONDS=300",
                        "--env", "POLL_SECONDS=15",
                        "--env", "START_SECONDS=10",
                        "--env", "LOG_LEVEL=INFO",
                        "--env", "DEBUG=false",
                        "--env", "VAULT_SKIP_VERIFY=true",
                        "--secret", "neon-proxy-private-credentials",
                        privatePublish,
                        proxyConstraint,
                        proxyReplicas,
                        proxyMode,
                        "--restart-delay", hive.Definition.Docker.RestartDelay,
                        "--network", HiveConst.PrivateNetwork,
                        Program.ResolveDockerImage(hive.Definition.ProxyImage));
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
        private void DeployRabbitMQ(SshProxy<NodeDefinition> node)
        {
            // Deploy RabbitMQ only on the labeled nodes.

            if (node.Metadata.Labels.RabbitMQ)
            {
                // Build a comma separated list of fully qualified RabbitMQ hostnames so we
                // can pass them as the CLUSTER environment variable.

                var rabbitNodes = hive.Definition.SortedNodes.Where(n => n.Labels.RabbitMQ).ToList();
                var sbCluster   = new StringBuilder();

                foreach (var rabbitNode in rabbitNodes)
                {
                    sbCluster.AppendWithSeparator($"{rabbitNode.Name}@{rabbitNode.Name}.{hive.Definition.Hostnames.RabbitMQ}", ",");
                }

                var hipeCompile = new List<string>();

                if (hive.Definition.RabbitMQ.Precompile)
                {
                    hipeCompile.Add("--env");
                    hipeCompile.Add("RABBITMQ_HIPE_COMPILE=1");
                }

                // $todo(jeff.lill):
                //
                // I was unable to get TLS working correctly for RabbitMQ.  I'll come back
                // and revisit this later:
                //
                //      https://github.com/jefflill/NeonForge/issues/319

                node.InvokeIdempotentAction("setup/rabbitmq-node",
                    () =>
                    {
                        node.Status = "rabbitmq: start";

                        var response = node.DockerCommand(
                                    "docker run",
                                    "--detach",
                                    "--name", "neon-rabbitmq",
                                    "--env", $"CLUSTER_NAME={hive.Definition.Name}",
                                    "--env", $"CLUSTER_NODES={sbCluster}",
                                    "--env", $"CLUSTER_PARTITION_MODE=autoheal",
                                    "--env", $"NODENAME={node.Name}@{node.Name}.{hive.Definition.Hostnames.RabbitMQ}",
                                    "--env", $"RABBITMQ_USE_LONGNAME=true",
                                    "--env", $"RABBITMQ_DEFAULT_USER=sysadmin",
                                    "--env", $"RABBITMQ_DEFAULT_PASS=password",
                                    "--env", $"RABBITMQ_NODE_PORT={HiveHostPorts.RabbitMQAMPQ}",
                                    "--env", $"RABBITMQ_DIST_PORT={HiveHostPorts.RabbitMQDIST}",
                                    "--env", $"RABBITMQ_MANAGEMENT_PORT={HiveHostPorts.RabbitMQDashboard}",
                                    "--env", $"RABBITMQ_ERLANG_COOKIE={hive.Definition.RabbitMQ.ErlangCookie}",
                                    "--env", $"RABBITMQ_VM_MEMORY_HIGH_WATERMARK={hive.Definition.RabbitMQ.RamHighWatermark}",
                                    hipeCompile,
                                    "--env", $"RABBITMQ_DISK_FREE_LIMIT={HiveDefinition.ValidateSize(hive.Definition.RabbitMQ.DiskFreeLimit, typeof(RabbitMQOptions), nameof(hive.Definition.RabbitMQ.DiskFreeLimit))}",
                                    //"--env", $"RABBITMQ_SSL_CERTFILE=/etc/neon/certs/hive.crt",
                                    //"--env", $"RABBITMQ_SSL_KEYFILE=/etc/neon/certs/hive.key",
                                    "--env", $"ERL_EPMD_PORT={HiveHostPorts.RabbitMQEPMD}",
                                    "--mount", "type=volume,source=neon-rabbitmq,target=/var/lib/rabbitmq",
                                    "--mount", "type=bind,source=/etc/neon/certs,target=/etc/neon/certs,readonly",
                                    "--publish", $"{HiveHostPorts.RabbitMQEPMD}:{HiveHostPorts.RabbitMQEPMD}",
                                    "--publish", $"{HiveHostPorts.RabbitMQAMPQ}:{HiveHostPorts.RabbitMQAMPQ}",
                                    "--publish", $"{HiveHostPorts.RabbitMQDIST}:{HiveHostPorts.RabbitMQDIST}",
                                    "--publish", $"{HiveHostPorts.RabbitMQDashboard}:{HiveHostPorts.RabbitMQDashboard}",
                                    "--memory", HiveDefinition.ValidateSize(hive.Definition.RabbitMQ.RamLimit, typeof(RabbitMQOptions), nameof(hive.Definition.RabbitMQ.RamLimit)),
                                    "--restart", "always",
                                    Program.ResolveDockerImage(hive.Definition.RabbitMQ.RabbitMQImage));

                        node.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-rabbitmq.sh"), response.BashCommand);
                    });

                // Wait for the RabbitMQ node to report that it's ready.

                var timeout  = TimeSpan.FromMinutes(4);
                var pollTime = TimeSpan.FromSeconds(2);

                node.Status = "rabbitmq: waiting";

                try
                {
                    NeonHelper.WaitFor(
                    () =>
                    {
                        var readyReponse = node.SudoCommand("docker exec neon-rabbitmq rabbitmqctl status", node.DefaultRunOptions & ~RunOptions.FaultOnError);

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

                node.Status = "rabbitmq: ready";
            }
        }

        /// <summary>
        /// Deploys hive containers to a node.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        public void DeployContainers(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            // NOTE: We only need to deploy the proxy bridges to the pet nodes, 
            //       because these will be deployed as global services on the 
            //       swarm nodes.

            if (node.Metadata.IsPet)
            {
                node.InvokeIdempotentAction("setup/neon-proxy-public-bridge",
                    () =>
                    {
                        Thread.Sleep(stepDelay);

                        node.Status = "start: neon-proxy-public-bridge";

                        var response = node.DockerCommand(
                            "docker run",
                            "--detach",
                            "--name", "neon-proxy-public-bridge",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public-bridge/proxy-conf",
                            "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/public-bridge/proxy-hash",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-private-credentials",
                            "--env", "WARN_SECONDS=300",
                            "--env", "POLL_SECONDS=15",
                            "--env", "START_SECONDS=10",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--env", "VAULT_SKIP_VERIFY=true",
                            "--network", "host",
                            "--restart", "always",
                            Program.ResolveDockerImage(hive.Definition.ProxyImage));

                        node.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-proxy-public-bridge.sh"), response.BashCommand);
                        node.Status = string.Empty;
                    });

                node.InvokeIdempotentAction("setup/neon-proxy-private-bridge",
                    () =>
                    {
                        node.Status = "start: neon-proxy-private-bridge";

                        var response = node.DockerCommand(
                            "docker run",
                            "--detach",
                            "--name", "neon-proxy-private-bridge",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private-bridge/proxy-conf",
                            "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/private-bridge/proxy-hash",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-private-credentials",
                            "--env", "WARN_SECONDS=300",
                            "--env", "POLL_SECONDS=15",
                            "--env", "START_SECONDS=10",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--env", "VAULT_SKIP_VERIFY=true",
                            "--network", "host",
                            "--restart", "always",
                            Program.ResolveDockerImage(hive.Definition.ProxyImage));

                        node.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-proxy-private-bridge.sh"), response.BashCommand);
                        node.Status = string.Empty;
                    });
            }
        }
    }
}
