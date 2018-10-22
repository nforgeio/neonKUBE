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

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Hive;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace NeonCli
{
    /// <summary>
    /// Handles the provisioning of hive Docker registry cache services.
    /// </summary>
    /// <remarks>
    /// </remarks>
    public class RegistryCache
    {
        private HiveProxy   hive;
        private string      hiveLoginPath;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="hive">The hive proxy.</param>
        /// <param name="hiveLoginPath">The path to the hive login file.</param>
        public RegistryCache(HiveProxy hive, string hiveLoginPath)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive          = hive;
            this.hiveLoginPath = hiveLoginPath;
        }

        /// <summary>
        /// Performs the Docker registry cache related configuration of the node.
        /// </summary>
        public void Configure(SshProxy<NodeDefinition> node)
        {
            // NOTE:
            //
            // We're going to configure the certificates even if the registry cache
            // isn't enabled so it'll be easier to upgrade the hive later.

            // For managers, upload the individual cache certificate and 
            // private key files for managers [cache.crt] and [cache.key] at
            // [/etc/neon-registry-cache/].  This directory will be 
            // mapped into the cache container.
            //
            // Then create the cache's data volume and start the manager's 
            // Registry cache container.

            if (node.Metadata.IsManager)
            {
                node.InvokeIdempotentAction("setup/registrycache",
                    () =>
                    {
                        // Copy the registry cache certificate and private key to
                        // 
                        //      /etc/neon-registry-cache

                        node.Status = "run: registry-cache-server-certs.sh";

                        var copyCommand  = new CommandBundle("./registry-cache-server-certs.sh");
                        var sbCopyScript = new StringBuilder();

                        sbCopyScript.AppendLine("mkdir -p /etc/neon-registry-cache");
                        sbCopyScript.AppendLine("chmod 750 /etc/neon-registry-cache");

                        copyCommand.AddFile($"cache.crt", hive.HiveLogin.HiveCertificate.CertPem);
                        copyCommand.AddFile($"cache.key", hive.HiveLogin.HiveCertificate.KeyPem);

                        sbCopyScript.AppendLine($"cp cache.crt /etc/neon-registry-cache/cache.crt");
                        sbCopyScript.AppendLine($"cp cache.key /etc/neon-registry-cache/cache.key");
                        sbCopyScript.AppendLine($"chmod 640 /etc/neon-registry-cache/*");

                        copyCommand.AddFile("registry-cache-server-certs.sh", sbCopyScript.ToString(), isExecutable: true);
                        node.SudoCommand(copyCommand);

                        // Upload the cache certificates to every hive node at:
                        //
                        //      /etc/docker/certs.d/<hostname>:{HiveHostPorts.RegistryCache}/ca.crt
                        //
                        // and then have Linux reload the trusted certificates.

                        node.InvokeIdempotentAction("setup/registrycache-cert",
                            () =>
                            {
                                node.Status = "upload: registry cache certs";

                                var uploadCommand  = new CommandBundle("./registry-cache-client-certs.sh");
                                var sbUploadScript = new StringBuilder();

                                uploadCommand.AddFile($"hive-neon-registry-cache.crt", hive.HiveLogin.HiveCertificate.CertPem);

                                foreach (var manager in hive.Definition.SortedManagers)
                                {
                                    var cacheHostName = hive.Definition.GetRegistryCacheHost(manager);

                                    sbUploadScript.AppendLine($"mkdir -p /etc/docker/certs.d/{cacheHostName}:{HiveHostPorts.DockerRegistryCache}");
                                    sbUploadScript.AppendLine($"cp hive-neon-registry-cache.crt /etc/docker/certs.d/{cacheHostName}:{HiveHostPorts.DockerRegistryCache}/ca.crt");
                                }

                                uploadCommand.AddFile("registry-cache-client-certs.sh", sbUploadScript.ToString(), isExecutable: true);
                                node.SudoCommand(uploadCommand);
                            });

                        // Start the registry cache containers if enabled for the hive.

                        if (hive.Definition.Docker.RegistryCache)
                        {
                            // Create the registry data volume.

                            node.Status = "create: registry cache volume";
                            node.SudoCommand(new CommandBundle("docker-volume-create \"neon-registry-cache\""));

                            // Start the registry cache using the required Docker public registry
                            // credentials, if any.

                            var publicRegistryCredentials = hive.Definition.Docker.Registries.SingleOrDefault(r => HiveHelper.IsDockerPublicRegistry(r.Registry));

                            publicRegistryCredentials          = publicRegistryCredentials ?? new RegistryCredentials() { Registry = HiveConst.DockerPublicRegistry };
                            publicRegistryCredentials.Username = publicRegistryCredentials.Username ?? string.Empty;
                            publicRegistryCredentials.Password = publicRegistryCredentials.Password ?? string.Empty;

                            node.Status = "start: neon-registry-cache";

                            var registry = publicRegistryCredentials.Registry;

                            if (string.IsNullOrEmpty(registry) || registry.Equals("docker.io", StringComparison.InvariantCultureIgnoreCase))
                            {
                                registry = "registry-1.docker.io";
                            }

                            ServiceHelper.StartContainer(node, "neon-registry-cache", hive.Definition.Image.RegistryCache, RunOptions.FaultOnError | hive.SecureRunOptions,
                                new CommandBundle(
                                    "docker run",
                                    "--name", "neon-registry-cache",
                                    "--detach",
                                    "--restart", "always",
                                    "--publish", $"{HiveHostPorts.DockerRegistryCache}:5000",
                                    "--volume", "/etc/neon-registry-cache:/etc/neon-registry-cache:ro",     // Registry cache certificates folder
                                    "--volume", "neon-registry-cache:/var/lib/neon-registry-cache", 
                                    "--env", $"HOSTNAME={node.Name}.{hive.Definition.Hostnames.RegistryCache}",
                                    "--env", $"REGISTRY=https://{registry}",
                                    "--env", $"USERNAME={publicRegistryCredentials.Username}",
                                    "--env", $"PASSWORD={publicRegistryCredentials.Password}",
                                    "--env", "LOG_LEVEL=info",
                                    ServiceHelper.ImagePlaceholderArg));
                        }
                    });

                node.Status = string.Empty;
            }
        }
    }
}
