//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.Couchbase.Query.cs
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
using Couchbase;
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
        //                                                  Couchbase servers.  Each element can 
        //                                                  be an IP address, a FQDN, cluster
        //                                                  node name or cluster node group name
        //
        // port         no          8091                    Couchbase server port
        //                          18902 (for SSL)
        //
        // ssl          no          no          yes         use SSL to secure the connections
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
        // limit        no          0                       specifies the maximum number of documents
        //                                                  to be returned (0 for unlimited)
        //
        // format       no          json-lines  json-array  write output as a JSON array
        //                                      json-lines  write output as one JSON object per line
        //
        // output       no                                  optionally write the documents to
        //                                                  this file (UTF-8) rather then to
        //                                                  the Ansible module output
        //                                      
        // Remarks:
        // --------
        //
        // This module simply connects to the Couchbase server and submits the query.  Results
        // will be returned as a JSON array of documents.
        //
        // Examples:
        // ---------
        //

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Handles serialization of query results.
        /// </summary>
        private sealed class CouchbaseQueryResultWriter : IDisposable
        {
            private ModuleContext       context;
            private CouchbaseFileFormat format;
            private TextWriter          writer;

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="format">Specifies the output format.</param>
            /// <param name="path">Optional output file path.</param>
            public CouchbaseQueryResultWriter(ModuleContext context, CouchbaseFileFormat format, string path = null)
            {
                this.context = context;
                this.format  = format;

                if (!string.IsNullOrEmpty(path))
                {
                    writer = new StreamWriter(path, append: false, encoding: Encoding.UTF8);
                }

                if (format == CouchbaseFileFormat.JsonArray)
                {
                    WriteLine("[");
                }
            }

            /// <summary>
            /// Writes a line of text to the output.
            /// </summary>
            /// <param name="line">The text.</param>
            private void WriteLine(string line)
            {
                if (writer != null)
                {
                    writer.WriteLine(line);
                }
                else
                {
                    context.WriteLine(AnsibleVerbosity.Important, line);
                }
            }

            /// <summary>
            /// Writes a document to the output.
            /// </summary>
            /// <param name="document">The document.</param>
            /// <param name="isLast">Indicates whether this is the last document.</param>
            public void WriteDocument(JObject document, bool isLast)
            {
                var json = document.ToString(Formatting.None);

                if (format == CouchbaseFileFormat.JsonArray)
                {
                    if (isLast)
                    {
                        WriteLine(json);
                    }
                    else
                    {
                        WriteLine($"{json},");
                    }
                }
                else
                {
                    WriteLine(json);
                }
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                if (format == CouchbaseFileFormat.JsonArray)
                {
                    WriteLine("]");
                }

                writer.Dispose();
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Implements the built-in <b>neon_couchbase_query</b> module.
        /// </summary>
        /// <param name="context">The module context.</param>
        private void RunCouchbaseQueryModule(ModuleContext context)
        {
            var cluster       = NeonClusterHelper.Cluster;
            var nodeGroups    = cluster.Definition.GetNodeGroups(excludeAllGroup: true);

            //-----------------------------------------------------------------
            // Parse the module arguments.

            var couchbaseArgs = ParseCouchbaseSettings(context);

            if (couchbaseArgs == null)
            {
                return;
            }

            var query = context.ParseString("query", q => !string.IsNullOrWhiteSpace(q));

            if (context.HasErrors)
            {
                return;
            }

            var format = context.ParseEnum<CouchbaseFileFormat>("format");

            if (!format.HasValue)
            {
                format = default(CouchbaseFileFormat);
            }

            var limit = context.ParseLong("limit", v => v >= 0);

            if (!limit.HasValue || limit.Value == 0)
            {
                limit = long.MaxValue;
            }

            var output = context.ParseString("output");

            //-----------------------------------------------------------------
            // Execute the query.

            var bucket  = couchbaseArgs.Settings.OpenBucket(couchbaseArgs.Credentials);
            var results = bucket.QuerySafeAsync<JObject>(query).Result;
            var count   = Math.Min(results.Count, limit.Value);

            using (var writer = new CouchbaseQueryResultWriter(context, format.Value, output))
            {
                for (int i = 0; i < count; i++)
                {
                    var document = results[i];
                    var isLast   = i == count - 1;

                    writer.WriteDocument(document, isLast);
                }
            }
        }
    }
}
