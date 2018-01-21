//-----------------------------------------------------------------------------
// FILE:	    GatewayManager.cs
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
using Neon.Net;

namespace Neon.Couchbase.SyncGateway
{
    /// <summary>
    /// Performs operations against a Couchbase Sync Gateway's administration REST endpoint.
    /// </summary>
    public partial class GatewayManager
    {
        private Gateway         gateway;
        private string          baseUri;
        private JsonClient  jsonClient;

        /// <summary>
        /// Constructs a manager for a Sync Gateway.
        /// </summary>
        /// <param name="gateway">The parent gateway.</param>
        /// <param name="jsonClient">The <see cref="JsonClient"/> to use.</param>
        internal GatewayManager(Gateway gateway, JsonClient jsonClient)
        {
            this.gateway    = gateway;
            this.jsonClient = jsonClient;
            this.baseUri    = $"http://{gateway.Settings.Host}:{gateway.Settings.AdminPort}/";
        }

        /// <summary>
        /// Returns the URI to be used for a REST call. 
        /// </summary>
        /// <param name="segments">The URI segments.</param>
        /// <returns>The URI string.</returns>
        private string GetUri(params object[] segments)
        {
            // We're going to optimize the common cases.

            switch (segments.Length)
            {
                case 0: return baseUri;
                case 1: return $"{baseUri}{segments[0]}";
                case 2: return $"{baseUri}{segments[0]}/{segments[1]}";
            }

            // Handle the remaining scenarios.

            var uri = baseUri;

            for (int i = 0; i < segments.Length; i++)
            {
                if (i > 0)
                {
                    uri += "/";
                }

                uri += segments[i];
            }

            return uri;
        }

        /// <summary>
        /// Returns information about the server.
        /// </summary>
        /// <returns>A <see cref="ServerInformation"/> instance.</returns>
        public async Task<ServerInformation> GetServerInformationAsync()
        {
            var     response = await jsonClient.GetAsync(GetUri());
            var     doc      = response.AsDynamic();
            var     jObject  = response.As<JObject>();
            var     isAdmin  = false;
            JToken  token;

            if (jObject.TryGetValue("ADMIN", out token))
            {
                isAdmin = (bool)token;
            }

            return new ServerInformation()
            {
                IsAdmin        = isAdmin,
                ProductName    = doc.vendor.name,
                ProductVersion = doc.vendor.version,
                Version        = doc.version
            };
        }
    }
}
