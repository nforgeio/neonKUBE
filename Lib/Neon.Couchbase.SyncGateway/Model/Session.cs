//-----------------------------------------------------------------------------
// FILE:	    Session.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Couchbase.SyncGateway
{
    /// <summary>
    /// Describes a newly created Couchbase Sync Gateway session.
    /// </summary>
    public class Session
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public Session()
        {
        }

        /// <summary>
        /// The HTTP cookie name to be used for session handling.
        /// </summary>
        [JsonProperty(PropertyName = "Cookie")]
        [YamlMember(Alias = "Cookie", ApplyNamingConventions = false)]
        public string Cookie { get; set; }

        /// <summary>
        /// The session expiration time (local server time).
        /// </summary>
        [JsonProperty(PropertyName = "Expires")]
        [YamlMember(Alias = "Expires", ApplyNamingConventions = false)]
        public DateTime Expires { get; set; }

        /// <summary>
        /// The session ID.
        /// </summary>
        [JsonProperty(PropertyName = "Id")]
        [YamlMember(Alias = "Id", ApplyNamingConventions = false)]
        public string Id { get; set; }
    }
}
