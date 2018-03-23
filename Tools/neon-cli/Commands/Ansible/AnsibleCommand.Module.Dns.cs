//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.Dns.cs
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

namespace NeonCli
{
    public partial class AnsibleCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // neon_dns:
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
        // hostname     yes                                 DNS hostname
        //
        // endpoints    see comment                         target endpoint array (see remarks)  
        //                                                  required when [state=present]
        //
        // state        no          present     absent      indicates whether the DNS entry
        //                                      present     should be created or removed
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
        // check        no          no          yes/no      Require endpoint health checks
        //
        // Examples:
        // ---------
        //
        // This simple example associates a single IP address to FOO.COM:
        //
        //      hostname: foo.com
        //      state: present
        //      endpoints:
        //        - target: 10.0.0.30
        //
        // This example enables health checks for a single address:
        //
        //      hostname: foo.com
        //      state: present
        //      endpoints:
        //        - target: 10.0.0.30
        //          check: yes
        //
        // This example associates multiple addresses, some with health
        // checks and others without:
        //
        //      hostname: foo.com
        //      state: present
        //      endpoints:
        //        - target: 10.0.0.30
        //          check: yes
        //        - target: 10.0.0.31
        //          check: no
        //        - target: 10.0.0.32
        //          check: yes
        //
        // This example simulates a CNAME record by associating WWW.GOOGLE.COM
        // with the FOO.COM.  This means DNS lookups for FOO.COM will return
        // the last IP address we retrieved for WWW.GOOGLE.COM:
        //
        //      hostname: foo.com
        //      state: present
        //      endpoints:
        //        - target: www.google.com
        //
        // This example expands the neonCLUSTER [swarm] host group so that
        // FOO.COM will resolve to the IP addresses for all cluster Swarm
        // nodes.  Checking is now enabled, so the IP addresses for all Swarm
        // nodes will be returned, regardless of their health.
        //
        //      hostname: foo.com
        //      state: present
        //      endpoints:
        //        - target: group=swarm
        //
        // This example expands the neonCLUSTER [swarm] host group so that
        // FOO.COM will resolve to the IP addresses for all cluster Swarm
        // nodes.  Checking is enabled this time, so the IP addresses for 
        // healthy Swarm nodes will be returned, regardless of their health.
        //
        //      hostname: foo.com
        //      state: present
        //      endpoints:
        //        - target: group=swarm
        //          check: yes

        /// <summary>
        /// Implements the built-in <b>neon_dns</b> module.
        /// </summary>
        /// <param name="context">The module execution context.</param>
        private void RunDnsModule(ModuleContext context)
        {
            var cluster = NeonClusterHelper.Cluster;
            var consul  = NeonClusterHelper.Consul;

            // Obtain common arguments.

            context.WriteLine(Verbosity.Trace, $"Parsing [hostname]");

            if (!context.Arguments.TryGetValue<string>("hostname", out var hostname))
            {
                throw new ArgumentException($"[hostname] module argument is required.");
            }

            if (!ClusterDefinition.DnsHostRegex.IsMatch(hostname))
            {
                throw new ArgumentException($"[hostname={hostname}] is not a valid DNS hostname.");
            }

            context.WriteLine(Verbosity.Trace, $"Parsing [state]");

            if (!context.Arguments.TryGetValue<string>("state", out var state))
            {
                state = "present";
            }

            state = state.ToLowerInvariant();

            context.WriteLine(Verbosity.Trace, $"Parsing [endpoints]");

            if (!context.Arguments.TryGetValue<JToken>("endpoints", out var endpointsToken) && state == "present")
            {
                throw new ArgumentException($"[endpoints] module argument is required when [state={state}].");
            }

            // We have the required arguments, so perform the operation.

            var hostKey = $"{NeonClusterConst.ConsulDnsEntriesKey}/{hostname}";

            switch (state)
            {
                case "absent":

                    context.WriteLine(Verbosity.Trace, $"Check if DNS entry [{hostname}] exists.");

                    if (consul.KV.Exists(hostKey).Result)
                    {
                        context.WriteLine(Verbosity.Trace, $"DNS entry [{hostname}] does exist.");
                        context.WriteLine(Verbosity.Info, $"Deleting DNS entry [{hostname}].");

                        if (context.CheckMode)
                        {
                            context.WriteLine(Verbosity.Info, $"DNS entry [{hostname}] will be deleted when CHECKMODE is disabled.");
                        }
                        else
                        {
                            consul.KV.Delete(hostKey);
                            context.WriteLine(Verbosity.Trace, $"DNS entry [{hostname}] deleted.");
                        }

                        context.Changed = true;
                    }
                    else
                    {
                        context.WriteLine(Verbosity.Trace, $"DNS entry [{hostname}] does not exist.");
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

                    context.WriteLine(Verbosity.Trace, $"[{endpoints.Count}] endpoints parsed");

                    // Construct the new entry.

                    var newEntry = new DnsEntry()
                    {
                        Hostname  = hostname,
                        Endpoints = endpoints
                    };

                    // Validate the new DNS entry.

                    context.WriteLine(Verbosity.Trace, "Validating DNS entry.");

                    var errors = newEntry.Validate(cluster.Definition, cluster.Definition.GetNodeGroups(excludeAllGroup: true));

                    if (errors.Count > 0)
                    {
                        context.WriteLine(Verbosity.Trace, $"[{errors.Count}] DNS entry validation errors.");

                        foreach (var error in errors)
                        {
                            context.WriteLine(Verbosity.Important, error);
                            context.WriteErrorLine(error);
                        }

                        context.Failed = true;
                        return;
                    }

                    context.WriteLine(Verbosity.Trace, "DNS entry is valid.");

                    // Try reading an existing entry with this name and then determine
                    // whether the two versions of the entry are actually different. 

                    context.WriteLine(Verbosity.Trace, $"Look up existing DNS entry for [{hostname}]");

                    var existingEntry = consul.KV.GetObjectOrDefault<DnsEntry>(hostKey).Result;

                    if (existingEntry != null)
                    {
                        context.WriteLine(Verbosity.Trace, $"DNS entry exists: checking for differences.");

                        context.Changed = !NeonHelper.JsonEquals(newEntry, existingEntry);

                        if (context.Changed)
                        {
                            context.WriteLine(Verbosity.Trace, $"DNS entries are different.");
                        }
                        else
                        {
                            context.WriteLine(Verbosity.Info, $"DNS entries are the same.  No need to update.");
                        }
                    }
                    else
                    {
                        context.Changed = true;
                        context.WriteLine(Verbosity.Trace, $"DNS entry for [hostname={hostname}] does not exist.");
                    }

                    if (context.Changed)
                    {
                        if (context.CheckMode)
                        {
                            context.WriteLine(Verbosity.Info, $"DNS entry [{hostname}] will be updated when CHECKMODE is disabled.");
                        }
                        else
                        {
                            context.WriteLine(Verbosity.Trace, $"Updating DNS entry.");
                            consul.KV.PutObject(hostKey, newEntry).Wait();
                            context.WriteLine(Verbosity.Info, $"DNS entry updated.");
                        }
                    }

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
            }
        }
    }
}
