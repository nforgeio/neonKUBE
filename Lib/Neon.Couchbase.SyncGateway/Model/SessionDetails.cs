//-----------------------------------------------------------------------------
// FILE:	    SessionDetails.cs
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
