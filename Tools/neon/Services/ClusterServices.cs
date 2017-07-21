//-----------------------------------------------------------------------------
// FILE:	    ClusterServices.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

using Neon.Cluster;
using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Time;

namespace NeonTool
{
    /// <summary>
    /// Handles the provisioning of the global cluster proxy services including: 
    /// <b>neon-cluster-manager</b>, <b>neon-proxy-manager</b>,
    /// <b>neon-proxy-public</b> and <b>neon-proxy-private</b>.
    /// </summary>
    public class ClusterServices
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster proxy.</param>
        public ClusterServices(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Configures the cluster proxy related services.
        /// </summary>
        /// <param name="firstManager">The first cluster proxy manager.</param>
        public void Configure(NodeProxy<NodeDefinition> firstManager)
        {
            firstManager.InvokeIdempotentAction("setup-cluster-services",
                () =>
                {
                    // Ensure that Vault has been initialized.

                    if (cluster.ClusterLogin.VaultCredentials == null)
                    {
                        throw new InvalidOperationException("Vault has not been initialized yet.");
                    }

                    //---------------------------------------------------------
                    // Deploy [neon-cluster-manager] as a container on each
                    // manager node.

                    string unsealSecretOption = null;

                    if (cluster.Definition.Vault.AutoUnseal)
                    {
                        var vaultCredentials = NeonHelper.JsonClone<VaultCredentials>(cluster.ClusterLogin.VaultCredentials);

                        // We really don't want to include the root token in the credentials
                        // passed to [neon-cluster-manager], which needs the unseal keys.

                        vaultCredentials.RootToken = null;

                        cluster.DockerSecret.Set("neon-cluster-manager-vaultkeys", Encoding.UTF8.GetBytes(NeonHelper.JsonSerialize(vaultCredentials, Formatting.Indented)));

                        unsealSecretOption = "--secret=neon-cluster-manager-vaultkeys";
                    }

                    cluster.FirstManager.Status = "start: neon-cluster-manager";
                    cluster.FirstManager.DockerCommand(RunOptions.Classified,
                        "docker service create",
                            "--name", "neon-cluster-manager",
                            "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                            "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                            "--mount", "type=bind,src=/var/run/docker.sock,dst=/var/run/docker.sock",
                            "--env", "LOG_LEVEL=INFO",
                            unsealSecretOption,
                            "--constraint", "node.role==manager",
                            "--replicas", 1,
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            "neoncluster/neon-cluster-manager");

                    //---------------------------------------------------------
                    // Deploy proxy related services

                    // Obtain the AppRole credentials from Vault for the proxy manager as well as the
                    // public and private proxy services and persist these as Docker secrets.

                    firstManager.Status = "secrets: proxy services";

                    cluster.DockerSecret.Set("neon-proxy-manager-credentials", NeonHelper.JsonSerialize(cluster.Vault.GetAppRoleCredentialsAsync("neon-proxy-manager").Result, Formatting.Indented));
                    cluster.DockerSecret.Set("neon-proxy-public-credentials", NeonHelper.JsonSerialize(cluster.Vault.GetAppRoleCredentialsAsync("neon-proxy-public").Result, Formatting.Indented));
                    cluster.DockerSecret.Set("neon-proxy-private-credentials", NeonHelper.JsonSerialize(cluster.Vault.GetAppRoleCredentialsAsync("neon-proxy-private").Result, Formatting.Indented));

                    // Deploy the proxy manager service.

                    firstManager.Status = "start: neon-proxy-manager";
                    firstManager.DockerCommand(
                        "docker service create",
                            "--name", "neon-proxy-manager",
                            "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                            "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-manager-credentials",
                            "--env", "LOG_LEVEL=INFO",
                            "--secret", "neon-proxy-manager-credentials",
                            "--constraint", "node.role==manager",
                            "--replicas", 1,
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            "neoncluster/neon-proxy-manager");

                    // Initialize the public and private proxies.

                    cluster.PublicProxy.UpdateSettings(
                        new ProxySettings()
                        {
                            FirstPort = NeonHostPorts.ProxyPublicFirst,
                            LastPort  = NeonHostPorts.ProxyPublicLast
                        });

                    cluster.PrivateProxy.UpdateSettings(
                        new ProxySettings()
                        {
                            FirstPort = NeonHostPorts.ProxyPrivateFirst,
                            LastPort  = NeonHostPorts.ProxyPrivateLast
                        });

                    // $todo(jeff.lill):
                    //
                    // Docker mesh routing seems unstable right now on versions 17.03.0-ce
                    // thru 17.06.0-ce so we're going to temporarily work around this by
                    // running the PUBLIC, PRIVATE and VAULT proxies on all nodes and 
                    // publishing the ports to the host (not the mesh).
                    //
                    //      https://github.com/jefflill/NeonForge/issues/104
                    //
                    // Note that this mode feature is documented (somewhat poorly) here:
                    //
                    //      https://docs.docker.com/engine/swarm/services/#publish-ports

                    string proxyConstraint;

#if !MESH_NETWORK_WORKS
                    // The parameterized [service create --publish] option doesn't handle port ranges so we need to 
                    // specify multiple publish options.

                    var publicPublish = new List<string>();

                    for (int port = NeonHostPorts.ProxyPublicFirst; port <= NeonHostPorts.ProxyPublicLast; port++)
                    {
                        publicPublish.Add("--publish");
                        publicPublish.Add($"mode=host,published={port},target={port}");
                    }

                    var privatePublish = new List<string>();

                    for (int port = NeonHostPorts.ProxyPrivateFirst; port <= NeonHostPorts.ProxyPrivateLast; port++)
                    {
                        privatePublish.Add("--publish");
                        privatePublish.Add($"mode=host,published={port},target={port}");
                    }

                    proxyConstraint = (string)null;
#else
                    var publicPublish  = $"--pubish {NeonHostPorts.ProxyPublicFirst}-{NeonHostPorts.ProxyPublicLast}:{NeonHostPorts.ProxyPublicFirst}-{NeonHostPorts.ProxyPublicLast}";
                    var privatePublish = $"--pubish {NeonHostPorts.ProxyPrivateFirst}-{NeonHostPorts.ProxyPrivateLast}:{NeonHostPorts.ProxyPrivateFirst}-{NeonHostPorts.ProxyPrivateLast}";

                    if (cluster.Definition.Workers.Count() > 0)
                    {
                        // Constrain proxies to all worker nodes if there are any.

                        proxyConstraint = "--constraint node.role!=manager";
                    }
                    else
                    {
                        // Constrain proxies to manager nodes nodes if there are no workers.

                        proxyConstraint = "--constraint node.role==manager";
                    }
#endif

                    firstManager.Status = "start: neon-proxy-public";
                    firstManager.DockerCommand(
                        "docker service create",
                            "--name", "neon-proxy-public",
                            "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                            "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/public/conf",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-public-credentials",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--secret", "neon-proxy-public-credentials",
                            publicPublish,
                            proxyConstraint,
                            "--mode", "global",
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            "--network", NeonClusterConst.ClusterPublicNetwork,
                            "neoncluster/neon-proxy");

                    firstManager.Status = "start: neon-proxy-private";
                    firstManager.DockerCommand(
                        "docker service create",
                            "--name", "neon-proxy-private",
                            "--mount", "type=bind,src=/etc/neoncluster/env-host,dst=/etc/neoncluster/env-host,readonly=true",
                            "--mount", "type=bind,src=/etc/ssl/certs,dst=/etc/ssl/certs,readonly=true",
                            "--env", "CONFIG_KEY=neon/service/neon-proxy-manager/proxies/private/conf",
                            "--env", "VAULT_CREDENTIALS=neon-proxy-private-credentials",
                            "--env", "LOG_LEVEL=INFO",
                            "--env", "DEBUG=false",
                            "--secret", "neon-proxy-private-credentials",
                            privatePublish,
                            proxyConstraint,
                            "--mode", "global",
                            "--restart-delay", cluster.Definition.Docker.RestartDelay,
                            "--network", NeonClusterConst.ClusterPrivateNetwork,
                            "neoncluster/neon-proxy");

                    firstManager.Status = string.Empty;
                });
        }
    }
}
