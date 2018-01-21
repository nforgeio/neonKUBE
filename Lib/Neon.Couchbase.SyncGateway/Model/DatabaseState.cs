//-----------------------------------------------------------------------------
// FILE:	    DatabaseState.cs
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
    /// Enumerates the possible Sync Gateway database states.
    /// </summary>
    public enum DatabaseState
    {
        /// <summary>
        /// The database is offline.
        /// </summary>
        Offline,

        /// <summary>
        /// The database is online.
        /// </summary>
        Online,

        /// <summary>
        /// The database is starting.
        /// </summary>
        Starting,

        /// <summary>
        /// The database is stopping.
        /// </summary>
        Stopping,

        /// <summary>
        /// The database is resynchronizing documents.
        /// </summary>
        Resyncing
    }
}
