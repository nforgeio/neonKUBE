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

using Neon.Cluster;
using Neon.Cryptography;
using Neon.Common;
using Neon.IO;
using Neon.Net;

// $todo(jeff.lill): This needs to be implemented sometime.

namespace NeonCli.Ansible
{
    /// <summary>
    /// Implements the <b>neon_docker_secret</b> Ansible module.
    /// </summary>
    public class DockerSecretModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_dns:
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
        //      1. All cluster secrets are listed so we'll be able to determine
        //         the latest version of MY-SECRET.
        //
        //      2. All cluster services are inspected to discover any services that
        //         reference MY-SECRET or any version the secret like: MY-SECRET-#.
        //
        //      3. Each of these discovered services will be updated to pickup
        //         that latest version of the secret.  [update_parallism] controls
        //         how many replicas are updated in parallel.

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var cluster = NeonClusterHelper.Cluster;
            var consul  = NeonClusterHelper.Consul;

            // Obtain common arguments.

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [hostname]");

            if (!context.Arguments.TryGetValue<string>("hostname", out var hostname))
            {
                throw new ArgumentException($"[hostname] module argument is required.");
            }

            if (!ClusterDefinition.DnsHostRegex.IsMatch(hostname))
            {
                throw new ArgumentException($"[hostname={hostname}] is not a valid DNS hostname.");
            }

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [endpoints]");

            if (!context.Arguments.TryGetValue<JToken>("endpoints", out var endpointsToken) && state == "present")
            {
                throw new ArgumentException($"[endpoints] module argument is required when [state={state}].");
            }

            if (context.HasErrors)
            {
                return;
            }
            
            // We have the required arguments, so perform the operation.

            var hostKey = $"{NeonClusterConst.ConsulDnsEntriesKey}/{hostname}";

            switch (state)
            {
                case "absent":

                    context.WriteLine(AnsibleVerbosity.Trace, $"Check if DNS entry [{hostname}] exists.");

                    if (consul.KV.Exists(hostKey).Result)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"DNS entry [{hostname}] does exist.");
                        context.WriteLine(AnsibleVerbosity.Info, $"Deleting DNS entry [{hostname}].");

                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"DNS entry [{hostname}] would be deleted when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            consul.KV.Delete(hostKey);
                            context.WriteLine(AnsibleVerbosity.Trace, $"DNS entry [{hostname}] deleted.");
                        }

                        context.Changed = true;
                    }
                    else
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"DNS entry [{hostname}] does not exist.");
                    }
                    break;

                case "present":

                    var endpointsArray = endpointsToken as JArray;

                    if (endpointsArray == null)
                    {
                        throw new ArgumentException($"[endpoints] module argument must be an array.");
                    }

                    var endpoints = new List<DnsEndpoint>();

                    foreach (var item in endpointsArray)
                    {
                        endpoints.Add(item.ToObject<DnsEndpoint>());
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"[{endpoints.Count}] endpoints parsed");

                    // Construct the new entry.

                    var newEntry = new DnsEntry()
                    {
                        Hostname  = hostname,
                        Endpoints = endpoints
                    };

                    // Validate the new DNS entry.

                    context.WriteLine(AnsibleVerbosity.Trace, "Validating DNS entry.");

                    var errors = newEntry.Validate(cluster.Definition, cluster.Definition.GetNodeGroups(excludeAllGroup: true));

                    if (errors.Count > 0)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"[{errors.Count}] DNS entry validation errors.");

                        foreach (var error in errors)
                        {
                            context.WriteLine(AnsibleVerbosity.Important, error);
                            context.WriteErrorLine(error);
                        }

                        context.Failed = true;
                        return;
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, "DNS entry is valid.");

                    // Try reading an existing entry with this name and then determine
                    // whether the two versions of the entry are actually different. 

                    context.WriteLine(AnsibleVerbosity.Trace, $"Look up existing DNS entry for [{hostname}]");

                    var existingEntry = consul.KV.GetObjectOrDefault<DnsEntry>(hostKey).Result;

                    if (existingEntry != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"DNS entry exists: checking for differences.");

                        context.Changed = !NeonHelper.JsonEquals(newEntry, existingEntry);

                        if (context.Changed)
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"DNS entries are different.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"DNS entries are the same.  No need to update.");
                        }
                    }
                    else
                    {
                        context.Changed = true;
                        context.WriteLine(AnsibleVerbosity.Trace, $"DNS entry for [hostname={hostname}] does not exist.");
                    }

                    if (context.Changed)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"DNS entry [{hostname}] would be updated when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating DNS entry.");
                            consul.KV.PutObject(hostKey, newEntry).Wait();
                            context.WriteLine(AnsibleVerbosity.Info, $"DNS entry updated.");
                        }
                    }

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
            }
        }
    }
}
