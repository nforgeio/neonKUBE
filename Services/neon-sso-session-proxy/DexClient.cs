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

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;
using Neon.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeonSsoSessionProxy
{

    public class DexClient : IDisposable
    {
        public Dictionary<string, BasicAuthenticationHeaderValue> AuthHeaders;
        public Uri BaseAddress => jsonClient.BaseAddress;
        public ILogger Logger { get; set; }

        private readonly    JsonClient jsonClient;
        private bool        isDisposed;
        
        public DexClient(Uri baseAddress, ILogger logger = null)
        {
            this.jsonClient = new JsonClient()
            {
                BaseAddress = baseAddress
            };
            this.AuthHeaders = new Dictionary<string, BasicAuthenticationHeaderValue>();
            this.Logger = logger;
        }

        /// <summary>
        /// Converts JSON to a application/x-www-form-urlencoded before POSTing to the Postmates API.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="_object"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task<T> PostFormAsync<T>(string url, dynamic _object, CancellationToken cancellationToken = default)
        {
            var payloadString = "";
            var first         = true;

            dynamic data = JObject.FromObject(_object);

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
                    value = (string)value.Trim('"');
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
                Logger.LogDebug(await response.HttpResponse.Content.ReadAsStringAsync());
                throw new HttpException(response.HttpResponse);
            }
        }

        /// <summary>
        /// Gets a token from Dex.
        /// </summary>
        /// <param name="url"></param>
        /// <param name="client"></param>
        /// <param name="code"></param>
        /// <param name="redirect_uri"></param>
        /// <param name="grant_type"></param>
        /// <param name="cancellationToken"></param>
        /// <returns>The token response.</returns>
        public async Task<TokenResponse> GetTokenAsync(
            string              client,
            string              code, 
            string              redirect_uri, 
            string              grant_type, 
            CancellationToken   cancellationToken = default)
        {
            jsonClient.DefaultRequestHeaders.Authorization = AuthHeaders[client];
            var args = new
            {
                code         = code,
                redirect_uri = redirect_uri,
                grant_type   = grant_type
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


