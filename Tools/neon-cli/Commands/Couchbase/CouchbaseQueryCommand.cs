//-----------------------------------------------------------------------------
// FILE:	    CouchbaseQueryCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Couchbase;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Data;
using Neon.Kube;
using Couchbase.N1QL;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>couchbase query</b> command.
    /// </summary>
    public class CouchbaseQueryCommand : CommandBase
    {
        private const string usage = @"
Implements Couchbase commands

USAGE:

    neon couchbase query TARGET QUERY
    neon couchbase query TARGET @QUERY-FILE
    neon couchbase query TARGET -

ARGUMENTS:

    TARGET         - The Couchbase server, credentials, and bucket
                     formatted like one of:

                        http(s)://HOST:PORT@USER:PASSWORD:BUCKET
                        couchbase://HOST@USER:PASSWORD:BUCKET

    QUERY           - A N1QL query (quoted as required).
    QUERY-FILE      - Name of a file with a N1QL query.
    -               - Indicates that the query or JSON documents
                      will be read from STDIN.

REMARKS:

This command performs a N1QL query on a Couchbase cluster and writes the
output to STDOUT.  You may submit the query as a quoted string on the 
command line, as a text file, or as text passed on STDIN.

";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "couchbase", "query" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override async Task RunAsync(CommandLine commandLine)
        {
            if (commandLine.HasHelpOption || commandLine.Arguments.Length == 0)
            {
                Console.WriteLine(usage);
                Program.Exit(0);
            }

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
            var fields = target.Split('@', 2);

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

                var queryRequest = new QueryRequest(query).ScanConsistency(ScanConsistency.RequestPlus);
                var queryResults = bucket.Query<JToken>(queryRequest);

                Console.WriteLine(JsonConvert.SerializeObject(queryResults, Formatting.Indented));
            }

            Program.Exit(0);
            await Task.CompletedTask;
        }
    }
}
