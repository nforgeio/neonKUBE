//-----------------------------------------------------------------------------
// FILE:	    RegistryManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;

using Neon.Common;
using Neon.Cryptography;
using Neon.Retry;

namespace Neon.Hive
{
    /// <summary>
    /// Handles hive Docker registry related operations for <see cref="HiveProxy"/>.
    /// </summary>
    public sealed class RegistryManager
    {
        private HiveProxy hive;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hive">The parent <see cref="HiveProxy"/>.</param>
        internal RegistryManager(HiveProxy hive)
        {
            Covenant.Requires<ArgumentNullException>(hive != null);

            this.hive = hive;
        }

        /// <summary>
        /// Lists the Docker registries currently connected to the hive along
        /// with the registry credentials.
        /// </summary>
        /// <returns>The list of credentials.</returns>
        public List<RegistryCredentials> List()
        {
            var credentials = new List<RegistryCredentials>();

            foreach (var hostname in hive.Vault.Client.ListAsync(HiveConst.VaultRegistryCredentialsKey).Result)
            {
                var usernamePassword = hive.Vault.Client.ReadStringAsync($"{HiveConst.VaultRegistryCredentialsKey}/{hostname}").Result;
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
                    throw new HiveException($"Invalid credentials for the [{hostname}] registry.");
                }
            }

            return credentials;
        }

        /// <summary>
        /// Logs the hive into a Docker registry or updates the registry credentials
        /// if already logged in.
        /// </summary>
        /// <param name="registry">The registry hostname or <c>null</c> to specify the Docker public registry.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <exception cref="HiveException">Thrown if one or more of the hive nodes could not be logged in.</exception>
        public void Login(string registry, string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(string.IsNullOrEmpty(registry) || HiveDefinition.DnsHostRegex.IsMatch(registry));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(password != null);

            // Update the registry credentials in Vault.

            hive.Vault.Client.WriteStringAsync($"{HiveConst.VaultRegistryCredentialsKey}/{registry}", $"{username}/{password}").Wait();

            // Login all of the hive nodes in parallel.

            var sleepSeconds = 5;
            var maxAttempts  = 75 / sleepSeconds;
            var actions      = new List<Action>();
            var errors       = new List<string>();

            foreach (var node in hive.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var clonedNode = node.Clone())
                        {
                            for (int attempt = 1; attempt <= maxAttempts; attempt++)
                            {
                                var response = clonedNode.SudoCommand("docker login", RunOptions.None, "--username", username, "--password", password, registry);

                                if (response.ExitCode == 0)
                                {
                                    SyncDockerConf(node);
                                    return;
                                }

                                if (attempt == maxAttempts)
                                {
                                    // This is the last attempt.

                                    if (response.ExitCode != 0)
                                    {
                                        lock (errors)
                                        {
                                            errors.Add($"{clonedNode.Name}: {response.ErrorSummary}");
                                        }
                                    }
                                    else
                                    {
                                        SyncDockerConf(node);
                                    }

                                    break;
                                }
                                else
                                {
                                    // Pause for 5 seconds to mitigate transient errors.

                                    Thread.Sleep(TimeSpan.FromSeconds(sleepSeconds));
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
                sb.AppendLine($"Your hive may now be in an inconsistent state.");
                sb.AppendLine();

                foreach (var error in errors)
                {
                    sb.AppendLine(error);
                }

                throw new HiveException(sb.ToString());
            }
        }

        /// <summary>
        /// Logs the hive out of a Docker registry.
        /// </summary>
        /// <param name="registry">The registry hostname.</param>
        /// <exception cref="HiveException">Thrown if one or more of the hive nodes could not be logged out.</exception>
        public void Logout(string registry)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(registry));

            // Remove the registry credentials from Vault.

            hive.Vault.Client.DeleteAsync($"{HiveConst.VaultRegistryCredentialsKey}/{registry}").Wait();

            // Logout of all of the hive nodes in parallel.

            var actions = new List<Action>();
            var errors  = new List<string>();

            foreach (var node in hive.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var clonedNode = node.Clone())
                        {
                            // We need to special case logging out of the Docker public registry
                            // by not passing a registry hostname.

                            var registryArg = registry;

                            if (registryArg.Equals("docker.io", StringComparison.InvariantCultureIgnoreCase) ||
                                registryArg.Equals("registry-1.docker.io", StringComparison.InvariantCultureIgnoreCase))
                            {
                                registryArg = null;
                            }

                            var response = clonedNode.SudoCommand($"docker logout", RunOptions.None, registryArg);

                            if (response.ExitCode != 0)
                            {
                                lock (errors)
                                {
                                    errors.Add($"{clonedNode.Name}: {response.ErrorSummary}");
                                }
                            }
                            else
                            {
                                SyncDockerConf(node);
                            }
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);

            if (errors.Count > 0)
            {
                var sb = new StringBuilder();

                sb.AppendLine($"Could not logout [{errors.Count}] nodes from the [{registry}] Docker registry.");
                sb.AppendLine($"Your hive may now be in an inconsistent state.");
                sb.AppendLine();

                foreach (var error in errors)
                {
                    sb.AppendLine(error);
                }

                throw new HiveException(sb.ToString());
            }
        }

        /// <summary>
        /// Ensures that the Docker <b>config.json</b> file for the node's root 
        /// user matches that for the sysadmin user.
        /// </summary>
        private void SyncDockerConf(SshProxy<NodeDefinition> node)
        {
            // We also need to manage the login for the [root] account due
            // to issue
            //
            //      https://github.com/jefflill/NeonForge/issues/265

            // $hack(jeff.lill):
            //
            // We're simply going ensure that the [/root/.docker/config.json]
            // file matches the equivalent file for the node sysadmin account,
            // removing the root file if this was deleted for sysadmin.
            //
            // This is a bit of a hack because it assumes that the Docker config
            // for the root and sysadmin account never diverge, which is probably
            // a reasonable assumption given that these are managed hosts.
            //
            // We're also going to ensure that these directories and files have the
            // correct owners and permissions.

            var bundle = new CommandBundle("./sync.sh");

            bundle.AddFile("sync.sh",
$@"#!/bin/bash

if [ ! -d /root/.docker ] ; then
    mkdir -p /root/.docker
fi

if [ -f /home/{node.Username}/.docker/config.json ] ; then
    cp /home/{node.Username}/.docker/config.json /root/.docker/config.json
else
    if [ -f /root/.docker/config.json ] ; then
        rm /root/.docker/config.json
    fi
fi

if [ -d /root/.docker ] ; then
    chown -R root:root /root/.docker
    chmod 660 /root/.docker/*
fi

if [ -d /home/{node.Username}/.docker ] ; then
    chown -R {node.Username}:{node.Username} /home/{node.Username}/.docker
    chmod 660 /home/{node.Username}/.docker/*
fi
",
                isExecutable: true);

            var response = node.SudoCommand(bundle);

            if (response.ExitCode != 0)
            {
                throw new HiveException(response.ErrorSummary);
            }
        }

        /// <summary>
        /// Returns the credentials for a specific Docker registry connected to the hive.
        /// </summary>
        /// <param name="registry">The target registry hostname.</param>
        /// <returns>The credentials or <c>null</c> if no credentials exists.</returns>
        public RegistryCredentials GetCredentials(string registry)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(registry));

            var usernamePassword = hive.Vault.Client.ReadStringOrDefaultAsync($"{HiveConst.VaultRegistryCredentialsKey}/{registry}").Result;

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
                throw new HiveException($"Invalid credentials for the [{registry}] registry.");
            }
        }

        /// <summary>
        /// Restarts the hive registry caches as required, using the 
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
        /// is not enabled for this hive.
        /// </note>
        /// </remarks>
        public bool RestartCache(string registry, string username, string password)
        {
            // Return immediately if this is a NOP.

            if (!HiveHelper.IsDockerPublicRegistry(registry) || !hive.Definition.Docker.RegistryCache)
            {
                return true;
            }

            // We're not going to restart these in parallel so only one
            // manager cache will be down at any given time.  This should
            // result in no cache downtime for hives with multiple
            // managers.

            foreach (var manager in hive.Managers)
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
            return hive.Consul.Client.KV.GetStringOrDefault($"{HiveConst.ConsulRegistryRootKey}/hostname").Result;
        }

        /// <summary>
        /// Persists the hostname for the local Docker registry.
        /// </summary>
        /// <param name="hostname">The new hostname for the local Docker registry or <c>null</c> to remove it.</param>
        public void SetLocalHostname(string hostname)
        {
            Covenant.Requires<ArgumentException>(string.IsNullOrEmpty(hostname) || HiveDefinition.NameRegex.IsMatch(hostname));

            if (string.IsNullOrEmpty(hostname))
            {
                hive.Consul.Client.KV.Delete($"{HiveConst.ConsulRegistryRootKey}/hostname").Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString($"{HiveConst.ConsulRegistryRootKey}/hostname", hostname).Wait();
            }
        }

        /// <summary>
        /// Returns the secret for the local Docker registry if one is
        /// deployed.
        /// </summary>
        /// <returns>The hostname or <c>null</c>.</returns>
        public string GetLocalSecret()
        {
            return hive.Consul.Client.KV.GetStringOrDefault($"{HiveConst.ConsulRegistryRootKey}/secret").Result;
        }

        /// <summary>
        /// Persists the secret for the local Docker registry.
        /// </summary>
        /// <param name="secret">The new secret for the local Docker registry or <c>null</c> to remove it.</param>
        public void SetLocalSecret(string secret)
        {
            if (string.IsNullOrEmpty(secret))
            {
                hive.Consul.Client.KV.Delete($"{HiveConst.ConsulRegistryRootKey}/secret").Wait();
            }
            else
            {
                hive.Consul.Client.KV.PutString($"{HiveConst.ConsulRegistryRootKey}/secret", secret).Wait();
            }
        }

        /// <summary>
        /// Determines whether a local Docker registry is deployed to the hive.
        /// </summary>
        public bool HasLocalRegistry
        {
            get { return hive.Docker.InspectService("neon-registry") != null;  }
        }

        /// <summary>
        /// Deploys the local Docker registry to the hive.
        /// </summary>
        /// <param name="hostname">The registry hostname.</param>
        /// <param name="username">The registry username.</param>
        /// <param name="password">The registry password.</param>
        /// <param name="secret">The registry secret.</param>
        /// <param name="certificate">The certificate used to secure the registry.</param>
        /// <param name="image">Optionally specifies the Docker image to be deployed (defaults to <b>nhive/neon-registry</b>).</param>
        /// <param name="progress">Optional action that will be called with a progress message.</param>
        /// <exception cref="HiveException">Thrown if a registry is already deployed or deployment failed.</exception>
        /// <exception cref="NotSupportedException">Thrown if the hive does not support local registries.</exception>
        public void CreateLocalRegistry(
            string          hostname, 
            string          username, 
            string          password, 
            string          secret, 
            TlsCertificate  certificate, 
            string          image    = HiveConst.NeonProdRegistry + "/neon-registry", 
            Action<string>  progress = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(hostname));
            Covenant.Requires<ArgumentException>(HiveDefinition.DnsHostRegex.IsMatch(hostname));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(password));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secret));
            Covenant.Requires<ArgumentNullException>(certificate != null);

            if (!hive.Definition.HiveFS.Enabled)
            {
                throw new NotSupportedException("Cannot deploy a local Docker registry to the hive because the hive's Cepf file system is not enabled.");
            }

            if (HasLocalRegistry)
            {
                throw new HiveException("The [neon-registry] service is already deployed.");
            }

            progress?.Invoke($"Setting certificate.");
            hive.Certificate.Set("neon-registry", certificate);

            progress?.Invoke($"Updating Consul settings.");
            hive.Registry.SetLocalHostname(hostname);
            hive.Registry.SetLocalSecret(secret);

            progress?.Invoke($"Adding hive DNS host entry for [{hostname}] (60 seconds).");
            hive.Dns.Set(GetRegistryDnsEntry(hostname), waitUntilPropagated: true);

            progress?.Invoke($"Writing traffic manager rule.");
            hive.PublicTraffic.SetRule(GetRegistryTrafficManagerRule(hostname));

            progress?.Invoke($"Creating [neon-registry] service.");

            var manager = hive.GetReachableManager();

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
                throw new HiveException($"[neon-registry] service create failed: {createResponse.ErrorText}");
            }

            progress?.Invoke($"Service created.");
            progress?.Invoke($"Logging the hive into the [{hostname}] registry.");
            hive.Registry.Login(hostname, username, password);
        }

        /// <summary>
        /// Removes then local Docker registry from the hive.
        /// </summary>
        /// <param name="progress">Optional action that will be called with a progress message.</param>
        /// <exception cref="HiveException">Thrown if no registry is deployed or there was an error removing it.</exception>
        public void RemoveLocalRegistry(Action<string> progress = null)
        {
            if (!HasLocalRegistry)
            {
                throw new HiveException("The [neon-registry] service is not deployed.");
            }

            var syncLock = new object();
            var manager  = hive.GetReachableManager();
            var hostname = hive.Registry.GetLocalHostname();

            // Logout of the registry.

            progress?.Invoke($"Logging the hive out of the [{hostname}] registry.");
            hive.Registry.Logout(hostname);

            // Delete the [neon-registry] service and volume.  Note that
            // the volume should exist on all of the manager nodes.

            progress?.Invoke($"Removing the [neon-registry] service.");
            manager.DockerCommand(RunOptions.None, "docker", "service", "rm", "neon-registry");

            progress?.Invoke($"Removing the [neon-registry] volumes.");

            var volumeRemoveActions = new List<Action>();
            var volumeRetryPolicy   = new LinearRetryPolicy(typeof(TransientException), maxAttempts: 10, retryInterval: TimeSpan.FromSeconds(2));

            foreach (var node in hive.Managers)
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

            // Remove the traffic manager rule and certificate.

            progress?.Invoke($"Removing the [neon-registry] traffic manager rule.");
            hive.PublicTraffic.RemoveRule("neon-registry");
            progress?.Invoke($"Removing the [neon-registry] traffic manager certificate.");
            hive.Certificate.Remove("neon-registry");

            // Remove any related Consul state.

            progress?.Invoke($"Removing the [neon-registry] Consul [hostname] and [secret].");
            hive.Registry.SetLocalHostname(null);
            hive.Registry.SetLocalSecret(null);

            // Logout the hive from the registry.

            progress?.Invoke($"Logging the hive out of the [{hostname}] registry.");
            hive.Registry.Logout(hostname);

            // Remove the hive DNS host entry.

            progress?.Invoke($"Removing the [{hostname}] registry DNS hosts entry.");
            hive.Dns.Remove(hostname);
        }

        /// <summary>
        /// Removes then local Docker registry from the hive.
        /// </summary>
        /// <param name="progress">Optional action that will be called with a progress message.</param>
        /// <exception cref="HiveException">Thrown if no registry is deployed or there was an error removing it.</exception>
        public void PruneLocalRegistry(Action<string> progress = null)
        {
            // We're going to upload a script to one of the managers that handles
            // putting the [neon-registry] service into READ-ONLY mode, running
            // the garbage collection container and then restoring [neon-registry]
            // to READ/WRITE mode.
            //
            // The nice thing about this is that the operation will continue to
            // completion on the manager node even if we lose the SSH connection.

            var manager      = hive.GetReachableManager();
            var updateScript =
@"#!/bin/bash
# Update [neon-registry] to READ-ONLY mode:

docker service update --env-rm READ_ONLY --env-add READ_ONLY=true neon-registry

# Prune the registry:

docker run \
   --name neon-registry-prune \
   --restart-condition=none \
   --mount type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry \
   nhive/neon-registry garbage-collect

# Restore [neon-registry] to READ/WRITE mode:

docker service update --env-rm READ_ONLY --env-add READ_ONLY=false neon-registry
";
            var bundle = new CommandBundle("./collect.sh");

            bundle.AddFile("collect.sh", updateScript, isExecutable: true);

            progress?.Invoke("Registry prune started.");

            var pruneResponse = manager.SudoCommand(bundle, RunOptions.None);

            if (pruneResponse.ExitCode != 0)
            {
                throw new HiveException($"The prune operation failed.  The registry may be running in READ-ONLY mode: {pruneResponse.ErrorText}");
            }

            progress?.Invoke("Registry prune completed.");
        }

        /// <summary>
        /// Returns the local hive DNS override for the registry.
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
        /// Returns the traffic manager rule for the [neon-registry] service.
        /// </summary>
        /// <param name="hostname">The registry hostname.</param>
        /// <returns>The <see cref="TrafficHttpRule"/>.</returns>
        private TrafficHttpRule GetRegistryTrafficManagerRule(string hostname)
        {
            return new TrafficHttpRule()
            {
                Name   = "neon-registry",
                System = true,

                Frontends = new List<TrafficHttpFrontend>()
                {
                    new TrafficHttpFrontend()
                    {
                        Host     = hostname,
                        CertName = "neon-registry",
                    }
                },

                Backends = new List<TrafficHttpBackend>()
                {
                    new TrafficHttpBackend()
                    {
                        Server = "neon-registry",
                        Port   = 5000
                    }
                }
            };
        }
    }
}
