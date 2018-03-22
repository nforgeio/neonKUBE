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
        // entry        see comment                         DNS entry structured as YAML.  
        //                                                  Required when [state=present]
        //
        // state        no          present     absent      indicates whether the DNS entry
        //                                      present     should be created or removed

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

            context.WriteLine(Verbosity.Trace, $"Parsing [entry]");

            if (!context.Arguments.TryGetValue<string>("entry", out var proxy) && state == "present")
            {
                throw new ArgumentException($"[entry] module argument is required when [state={state}].");
            }

            // We have the required arguments, so perform the operation.

            var hostKey = $"{NeonClusterConst.DnsConsulEntriesKey}/{hostname}";

            switch (state)
            {
                case "absent":

                    context.WriteLine(Verbosity.Trace, $"Check if DNS entry [{hostname}] exists.");

                    if (consul.KV.Exists(hostKey).Result)
                    {
                        context.WriteLine(Verbosity.Trace, $"DNS entry [{hostname}] does exist.");
                        context.WriteLine(Verbosity.Info, $"Deleting DNS entry [{hostname}].");

                        if (!context.CheckMode)
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

                    context.WriteLine(Verbosity.Trace, $"Parsing [entry]");

                    if (!context.Arguments.TryGetValue<JObject>("entry", out var entryObject))
                    {
                        throw new ArgumentException($"[entry] module argument is required.");
                    }

                    var entryText = entryObject.ToString();

                    context.WriteLine(Verbosity.Trace, "Parsing entry");

                    var newEntry = NeonHelper.JsonOrYamlDeserialize<DnsEntry>(entryText, strict: true);

                    context.WriteLine(Verbosity.Trace, "Entry parsed successfully");

                    // Use the entry hostname argument if the deserialized entry doesn't
                    // have a name.  This will make it easier on operators because 
                    // they won't need to specify the name twice.

                    if (string.IsNullOrWhiteSpace(newEntry.Hostname))
                    {
                        newEntry.Hostname = hostname;
                    }

                    // Ensure that the entry name passed as an argument and the
                    // name within the entry definition match.

                    if (!string.Equals(hostname, newEntry.Hostname, StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw new ArgumentException($"The [hostname={hostname}] argument and the entrie's [{nameof(DnsEntry.Hostname)}={newEntry.Hostname}] property are not the same.");
                    }

                    context.WriteLine(Verbosity.Trace, "Entry hostname matched.");

                    // Validate the DNS entry.

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

                    // Try reading any existing entry with this name and then determine
                    // whether the two versions of the entry are actually different. 

                    context.WriteLine(Verbosity.Trace, $"Looking for existing entry for [{hostname}]");

                    var existingEntry = consul.KV.GetObjectOrDefault<DnsEntry>(hostKey).Result;

                    if (existingEntry != null)
                    {
                        context.WriteLine(Verbosity.Trace, $"Entry exists: checking for differences.");

                        context.Changed = !NeonHelper.JsonEquals(newEntry, existingEntry);

                        if (context.Changed)
                        {
                            context.WriteLine(Verbosity.Trace, $"Entries are different.");
                        }
                        else
                        {
                            context.WriteLine(Verbosity.Info, $"Entries are the same.  No need to update.");
                        }
                    }
                    else
                    {
                        context.Changed = true;
                        context.WriteLine(Verbosity.Trace, $"Entry for [hostname={hostname}] does not exist.");
                    }

                    if (context.Changed)
                    {
                        context.WriteLine(Verbosity.Trace, $"Updating entry.");
                        consul.KV.PutObject(hostKey, newEntry).Wait();
                        context.WriteLine(Verbosity.Info, $"DNS entry updated.");
                    }

                    break;

                default:

                    throw new ArgumentException($"[state={state}] is not one of the valid choices: [absent] or [present].");
            }
        }
    }
}
