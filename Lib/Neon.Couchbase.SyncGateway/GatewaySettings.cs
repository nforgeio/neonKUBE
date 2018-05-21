//-----------------------------------------------------------------------------
// FILE:	    GatewaySettings.cs
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

using Neon.Common;
using Neon.Net;
using Neon.Retry;

// $todo(jeff.lill): Support secure connections.

namespace Neon.Couchbase.SyncGateway
{
    /// <summary>
    /// Describes the <see cref="Gateway"/> REST client settings.
    /// </summary>
    public class GatewaySettings
    {
        /// <summary>
        /// The hostname of the Sync Gateway. 
        /// </summary>
        public string Host { get; set; }

        /// <summary>
        /// The Sync Gateway administration administration REST API port.  This defaults to <b>4984</b>.
        /// </summary>
        public int AdminPort { get; set; } = NetworkPorts.CouchbaseSyncGatewayAdmin;

        /// <summary>
        /// The Sync Gateway public REST API port.  This defaults to <b>4985</b>.
        /// </summary>
        public int PublicPort { get; set; } = NetworkPorts.CouchbaseSyncGatewayPublic;

        /// <summary>
        /// The operation retry policy.  This defaults to a reasonable <see cref="ExponentialRetryPolicy"/>.
        /// </summary>
        public IRetryPolicy RetryPolicy { get; set; } = new ExponentialRetryPolicy(TransientDetector.NetworkOrHttp);
    }
}
