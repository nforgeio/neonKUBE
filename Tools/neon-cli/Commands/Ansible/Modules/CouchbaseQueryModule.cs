//-----------------------------------------------------------------------------
// FILE:	    CouchbaseQueryModule.cs
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
using Couchbase.N1QL;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

using Neon.Cryptography;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Hive;
using Neon.Net;

using NeonCli.Ansible.Couchbase;

namespace NeonCli.Ansible
{
    /// <summary>
    /// Implements the <b>neon_couchbase_query</b> Ansible module.
    /// </summary>
    public class CouchbaseQueryModule : IAnsibleModule
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
        //                                                  be an IP address, a FQDN, hive
        //                                                  node name or hive node group name
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
        // query        yes                                 specifies the Couchbase N1QL query
        //
        // limit        no          0                       specifies the maximum number of documents
        //                                                  to be returned (0 for unlimited)
        //
        // format       no          json-lines  json-array  write output as a JSON array
        //                                      json-lines  write output as one JSON object per line
        //
        // output       no                                  optionally write the documents to
        //                                                  this file (UTF-8) rather than to
        //                                                  the Ansible module output
        //                                      
        // Remarks:
        // --------
        //
        // This module simply connects to the Couchbase server and submits the query.  Results
        // will be returned as a lines of JSON objects by default but you can also output
        // a JSON array of of documents and these are written to the module output by 
        // default.  You can also specify that this be written to a file and limit the
        // number of items returned.
        //
        // NOTE: This module always returns with [changed=FALSE] even if the query
        //       may have made changes to the database.  The module does not attempt
        //       to determine when queries make changes.
        //
        // NOTE: The [output] file must be located in the same folder as the playbook
        //       or within a subfolder.
        //
        // Examples:
        // ---------
        //
        // This example executes the [select * from test] query and writes the output
        // documents to [data.txt] as one JSON object per line.
        // 
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: query example
        //        neon_couchbase_query:
        //          name: query
        //          servers:
        //            - 10.50.0.3
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          query: "select test.* from test"
        //          output: data.txt
        //          format: json-lines

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
            /// <param name="context">The Ansible module context.</param>
            /// <param name="format">Specifies the output format.</param>
            /// <param name="path">Optional output file path.</param>
            public CouchbaseQueryResultWriter(ModuleContext context, CouchbaseFileFormat format, string path = null)
            {
                this.context = context;
                this.format  = format;

                if (!string.IsNullOrEmpty(path))
                {
                    writer = new StreamWriter(path, append: false, encoding: System.Text.Encoding.UTF8);
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

                if (writer != null)
                {
                    writer.Dispose();
                    writer = null;
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "servers",
            "port",
            "ssl",
            "bucket",
            "username",
            "password",
            "query",
            "limit",
            "format",
            "output"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var hive          = HiveHelper.Hive;
            var nodeGroups    = hive.Definition.GetHostGroups(excludeAllGroup: true);

            //-----------------------------------------------------------------
            // Parse the module arguments.

            if (!context.ValidateArguments(context.Arguments, validModuleArgs))
            {
                context.Failed = true;
                return;
            }

            var couchbaseArgs = CouchbaseArgs.Parse(context);

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
                format = CouchbaseFileFormat.JsonLines;
            }

            var limit = context.ParseLong("limit", v => v >= 0);

            if (!limit.HasValue || limit.Value == 0)
            {
                limit = long.MaxValue;
            }

            var output = context.ParseString("output");

            //-----------------------------------------------------------------
            // Execute the query.

            using (var bucket = couchbaseArgs.Settings.OpenBucket(couchbaseArgs.Credentials))
            {
                try
                {
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
                catch (AggregateException e)
                {
                    var queryException = e.Find<CouchbaseQueryResponseException>();

                    if (queryException == null)
                    {
                        throw;
                    }

                    foreach (var error in queryException.Errors)
                    {
                        context.WriteErrorLine($"Couchbase [{error.Code}]: {error.Message}");
                    }
                }
            }
        }
    }
}
