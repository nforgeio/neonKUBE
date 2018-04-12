//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.Query.cs
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
using Neon.Data;
using Neon.IO;
using Neon.Net;

namespace NeonCli
{
    public partial class AnsibleCommand : CommandBase
    {
        //---------------------------------------------------------------------
        // neon_couchbase_query:
        //
        // Synopsis:
        // ---------
        //
        // Executes 
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
        // servers      yes                                 array specifying one or more target
        //                                                  Couchbase servers.  Each entry can be 
        //                                                  an IP address, a FQDN, cluster node
        //                                                  name or cluster node group name.
        //
        // port         no          8091                    Couchbase server port
        //                          18902 (for SSL)
        //
        // ssl          no          no          yes         use SSL to secure the connections.
        //                                      no
        //
        // bucket       yes                                 identifies the target bucket
        //
        // username     yes                                 identifies the Couchbase user
        //
        // password     yes                                 specifies the Couchbase password
        //
        // query        yes                                 specifies the Couchbase nickel query
        //
        // Remarks:
        // --------
        //
        // This module simply connects to the Couchbase server and submits the query.
        //
        // Examples:
        // ---------
        //

        /// <summary>
        /// Implements the built-in <b>neon_couchbase_query</b> module.
        /// </summary>
        /// <param name="context">The module execution context.</param>
        private void RunCouchbaseQueryModule(ModuleContext context)
        {
            var cluster    = NeonClusterHelper.Cluster;
            var nodeGroups = cluster.Definition.GetNodeGroups(excludeAllGroup: true);
            var settings   = new CouchbaseSettings();

            //-----------------------------------------------------------------
            // Parse the module arguments.

            context.WriteLine(AnsibleVerbosity.Trace, $"Parsing [servers]");

            var servers = context.ParseStringArray("servers");

            if (servers.Count == 0)
            {
                throw new ArgumentException($"[servers] must specify at least one server.");
            }

            var ssl = context.ParseBool("ssl");

            if (!ssl.HasValue)
            {
                ssl = false;
            }

            var scheme = ssl.Value ? "https" : "http";

            var port = context.ParseInt("port", v => 0 < v && v <= ushort.MaxValue);

            foreach (var server in servers)
            {
                // The server can be an IP address, FQDN (with at least one dot), a cluster
                // node name or a cluster node group name.

                if (IPAddress.TryParse(server, out var address))
                {
                    settings.Servers.Add(new Uri($"{scheme}://{address}:{port}"));
                }
                else if (server.Contains("."))
                {
                    // Must be a FQDN

                    settings.Servers.Add(new Uri($"{scheme}://{server}:{port}"));
                }
                else
                {
                    if (nodeGroups.TryGetValue(server, out var group))
                    {
                        // It's a node group so add a URL with the IP address for each
                        // group node.

                        foreach (var node in group)
                        {
                            settings.Servers.Add(new Uri($"{scheme}://{node.PrivateAddress}:{port}"));
                        }
                    }
                    else
                    {
                        // Must be a node name.

                        if (cluster.Definition.NodeDefinitions.TryGetValue(server, out var node))
                        {
                            settings.Servers.Add(new Uri($"{scheme}://{node.PrivateAddress}:{port}"));
                        }
                        else
                        {
                            context.WriteErrorLine($"[{server}] is not a valid IP address, FQDN, or known cluster node or node group name.");
                            return;
                        }
                    }
                }
            }

            var bucket   = context.ParseString("bucket", v => !string.IsNullOrWhiteSpace(v));
            var username = context.ParseString("username");
            var password = context.ParseString("password");
            var query    = context.ParseString("query", q => !string.IsNullOrWhiteSpace(q));

            //-----------------------------------------------------------------
            // Execute the query.


        }
    }
}
