//-----------------------------------------------------------------------------
// FILE:	    RegistryManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Consul;

using Neon.Common;
using Neon.Cryptography;
using Neon.Retry;

namespace Neon.Cluster
{
    /// <summary>
    /// Handles cluster Docker registry related operations for <see cref="ClusterProxy"/>.
    /// </summary>
    public sealed class RegistryManager
    {
        private ClusterProxy cluster;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="cluster">The parent <see cref="ClusterProxy"/>.</param>
        internal RegistryManager(ClusterProxy cluster)
        {
            Covenant.Requires<ArgumentNullException>(cluster != null);

            this.cluster = cluster;
        }

        /// <summary>
        /// Lists the Docker registries currently connected to the cluster along
        /// with the registry credentials.
        /// </summary>
        /// <returns>The list of credentials.</returns>
        public List<RegistryCredentials> List()
        {
            var credentials = new List<RegistryCredentials>();

            foreach (var hostname in cluster.Vault.ListAsync(NeonClusterConst.VaultRegistryCredentialsKey).Result)
            {
                var usernamePassword = cluster.Vault.ReadStringAsync($"{NeonClusterConst.VaultRegistryCredentialsKey}/{hostname}").Result;
                var fields = usernamePassword.Split(new char[] { '/' }, 2);

                if (fields.Length == 2)
                {
                    credentials.Add(
                        new RegistryCredentials()
                        {
                            Registry = hostname,
                            Username = fields[0],
                            Password = fields[1]
                        });
                }
                else
                {
                    throw new ClusterException($"Invalid credentials for the [{hostname}] registry.");
                }
            }

            return credentials;
        }

        /// <summary>
        /// Logs the cluster into a Docker registry or updates the registry credentials
        /// if already logged in.
        /// </summary>
        /// <param name="registry">The registry hostname or <c>null</c> to specify the Docker public registry.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <exception cref="ClusterException">Thrown if one or more of the cluster nodes could not be logged in.</exception>
        public void Login(string registry, string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(string.IsNullOrEmpty(registry) || ClusterDefinition.DnsHostRegex.IsMatch(registry));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(password != null);

            // Update the registry credentials in Vault.

            cluster.Vault.WriteStringAsync($"{NeonClusterConst.VaultRegistryCredentialsKey}/{registry}", $"{username}/{password}").Wait();

            // Login all of the cluster nodes in parallel.

            var actions = new List<Action>();
            var errors  = new List<string>();

            foreach (var node in cluster.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var clonedNode = node.Clone())
                        {
                            var response = clonedNode.SudoCommand("docker login", RunOptions.None, "--username", username, "--password", password, registry);

                            if (response.ExitCode != 0)
                            {
                                lock (errors)
                                {
                                    errors.Add($"{clonedNode.Name}: {response.ErrorSummary}");
                                }
                            }
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);

            if (errors.Count > 0)
            {
                var sb = new StringBuilder();

                sb.AppendLine($"Could not login [{errors.Count}] nodes to the [{registry}] Docker registry.");
                sb.AppendLine($"Your cluster may now be in an inconsistent state.");
                sb.AppendLine();

                foreach (var error in errors)
                {
                    sb.AppendLine(error);
                }

                throw new ClusterException(sb.ToString());
            }
        }

        /// <summary>
        /// Logs the cluster out of a Docker registry.
        /// </summary>
        /// <param name="registry">The registry hostname.</param>
        /// <exception cref="ClusterException">Thrown if one or more of the cluster nodes could not be logged out.</exception>
        public void Logout(string registry)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(registry));

            // Remove the registry credentials from Vault.

            cluster.Vault.DeleteAsync($"{NeonClusterConst.VaultRegistryCredentialsKey}/{registry}").Wait();

            // Logout of all of the cluster nodes in parallel.

            var actions = new List<Action>();
            var errors  = new List<string>();

            foreach (var node in cluster.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var clonedNode = node.Clone())
                        {
                            var response = clonedNode.SudoCommand($"docker logout", RunOptions.None, registry);

                            if (response.ExitCode != 0)
                            {
                                lock (errors)
                                {
                                    errors.Add($"{clonedNode.Name}: {response.ErrorSummary}");
                                }
                            }
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);

            if (errors.Count > 0)
            {
                var sb = new StringBuilder();

                sb.AppendLine($"Could not logout [{errors.Count}] nodes from the [{registry}] Docker registry.");
                sb.AppendLine($"Your cluster may now be in an inconsistent state.");
                sb.AppendLine();

                foreach (var error in errors)
                {
                    sb.AppendLine(error);
                }

                throw new ClusterException(sb.ToString());
            }
        }

        /// <summary>
        /// Returns the credentials for a specific Docker registry connected to the cluster.
        /// </summary>
        /// <param name="registry">The target registry hostname.</param>
        /// <returns>The credentials or <c>null</c> if no credentials exists.</returns>
        public RegistryCredentials GetCredentials(string registry)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(registry));

            var usernamePassword = cluster.Vault.ReadStringOrDefaultAsync($"{NeonClusterConst.VaultRegistryCredentialsKey}/{registry}").Result;

            if (usernamePassword == null)
            {
                return null;
            }

            var fields = usernamePassword.Split(new char[] { '/' }, 2);

            if (fields.Length == 2)
            {
                return new RegistryCredentials()
                {
                    Registry = registry,
                    Username = fields[0],
                    Password = fields[1]
                };
            }
            else
            {
                throw new ClusterException($"Invalid credentials for the [{registry}] registry.");
            }
        }

        /// <summary>
        /// Restarts the cluster registry caches as required, using the 
        /// upstream credentials passed.
        /// </summary>
        /// <param name="registry">The target registry hostname.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns><c>true</c> if the operation succeeded or was unnecessary.</returns>
        /// <remarks>
        /// <note>
        /// This method currently does nothing but return <c>true</c> if the 
        /// registry specified is not the Docker public registry because the 
        /// cache supports only the public registry or if the registry cache
        /// is not enabled for this cluster.
        /// </note>
        /// </remarks>
        public bool RestartCache(string registry, string username, string password)
        {
            // Return immediately if this is a NOP.

            if (!NeonClusterHelper.IsDockerPublicRegistry(registry) || !cluster.Definition.Docker.RegistryCache)
            {
                return true;
            }

            // We're not going to restart these in parallel so only one
            // manager cache will be down at any given time.  This should
            // result in no cache downtime for clusters with multiple
            // managers.

            foreach (var manager in cluster.Managers)
            {
                if (!manager.RestartRegistryCache(registry, username, password))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Returns the hostname for the local Docker registry if one is deployed.
        /// </summary>
        /// <returns>The hostname or <c>null</c>.</returns>
        public string GetLocalHostname()
        {
            return cluster.Consul.KV.GetStringOrDefault($"{NeonClusterConst.ConsulRegistryRootKey}/hostname").Result;
        }

        /// <summary>
        /// Persists the hostname for the local Docker registry.
        /// </summary>
        /// <param name="hostname">The new hostname for the local Docker registry or <c>null</c> to remove it.</param>
        public void SetLocalHostname(string hostname)
        {
            Covenant.Requires<ArgumentException>(string.IsNullOrEmpty(hostname) || ClusterDefinition.NameRegex.IsMatch(hostname));

            if (string.IsNullOrEmpty(hostname))
            {
                cluster.Consul.KV.Delete($"{NeonClusterConst.ConsulRegistryRootKey}/hostname").Wait();
            }
            else
            {
                cluster.Consul.KV.PutString($"{NeonClusterConst.ConsulRegistryRootKey}/hostname", hostname).Wait();
            }
        }

        /// <summary>
        /// Returns the secret for the local Docker registry if one is
        /// deployed.
        /// </summary>
        /// <returns>The hostname or <c>null</c>.</returns>
        public string GetLocalSecret()
        {
            return cluster.Consul.KV.GetStringOrDefault($"{NeonClusterConst.ConsulRegistryRootKey}/secret").Result;
        }

        /// <summary>
        /// Persists the secret for the local Docker registry.
        /// </summary>
        /// <param name="secret">The new secret for the local Docker registry or <c>null</c> to remove it.</param>
        public void SetLocalSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                cluster.Consul.KV.Delete($"{NeonClusterConst.ConsulRegistryRootKey}/secret").Wait();
            }
            else
            {
                cluster.Consul.KV.PutString($"{NeonClusterConst.ConsulRegistryRootKey}/secret", secret).Wait();
            }
        }

        /// <summary>
        /// Determines whether a local Docker registry is deployed to the cluster.
        /// </summary>
        public bool HasLocalRegistry
        {
            get { return cluster.Docker.InspectService("neon-registry") != null;  }
        }

        /// <summary>
        /// Deploys the local Docker registry to the cluster.
        /// </summary>
        /// <param name="hostname">The registry hostname.</param>
        /// <param name="username">The registry username.</param>
        /// <param name="password">The registry password.</param>
        /// <param name="secret">The registry secret.</param>
        /// <param name="certificate">The certificate used to secure the registry.</param>
        /// <param name="image">Optionally specifies the Docker image to be deployed (defaults to <b>neoncluster/neon-registry</b>).</param>
        /// <param name="progress">Optional action that will be called with a progress message.</param>
        /// <exception cref="ClusterException">Thrown if a registry is already deployed or deployment failed.</exception>
        /// <exception cref="NotSupportedException">Thrown if the cluster does not support local registries.</exception>
        public void CreateLocalRegistry(
            string          hostname, 
            string          username, 
            string          password, 
            string          secret, 
            TlsCertificate  certificate, 
            string          image    = NeonClusterConst.NeonPublicRegistry + "/neon-registry", 
            Action<string>  progress = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));
            Covenant.Requires<ArgumentException>(ClusterDefinition.DnsHostRegex.IsMatch(hostname));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(password));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secret));
            Covenant.Requires<ArgumentNullException>(certificate != null);

            if (!cluster.Definition.Ceph.Enabled)
            {
                throw new NotSupportedException("Cannot deploy a local Docker registry to the cluster because the cluster's Cepf file system is not enabled.");
            }

            if (HasLocalRegistry)
            {
                throw new ClusterException("The [neon-registry] service is already deployed.");
            }

            progress?.Invoke($"Setting certificate.");
            cluster.Certificate.Set("neon-registry", certificate);

            progress?.Invoke($"Updating Consul settings.");
            cluster.Registry.SetLocalHostname(hostname);
            cluster.Registry.SetLocalSecret(secret);

            progress?.Invoke($"Adding cluster DNS host entry for [{hostname}] (60 seconds).");
            cluster.DnsHosts.Set(GetRegistryDnsEntry(hostname), waitUntilPropagated: true);

            progress?.Invoke($"Writing load balancer rule.");
            cluster.PublicLoadBalancer.SetRule(GetRegistryLoadBalancerRule(hostname));

            progress?.Invoke($"Creating [neon-registry] service.");

            var manager = cluster.GetHealthyManager();

            var createResponse = manager.DockerCommand(RunOptions.None,
                "docker service create",
                "--name", "neon-registry",
                "--mode", "global",
                "--constraint", "node.role==manager",
                "--env", $"USERNAME={username}",
                "--env", $"PASSWORD={password}",
                "--env", $"SECRET={secret}",
                "--env", $"LOG_LEVEL=info",
                "--env", $"READ_ONLY=false",
                "--mount", "type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry",
                "--network", "neon-public",
                "--restart-delay", "10s",
                image);

            if (createResponse.ExitCode != 0)
            {
                throw new ClusterException($"[neon-registry] service create failed: {createResponse.ErrorText}");
            }

            progress?.Invoke($"Service created.");
            progress?.Invoke($"Logging the cluster into the [{hostname}] registry.");
            cluster.Registry.Login(hostname, username, password);
        }

        /// <summary>
        /// Removes then local Docker registry from the cluster.
        /// </summary>
        /// <param name="progress">Optional action that will be called with a progress message.</param>
        /// <exception cref="ClusterException">Thrown if no registry is deployed or there was an error removing it.</exception>
        public void RemoveLocalRegistry(Action<string> progress = null)
        {
            if (!HasLocalRegistry)
            {
                throw new ClusterException("The [neon-registry] service is not deployed.");
            }

            var syncLock = new object();
            var manager  = cluster.GetHealthyManager();
            var hostname = cluster.Registry.GetLocalHostname();

            // Logout of the registry.

            progress?.Invoke($"Logging the cluster out of the [{hostname}] registry.");
            cluster.Registry.Logout(hostname);

            // Delete the [neon-registry] service and volume.  Note that
            // the volume should exist on all of the manager nodes.

            progress?.Invoke($"Removing the [neon-registry] service.");
            manager.DockerCommand(RunOptions.None, "docker", "service", "rm", "neon-registry");

            progress?.Invoke($"Removing the [neon-registry] volumes.");

            var volumeRemoveActions = new List<Action>();
            var volumeRetryPolicy   = new LinearRetryPolicy(typeof(TransientException), maxAttempts: 10, retryInterval: TimeSpan.FromSeconds(2));

            foreach (var node in cluster.Managers)
            {
                volumeRemoveActions.Add(
                    () =>
                    {
                        // $hack(jeff.lill):
                        //
                        // Docker service removal appears to be synchronous but the removal of the
                        // actual service task containers is not.  We're going to detect this and
                        // throw a [TransientException] and then retry.

                        using (var clonedNode = node.Clone())
                        {
                            lock (syncLock)
                            {
                                progress?.Invoke($"Removing [neon-registry] volume on [{clonedNode.Name}].");
                            }

                            volumeRetryPolicy.InvokeAsync(
                                async () =>
                                {
                                    var response = clonedNode.DockerCommand(RunOptions.None, "docker", "volume", "rm", "neon-registry");

                                    if (response.ExitCode != 0)
                                    {
                                        if (response.AllText.Contains("volume is in use"))
                                        {
                                            throw new TransientException($"Error removing [neon-registry] volume from [{clonedNode.Name}: {response.ErrorText}");
                                        }
                                    }
                                    else
                                    {
                                        lock (syncLock)
                                        {
                                            progress?.Invoke($"Removed [neon-registry] volume on [{clonedNode.Name}].");
                                        }
                                    }

                                    await Task.Delay(0);

                                }).Wait();
                        }
                    });
            }

            NeonHelper.WaitForParallel(volumeRemoveActions);

            // Remove the load balancer rule and certificate.

            progress?.Invoke($"Removing the [neon-registry] load balancer rule.");
            cluster.PublicLoadBalancer.RemoveRule("neon-registry");
            progress?.Invoke($"Removing the [neon-registry] load balancer certificate.");
            cluster.Certificate.Remove("neon-registry");

            // Remove any related Consul state.

            progress?.Invoke($"Removing the [neon-registry] Consul [hostname] and [secret].");
            cluster.Registry.SetLocalHostname(null);
            cluster.Registry.SetLocalSecret(null);

            // Logout the cluster from the registry.

            progress?.Invoke($"Logging the cluster out of the [{hostname}] registry.");
            cluster.Registry.Logout(hostname);

            // Remove the cluster DNS host entry.

            progress?.Invoke($"Removing the [{hostname}] registry DNS hosts entry.");
            cluster.DnsHosts.Remove(hostname);
        }

        /// <summary>
        /// Removes then local Docker registry from the cluster.
        /// </summary>
        /// <param name="progress">Optional action that will be called with a progress message.</param>
        /// <exception cref="ClusterException">Thrown if no registry is deployed or there was an error removing it.</exception>
        public void PruneLocalRegistry(Action<string> progress = null)
        {
            // We're going to upload a script to one of the managers that handles
            // putting the [neon-registry] service into READ-ONLY mode, running
            // the garbage collection container and then restoring [neon-registry]
            // to READ/WRITE mode.
            //
            // The nice thing about this is that the operation will continue to
            // completion on the manager node even if we lose the SSH connection.

            var manager      = cluster.GetHealthyManager();
            var updateScript =
@"#!/bin/bash
# Update [neon-registry] to READ-ONLY mode:

docker service update --env-rm READ_ONLY --env-add READ_ONLY=true neon-registry

# Prune the registry:

docker run \
   --name neon-registry-prune \
   --restart-condition=none \
   --mount type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry \
   neoncluster/neon-registry garbage-collect

# Restore [neon-registry] to READ/WRITE mode:

docker service update --env-rm READ_ONLY --env-add READ_ONLY=false neon-registry
";
            var bundle = new CommandBundle("./collect.sh");

            bundle.AddFile("collect.sh", updateScript, isExecutable: true);

            progress?.Invoke("Registry prune started.");

            var pruneResponse = manager.SudoCommand(bundle, RunOptions.None);

            if (pruneResponse.ExitCode != 0)
            {
                throw new ClusterException($"The prune operation failed.  The registry may be running in READ-ONLY mode: {pruneResponse.ErrorText}");
            }

            progress?.Invoke("Registry prune completed.");
        }

        /// <summary>
        /// Returns the local cluster DNS override for the registry.
        /// </summary>
        /// <param name="hostname">The registry hostname.</param>
        /// <returns>The <see cref="DnsEntry"/>.</returns>
        private DnsEntry GetRegistryDnsEntry(string hostname)
        {
            return new DnsEntry()
            {
                Hostname  = hostname,
                IsSystem  = true,
                Endpoints = new List<DnsEndpoint>()
                {
                    new DnsEndpoint()
                    {
                        Check   = true,
                        Target = "group=managers"
                    }
                }
            };
        }

        /// <summary>
        /// Returns the load balancer rule for the [neon-registry] service.
        /// </summary>
        /// <param name="hostname">The registry hostname.</param>
        /// <returns>The <see cref="LoadBalancerHttpRule"/>.</returns>
        private LoadBalancerHttpRule GetRegistryLoadBalancerRule(string hostname)
        {
            return new LoadBalancerHttpRule()
            {
                Name = "neon-registry",
                Frontends = new List<LoadBalancerHttpFrontend>()
                {
                    new LoadBalancerHttpFrontend()
                    {
                        Host     = hostname,
                        CertName = "neon-registry",
                    }
                },

                Backends = new List<LoadBalancerHttpBackend>()
                {
                    new LoadBalancerHttpBackend()
                    {
                        Server = "neon-registry",
                        Port   = 5000
                    }
                }
            };
        }
    }
}
