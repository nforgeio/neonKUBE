//-----------------------------------------------------------------------------
// FILE:	    AnsibleCommand.Module.Couchbase.Import.cs
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
        // path         yes                                 JSON file with the JSON documents
        //                                                  to be loaded
        //
        // format       no          json-lines  json-array  file holds an array of JSON objects
        //                                      json-lines  file holds JSON objects (one per line)
        //
        // key          no                                  specifies the key pattern to be when
        //                                                  persisting documents.  A UUID will be
        //                                                  generated when this isn't specified.
        //                                                  See the remarks for more information.
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
        //        [@@key] will be ignored if a specific key pattern is specified. 
        //
        //      * Specify the [key] module parameter.  This is a string that may include
        //        references to top-level document properties like %PROPERTY% or the
        //        build-in values:
        //
        //              #UUID#          - inserts a UUID
        //              #MONO_INCR#     - inserts an integer counter (starting at 1)
        //
        //        Note that you'll need to escape any '%' or '#' characters that are
        //        to be included in the key by using '%%' or '##'.
        //
        // Examples:
        // ---------
        //

        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Handles persisting JSON documents to Couchbase.
        /// </summary>
        private class CouchbaseImportWriter
        {
            private ModuleContext   context;
            private IBucket         bucket;
            private string          keyPattern;
            private long            docNumber;
            private StringBuilder   sbKey;      // Using a common instance for performance

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="context">The module context.</param>
            /// <param name="bucket">The target Couchbase bucket.</param>
            /// <param name="keyPattern">The key pattern (or <c>null</c>).</param>
            public CouchbaseImportWriter(ModuleContext context, IBucket bucket, string keyPattern)
            {
                this.context    = context;
                this.bucket     = bucket;
                this.keyPattern = keyPattern;
                this.docNumber  = 1;
                this.sbKey      = new StringBuilder();
            }

            /// <summary>
            /// Persists a document to Couchbase.
            /// </summary>
            /// <param name="document">The document.</param>
            /// <remarks>
            /// <note>
            /// This method will remove any <b>@@key</b> property from 
            /// the document if present.
            /// </note>
            /// </remarks>
            public void WriteDocument(JObject document)
            {
                string key;

                if (context.HasErrors)
                {
                    // Stop writing documents when we've reported errors.

                    return;
                }

                if (!string.IsNullOrEmpty(keyPattern))
                {
                    // We need to parse the key pattern.

                    switch (keyPattern)
                    {
                        // Optimize some common cases.

                        case "#UUID#":

                            key = EntityHelper.CreateUuid();
                            break;

                        case "#MONO_INCR#":

                            key = docNumber.ToString();
                            break;

                        // We need to parse the key pattern. 

                        default:

                            sbKey.Clear();

                            var pos = 0;

                            while (pos < keyPattern.Length)
                            {
                                var ch = keyPattern[pos];

                                switch (ch)
                                {
                                    case '#':

                                        if (pos < keyPattern.Length && keyPattern[pos + 1] == '#')
                                        {
                                            // Escaped '#'

                                            sbKey.Append(ch);
                                            pos++;
                                            break;
                                        }
                                        else
                                        {
                                            // Scan forward for the closing '#'.

                                            var posEnd = keyPattern.IndexOf('#', pos + 1);

                                            if (posEnd == -1)
                                            {
                                                context.WriteErrorLine($"Key pattern [{keyPattern}] is missing a closing [#].");
                                                return;
                                            }

                                            var identifier = keyPattern.Substring(pos + 1, posEnd - (pos + 1));

                                            switch (identifier)
                                            {
                                                case "#UUID#":

                                                    sbKey.Append(EntityHelper.CreateUuid());
                                                    break;

                                                case "#MONO_INCR#":

                                                    sbKey.Append(docNumber);
                                                    break;

                                                default:

                                                    context.WriteErrorLine($"Key pattern [{keyPattern}] includes unknown generator [#{identifier}#].");
                                                    return;
                                            }

                                            pos = posEnd;
                                        }
                                        break;

                                    case '%':

                                        if (pos < keyPattern.Length && keyPattern[pos + 1] == '%')
                                        {
                                            // Escaped '%'

                                            sbKey.Append(ch);
                                            pos++;
                                            break;
                                        }
                                        else
                                        {
                                            // Scan forward for the closing '%'.

                                            var posEnd = keyPattern.IndexOf('%', pos + 1);

                                            if (posEnd == -1)
                                            {
                                                context.WriteErrorLine($"Key pattern [{keyPattern}] is missing a closing [%].");
                                                return;
                                            }

                                            var propertyName  = keyPattern.Substring(pos + 1, posEnd - (pos + 1));
                                            var propertyValue = document.GetValue(propertyName);

                                            if (propertyValue != null)
                                            {
                                                switch (propertyValue.Type)
                                                {
                                                    case JTokenType.String:
                                                    case JTokenType.Integer:
                                                    case JTokenType.Float:
                                                    case JTokenType.Guid:
                                                    case JTokenType.Date:
                                                    case JTokenType.Uri:

                                                        sbKey.Append(propertyValue);
                                                        break;

                                                    default:

                                                        context.WriteErrorLine($"Document number [{docNumber}] does is missing the [{propertyName}] property referenced by the key pattern.");
                                                        return;
                                                }

                                                pos = posEnd;
                                            }
                                        }
                                        break;

                                    default:

                                        sbKey.Append(ch);
                                        break;
                                }

                                pos++;
                            }

                            key = sbKey.ToString();
                            break;
                    }
                }
                else
                {
                    // We don't have a key pattern so we'll look for a
                    // [@@key] property and use that value if present.
                    // Otherwise, we'll generate a UUID.

                    var keyProperty = document.GetValue("@@key");

                    if (keyProperty != null)
                    {
                        switch (keyProperty.Type)
                        {
                            case JTokenType.String:
                            case JTokenType.Integer:
                            case JTokenType.Float:
                            case JTokenType.Guid:
                            case JTokenType.Date:
                            case JTokenType.Uri:

                                key = keyProperty.ToString();
                                break;

                            default:

                                key = EntityHelper.CreateUuid();
                                break;
                        }
                    }
                    else
                    {
                        key = EntityHelper.CreateUuid();
                    }

                    document.Remove("@@key"); // We don't perisit the special key property.
                }

                bucket.Upsert(key, document);
                docNumber++;
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Implements the built-in <b>neon_couchbase_query</b> module.
        /// </summary>
        /// <param name="context">The module execution context.</param>
        private void RunCouchbaseImportModule(ModuleContext context)
        {
            var cluster    = NeonClusterHelper.Cluster;
            var nodeGroups = cluster.Definition.GetNodeGroups(excludeAllGroup: true);

            //-----------------------------------------------------------------
            // Parse the module arguments.

            var couchbaseArgs = ParseCouchbaseSettings(context);

            if (couchbaseArgs == null)
            {
                return;
            }

            var format = context.ParseEnum<CouchbaseFileFormat>("format");

            if (!format.HasValue)
            {
                format = default(CouchbaseFileFormat);
            }

            var path = context.ParseString("path");

            if (string.IsNullOrEmpty(path))
            {
                context.WriteErrorLine("[path] module parameter is required.");
                return;
            }

            if (!File.Exists(path))
            {
                context.WriteErrorLine($"File [{path}] does not exist.");
                return;
            }

            var keyPattern = context.ParseString("key");

            if (context.HasErrors)
            {
                return;
            }

            //-----------------------------------------------------------------
            // Import the data.

            var bucket = couchbaseArgs.Settings.OpenBucket(couchbaseArgs.Credentials);
            var writer = new CouchbaseImportWriter(context, bucket, keyPattern);

            switch (format.Value)
            {
                case CouchbaseFileFormat.JsonArray:

                    // $todo(jeff.lill): 
                    //
                    // Would be nice not to read this whole thing in memory
                    // and then essentially duplicate it by parsing.

                    var jToken = JToken.Parse(File.ReadAllText(path));

                    if (jToken.Type != JTokenType.Array)
                    {
                        context.WriteErrorLine($"[{path}] is not a JSON array of documents.");
                        return;
                    }

                    var jArray = (JArray)jToken;

                    foreach (var item in jArray)
                    {
                        if (item.Type != JTokenType.Object)
                        {
                            context.WriteErrorLine($"[{path}] includes one or more non-document objects in the array.");
                            return;
                        }

                        writer.WriteDocument((JObject)item);
                    }
                    break;

                case CouchbaseFileFormat.JsonLines:

                    using (var reader = new StreamReader(path, Encoding.UTF8))
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
                                context.WriteErrorLine($"[{path}] includes one or more non-document objects.");
                                return;
                            }

                            writer.WriteDocument((JObject)item);
                        }
                    }
                    break;

                default:

                    throw new NotImplementedException($"Format [{format}] is not implemented.");
            }
        }
    }
}
