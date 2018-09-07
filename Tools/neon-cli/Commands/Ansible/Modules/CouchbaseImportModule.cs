//-----------------------------------------------------------------------------
// FILE:	    CouchbaseImportModule.cs
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
using Couchbase.Core;
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
    /// Implements the <b>neon_couchbase_import</b> module.
    /// </summary>
    public class CouchbaseImportModule : IAnsibleModule
    {
        //---------------------------------------------------------------------
        // neon_couchbase_import:
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
        // source       yes                                 text file with the JSON documents
        //                                                  to be loaded (UTF-8 encoded)
        //
        // format       no          json-lines  json-array  file holds an JSON array of objects
        //                                      json-lines  file holds JSON objects (one per line)
        //
        // key          no                                  specifies the key pattern to be when
        //                                                  persisting documents.  A UUID will be
        //                                                  generated when this isn't specified.
        //                                                  See the remarks for more information.
        //
        //                                                  NOTE: You should surround the key
        //                                                        value with double quotes.
        //
        // first_key    no          1                       specifies the first integer key
        //                                                  value to use for #MONO_INCR#.
        //                                                  This defaults to 1.
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
        // This module reads a JSON file formatted as an array of JSON objects and persists
        // these to the specified Couchbase database.  By default, the objects will be saved
        // using a generated UUID.
        //
        // You can specify a custom keys two ways:
        //
        //      * Include a top-level property named [@@key] in each document.  This will
        //        be removed and then used as the document key when present.  Note that
        //        [@@key] will be ignored if a [key] pattern is specified. 
        //
        //      * Specify the [key] module parameter.  This is a string that may include
        //        references to top-level document properties like %PROPERTY% or the
        //        build-in key generators:
        //
        //              #UUID#          - inserts a UUID
        //              #MONO_INCR#     - inserts an integer counter.  This starts at
        //                                [first_key] which defaults to 1.
        //
        //        Note that you'll need to escape any '%' or '#' characters that are
        //        to be included in the key by using '%%' or '##'.
        //
        // Examples:
        // ---------
        //
        // This example imports JSON objects from [data.txt] formatted as one JSON
        // object per line to Couchbase at 10.50.0.3 using the credentials passed,
        // generating IDs as an integer counter.
        //
        // Note that we surrounded the key pattern with double quotes to prevent
        // Ansible from treating the leading '#' as a comment.
        //
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: import example
        //        neon_couchbase_import:
        //          name: import
        //          servers:
        //            - 10.50.0.3
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          source: data.txt
        //          format: json-lines
        //          key: "#MONO_INCR#"
        //
        // This example imports JSON objects generating a key that looks like
        //
        //      ID-#
        //
        // where [ID-1000] will be the first generated key:
        // 
        //  - name: test
        //    hosts: localhost
        //    tasks:
        //      - name: import example
        //        neon_couchbase_import:
        //          name: import
        //          servers:
        //            - 10.50.0.3
        //          bucket: test
        //          username: Administrator
        //          password: password
        //          source: data.txt
        //          format: json-lines
        //          key: "ID-#MONO_INCR#"
        //          first_key: 1000

        private HashSet<string> validModuleArgs = new HashSet<string>()
        {
            "servers",
            "port",
            "ssl",
            "bucket",
            "username",
            "password",
            "source",
            "format",
            "key",
            "first_key"
        };

        /// <inheritdoc/>
        public void Run(ModuleContext context)
        {
            var hive       = HiveHelper.Hive;
            var nodeGroups = hive.Definition.GetHostGroups(excludeAllGroup: true);

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

            var format = context.ParseEnum<CouchbaseFileFormat>("format");

            if (!format.HasValue)
            {
                format = default(CouchbaseFileFormat);
            }

            var source = context.ParseString("source");

            if (string.IsNullOrEmpty(source))
            {
                context.WriteErrorLine("[source] module parameter is required.");
                return;
            }

            if (!File.Exists(source))
            {
                context.WriteErrorLine($"File [{source}] does not exist.");
                return;
            }

            var keyPattern = context.ParseString("key");
            var firstKey   = context.ParseLong("first_key") ?? 1;

            if (context.HasErrors)
            {
                return;
            }

            //-----------------------------------------------------------------
            // Import the data.

            using (var bucket = couchbaseArgs.Settings.OpenBucket(couchbaseArgs.Credentials))
            {
                var importer = new CouchbaseImporter(message => context.WriteErrorLine(message), bucket, keyPattern, firstKey, context.CheckMode);

                switch (format.Value)
                {
                    case CouchbaseFileFormat.JsonArray:

                        // $todo(jeff.lill): 
                        //
                        // Would be nice not to read this whole thing in memory and then
                        // effectibely duplicating it in memory again when parsing.

                        var jToken = JToken.Parse(File.ReadAllText(source));

                        if (jToken.Type != JTokenType.Array)
                        {
                            context.WriteErrorLine($"[{source}] is not a JSON array of documents.");
                            return;
                        }

                        var jArray = (JArray)jToken;

                        foreach (var item in jArray)
                        {
                            if (item.Type != JTokenType.Object)
                            {
                                context.WriteErrorLine($"[{source}] includes one or more non-document objects in the array.");
                                return;
                            }

                            importer.WriteDocument((JObject)item);
                        }
                        break;

                    case CouchbaseFileFormat.JsonLines:

                        using (var reader = new StreamReader(source, Encoding.UTF8))
                        {
                            foreach (var line in reader.Lines())
                            {
                                if (line.Trim() == string.Empty)
                                {
                                    continue;   // Ignore blank lines
                                }

                                var item = JToken.Parse(line);

                                if (item.Type != JTokenType.Object)
                                {
                                    context.WriteErrorLine($"[{source}] includes one or more lines with non-document objects.");
                                    return;
                                }

                                importer.WriteDocument((JObject)item);
                            }
                        }
                        break;

                    default:

                        throw new NotImplementedException($"Format [{format}] is not implemented.");
                }

                context.Changed = importer.DocumentCount > 0;

                if (context.CheckMode)
                {
                    context.WriteLine(AnsibleVerbosity.Info, $"[{importer.DocumentCount}] documents will be added when CHECK-MODE is disabled.");
                }
                else
                {
                    context.WriteLine(AnsibleVerbosity.Info, $"[{importer.DocumentCount}] documents were imported.");
                }
            }
        }
    }
}
