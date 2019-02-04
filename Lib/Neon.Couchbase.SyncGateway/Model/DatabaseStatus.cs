//-----------------------------------------------------------------------------
// FILE:	    DatabaseStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Couchbase.SyncGateway
{
    /// <summary>
    /// Current status of a Sync Gateway database.
    /// </summary>
    public class DatabaseStatus
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public DatabaseStatus()
        {
        }

        /// <summary>
        /// The database name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The latest sequence number.
        /// </summary>
        public long UpdateSequence { get; set; }

        /// <summary>
        /// Latest committed sequence number.
        /// </summary>
        public long CommitUpdateSequence { get; set; }

        /// <summary>
        /// Indicates that a database compaction is in progress.
        /// </summary>
        public bool IsCompacting { get; set; }

        /// <summary>
        /// The disk format version.
        /// </summary>
        public int DiskFormatVersion { get; set; }

        /// <summary>
        /// Time (UTC) when the database was started.
        /// </summary>
        public DateTime StartTimeUtc { get; set; }
    
        /// <summary>
        /// The current database state.
        /// </summary>
        public DatabaseState State { get; set; }
    }
}
