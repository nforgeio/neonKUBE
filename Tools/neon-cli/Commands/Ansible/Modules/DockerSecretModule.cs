//-----------------------------------------------------------------------------
// FILE:	    DockerSecretModule.cs
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

namespace NeonCli.Ansible
{
    /// <summary>
    /// Implements the <b>neon_docker_secret</b> Ansible module.
    /// </summary>
    public class DockerSecretModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_docker_secret:
        //
        // Synopsis:
        // ---------
        //
        // Manages Docker secrets.
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
        // state        no          present     present     indicates whether the secret 
        //                                      absent      should be created or removed
        //
        // name         yes                                 the secret name
        //
        // bytes        see comment                         base-64 encoded binary secret
        //                                                  data.  One of [bytes] or [text]
        //                                                  must be present if state=present
        //
        // text         see comment                         secret text.  One of [bytes] or [text]
        //                                                  must be present if state=present
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
        // This module simply adds or removes a named Docker secret.  Note that you cannot
        // add or remove secrets if they are already referenced by a Docker service.
        //
        // IMPORTANT: It not currently possible to update an existing secret.
        //
        // Examples:
        // ---------
        //
        // This example adds a textual secret:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: create secret
        //        neon_docker_secret:
        //          name: my-secret
        //          state: present
        //          text: password
        //
        // This example adds a binary secret:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: create secret
        //        neon_docker_secret:
        //          name: my-secret
        //          state: present
        //          bytes: cGFzc3dvcmQ=
        //
        // This example removes a secret:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: remove secret
        //        neon_docker_secret:
        //          name: my-secret
        //          state: absent

        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "state",
            "name",
            "bytes",
            "text"
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

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [name]");

            if (!context.Arguments.TryGetValue<string>("name", out var secretName))
            {
                throw new ArgumentException($"[name] module argument is required.");
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [text]");

            context.Arguments.TryGetValue<string>("text", out var secretText);
            context.Arguments.TryGetValue<string>("bytes", out var secretBytes);

            if (context.HasErrors)
            {
                return;
            }

            // We have the required arguments, so perform the operation.

            context.WriteLine(AnsibleVerbosity.Trace, $"Inspecting [{secretName}] secret.");

            var manager = hive.GetReachableManager();
            var exists  = hive.Docker.Secret.Exists(secretName);
            var bytes   = (byte[])null;

            if (exists)
            {
                context.WriteLine(AnsibleVerbosity.Trace, $"{secretName}] secret exists.");
            }
            else
            {
                context.WriteLine(AnsibleVerbosity.Trace, $"[{secretName}] secret does not exist.");
            }

            switch (state)
            {
                case "absent":

                    if (exists)
                    {
                        context.Changed = !context.CheckMode;

                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Secret [{secretName}] will be removed when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.Changed = true;
                            context.WriteLine(AnsibleVerbosity.Trace, $"Removing secret [{secretName}].");

                            hive.Docker.Secret.Remove(secretName);
                        }
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"Secret [{secretName}] does not exist.");
                    }
                    break;

                case "present":

                    if (secretText == null && secretBytes == null)
                    {
                        context.WriteErrorLine("One of the [text] or [bytes] module parameters is required.");
                        return;
                    }
                    else if (secretText != null && secretBytes != null)
                    {
                        context.WriteErrorLine("Only one of [text] or [bytes] can be specified.");
                        return;
                    }

                    if (secretBytes != null)
                    {
                        try
                        {
                            bytes = Convert.FromBase64String(secretBytes);
                        }
                        catch
                        {
                            context.WriteErrorLine("[bytes] is not a valid base-64 encoded value.");
                            return;
                        }
                    }

                    if (exists)
                    {
                        context.WriteLine(AnsibleVerbosity.Info, $"Secret [{secretName}] already exists.");
                    }
                    else
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"Secret [{secretName}] will be created when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.Changed = true;
                            context.WriteLine(AnsibleVerbosity.Trace, $"Creating secret [{secretName}].");

                            if (bytes != null)
                            {
                                hive.Docker.Secret.Set(secretName, bytes);
                            }
                            else
                            {
                                hive.Docker.Secret.Set(secretName, secretText);
                            }
                        }
                    }
                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present] or [absent].");
            }
        }
    }
}

//-----------------------------------------------------------------------------
// Here's some of the ideas for when we implement advanced
// secret/config handling.  Here's the tracking issue:
//
//      https://github.com/jefflill/NeonForge/issues/231

//---------------------------------------------------------------------
// neon_secret:
//
// Synopsis:
// ---------
//
// Manages Docker secrets including implementing advanced secret roll-over
// behavors.
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
// state        no          present     present     indicates whether the secret 
//                                      absent      should be created, removed,
//                                      update      or updated if it exists
//
// name         yes                                 the secret name
//
// bytes        see comment                         base-64 encoded binary secret
//                                                  data.  One of [bytes] or [text]
//                                                  must be present if state=present
//
// text         see comment                         secret text.  One of [bytes] or [text]
//                                                  must be present if state=present
//
// no_rotate    no          no          yes         disable secret rotation (see remarks)
//                                      no
//
// update_services no       no          yes         automatically update any services 
//                                      no          referencing the secret when rotation
//                                                  is enabled
//
// update_parallism no      1                       specifies how many replicas of a
//                                                  service will be updated in parallel
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
// This module is used to manage Docker secrets.  Secrets can be specified as UTF-8
// encoded text or as base-64 encoded binary data.
//
// Secret Rotation:
//
// Docker secrets are somewhat cumbersome to use by default.  Here's how I believe
// the typical user wishes secrets would work:
//
//      1. Create a secret named MY-SECRET
//      2. Deploy a service that uses MY-SECRET
//      3. Sometime later, update MY-SECRET with a new value
//      4. Update the service to pick up the new secret
//
// Unfortunately, this fails at step #3.  Docker doesn't include update secret
// functionality so you need to remove and then recreate the secret and Docker
// prevents a secret from being removed if it's referenced by any services.
// One workaround would be to remove the service, remove/recreate the secret,
// and then recreate the service.  The problem with this is that the service
// will be unavailable during this time.  It would be much better to be able
// to keep the service running and update it in-place.
//
// This module and the [neon secret ...] commands implement a secret naming
// convention so that secrets and services can be updated in place.
//
// The convention is to automatically append a version number onto the end of
// the secret name persisted to Docker like MY-SECRET-0, MY-SECRET-1,...
// This version number is incremented automatically by this module and the
// [neon secret put MY-SECRET ...] command.  Under the covers, a new Docker
// secret will be created with an incremented version number.  Doing this
// avoids the "can't change a secret when referenced" Docker restriction
// encountered at step #3 above.
//
// The next problem is updating any services that reference the secret
// so that they use the latest version.  This module will handle this
// if [update_services=yes].  Here's how this works:
//
//      1. All hive secrets are listed so we'll be able to determine
//         the latest version of MY-SECRET.
//
//      2. All hive services are inspected to discover any services that
//         reference MY-SECRET or any version the secret like: MY-SECRET-#.
//
//      3. Each of these discovered services will be updated to pickup
//         that latest version of the secret.  [update_parallism] controls
//         how many replicas are updated in parallel.
