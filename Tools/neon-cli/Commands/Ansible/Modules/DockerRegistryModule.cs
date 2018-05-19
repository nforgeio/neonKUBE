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
        //                                      collect     garbage collects unreferenced layers
        //
        // hostname     see comment                         registry public DNS name
        //                                                  required if [state=present]
        //
        // certificate  see comment                         registry TLS certificate
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
        // This has three requiments:
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
        //      collect         - Temporarily removes the registry service while
        //                        single instance is started in garbage collection
        //                        mode.  The garbage collection instance will handle
        //                        read-only registry requests while it's removing
        //                        unreferenced layers.  The fully functional registry
        //                        service will be recreated once garbage collection
        //                        has completed.
        //
        // This module uses the following reserved names:
        //
        //      neon-registry   - registry service
        //      neon-registry   - service load balancer rule
        //      neon-registry   - registry certificate
        //
        // Examples:
        // ---------
        //
        // This example logs into a custom registry.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: login
        //        neon_docker_login:
        //          state: present
        //          registry: registry.test.com
        //          username: billy
        //          password: bob
        //
        // This example log out of the custom registry:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: logout
        //        neon_docker_login:
        //          state: absent
        //          registry: registry.test.com

        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "state",
            "registry",
            "username",
            "password"
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

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [registry]");

            if (!context.Arguments.TryGetValue<string>("registry", out var registry))
            {
                throw new ArgumentException($"[registry] module argument is required.");
            }

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

            context.WriteLine(AnsibleVerbosity.Trace, $"Reading existing credentials for [{registry}].");

            var existingCredentials = cluster.GetRegistryCredential(registry);

            if (existingCredentials != null)
            {
                context.WriteLine(AnsibleVerbosity.Info, $"Credentials for [{registry}] exist.");
            }
            else
            {
                context.WriteLine(AnsibleVerbosity.Info, $"Credentials for [{registry}] do not exist.");
            }

            var sbErrorNodes = new StringBuilder();

            switch (state)
            {
                case "absent":

                    if (context.CheckMode)
                    {
                        if (existingCredentials != null)
                        {
                            context.WriteLine(AnsibleVerbosity.Important, $"Credentials for [{registry}] will be deleted when CHECK-MODE is disabled.");
                        }

                        return;
                    }

                    // Remove the registry credentials from Vault if present and then
                    // have all nodes logout, ignoring any errors to be sure they're
                    // all logged out.

                    if (existingCredentials != null)
                    {
                        context.Changed = true;
                        context.WriteLine(AnsibleVerbosity.Trace, $"Removing credentials for [{registry}].");
                        cluster.RemoveRegistryCredential(registry);
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"Logging all cluster nodes out of [{registry}].");

                    var logoutActions = new List<Action>();

                    foreach (var node in cluster.Nodes)
                    {
                        logoutActions.Add(
                            () =>
                            {
                                if (!node.RegistryLogout(registry))
                                {
                                    lock (sbErrorNodes)
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
                    break;

                case "present":

                    if (context.CheckMode)
                    {
                        if (existingCredentials == null)
                        {
                            context.WriteLine(AnsibleVerbosity.Important, $"Credentials for [{registry}] will be added when CHECK-MODE is disabled.");
                        }

                        return;
                    }

                    // Parse the [username] and [password] credentials.

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

                    context.WriteLine(AnsibleVerbosity.Trace, $"Saving credentials for [{registry}].");

                    cluster.SetRegistryCredential(registry, username, password);

                    // Log all of the nodes in with the new registry credentials.
                    //
                    // Note that we won't do this if the registry cache is enabled and we're 
                    // updating credentials for the Docker public registry because for this 
                    // configuration, only the registry cache needs the upstream credentials.
                    // The nodes don't authenticate against the local registry cache.

                    if (!cluster.Definition.Docker.RegistryCache || !NeonClusterHelper.IsDockerPublicRegistry(registry))
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Logging all cluster nodes into [{registry}].");

                        var loginActions = new List<Action>();

                        foreach (var node in cluster.Nodes)
                        {
                            loginActions.Add(
                                () =>
                                {
                                    if (!node.RegistryLogin(registry, username, password))
                                    {
                                        lock (sbErrorNodes)
                                        {
                                            sbErrorNodes.AppendWithSeparator(node.Name, ", ");
                                        }
                                    }
                                });
                        }

                        NeonHelper.WaitForParallel(loginActions);

                        if (sbErrorNodes.Length == 0)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"All cluster nodes were updated.");
                        }
                        else
                        {
                            context.WriteErrorLine($"These nodes could not be updated: {sbErrorNodes}");
                            context.WriteErrorLine($"The cluster may be in an inconsistent state.");
                        }
                    }
                    else
                    {
                        // Restart the cluster registry cache containers with the new credentials.

                        context.WriteLine(AnsibleVerbosity.Trace, $"Restarting the cluster registry caches.");

                        if (!cluster.RestartRegistryCaches(registry, username, password))
                        {
                            context.WriteErrorLine("Unable to restart one or more of the cluster registry caches.");
                            return;
                        }

                        context.WriteLine(AnsibleVerbosity.Trace, $"Cluster registry caches restarted.");
                    }

                    context.Changed = existingCredentials == null;
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present] or [absent].");
            }
        }
    }
}
