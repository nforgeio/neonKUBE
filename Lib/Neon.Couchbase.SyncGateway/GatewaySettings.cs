//-----------------------------------------------------------------------------
// FILE:	    GatewaySettings.cs
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
