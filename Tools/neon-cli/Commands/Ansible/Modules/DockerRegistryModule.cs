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

using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Hive;
using Neon.Net;
using Neon.Retry;

// $note(jeff.lill):
//
// I would have loke to have used [RegistryManager] to consolidate this code
// but there's some subtle logic around registry deployment that prevents
// this, at least for now.

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
        // Manages a hive local Docker registry.
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
        //                                                  required if [state=present or absent]
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
        // secret       see comment                         anti-spoofing secret string
        //                                                  required if [state=present]
        //
        // image        no          nhive/neon-registry     the Docker image to deploy
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
        // This module manages a Docker registry deployed within a neonHIVE.  This can be
        // useful for storing private images without having to pay extra on DockerHub or to
        // host images locally to avoid the overhead and unpredictability of pulling images
        // from a remote registry over the Internet.
        //
        // This has three requirements:
        //
        //      1. A DNS hostname like: REGISTRY.MY-DOMAIN.COM
        //      2. A real certificate covering the domain.
        //      3. A neonHIVE with CephFS.
        //
        // You'll need to configure your DNS hostname to resolve to your Internet facing
        // router or load balancer IP address if the registry is to reachable from the
        // public Internet and then you'll need to configure external TLS port 443 traffic
        // to be directed to port 443 on one or more of the hive nodes.
        //
        // You'll also need a TLS certificate the covers the domain name.  Note that this
        // must be a real certificate, not a self-signed one.  Docker really wants a real
        // cert and single domain certificates cost less than $10/year if you shop around,
        // so there's not much of a reason no to purchase one these days.
        //
        // This module also requires that the neonHIVE have a CephFS distributed file
        // system deployed.  This is installed by default, but you should make sure that
        // enough storage capacity will be available.  CephFS allows multiple registry
        // servers to share the same file storage so all instances will always have a
        // consistent view of the stored Docker images.
        //
        // In theory, it could be possible to deploy a single registry instance with a
        // standard non-replicated Docker volume, but that's not cool, so we're not
        // going there.
        //
        // The registry needs to be secured with a username, password and anti-spoofing 
        // secret.  The current [neon-registry] image supports only one user that has 
        // read/write access to the registry.  This may change for future releases.
        //
        // Pass [secret] as a secure password that should be different from [password].
        // You can use this command to generate a cryptographically secure password:
        //
        //      neon create password
        //
        // IMPORTANT: You should avoid changing the [secret] once you've deployed
        //            your registry and have hives and users logged into it. 
        //            Changing the secret will cause these clusters and users to 
        //            reject your registry as potentially hacked and you'll need 
        //            to have everyone logout and then login again to correct this, 
        //            which probably isn't what you want.
        //
        // You can specify one of three module states:
        //
        //      present         - Deploys the registry and related things like the
        //                        certificate, CephFS volume, DNS override and load
        //                        balancer rule to the hive.  This also ensures 
        //                        that all hive nodes are logged into the registry.
        //
        //      absent          - Logs the hive nodes off the registry, removes
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
        // Examples:
        // ---------
        //
        // This example deploys a local Docker registry at REGISTRY.TEST.COM with
        // the specified certificate and credentials and then has all hive nodes
        // login into the registry using the credentials.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: registry create
        //        neon_docker_registry:
        //          state: present
        //          hostname: registry.test.com
        //          certificate: {{ my_certificate_pem }}
        //          username: billy
        //          password: bob
        //          secret: QKDa79aeVYd5t5W4rOHB
        //
        // This example changes the registry credentials and has all hive nodes
        // log back in with them.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: registry credentials
        //        neon_docker_registry:
        //          state: present
        //          hostname: registry.test.com
        //          certificate: {{ my_certificate_pem }}
        //          username: billy
        //          password: newpassword2
        //          secret: QKDa79aeVYd5t5W4rOHB
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
        //          certificate: {{ my_certificate_pem }}
        //          username: billy
        //          password: newpassword2
        //          secret: QKDa79aeVYd5t5W4rOHB
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
            "secret",
            "image"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var         hive = HiveHelper.Hive;
            string      hostname;

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

            var manager      = hive.GetReachableManager();
            var sbErrorNodes = new StringBuilder();

            // Determine whether the registry service is already deployed and 
            // also retrieve the registry credentials from Vault if present.
            // Note that the current registry hostname will be persisted to
            // Consul at [neon/service/neon-registry/hostname] when a registry
            // is deployed.

            context.WriteLine(AnsibleVerbosity.Trace, $"Inspecting the [neon-registry] service.");

            var currentService = hive.Docker.InspectService("neon-registry");

            context.WriteLine(AnsibleVerbosity.Trace, $"Getting current registry hostname from Consul.");

            var currentHostname = hive.Registry.GetLocalHostname();
            var currentSecret   = hive.Registry.GetLocalSecret();
            var currentImage    = currentService?.Spec.TaskTemplate.ContainerSpec.ImageWithoutSHA;

            var currentCredentials =        // Set blank properties for the change detection below.
                new RegistryCredentials()
                {
                    Registry = string.Empty,
                    Username = string.Empty,
                    Password = string.Empty
                };

            if (!string.IsNullOrEmpty(currentHostname))
            {
                context.WriteLine(AnsibleVerbosity.Trace, $"Reading existing registry credentials for [{currentHostname}].");

                currentCredentials = hive.Registry.GetCredentials(currentHostname);

                if (currentCredentials != null)
                {
                    context.WriteLine(AnsibleVerbosity.Info, $"Registry credentials for [{currentHostname}] exist.");
                }
                else
                {
                    context.WriteLine(AnsibleVerbosity.Info, $"Registry credentials for [{currentHostname}] do not exist.");
                }
            }

            // Obtain the current registry TLS certificate (if any).

            var currentCertificate = hive.Certificate.Get("neon-registry");

            // Perform the operation.

            switch (state)
            {
                case "absent":

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [hostname]");

                    if (!context.Arguments.TryGetValue<string>("hostname", out hostname))
                    {
                        throw new ArgumentException($"[hostname] module argument is required.");
                    }

                    if (currentService == null)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, "[neon-registry] is not currently deployed.");
                    }

                    if (context.CheckMode)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"Local registry will be removed when CHECK-MODE is disabled.");
                        return;
                    }

                    if (currentService == null)
                    {
                        return; // Nothing to do
                    }

                    context.Changed = true;

                    // Logout of the registry.

                    if (currentCredentials != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Logging the hive out of the [{currentHostname}] registry.");
                        hive.Registry.Logout(currentHostname);
                    }

                    // Delete the [neon-registry] service and volume.  Note that
                    // the volume should exist on all of the manager nodes.

                    context.WriteLine(AnsibleVerbosity.Trace, $"Removing the [neon-registry] service.");
                    manager.DockerCommand(RunOptions.None, "docker", "service", "rm", "neon-registry");

                    context.WriteLine(AnsibleVerbosity.Trace, $"Removing the [neon-registry] volumes.");

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
                                    lock (context)
                                    {
                                        context.WriteLine(AnsibleVerbosity.Trace, $"Removing [neon-registry] volume on [{clonedNode.Name}].");
                                    }

                                    volumeRetryPolicy.InvokeAsync(
                                        async () =>
                                        {
                                            var response = clonedNode.DockerCommand(RunOptions.None, "docker", "volume", "rm", "neon-registry");

                                            if (response.ExitCode != 0)
                                            {
                                                var message = $"Error removing [neon-registry] volume from [{clonedNode.Name}: {response.ErrorText}";

                                                lock (syncLock)
                                                {
                                                    context.WriteLine(AnsibleVerbosity.Info, message);
                                                }

                                                if (response.AllText.Contains("volume is in use"))
                                                {
                                                    throw new TransientException(message);
                                                }
                                            }
                                            else
                                            {
                                                lock (context)
                                                {
                                                    context.WriteLine(AnsibleVerbosity.Trace, $"Removed [neon-registry] volume on [{clonedNode.Name}].");
                                                }
                                            }

                                            await Task.Delay(0);

                                        }).Wait();
                                }
                            });
                    }

                    NeonHelper.WaitForParallel(volumeRemoveActions);

                    // Remove the traffic manager rule and certificate.

                    context.WriteLine(AnsibleVerbosity.Trace, $"Removing the [neon-registry] traffic manager rule.");
                    hive.PublicTraffic.RemoveRule("neon-registry");
                    context.WriteLine(AnsibleVerbosity.Trace, $"Removing the [neon-registry] traffic manager certificate.");
                    hive.Certificate.Remove("neon-registry");

                    // Remove any related Consul state.

                    context.WriteLine(AnsibleVerbosity.Trace, $"Removing the [neon-registry] Consul [hostname] and [secret].");
                    hive.Registry.SetLocalHostname(null);
                    hive.Registry.SetLocalSecret(null);

                    // Logout the hive from the registry.

                    context.WriteLine(AnsibleVerbosity.Trace, $"Logging the hive out of the [{currentHostname}] registry.");
                    hive.Registry.Logout(currentHostname);

                    // Remove the hive DNS host entry.

                    context.WriteLine(AnsibleVerbosity.Trace, $"Removing the [{currentHostname}] registry DNS hosts entry.");
                    hive.Dns.Remove(hostname);
                    break;

                case "present":

                    if (!hive.Definition.HiveFS.Enabled)
                    {
                        context.WriteErrorLine("The local registry service requires hive CephFS.");
                        return;
                    }

                    // Parse the [hostname], [certificate], [username] and [password] arguments.

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [hostname]");

                    if (!context.Arguments.TryGetValue<string>("hostname", out hostname))
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

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [secret]");

                    if (!context.Arguments.TryGetValue<string>("secret", out var secret) || string.IsNullOrEmpty(secret))
                    {
                        throw new ArgumentException($"[secret] module argument is required.");
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [image]");

                    if (!context.Arguments.TryGetValue<string>("image", out var image))
                    {
                        image = HiveConst.NeonProdRegistry + "/neon-registry:latest";
                    }

                    // Detect service changes.

                    var hostnameChanged    = hostname != currentCredentials?.Registry;
                    var usernameChanged    = username != currentCredentials?.Username;
                    var passwordChanged    = password != currentCredentials?.Password;
                    var secretChanged      = secret != currentSecret;
                    var imageChanged       = image != currentImage;
                    var certificateChanged = certificate?.CombinedPemNormalized != currentCertificate?.CombinedPemNormalized;
                    var updateRequired     = hostnameChanged || 
                                             usernameChanged || 
                                             passwordChanged || 
                                             secretChanged || 
                                             imageChanged || 
                                             certificateChanged;

                    if (hostnameChanged)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[hostname] changed from [{currentCredentials?.Registry}] --> [{hostname}]");
                    }

                    if (usernameChanged)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[username] changed from [{currentCredentials?.Username}] --> [{username}]");
                    }

                    if (usernameChanged)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[password] changed from [{currentCredentials?.Password}] --> [**REDACTED**]");
                    }

                    if (secretChanged)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[secret] changed from [{currentSecret}] --> [**REDACTED**]");
                    }

                    if (imageChanged)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"[image] changed from [{currentImage}] --> [{image}]");
                    }

                    if (certificateChanged)
                    {
                        var currentCertRedacted = currentCertificate != null ? "**REDACTED**" : "**NONE**";

                        context.WriteLine(AnsibleVerbosity.Info, $"[certificate] changed from [{currentCertRedacted}] --> [**REDACTED**]");
                    }

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

                    // Create the hive DNS host entry we'll use to redirect traffic targeting the registry
                    // hostname to the hive managers.  We need to do this because registry IP addresses
                    // are typically public, typically targeting the external firewall or load balancer
                    // interface.
                    //
                    // The problem is that hive nodes will generally be unable to connect to the
                    // local managers through the firewall/load balancer because most network routers
                    // block network traffic that originates from inside the hive, then leaves
                    // to hit the external router interface with the expectation of being routed
                    // back inside.  I believe this is an anti-spoofing security measure.

                    var dnsRedirect = GetRegistryDnsEntry(hostname);

                    // Perform the operation.

                    if (currentService == null)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service needs to be created.");
                        context.Changed = true;

                        // The registry service isn't running, so we'll do a full deployment.

                        context.WriteLine(AnsibleVerbosity.Trace, $"Setting certificate.");
                        hive.Certificate.Set("neon-registry", certificate);

                        context.WriteLine(AnsibleVerbosity.Trace, $"Updating Consul settings.");
                        hive.Registry.SetLocalHostname(hostname);
                        hive.Registry.SetLocalSecret(secret);

                        context.WriteLine(AnsibleVerbosity.Trace, $"Adding hive DNS host entry for [{hostname}].");
                        hive.Dns.Set(dnsRedirect, waitUntilPropagated: true);

                        context.WriteLine(AnsibleVerbosity.Trace, $"Writing traffic manager rule.");
                        hive.PublicTraffic.SetRule(GetRegistryTrafficManagerRule(hostname));

                        context.WriteLine(AnsibleVerbosity.Trace, $"Creating the [neon-registry] service.");

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
                            context.WriteErrorLine($"[neon-registry] service create failed: {createResponse.ErrorText}");
                            return;
                        }

                        context.WriteLine(AnsibleVerbosity.Trace, $"Service created.");
                        context.WriteLine(AnsibleVerbosity.Trace, $"Wait for [neon-registry] service to stabilize (30s).");
                        Thread.Sleep(TimeSpan.FromSeconds(30));
                        context.WriteLine(AnsibleVerbosity.Trace, $"Logging the hive into the [{hostname}] registry.");
                        hive.Registry.Login(hostname, username, password);
                    }
                    else if (updateRequired)
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service update is required.");
                        context.Changed = true;

                        // Update the service and related settings as required.

                        if (certificateChanged)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating certificate.");
                            hive.Certificate.Set("neon-registry", certificate);
                        }

                        if (hostnameChanged)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating traffic manager rule.");
                            hive.PublicTraffic.SetRule(GetRegistryTrafficManagerRule(hostname));

                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating hive DNS host entry for [{hostname}] (60 seconds).");
                            hive.Dns.Set(dnsRedirect, waitUntilPropagated: true);

                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating local hive hostname [{hostname}].");
                            hive.Registry.SetLocalHostname(hostname);

                            if (!string.IsNullOrEmpty(currentHostname))
                            {
                                context.WriteLine(AnsibleVerbosity.Trace, $"Logging the hive out of the [{currentHostname}] registry.");
                                hive.Registry.Logout(currentHostname);
                            }
                        }

                        if (secretChanged)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating local hive secret.");
                            hive.Registry.SetLocalSecret(secret);
                        }

                        context.WriteLine(AnsibleVerbosity.Trace, $"Updating service.");

                        var updateResponse = manager.DockerCommand(RunOptions.None,
                            "docker service update",
                            "--env-add", $"USERNAME={username}",
                            "--env-add", $"PASSWORD={password}",
                            "--env-add", $"SECRET={secret}",
                            "--env-add", $"LOG_LEVEL=info",
                            "--env-add", $"READ_ONLY=false",
                            "--image", image,
                            "neon-registry");

                        if (updateResponse.ExitCode != 0)
                        {
                            context.WriteErrorLine($"[neon-registry] service update failed: {updateResponse.ErrorText}");
                            return;
                        }

                        context.WriteLine(AnsibleVerbosity.Trace, $"Service updated.");

                        context.WriteLine(AnsibleVerbosity.Trace, $"Logging the hive into the [{hostname}] registry.");
                        hive.Registry.Login(hostname, username, password);
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Important, $"[neon-registry] service update is not required but we're logging all nodes into [{hostname}] to ensure hive consistency.");
                        hive.Registry.Login(hostname, username, password);

                        context.Changed = false;
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
                    // completion on the manager node even if we lose the SSH connection.

                    var updateScript =
$@"#!/bin/bash
# Update [neon-registry] to READ-ONLY mode:

docker service update --env-rm READ_ONLY --env-add READ_ONLY=true neon-registry

# Prune the registry:

docker run \
   --name neon-registry-prune \
   --restart-condition=none \
   --mount type=volume,src=neon-registry,volume-driver=neon,dst=/var/lib/neon-registry \
   {HiveConst.NeonProdRegistry}/neon-registry garbage-collect

# Restore [neon-registry] to READ/WRITE mode:

docker service update --env-rm READ_ONLY --env-add READ_ONLY=false neon-registry
";
                    var bundle = new CommandBundle("./collect.sh");

                    bundle.AddFile("collect.sh", updateScript, isExecutable: true);

                    context.WriteLine(AnsibleVerbosity.Info, "Registry prune started.");

                    var pruneResponse = manager.SudoCommand(bundle, RunOptions.None);

                    if (pruneResponse.ExitCode != 0)
                    {
                        context.WriteErrorLine($"The prune operation failed.  The registry may be running in READ-ONLY mode: {pruneResponse.ErrorText}");
                        return;
                    }

                    context.WriteLine(AnsibleVerbosity.Info, "Registry prune completed.");
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present], [absent], or [prune].");
            }
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
                Name      = "neon-registry",
                System    = true,
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
                Endpoints = 
                new List<DnsEndpoint>()
                {
                    new DnsEndpoint()
                    {
                        Check   = true,
                        Target = "group=managers"
                    }
                }
            };
        }
    }
}
