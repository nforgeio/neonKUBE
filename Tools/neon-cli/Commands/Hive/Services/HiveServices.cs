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
    /// <b>neon-hive-manager</b>, <b>neon-proxy-manager</b>,
    /// <b>neon-proxy-public</b> and <b>neon-proxy-private</b>,
    /// <b>neon-dns</b>, <b>neon-dns-mon</b> as well as the
    /// <b>neon-proxy-public-bridge</b> and <b>neon-proxy-private-bridge</b>
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
        /// Configures the hive proxy related services.
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
                    // Deploy: neon-dns

                    firstManager.Status = "start: neon-dns";

                    firstManager.IdempotentDockerCommand("setup/neon-dns",
                        response =>
                        {
                            foreach (var manager in hive.Managers)
                            {
                                manager.UploadText(LinuxPath.Combine(HiveHostFolders.Scripts, "neon-dns.sh"), response.BashCommand);
                            }
                        },
                        "docker service create",
                        "--name", "neon-dns",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/env-host,dst=/etc/neon/env-host,readonly=true",
                        "--mount", "type=bind,src=/etc/powerdns/hosts,dst=/etc/powerdns/hosts",
                        "--mount", "type=bind,src=/dev/shm/neon-dns,dst=/neon-dns",
                        "--env", "POLL_INTERVAL=15s",
                        "--env", "VERIFY_INTERVAL=5m",
                        "--env", "LOG_LEVEL=INFO",
                        "--constraint", "node.role==manager",
                        "--mode", "global",
                        "--restart-delay", hive.Definition.Docker.RestartDelay,
                        Program.ResolveDockerImage(hive.Definition.DnsImage));

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
                        },
                        "docker service create",
                        "--name", "neon-dns-mon",
                        "--detach=false",
                        "--env", "POLL_INTERVAL=15s",
                        "--env", "LOG_LEVEL=INFO",
                        "--constraint", "node.role==manager",
                        "--replicas", "1",
                        "--restart-delay", hive.Definition.Docker.RestartDelay,
                        Program.ResolveDockerImage(hive.Definition.DnsMonImage));

                    firstManager.Status = string.Empty;

                    //---------------------------------------------------------
                    // Deploy [neon-hive-manager] as a service on each manager node.

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
                        },
                        hive.SecureRunOptions | RunOptions.FaultOnError,
                        "docker service create",
                        "--name", "neon-hive-manager",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/env-host,dst=/etc/neon/env-host,readonly=true",
                        "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                        "--mount", "type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock",
                        "--env", "LOG_LEVEL=INFO",
                        "--secret", "neon-ssh-credentials",
                        unsealSecretOption,
                        "--constraint", "node.role==manager",
                        "--replicas", 1,
                        "--restart-delay", hive.Definition.Docker.RestartDelay,
                        Program.ResolveDockerImage(hive.Definition.HiveManagerImage));

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
                            FirstPort = HiveHostPorts.ProxyPublicFirst,
                            LastPort  = HiveHostPorts.ProxyPublicLast
                        });

                    hive.PrivateLoadBalancer.UpdateSettings(
                        new LoadBalancerSettings()
                        {
                            FirstPort = HiveHostPorts.ProxyPrivateFirst,
                            LastPort  = HiveHostPorts.ProxyPrivateLast
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
                        },
                        "docker service create",
                        "--name", "neon-proxy-manager",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/env-host,dst=/etc/neon/env-host,readonly=true",
                        "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
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

                    if (hive.Definition.Docker.AvoidIngressNetwork)
                    {
                        // The parameterized [service create --publish] option doesn't handle port ranges so we need to 
                        // specify multiple publish options.

                        for (int port = HiveHostPorts.ProxyPublicFirst; port <= HiveHostPorts.ProxyPublicLast; port++)
                        {
                            publicPublish.Add($"--publish");
                            publicPublish.Add($"mode=host,published={port},target={port}");
                        }

                        for (int port = HiveHostPorts.ProxyPrivateFirst; port <= HiveHostPorts.ProxyPrivateLast; port++)
                        {
                            privatePublish.Add($"--publish");
                            privatePublish.Add($"mode=host,published={port},target={port}");
                        }
                    }
                    else
                    {
                        publicPublish.Add($"--publish");
                        publicPublish.Add($"{HiveHostPorts.ProxyPublicFirst}-{HiveHostPorts.ProxyPublicLast}:{HiveHostPorts.ProxyPublicFirst}-{HiveHostPorts.ProxyPublicLast}");

                        privatePublish.Add($"--publish");
                        privatePublish.Add($"{HiveHostPorts.ProxyPrivateFirst}-{HiveHostPorts.ProxyPrivateLast}:{HiveHostPorts.ProxyPrivateFirst}-{HiveHostPorts.ProxyPrivateLast}");

                        proxyConstraint.Add($"--constraint");

                        if (hive.Definition.Workers.Count() > 0)
                        {
                            // Constrain proxies to worker nodes if there are any.

                            proxyConstraint.Add($"node.role!=manager");
                        }
                        else
                        {
                            // Constrain proxies to manager nodes nodes if there are no workers.

                            proxyConstraint.Add($"node.role==manager");
                        }
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
                        },
                        "docker service create",
                        "--name", "neon-proxy-public",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/env-host,dst=/etc/neon/env-host,readonly=true",
                        "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                        "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-conf",
                        "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/public/proxy-hash",
                        "--env", "VAULT_CREDENTIALS=neon-proxy-public-credentials",
                        "--env", "WARN_SECONDS=300",
                        "--env", "POLL_SECONDS=15",
                        "--env", "START_SECONDS=10",
                        "--env", "LOG_LEVEL=INFO",
                        "--env", "DEBUG=true",          // $todo(jeff.lill): Revert this to FALSE
                        "--env", "VAULT_SKIP_VERIFY=true",
                        "--secret", "neon-proxy-public-credentials",
                        publicPublish,
                        proxyConstraint,
                        "--mode", "global",
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
                        },
                        "docker service create",
                        "--name", "neon-proxy-private",
                        "--detach=false",
                        "--mount", "type=bind,src=/etc/neon/env-host,dst=/etc/neon/env-host,readonly=true",
                        "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                        "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-conf",
                        "--env", "CONFIG_HASH_KEY=neon/service/neon-proxy-manager/proxies/private/proxy-hash",
                        "--env", "VAULT_CREDENTIALS=neon-proxy-private-credentials",
                        "--env", "WARN_SECONDS=300",
                        "--env", "POLL_SECONDS=15",
                        "--env", "START_SECONDS=10",
                        "--env", "LOG_LEVEL=INFO",
                        "--env", "DEBUG=true",          // $todo(jeff.lill): Revert this to FALSE
                        "--env", "VAULT_SKIP_VERIFY=true",
                        "--secret", "neon-proxy-private-credentials",
                        privatePublish,
                        proxyConstraint,
                        "--mode", "global",
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
        /// Deploys hive service containers to a node.
        /// </summary>
        /// <param name="node">The target hive node.</param>
        /// <param name="stepDelay">The step delay if the operation hasn't already been completed.</param>
        public void DeployContainers(SshProxy<NodeDefinition> node, TimeSpan stepDelay)
        {
            // NOTE: A this time, we only need to deploy the proxy bridges to the
            //       pet nodes, because these will be deployed as global services
            //       on the swarm nodes.

            if (!node.Metadata.IsPet)
            {
                return;
            }

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
                        "--env", "DEBUG=true",          // $todo(jeff.lill): Revert this to FALSE
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
                        "--env", "DEBUG=true",          // $todo(jeff.lill): Revert this to FALSE
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
