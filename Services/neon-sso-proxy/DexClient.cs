//-----------------------------------------------------------------------------
// FILE:	      DexClient.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.

using System.ComponentModel;

using Neon.Common;
using Neon.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NeonSsoProxy
{

    public class DexClient : IDisposable
    {
        public Dictionary<string, BasicAuthenticationHeaderValue> AuthHeaders;
        public Uri BaseAddress => jsonClient.BaseAddress;

        private readonly JsonClient jsonClient;
        private bool isDisposed;
        
        public DexClient(Uri baseAddress)
        {
            this.jsonClient = new JsonClient()
            {
                BaseAddress = baseAddress
            };
            this.AuthHeaders = new Dictionary<string, BasicAuthenticationHeaderValue>();
        }

        /// <summary>
        /// Converts JSON to a application/x-www-form-urlencoded before POSTing to the Postmates API.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="_object"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<T> PostFormAsync<T>(string url, dynamic _object, CancellationToken cancellationToken = default)
        {
            var payloadString = "";
            var first = true;

            //dynamic data = JObject.Parse(_object);

            dynamic data = JObject.FromObject(_object);

            foreach (var descriptor in TypeDescriptor.GetProperties(data))
            {
                var key = descriptor.Name;
                var value = descriptor.GetValue(data).Value;
                if (value is null)
                {
                    continue;
                }
                if (!(value is string))
                {
                    value = (descriptor.GetValue(data) == null) ? null : JsonConvert.SerializeObject(descriptor.GetValue(data),
                        Newtonsoft.Json.Formatting.None);
                    value = (string)value.Trim('"');
                }
                if (!string.IsNullOrEmpty(value))
                {
                    if (!first)
                    {
                        payloadString += "&";
                    }
                    payloadString += $"{key}={value}";
                    first = false;
                }
            }

            var payload = new JsonClientPayload("application/x-www-form-urlencoded", payloadString);

            var response = await jsonClient.PostUnsafeAsync(url, payload);

            if (response.IsSuccess)
            {
                return response.As<T>();
            }
            else
            {
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
        /// <returns></returns>
        public async Task<TokenResponse> GetTokenAsync(
            string client,
            string code, 
            string redirect_uri, 
            string grant_type, 
            CancellationToken cancellationToken = default)
        {
            jsonClient.DefaultRequestHeaders.Authorization = AuthHeaders[client];
            var args = new
            {
                code = code,
                redirect_uri = redirect_uri,
                grant_type = grant_type
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


