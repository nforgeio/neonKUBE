//-----------------------------------------------------------------------------
// FILE:	    Gateway.cs
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
using Neon.Net;

// $todo(jeff.lill): Implement the APIs

namespace Neon.Couchbase.SyncGateway
{
    /// <summary>
    /// Performs operations against a Couchbase Sync Gateway's public REST endpoint.
    /// </summary>
    public class Gateway : IDisposable
    {
        private JsonClient      jsonClient;
        private string              baseUri;

        /// <summary>
        /// Constructs a connection to a Couchbase Sync Gateway.
        /// </summary>
        /// <param name="settings">The gateway settings.</param>
        public Gateway(GatewaySettings settings)
        {
            Covenant.Requires<ArgumentNullException>(settings != null);

            if (string.IsNullOrEmpty(settings.Host))
            {
                throw new ArgumentException("[settings.Host] cannot be null or empty.");
            }

            this.Settings   = settings;
            this.jsonClient = new JsonClient(
                new HttpClientHandler()
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                }, 
                true);

            jsonClient.SafeRetryPolicy = settings.RetryPolicy;

            baseUri = $"http://{settings.Host}:{settings.PublicPort}/";
            Uri     = $"http://{settings.Host}:{settings.PublicPort}";
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~Gateway()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected void Dispose(bool disposing)
        {
            if (disposing)
            {
                var client = jsonClient;

                if (client != null)
                {
                    client.Dispose();
                    GC.SuppressFinalize(this);
                }
            }

            jsonClient = null;
        }

        /// <summary>
        /// Returns the gateway client settings.
        /// </summary>
        public GatewaySettings Settings { get; private set; }

        /// <summary>
        /// Returns the base URI to the Sync Gateway's public REST interface.
        /// </summary>
        /// <remarks>
        /// <note>
        /// This URI does not include a trailing forward slash (<b>/</b>).
        /// </note>
        /// </remarks>
        public string Uri { get; private set; }

        /// <summary>
        /// Returns the URI for a specific database managed by the sync gateway.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The database URI string.</returns>
        public string GetDatabaseUri(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            return baseUri + database;
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

            // Just being complete here.  The Sync-Server REST API doesn't 
            // currently have APIs with more than two segments.

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
        /// Creates a <see cref="GatewayManager"/> that can be used to perform administrative
        /// operations on the Sync Gateway.
        /// </summary>
        /// <returns>A <see cref="GatewayManager"/>.</returns>
        public GatewayManager CreateManager()
        {
            return new GatewayManager(this, jsonClient);
        }

        /// <summary>
        /// Returns information about the server.
        /// </summary>
        /// <returns>A <see cref="ServerInformation"/> instance.</returns>
        public async Task<ServerInformation> GetServerInformationAsync()
        {
            var response = await jsonClient.GetAsync(GetUri());
            var doc      = response.AsDynamic();

            return new ServerInformation()
            {
                IsAdmin        = false,
                ProductName    = doc.vendor.name,
                ProductVersion = doc.vendor.version,
                Version        = doc.version
            };
        }
    }
}
