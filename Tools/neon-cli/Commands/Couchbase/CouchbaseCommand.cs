//-----------------------------------------------------------------------------
// FILE:	    CouchbaseCommand.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

using Newtonsoft;
using Newtonsoft.Json;

using Neon.Common;
using Neon.Kube;

namespace NeonCli
{
    /// <summary>
    /// Implements the <b>couchbase</b> command.
    /// </summary>
    public class CouchbaseCommand : CommandBase
    {
        private const string usage = @"
Performs Couchbase related operations.

USAGE:

    neon couchbase index ...
    neon couchbase query ...
    neon couchbase upsert ...

COMMANDS:

    index       - Manages Couchbase indexes
    query       - Performs a N1QL query.
    upsert      - Upserts JSON documents to the bucket.
";
        /// <inheritdoc/>
        public override string[] Words
        {
            get { return new string[] { "couchbase" }; }
        }

        /// <inheritdoc/>
        public override void Help()
        {
            Console.WriteLine(usage);
        }

        /// <inheritdoc/>
        public override void Run(CommandLine commandLine)
        {
            Console.WriteLine(usage);
            Program.Exit(0);
        }
    }
}