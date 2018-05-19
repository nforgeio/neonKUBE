//-----------------------------------------------------------------------------
// FILE:	    DockerRegistryModule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Neon.Cluster;
using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace NeonCli.Ansible
{
    /// <summary>
    /// Implements the <b>neon_docker_registry</b> Ansible module.
    /// </summary>
    public class DockerRegistryModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_docker_registry:
        //
        // Synopsis:
        // ---------
        //
        // Manages a cluster local Docker registry.
        //
        // Requirements:
        // -------------
        //
        // This module runs only within the [neon-cli] container when invoked
        // by [neon ansible exec ...] or [neon ansible play ...].
        //
        // Options:
        // --------
        //
        // parameter    required    default     choices     comments
        // --------------------------------------------------------------------
        //
        // state        no          present     present     deploys the local registry
        //                                      absent      removes the local registry
        //                                      prune       removed unreferenced image layers
        //
        // hostname     see comment                         registry public DNS name
        //                                                  required if [state=present]
        //
        // certificate  see comment                         registry PEM encode TLS certificate
        //                                                  and private key
        //                                                  required if [state=present]
        //
        // username     see comment                         registry username
        //                                                  required if [state=present]
        //
        // password     see comment                         registry password
        //                                                  required if [state=present]
        //
        // image        no          neoncluster/neon-registry the Docker image to deploy
        //
        // Check Mode:
        // -----------
        //
        // This module supports the [--check] Ansible command line option and [check_mode] task
        // property by determining whether any changes would have been made and also logging
        // a desciption of the changes when Ansible verbosity is increased.
        //
        // Remarks:
        // --------
        //
        // This module manages a Docker registry deployed within a neonCLUSTER.  This can be
        // useful for storing private images without having to pay extra on DockerHub or to
        // host images locally to avoid the overhead and unpredictability of pulling images
        // from a remote registry over the Internet.
        //
        // This has three requirements:
        //
        //      1. A DNS hostname like: REGISTRY.MY-DOMAIN.COM
        //      2. A real certificate covering the domain.
        //      3. A neonCLUSTER with CephFS.
        //
        // You'll need to configure your DNS hostname to resolve to your Internet facing
        // router or load balancer IP address if the registry is to reachable from the
        // public Internet and then you'll need to configure external TLS port 443 traffic
        // to be directed to port 443 on one or more of the cluster nodes.
        //
        // You'll also need a TLS certificate the covers the domain name.  Note that this
        // must be a real certificate, not a self-signed one.  Docker really wants a real
        // cert and single domain certificates cost less than $10/year if you shop around,
        // so there's not much of a reason no to purchase one these days.
        //
        // This module also requires that the neonCLUSTER have a CephFS distributed file
        // system deployed.  This is installed by default, but you should make sure that
        // enough storage capacity will be available.  CephFS allows multiple registry
        // servers to share the same file storage so all instances will always have a
        // consistent view of the stored Docker images.
        //
        // In theory, it could be possible to deploy a single registry instance with a
        // standard non-replicated Docker volume, but that's not cool, so we're not
        // going there.
        //
        // The registry needs to be secured with a username and password.  The current
        // [neon-registry] image supports only one user that has read/write access to
        // the registry.  This may change for future releases.
        //
        // You can specify one of three module states:
        //
        //      present         - Deploys the registry and related things like the
        //                        certificate, CephFS volume, DNS override and load
        //                        balancer rule to the cluster.  This also ensures 
        //                        that all cluster nodes are logged into the registry.
        //
        //      absent          - Logs the cluster nodes off the registry, removes
        //                        the registry service along with all related items.
        //
        //      prune           - Temporarily limits the registry service to a single
        //                        replica running in garbage collection mode.
        //
        //                        The garbage collection instance will handle
        //                        read-only registry requests while it's removing
        //                        unreferenced layers.  The fully functional registry
        //                        service will be enabled again once garbage 
        //                        collection has completed.
        //
        // This module uses the following reserved names:
        //
        //      neon-registry   - registry service
        //      neon-registry   - neon (CephFS) docker volume for registry data
        //      neon-registry   - service load balancer rule
        //      neon-registry   - registry certificate
        //
        // Examples:
        // ---------
        //
        // This example deploys a local Docker registry at REGISTRY.TEST.COM with
        // the specified certificate and credentials and then has all cluster
        // nodes login into the registry using the credentials.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: registry create
        //        neon_docker_registry:
        //          state: present
        //          hostname: registry.test.com
        //          username: billy
        //          password: bob
        //
        // This example changes the registry credentials and has all cluster nodes
        // log back in with them.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: registry credentials
        //        neon_docker_registry:
        //          state: present
        //          hostname: registry.test.com
        //          username: billy
        //          password: newpassword2
        //
        // This example restarts or deploys the registry with a custom registry
        // image.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: registry credentials
        //        neon_docker_registry:
        //          state: present
        //          hostname: registry.test.com
        //          username: billy
        //          password: newpassword2
        //          image: mydocker/custom-image
        //
        // This example removes unreferenced image layers by temporarily putting
        // the  registry in garbage collection mode and removing any unreferenced
        // image layers.  Only a single read-only instance of the registry
        // will be available while pruning.  Normal service will resume once
        // this operation has completed.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: registry credentials
        //        neon_docker_registry:
        //          state: prune
        //
        // This example logs all nodes off of the local registry if one
        // is deployed and then removes the registry along with all
        // registry data.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: registry remove
        //        neon_docker_registry:
        //          state: absent

        private object syncLock = new object();

        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "state",
            "hostname",
            "certificate",
            "username",
            "password",
            "image"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var cluster = NeonClusterHelper.Cluster;

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

            // Obtain common arguments.

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            if (context.HasErrors)
            {
                return;
            }

            // Determine whether the registry is deployed.

            var manager      = cluster.GetHealthyManager();
            var sbErrorNodes = new StringBuilder();

            // Determine whether the registry service is already deployed and 
            // also retrieve the registry credentials from Vault if present.
            // Note that the current registry hostname will be persisted to
            // Consul at [neon/services/neon-registry/hostname] when a registry
            // is deployed.

            context.WriteLine(AnsibleVerbosity.Trace, $"Inspecting the [neon-registry] service.");

            var currentService = cluster.InspectService("neon-registry");

            context.WriteLine(AnsibleVerbosity.Trace, $"Getting current registry hostname from Consul.");

            var currentHostname = cluster.Consul.KV.GetStringOrDefault($"{NeonClusterConst.ConsulRegistryRootKey}/hostname").Result;
            var currentSecret   = cluster.Consul.KV.GetStringOrDefault($"{NeonClusterConst.ConsulRegistryRootKey}/secret").Result;

            if (currentService != null)
            {
                if (string.IsNullOrEmpty(currentHostname))
                {
                    // The service is running but the [hostname] Consul setting is not
                    // present.  We'll attempt to repair this by obtaining the hostname
                    // from the deployed service's environment variable.

                    context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service and its Consul [hostname] setting are inconsistent.  Attempting repair.");

                    currentHostname = currentService.GetEnv("hostname");

                    if (string.IsNullOrEmpty(currentHostname))
                    {
                        context.WriteErrorLine($"[neon-registry] Consul settings cannot be repaired.");
                        return;
                    }

                    cluster.Consul.KV.PutString($"{NeonClusterConst.ConsulRegistryRootKey}/hostname", currentHostname).Wait();
                    context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service Consul [hostname] setting is repaired.");
                }

                if (string.IsNullOrEmpty(currentSecret))
                {
                    // The service is running but the [secret] Consul setting is not
                    // present.  We'll attempt to repair this by obtaining the hostname
                    // from the deployed service's environment variable.

                    context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service and its Consul [secret] setting are inconsistent.  Attempting repair.");

                    currentSecret = currentService.GetEnv("secret");

                    if (string.IsNullOrEmpty(currentHostname))
                    {
                        context.WriteErrorLine($"[neon-registry] Consul settings cannot be repaired.");
                        return;
                    }

                    cluster.Consul.KV.PutString($"{NeonClusterConst.ConsulRegistryRootKey}/secret", currentSecret).Wait();
                    context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service Consul [secret] setting was repaired.");
                }
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Reading existing credentials for [{currentHostname}].");

            var currentCredentials = cluster.GetRegistryCredential(currentHostname);

            if (currentCredentials != null)
            {
                context.WriteLine(AnsibleVerbosity.Info, $"Credentials for [{currentHostname}] exist.");
            }
            else
            {
                context.WriteLine(AnsibleVerbosity.Info, $"Credentials for [{currentHostname}] do not exist.");
            }

            if (currentService != null && currentCredentials == null)
            {
                // The service is running but the registry credentials are not present
                // in Vault.  We'll attempt to repair this by obtaining the hostname from
                // the deployed service's environment variables.

                context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service and the Vault settings are inconsistent.  Attempting repair.");

                var currentUsername = currentService.GetEnv("username");
                var currentPassword = currentService.GetEnv("password");

                if (string.IsNullOrEmpty(currentUsername) || string.IsNullOrEmpty(currentPassword))
                {
                    context.WriteErrorLine($"[neon-registry] Consul settings cannot be repaired.");
                    return;
                }

                currentCredentials =
                    new RegistryCredentials()
                    {
                        Registry = currentHostname,
                        Username = currentUsername,
                        Password = currentPassword
                    };

                cluster.SetRegistryCredential(currentHostname, currentUsername, currentPassword);
                context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service Vault settings were repaired.");
            }

            // Obtain the current registry certificate, if any.

            var currentCertificate = cluster.Certificate.Get("neon-registry");

            switch (state)
            {
                case "absent":

                    if (currentService == null)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, "The local registry is not deployed.");
                    }

                    if (context.CheckMode)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"Local registry will be removed when CHECK-MODE is disabled.");
                        return;
                    }

                    // Remove the registry credentials from Vault if present and then
                    // have all nodes logout, ignoring any errors to be sure they're
                    // all logged out.

                    if (currentCredentials != null)
                    {
                        context.Changed = true;
                        context.WriteLine(AnsibleVerbosity.Trace, $"Removing credentials for [{currentHostname}].");
                        cluster.RemoveRegistryCredential(currentHostname);
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"Logging all cluster nodes out of [{currentHostname}].");

                    var logoutActions = new List<Action>();

                    foreach (var node in cluster.Nodes)
                    {
                        logoutActions.Add(
                            () =>
                            {
                                if (!node.RegistryLogout(currentHostname))
                                {
                                    lock (syncLock)
                                    {
                                        sbErrorNodes.AppendWithSeparator(node.Name, ", ");
                                    }
                                }
                            });
                    }

                    NeonHelper.WaitForParallel(logoutActions);

                    if (sbErrorNodes.Length == 0)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"All cluster nodes are logged out.");
                    }
                    else
                    {
                        context.WriteErrorLine($"These nodes could not be logged out: {sbErrorNodes}");
                        context.WriteErrorLine($"The cluster may be in an inconsistent state.");
                    }

                    // Delete the [neon-registry] service and volume.  Note that
                    // the volume should exist on all of the manager nodes.

                    manager.DockerCommand(RunOptions.None, "docker", "service", "rm", "neon-registry");

                    var volumeRemoveActions = new List<Action>();

                    foreach (var node in cluster.Managers)
                    {
                        volumeRemoveActions.Add(
                            () =>
                            {
                                response = node.DockerCommand(RunOptions.None, "docker", "volume", "rm", "neon-registry");

                                if (response.ExitCode != 0)
                                {
                                    lock (syncLock)
                                    {
                                        context.WriteLine(AnsibleVerbosity.Info, $"Error removing [neon-registry] volume from [{node.Name}: {response.ErrorText}");
                                    }
                                }
                            });
                    }

                    NeonHelper.WaitForParallel(volumeRemoveActions);

                    // Delete the load balancer rule and certificate.

                    cluster.PublicLoadBalancer.RemoveRule("neon-registry");
                    cluster.Certificate.Remove("neon-registry");
                    break;

                case "present":

                    // Parse the [hostname], [certificate], [username] and [password] arguments.

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [hostname]");

                    if (!context.Arguments.TryGetValue<string>("hostname", out var hostname))
                    {
                        throw new ArgumentException($"[hostname] module argument is required.");
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [certificate]");

                    if (!context.Arguments.TryGetValue<string>("certificate", out var certificatePem))
                    {
                        throw new ArgumentException($"[certificate] module argument is required.");
                    }

                    if (!TlsCertificate.TryParse(certificatePem, out var certificate))
                    {
                        throw new ArgumentException($"[certificate] is not a valid certificate.");
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [username]");

                    if (!context.Arguments.TryGetValue<string>("username", out var username))
                    {
                        throw new ArgumentException($"[username] module argument is required.");
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [password]");

                    if (!context.Arguments.TryGetValue<string>("password", out var password))
                    {
                        throw new ArgumentException($"[password] module argument is required.");
                    }

                    // Detect service changes.

                    var hostnameChanged    = hostname != currentCredentials.Registry;
                    var usernameChanged    = username != currentCredentials.Username;
                    var passwordChanged    = password != currentCredentials.Password;
                    var certificateChanged = certificate.CombinedNormalizedPem != currentCertificate.CombinedNormalizedPem;
                    var updateRequired     = hostnameChanged || usernameChanged || passwordChanged || certificateChanged;

                    // Handle CHECK-MODE.

                    if (context.CheckMode)
                    {
                        if (currentService == null)
                        {
                            context.WriteLine(AnsibleVerbosity.Important, $"Local registry will be deployed when CHECK-MODE is disabled.");
                            return;
                        }

                        if (updateRequired)
                        {
                            context.WriteLine(AnsibleVerbosity.Important, $"One or more of the arguments have changed so the registry will be updated when CHECK-MODE is disabled.");
                            return;
                        }

                        return;
                    }

                    // Perform the operation.

                    if (currentService == null)
                    {
                        context.Changed = true;

                        // The registry service isn't running, so we'll do a full deployment.

                        context.WriteLine(AnsibleVerbosity.Trace, $"Setting certificate.");
                        cluster.Certificate.Set("neon-registry", certificate);

                        var secret = NeonHelper.GetRandomPassword(20);

                        context.WriteLine(AnsibleVerbosity.Trace, $"Updating Consul settings.");
                        cluster.Consul.KV.PutString($"{NeonClusterConst.ConsulRegistryRootKey}/secret", secret).Wait();
                        cluster.Consul.KV.PutString($"{NeonClusterConst.ConsulRegistryRootKey}/hostname", hostname).Wait();

                        context.WriteLine(AnsibleVerbosity.Trace, $"Saving load balancer rule.");
                        cluster.PublicLoadBalancer.SetRule(
                            new LoadBalancerHttpRule()
                            {
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
                                        Group = "managers",
                                        Port  = 5000
                                    }
                                }
                            });


                        context.WriteLine(AnsibleVerbosity.Trace, $"Touching certificate.");
                        cluster.Certificate.Touch();

                        context.WriteLine(AnsibleVerbosity.Trace, $"Creating service.");

                        response = manager.DockerCommand(RunOptions.None,
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
                            "--restart-delay", "10s");

                        if (response.ExitCode != 0)
                        {
                            context.WriteErrorLine($"[neon-registry] service create failed: {response.ErrorText}");
                            return;
                        }

                        context.WriteLine(AnsibleVerbosity.Trace, $"Service created.");
                    }
                    else if (updateRequired)
                    {
                        context.Changed = true;

                        // Update the service and related settings as required.

                        if (certificateChanged)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating certificate.");
                            cluster.Certificate.Set("neon-registry", certificate);
                            cluster.Certificate.Touch();
                        }

                        if (hostnameChanged)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating load balancer rule.");

                            cluster.PublicLoadBalancer.SetRule(
                                new LoadBalancerHttpRule()
                                {
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
                                            Group = "managers",
                                            Port  = 5000
                                        }
                                    }
                                });
                        }

                        if (certificateChanged || hostnameChanged)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Touching certificate.");
                            cluster.Certificate.Touch();
                        }

                        context.WriteLine(AnsibleVerbosity.Trace, $"Updating service.");

                        response = manager.DockerCommand(RunOptions.None,
                            "docker service update",
                            "--mode", "global",
                            "--constraint", "node.role==manager",
                            "--env", $"USERNAME={username}",
                            "--env", $"PASSWORD={password}",
                            "--env", $"SECRET={currentSecret}",
                            "--env", $"LOG_LEVEL=info",
                            "--env", $"READ_ONLY=false",
                            "--mount", "type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry",
                            "--network", "neon-public",
                            "--restart-delay", "10s",
                            "neon-registry");

                        if (response.ExitCode != 0)
                        {
                            context.WriteErrorLine($"[neon-registry] service update failed: {response.ErrorText}");
                            return;
                        }

                        context.WriteLine(AnsibleVerbosity.Trace, $"Service updated.");
                    }
                    break;

                case "prune":

                    if (currentService == null)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, "Registry service is not running.");
                        return;
                    }

                    if (context.CheckMode)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, "Registry will be pruned when CHECK-MODE is disabled.");
                        return;
                    }

                    context.Changed = true; // Always set this to TRUE for prune.

                    // We're going to upload a script to one of the managers that handles
                    // putting the [neon-registry] service into READ-ONLY mode, running
                    // the garbage collection container and then restoring [neon-registry]
                    // to READ/WRITE mode.
                    //
                    // The nice thing about this is that the operation will continue to
                    // completion on the manager node even if we loose the SSH connection.

                    var updateScript =
@"#!/bin/bash
# Update [neon-registry] to READ-ONLY mode:

docker service update --env READ_ONLY=true neon-registry

# Prune the registry:

docker run \
   --name neon-registry-prune \
   --restart-condition=none \
   --mount type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry \
   neoncluster/neon-registry garbage-collect

# Restore [neon-registry] to READ/WRITE mode:

docker service update --env READ_ONLY=false neon-registry
";
                    var bundle = new CommandBundle("./collect.sh");

                    bundle.AddFile("collect.sh", updateScript, isExecutable: true);

                    context.WriteLine(AnsibleVerbosity.Info, "Registry prune started.");

                    var response = manager.SudoCommand(bundle, RunOptions.None);

                    if (response.ExitCode != 0)
                    {
                        context.WriteErrorLine($"The prune operation failed.  The registry may be running in READ-ONLY mode: {response.ErrorText}");
                        return;
                    }

                    context.WriteLine(AnsibleVerbosity.Info, "Registry prune completed.");
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present], [absent], or [prune].");
            }
        }
    }
}
