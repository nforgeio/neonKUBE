//-----------------------------------------------------------------------------
// FILE:	    RegistryCache.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
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

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Cluster;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Handles the provisioning of cluster Docker registry cache services.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class RegistryCache
    {
        private ClusterProxy    cluster;
        private string          clusterLoginPath;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="cluster">The cluster proxy.</param>
        /// <param name="clusterLoginPath">The path to the cluster login file.</param>
        public RegistryCache(ClusterProxy cluster, string clusterLoginPath)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster          = cluster;
            this.clusterLoginPath = clusterLoginPath;

            if (!cluster.Definition.Docker.RegistryCache)
            {
                return;
            }

            // Generate self-signed certificates and private keys for the registry
            // caches to be hosted on each of the managers.  Note that these
            // certs will expire in about 1,000 years, so they're effectively
            // permanent.

            if (cluster.ClusterLogin.RegistryCerts == null || cluster.ClusterLogin.RegistryCerts.Count == 0)
            {
                cluster.ClusterLogin.RegistryCerts = new Dictionary<string, string>();
                cluster.ClusterLogin.RegistryKeys  = new Dictionary<string, string>();

                foreach (var manager in cluster.Definition.SortedManagers)
                {
                    var certificate = TlsCertificate.CreateSelfSigned(GetCacheHost(manager), 4096, 365000);

                    try
                    {
                        cluster.ClusterLogin.RegistryCerts.Add(manager.Name, certificate.CertPem);
                        cluster.ClusterLogin.RegistryKeys.Add(manager.Name, certificate.KeyPem);
                    }
                    catch (Exception e)
                    {
                        Console.Error.WriteLine("*** ERROR: Could not generate registry cache TLS certificate.");
                        Console.Error.WriteLine(e.Message);
                        Program.Exit(1);
                    }
                }

                File.WriteAllText(clusterLoginPath, NeonHelper.JsonSerialize(cluster.ClusterLogin, Formatting.Indented));
            }
        }

        /// <summary>
        /// Performs the Docker registry cache related configuration of the node.
        /// </summary>
        public void Configure(SshProxy<NodeDefinition> node)
        {
            if (!cluster.Definition.Docker.RegistryCache)
            {
                return;
            }

            // For managers, upload the individual cache certificate and 
            // private key files for managers [cache.crt] and [cache.key] at
            // [/etc/neon-registry-cache/].  This directory will be 
            // mapped into the cache container.
            //
            // Then create the cache's data volume and start the manager's 
            // Registry cache container.

            if (node.Metadata.IsManager)
            {
                node.InvokeIdempotentAction("setup-registrycache",
                    () =>
                    {
                        // Copy the cert and key.

                        node.Status = "run: registry-cache-server-certs.sh";

                        var copyCommand = new CommandBundle("./registry-cache-server-certs.sh");
                        var sbScript    = new StringBuilder();

                        sbScript.AppendLine("mkdir -p /etc/neon-registry-cache");
                        sbScript.AppendLine("chmod 600 /etc/neon-registry-cache");

                        copyCommand.AddFile($"cache.crt", cluster.ClusterLogin.RegistryCerts[node.Name]);
                        copyCommand.AddFile($"cache.key", cluster.ClusterLogin.RegistryKeys[node.Name]);

                        sbScript.AppendLine($"cp cache.crt /etc/neon-registry-cache/cache.crt");
                        sbScript.AppendLine($"cp cache.key /etc/neon-registry-cache/cache.key");

                        copyCommand.AddFile("registry-cache-server-certs.sh", sbScript.ToString(), isExecutable: true);

                        node.SudoCommand(copyCommand);

                        // Create the registry data volume.

                        node.Status = "create: registry cache volume";
                        node.SudoCommand(new CommandBundle("docker-volume-create \"neon-registry-cache\""));

                        // Start the registry cache using the required Docker public registry
                        // credentials, if any.

                        var publicRegistryCredentials = cluster.Definition.Docker.Registries.SingleOrDefault(r => NeonClusterHelper.IsDockerPublicRegistry(r.Registry));

                        publicRegistryCredentials          = publicRegistryCredentials ?? new RegistryCredentials() { Registry = NeonClusterConst.DockerPublicRegistry };
                        publicRegistryCredentials.Username = publicRegistryCredentials.Username ?? string.Empty;
                        publicRegistryCredentials.Password = publicRegistryCredentials.Password ?? string.Empty;

                        node.Status = "start: neon-registry-cache";

                        var runCommand = new CommandBundle(
                            "docker run",
                            "--name", "neon-registry-cache",
                            "--detach",
                            "--restart", "always",
                            "--publish", $"{NeonHostPorts.DockerRegistryCache}:5000",
                            "--volume", "/etc/neon-registry-cache:/etc/neon-registry-cache:ro",
                            "--volume", "neon-registry-cache:/var/lib/neon-registry-cache",
                            "--env", $"HOSTNAME={node.Name}.{NeonHosts.RegistryCache}",
                            "--env", $"REGISTRY=https://{publicRegistryCredentials.Registry}",
                            "--env", $"USERNAME={publicRegistryCredentials.Username}",
                            "--env", $"PASSWORD={publicRegistryCredentials.Password}",
                            "--env", "LOG_LEVEL=info",
                            Program.ResolveDockerImage(cluster.Definition.Docker.RegistryCacheImage));

                        node.SudoCommand(runCommand);

                        // Upload a script so it will be easier to manually restart the container.

                        // $todo(jeff.lill);
                        //
                        // Persisting the registry credentials in the uploaded script here is 
                        // probably not the best idea, but I really like the idea of having
                        // this script available to make it easy to restart the cache if
                        // necessary.
                        //
                        // There are a couple of mitigating factors:
                        //
                        //      * The scripts folder can only be accessed by the ROOT user
                        //      * These are Docker public registry credentials only
                        //
                        // Users can create and use read-only credentials, which is 
                        // probably a best practice anyway for most clusters or they
                        // can deploy a custom registry (whose crdentials will be 
                        // persisted to Vault).

                        node.UploadText(LinuxPath.Combine(NeonHostFolders.Scripts, "neon-registry-cache.sh"), runCommand.ToBash());
                    });

                node.Status = string.Empty;
            }

            // Upload the cache certificates to every cluster node at:
            //
            //      /etc/docker/certs.d/<hostname>:{NeonHostPorts.RegistryCache}/ca.crt
            //      /usr/local/share/ca-certificates/<hostname>.crt
            //
            // and then have Linux update its known certificates.

            node.InvokeIdempotentAction("setup-registrycache-cert",
                () =>
                {
                    node.Status = "upload: registry cache certs";

                    var uploadCommand = new CommandBundle("./registry-cache-client-certs.sh");
                    var sbScript      = new StringBuilder();

                    sbScript.AppendLine("mkdir -p /etc/docker/certs.d");
                    sbScript.AppendLine("mkdir -p /usr/local/share/ca-certificates");

                    foreach (var manager in cluster.Definition.SortedManagers)
                    {
                        uploadCommand.AddFile($"{manager.Name}.crt", cluster.ClusterLogin.RegistryCerts[manager.Name]);

                        var cacheHostName = GetCacheHost(manager);

                        sbScript.AppendLine($"mkdir -p /etc/docker/certs.d/{cacheHostName}:{NeonHostPorts.DockerRegistryCache}");
                        sbScript.AppendLine($"cp {manager.Name}.crt /etc/docker/certs.d/{cacheHostName}:{NeonHostPorts.DockerRegistryCache}/ca.crt");
                        sbScript.AppendLine($"cp {manager.Name}.crt /usr/local/share/ca-certificates/{cacheHostName}.crt");
                    }

                    sbScript.AppendLineLinux();
                    sbScript.AppendLineLinux("update-ca-certificates");

                    uploadCommand.AddFile("registry-cache-client-certs.sh", sbScript.ToString(), isExecutable: true);

                    node.SudoCommand(uploadCommand);
                });
        }

        /// <summary>
        /// Returns the hostname for a registry cache instance hosted on a manager node.
        /// </summary>
        /// <param name="manager">The manager node.</param>
        /// <returns>The hostname.</returns>
        private string GetCacheHost(NodeDefinition manager)
        {
            return $"{manager.Name}.{NeonHosts.RegistryCache}";
        }
    }
}
