//-----------------------------------------------------------------------------
// FILE:	    RegistryManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using Consul;

using Neon.Common;

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
                var fields           = usernamePassword.Split(new char[] { '/' }, 2);

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
                    throw new NeonClusterException($"Invalid credentials for the [{hostname}] registry.");
                }
            }

            return credentials;
        }

        /// <summary>
        /// Logs the cluster into a Docker registry or updates the registry credentials
        /// if already logged in.
        /// </summary>
        /// <param name="registry">The registry hostname.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <exception cref="NeonClusterException">Thrown if one or more of the cluster nodes could not be logged in.</exception>
        public void Login(string registry, string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(registry));
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
                            // Use a command bundle so we're not passing the credentials
                            // on the command line.

                            var loginBundle = new CommandBundle("./docker-login.sh");

                            loginBundle.AddFile("docker-login.sh",
$@"#!/bin/bash

cat password.txt | docker login --username ""{username}"" --password-stdin {registry}
",
                                isExecutable: true);

                            loginBundle.AddFile("password.txt", password);

                            var response = clonedNode.SudoCommand(loginBundle, cluster.SecureRunOptions);

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

                throw new NeonClusterException(sb.ToString());
            }
        }

        /// <summary>
        /// Logs the cluster out of a Docker registry.
        /// </summary>
        /// <param name="registry">The registry hostname.</param>
        /// <exception cref="NeonClusterException">Thrown if one or more of the cluster nodes could not be logged out.</exception>
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
                        var response = node.SudoCommand($"docker logout", RunOptions.None, registry);

                        if (response.ExitCode != 0)
                        {
                            lock (errors)
                            {
                                errors.Add($"{node}: {response.ErrorSummary}");
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

                throw new NeonClusterException(sb.ToString());
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
                throw new NeonClusterException($"Invalid credentials for the [{registry}] registry.");
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
    }
}
