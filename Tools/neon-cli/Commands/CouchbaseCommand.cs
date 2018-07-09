//-----------------------------------------------------------------------------
// FILE:	    CouchbaseCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Consul;
using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core.Serialization;
using ICSharpCode.SharpZipLib.Zip;
using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Cryptography;
using Neon.Data;
using Neon.Hive;

// $todo(jeff.lill): Modify insert keys to work like the Ansible [neon_couchbase_import] module.

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>couchbase</b> command.
    /// </summary>
    public class CouchbaseCommand : CommandBase
    {
        private const string usage = @"
Implements Couchbase commands

USAGE:

    neon couchbase query TARGET QUERY
    neon couchbase query TARGET @QUERY-FILE
    neon couchbase query TARGET -

    neon couchbase upsert [--key=KEY] [--first-key=#] TARGET JSON
    neon couchbase upsert [--key=KEY] [--first-key=#] TARGET JSON-FILE
    neon couchbase upsert [--key=KEY] [--first-key=#] TARGET -

ARGUMENTS:

    TARGET         - The target server, credentials, and bucket
                     formatted like:

                        http(s)://HOST:PORT@USER:PASSWORD:BUCKET
                        couchbase://HOST@USER:PASSWORD:BUCKET

    QUERY           - A N1QL query (quoted as required).
    QUERY-FILE      - Name of a file with a N1QL query.
    JSON            - JSON document being upserted.
    JSON-FILE       - Name of the file with the JSON documents
                      to upserted (one per line).
    -               - Indicates that the query or JSON documents
                      will be read from STDIN.

OPTIONS:

    --key           - (optional) specifies the key pattern to be
                      used for document upserts.  See note below.

    --firstkey=#    - (optional) specifies the first key to use
                      for #MONO_INCR# based templates.
                      Defaults to 1.
    
COMMANDS:

    query       Performs a N1QL query and writes the JSON results
                to STDOUT.

    upsert      Upserts JSON documents to the bucket.  See the note
                below describing how document keys are constructed.

NOTE: Document Keys
-------------------
The [neon couchbase upsert] command loads JSON documents into the
specified Couchbase database and bucket.  By default, each document
will be persited using a generated UUID as the key.  You can 
customize this in two ways:

    * Include a top-level property named [@@key] in each document.  This will
      be removed and then used as the document key when present.  Note that
      [@@key] will be ignored if a specific key pattern is specified. 

    * Specify the [key] module parameter.  This is a string that may include
      references to top-level document properties like %PROPERTY% or the
      build-in key generators:

            #UUID#          - inserts a UUID
            #MONO_INCR#     - inserts an integer counter 
                              starting at [--firstkey], default 1

      Note that you'll need to escape any '%' or '#' characters that are
      to be included in the key by using '%%' or '##'.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "couchbase" }; }
        }

        /// <inheritdoc/>
        public override string[] ExtendedOptions
        {
            get { return new string[] { "--key", "--first-key" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

            Program.ConnectHive();

            // Process the command arguments.

            var command = commandLine.Arguments[0];

            commandLine = commandLine.Shift(1);

            if (commandLine.Arguments.Length == 0)
            {
                Console.Error.WriteLine("*** ERROR: Expected a TARGET argument like: [couchbase://HOST@USER:PASSWORD:BUCKET] or [http(s)://HOST:PORT@USER:PASSWORD:BUCKET]");
                Program.Exit(1);
            }

            // Open a Couchbase bucket for the TARGET.
            //
            //      http(s)://HOST:PORT@USER:PASSWORD:BUCKET
            //      couchbase://HOST@USER:PASSWORD:BUCKET

            var target = commandLine.Arguments[0];
            var error  = $"*** ERROR: [{target}] is not a valid Couchbase target.  Expected: [couchbase://HOST@USER:PASSWORD:BUCKET] or [http(s)://HOST:PORT@USER:PASSWORD:BUCKET]";
            var fields = target.Split(new char[] { '@' }, 2);

            if (fields.Length != 2)
            {
                Console.WriteLine(error);
                Program.Exit(1);
            }

            var uri = fields[0];

            fields = fields[1].Split(':');

            if (fields.Length != 3)
            {
                Console.WriteLine(error);
                Program.Exit(1);
            }

            var username   = fields[0];
            var password   = fields[1];
            var bucketName = fields[2];
            var config     = new CouchbaseSettings();

            config.Servers.Clear();
            config.Servers.Add(new Uri(uri));
            config.Bucket = bucketName;

            using (var bucket = config.OpenBucket(username, password))
            {
                commandLine = commandLine.Shift(1);

                switch (command)
                {
                    case "query":

                        // Get the N1QL query.

                        if (commandLine.Arguments.Length != 1)
                        {
                            Console.Error.WriteLine("*** ERROR: QUERY argument expected.");
                            Program.Exit(1);
                        }

                        var query = commandLine.Arguments[0];

                        if (query == "-")
                        {
                            // Read the query from STDIN.

                            query = NeonHelper.ReadStandardInputText();
                        }
                        else if (query.StartsWith("@"))
                        {
                            // Read the query from the file.

                            query = File.ReadAllText(query.Substring(1));
                        }

                        var queryResults = bucket.Query<JToken>(query);

                        Console.WriteLine(JsonConvert.SerializeObject(queryResults, Formatting.Indented));
                        break;

                    case "upsert":

                        if (commandLine.Arguments.Length != 1)
                        {
                            Console.Error.WriteLine("*** ERROR: JSON object argument expected.");
                            Program.Exit(1);
                        }

                        var fileArg = commandLine.Arguments[0];
                        var input   = (Stream)null;

                        if (fileArg == "-")
                        {
                            // Read the object from STDIN.

                            input = Console.OpenStandardInput();
                        }
                        else
                        {
                            input = new FileStream(fileArg, FileMode.Open, FileAccess.ReadWrite);
                        }

                        var keyPattern  = commandLine.GetOption("--key");

                        var firstKeyValue = commandLine.GetOption("--first-key", "1");

                        if (!long.TryParse(firstKeyValue, out var firstKey))
                        {
                            Console.Error.WriteLine($"*** ERROR: [--firstkey={firstKeyValue}] is not a valid integer.");
                            Program.Exit(1);
                        }

                        var upsertError = false;

                        using (var reader = new StreamReader(input, Encoding.UTF8))
                        {
                            var importer = new CouchbaseImporter(
                                message =>
                                {
                                    upsertError = true;
                                    Console.Error.WriteLine($"*** ERROR: {message}");
                                }, 
                                bucket, keyPattern, firstKey);

                            foreach (var line in reader.Lines())
                            {
                                if (line.Trim() == string.Empty)
                                {
                                    continue;   // Ignore blank lines
                                }

                                var item = JToken.Parse(line);

                                if (item.Type != JTokenType.Object)
                                {
                                    upsertError = true;
                                    Console.Error.WriteLine($"*** ERROR: [{fileArg}] includes one or more lines with non-document objects.");
                                    break;
                                }

                                importer.WriteDocument((JObject)item);
                            }
                        }

                        if (upsertError)
                        {
                            Program.Exit(1);
                        }
                        break;

                    default:

                        Console.Error.WriteLine($"*** ERROR: Unknown command: [{command}]");
                        Program.Exit(1);
                        break;
                }
            }
        }

        /// <inheritdoc/>
        public override DockerShimInfo Shim(DockerShim shim)
        {
            return new DockerShimInfo(shimability: DockerShimability.None, ensureConnection: true);
        }
    }
}
