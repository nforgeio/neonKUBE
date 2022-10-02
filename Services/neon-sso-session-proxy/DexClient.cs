//-----------------------------------------------------------------------------
// FILE:	      DexClient.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
// limitations under the License.

using System.ComponentModel;
using System.Diagnostics.Contracts;
using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeonSsoSessionProxy
{
    /// <summary>
    /// Used to manage Dex.
    /// </summary>
    public class DexClient : IDisposable
    {
        private readonly    JsonClient jsonClient;
        private bool        isDisposed;
        
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="baseAddress">Specifies the base Dex server address.</param>
        /// <param name="logger">Optionally specifies a logger.</param>
        public DexClient(Uri baseAddress, ILogger logger = null)
        {
            Covenant.Requires<ArgumentNullException>(baseAddress != null, nameof(baseAddress));

            this.jsonClient = new JsonClient()
            {
                BaseAddress = baseAddress
            };

            this.AuthHeaders = new Dictionary<string, BasicAuthenticationHeaderValue>();
            this.Logger      = logger;
        }

        /// <summary>
        /// Returns any authentication headers.
        /// </summary>
        public Dictionary<string, BasicAuthenticationHeaderValue> AuthHeaders { get; private set; }

        /// <summary>
        /// Returns the base server URI.
        /// </summary>
        public Uri BaseAddress => jsonClient.BaseAddress;

        /// <summary>
        /// Specifies the logger.
        /// </summary>
        public ILogger Logger { get; set; }

        /// <summary>
        /// Converts JSON to a application/x-www-form-urlencoded before POSTing to the Dex server.
        /// </summary>
        /// <typeparam name="T">Specifies the type of the entity being posted.</typeparam>
        /// <param name="url">The target API URI.</param>
        /// <param name="object">The object being posted.</param>
        /// <param name="cancellationToken">Optionally specifies a <see cref="CancellationToken"/>.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task<T> PostFormAsync<T>(string url, dynamic @object, CancellationToken cancellationToken = default)
        {
            var payloadString = "";
            var first         = true;

            dynamic data = JObject.FromObject(@object);

            foreach (var descriptor in TypeDescriptor.GetProperties(data))
            {
                var key   = descriptor.Name;
                var value = descriptor.GetValue(data).Value;

                if (value is null)
                {
                    continue;
                }
                else if (!(value is string))
                {
                    value = (descriptor.GetValue(data) == null) ? null : JsonConvert.SerializeObject(descriptor.GetValue(data), Newtonsoft.Json.Formatting.None);
                    value = value.Trim('"');
                }
                if (!string.IsNullOrEmpty(value))
                {
                    if (!first)
                    {
                        payloadString += "&";
                    }

                    payloadString += $"{key}={value}";
                    first          = false;
                }
            }

            var payload  = new JsonClientPayload("application/x-www-form-urlencoded", payloadString);
            var response = await jsonClient.PostUnsafeAsync(url, payload);

            if (response.IsSuccess)
            {
                return response.As<T>();
            }
            else
            {
                Logger.LogDebugEx(() => response.HttpResponse.Content.ReadAsStringAsync().Result);
                throw new HttpException(response.HttpResponse);
            }
        }

        /// <summary>
        /// Gets an authentication token from Dex.
        /// </summary>
        /// <param name="authHeader">Identifies the authentication header.</param>
        /// <param name="code"></param>
        /// <param name="redirectUri">The redirect URI.</param>
        /// <param name="grantType">Specifies the required grant.</param>
        /// <param name="cancellationToken">Optionally specifies a <see cref="CancellationToken"/>.</param>
        /// <returns>The token response.</returns>
        public async Task<TokenResponse> GetTokenAsync(
            string              authHeader,
            string              code, 
            string              redirectUri, 
            string              grantType, 
            CancellationToken   cancellationToken = default)
        {
            jsonClient.DefaultRequestHeaders.Authorization = AuthHeaders[authHeader];
            var args = new
            {
                code         = code,
                redirect_uri = redirectUri,
                grant_type   = grantType
            };

            var result = await PostFormAsync<TokenResponse>("/token", args, cancellationToken: cancellationToken);

            return result;
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (jsonClient)
            {
                if (!isDisposed)
                {
                    jsonClient.Dispose();
                    isDisposed = true;
                }
            }
        }
    }
}


