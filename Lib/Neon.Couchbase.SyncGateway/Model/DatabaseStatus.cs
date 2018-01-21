//-----------------------------------------------------------------------------
// FILE:	    DatabaseStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
