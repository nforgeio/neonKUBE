//-----------------------------------------------------------------------------
// FILE:	    TrafficHttpReuse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// <para>
    /// Enumerates the options for controlling the how idle backend to origin
    /// server connections can be reused between requests.  This maps directly
    /// to the <b>http-reuse </b>HAProxy and is discussed at length 
    /// <a href="https://cbonte.github.io/haproxy-dconv/1.8/configuration.html#4.2-http-reuse">here</a>.
    /// </para>
    /// <note>
    /// neonHIVE HTTP rules default to <see cref="Safe"/> where as HAProxy defaults
    /// to <see cref="Never"/>.
    /// </note>
    /// </summary>
    public enum TrafficHttpReuse
    {
        /// <summary>
        /// This is the recommended strategy. The first request of a
        /// session is always sent over its own connection, and only
        /// subsequent requests may be dispatched over other existing
        /// connections. This ensures that in case the server closes the
        /// connection when the request is being sent, the browser can
        /// decide to silently retry it. Since it is exactly equivalent to
        /// regular keep-alive, there should be no side effects.
        /// </summary>
        [EnumMember(Value = "safe")]
        Safe = 0,

        /// <summary>
        /// Idle connections are never shared between sessions. This is
        /// the default choice. It may be enforced to cancel a different
        /// strategy inherited from a defaults section or for
        /// troubleshooting. For example, if an old bogus application
        /// considers that multiple requests over the same connection come
        /// from the same client and it is not possible to fix the
        /// application, it may be desirable to disable connection sharing
        /// in a single backend. An example of such an application could
        /// be an old haproxy using cookie insertion in tunnel mode and
        /// not checking any request past the first one.
        /// </summary>
        [EnumMember(Value = "never")]
        Never,

        /// <summary>
        /// This mode may be useful in webservices environments where
        /// all servers are not necessarily known and where it would be
        /// appreciable to deliver most first requests over existing
        /// connections. In this case, first requests are only delivered
        /// over existing connections that have been reused at least once,
        /// proving that the server correctly supports connection reuse.
        /// It should only be used when it's sure that the client can
        /// retry a failed request once in a while and where the benefit
        /// of aggressive connection reuse significantly outweights the
        /// downsides of rare connection failures.
        /// </summary>
        [EnumMember(Value = "aggressive")]
        Aggressive,

        /// <summary>
        /// This mode is only recommended when the path to the server is
        /// known for never breaking existing connections quickly after
        /// releasing them. It allows the first request of a session to be
        /// sent to an existing connection. This can provide a significant
        /// performance increase over the "safe" strategy when the backend
        /// is a cache farm, since such components tend to show a
        /// consistent behavior and will benefit from the connection
        /// sharing. It is recommended that the "http-keep-alive" timeout
        /// remains low in this mode so that no dead connections remain
        /// usable. In most cases, this will lead to the same performance
        /// gains as "aggressive" but with more risks. It should only be
        /// used when it improves the situation over "aggressive".
        /// </summary>
        [EnumMember(Value = "always")]
        Always
    }
}
