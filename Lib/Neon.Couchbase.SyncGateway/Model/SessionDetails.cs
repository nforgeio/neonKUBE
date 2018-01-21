//-----------------------------------------------------------------------------
// FILE:	    SessionDetails.cs
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
    /// Details an existing Couchbase Sync-Server session.
    /// </summary>
    public class SessionDetails
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public SessionDetails()
        {
        }

        /// <summary>
        /// Indicates that the session exists.
        /// </summary>
        public bool IsSuccess { get; set; }
        
        /// <summary>
        /// The user associated with this session.
        /// </summary>
        public string User { get; set; }

        /// <summary>
        /// The authorized channels.
        /// </summary>
        public List<string> Channels { get; set; } = new List<string>();

        /// <summary>
        /// The list of supported authentication handlers.
        /// </summary>
        public List<string> Authenticators { get; set; } = new List<string>();
    }
}
