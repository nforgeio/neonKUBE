//-----------------------------------------------------------------------------
// FILE:	    HostsModule.cs
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
    /// Implements the <b>neon_hosts</b> Ansible module.
    /// </summary>
    public class HostsModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_hosts:
        //
        // Synopsis:
        // ---------
        //
        // Manages neonCLUSTER DNS host entries.
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
        // state        no          present     present     indicates whether the DNS entry
        //                                      absent      should be created or removed
        //
        // hostname     yes                                 DNS hostname
        //
        // endpoints    see comment                         target endpoint array (see remarks)  
        //                                                  required when [state=present]
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
        // The endpoints array is required when [state=present].  This describes
        // the targets to be resolved for the hostname.  Each array element
        // specifies the following fields:
        //
        // field        required    default     choices     comments
        // --------------------------------------------------------------------
        //
        // target       yes                     IPADDRESS   IP address like: 10.0.0.55
        //                                      HOSTNAME    CNAME like host: www.google.com
        //                                      GROUP       Host group like: group=managers
        //
        // system       no          no          yes/no      Indicates a built-in system entry
        //
        // check        no          no          yes/no      Require endpoint health checks
        //
        //
        // NOTE: DNS hostnames prefixed by "(neon)-" identify built-in 
        //       system DNS entries used to resolve things like the local 
        //       Docker registry service [neon-registry] if deployed.  You
        //       should leave the system entries alone unless you really 
        //       know what you're doing.
        //
        // Examples:
        // ---------
        //
        // This simple example associates a single IP address to FOO.COM:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: DNS task
        //        neon_hosts:
        //          hostname: foo.com
        //          state: present
        //          endpoints:
        //            - target: 10.0.0.30
        //
        // This example enables health checks for a single address:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: DNS task
        //        neon_hosts:
        //          hostname: foo.com
        //          state: present
        //          endpoints:
        //            - target: 10.0.0.30
        //              check: yes
        //
        // This example associates multiple addresses, some with health
        // checks and others without:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: DNS task
        //        neon_hosts:
        //          hostname: foo.com
        //          state: present
        //          endpoints:
        //            - target: 10.0.0.30
        //              check: yes
        //            - target: 10.0.0.31
        //              check: no
        //            - target: 10.0.0.32
        //              check: yes
        //
        // This example simulates a CNAME record by associating WWW.GOOGLE.COM
        // with the FOO.COM.  This means DNS lookups for FOO.COM will return
        // the last IP address we retrieved for WWW.GOOGLE.COM:
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: DNS task
        //        neon_hosts:
        //          hostname: foo.com
        //          state: present
        //          endpoints:
        //            - target: www.google.com
        //
        // This example expands the neonCLUSTER [swarm] host group so that
        // FOO.COM will resolve to the IP addresses for all cluster Swarm
        // nodes.  Checking is now enabled, so the IP addresses for all Swarm
        // nodes will be returned, regardless of their health.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: DNS task
        //        neon_hosts:
        //          hostname: foo.com
        //          state: present
        //          endpoints:
        //            - target: group=swarm
        //
        // This example expands the neonCLUSTER [swarm] host group so that
        // FOO.COM will resolve to the IP addresses for all cluster Swarm
        // nodes.  Checking is enabled this time, so the IP addresses for 
        // healthy Swarm nodes will be returned, regardless of their health.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: DNS task
        //        neon_hosts:
        //          hostname: foo.com
        //          state: present
        //          endpoints:
        //            - target: group=swarm
        //              check: yes

        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "state",
            "hostname",
            "system",
            "endpoints"
        };

        private HashSet<string> endpointArgs = new HashSet<string>()
        {
            "target",
            "check"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var cluster = NeonClusterHelper.Cluster;
            var consul  = NeonClusterHelper.Consul;

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

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

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [system]");

            var system = context.ParseBool("system") ?? false;

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
                            context.WriteLine(AnsibleVerbosity.Info, $"DNS entry [{hostname}] will be deleted when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            consul.KV.Delete(hostKey);
                            context.WriteLine(AnsibleVerbosity.Trace, $"DNS entry [{hostname}] deleted.");
                        }

                        context.Changed = !context.CheckMode;
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
                        var endpointJObject = item as JObject;

                        if (endpointJObject == null)
                        {
                            context.WriteErrorLine("One or more [endpoints] are invalid.");
                            context.Failed = true;
                            return;
                        }

                        if (!context.ValidateArguments(endpointJObject, endpointArgs, "endpoints"))
                        {
                            context.Failed = true;
                            return;
                        }

                        endpoints.Add(item.ToObject<DnsEndpoint>());
                    }

                    context.WriteLine(AnsibleVerbosity.Trace, $"[{endpoints.Count}] endpoints parsed");

                    // Construct the new entry.

                    var newEntry = new DnsEntry()
                    {
                        Hostname  = hostname,
                        IsSystem  = system,
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
                    var changed       = false;

                    if (existingEntry != null)
                    {
                        context.WriteLine(AnsibleVerbosity.Trace, $"DNS entry exists: checking for differences.");

                        changed = !NeonHelper.JsonEquals(newEntry, existingEntry);

                        if (changed)
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
                        changed = true;
                        context.WriteLine(AnsibleVerbosity.Trace, $"DNS entry for [hostname={hostname}] does not exist.");
                    }

                    if (changed)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(AnsibleVerbosity.Info, $"DNS entry [{hostname}] will be updated when CHECK-MODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(AnsibleVerbosity.Trace, $"Updating DNS entry.");
                            consul.KV.PutObject(hostKey, newEntry).Wait();
                            context.WriteLine(AnsibleVerbosity.Info, $"DNS entry updated.");
                        }

                        context.Changed = !context.CheckMode;
                    }

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [present] or [absent].");
            }
        }
    }
}
