//-----------------------------------------------------------------------------
// FILE:	    DockerLoginModule.cs
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

namespace NeonCli.Ansible
{
    /// <summary>
    /// Implements the <b>neon_docker_login</b> Ansible module.
    /// </summary>
    public class DockerLoginModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_docker_login:
        //
        // Synopsis:
        // ---------
        //
        // Manages Docker registry credentials.
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
        // state        no          present     present     indicates the hive should be
        //                                      absent      logged into or out of a Docker
        // name         yes                                 registry.
        //
        // registry     yes                                 the registry hostname
        //                                                  (e.g. docker.io)
        //
        // username     see comment                         required if [state=present]
        //
        // password     see comment                         required if [state=present]
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
        // This module is used to have a neonHIVE log into or out from a Docker registry.
        // All hive nodes including managers, workers, and pets, will be logged in or out
        // and registry credentials will be persisted to the hive Vault so they will
        // be available if new nodes are added to the hive at a later time.
        //
        // Note that a hive may be logged into multiple registries at any given time.
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
        // This example logs out of the custom registry:
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
            var hive = HiveHelper.Hive;

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

            var existingCredentials = hive.Registry.GetCredentials(registry);

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

                    // Log the hive out of the registry.

                    if (existingCredentials != null)
                    {
                        context.Changed = true;
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"Logging the hive out of the [{registry}] registry.");
                    hive.Registry.Logout(registry);
                    context.WriteLine(AnsibleVerbosity.Trace, $"All hive nodes are logged out.");
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

                    context.WriteLine(AnsibleVerbosity.Trace, $"Logging the hive into the [{registry}] registry.");
                    hive.Registry.Login(registry, username, password);

                    // Log all of the nodes in with the new registry credentials.
                    //
                    // Note that we won't do this if the registry cache is enabled and we're 
                    // updating credentials for the Docker public registry because for this 
                    // configuration, only the registry cache needs the upstream credentials.
                    // The nodes don't authenticate against the local registry cache.

                    if (!hive.Definition.Docker.RegistryCache || !HiveHelper.IsDockerPublicRegistry(registry))
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"Logging the hive into the [{registry}] registry.");
                        hive.Registry.Login(registry, username, password);
                    }
                    else
                    {
                        // Restart the hive registry cache containers with the new credentials.

                        context.WriteLine(AnsibleVerbosity.Trace, $"Restarting the hive registry caches.");

                        if (!hive.Registry.RestartCache(registry, username, password))
                        {
                            context.WriteErrorLine("Unable to restart one or more of the hive registry caches.");
                            return;
                        }

                        context.WriteLine(AnsibleVerbosity.Trace, $"Hive registry caches restarted.");
                    }

                    context.Changed = existingCredentials == null;
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present] or [absent].");
            }
        }
    }
}
