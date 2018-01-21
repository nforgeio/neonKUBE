//-----------------------------------------------------------------------------
// FILE:	    ServerInformation.cs
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
    /// Information about the Couchbase REST API.
    /// </summary>
    public class ServerInformation
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ServerInformation()
        {
        }

        /// <summary>
        /// <c>true</c> for admin APIs, <c>false</c> for public APIs.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// The vendor name and product.
        /// </summary>
        public string ProductName { get; set; }

        /// <summary>
        /// The friendly product version.
        /// </summary>
        public string ProductVersion { get; set; }

        /// <summary>
        /// The detailed product version incuding source repository information.
        /// </summary>
        public string Version { get; set; }
    }
}
