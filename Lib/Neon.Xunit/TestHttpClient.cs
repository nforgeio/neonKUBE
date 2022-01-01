//-----------------------------------------------------------------------------
// FILE:	    TestHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Tasks;
using Neon.Xunit;

using Xunit;

namespace Neon.Xunit
{
    /// <summary>
    /// Implements a <see cref="HttpClient"/> compatible client with additional capabilities,
    /// like disabling connection reuse.  This is intended for unit testing purposes like
    /// verifying that load balancing actually works.
    /// </summary>
    public class TestHttpClient : IDisposable
    {
        // Implementation Note:
        // --------------------
        //
        // When [disableConnectionReuse=false], we'll simply have a single HttpClient instance
        // handle all requests.  When [disableConnectionReuse=true], we're simply using the
        // the client below to hold the base address and default headers and we're going to
        // create a new HttpClient for each request.

        private HttpClient          client;
        private HttpMessageHandler  handler;
        private bool                disposeHandler;
        private bool                disableConnectionReuse;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="disableConnectionReuse">Indicates whether connection reuse should be disabled.</param>
        /// <param name="handler">Optionally specifies a message handler.</param>
        /// <param name="disposeHandler">Optionally specifies that the handler should be disposed when this instance is disposed.</param>
        public TestHttpClient(bool disableConnectionReuse, HttpMessageHandler handler = null, bool disposeHandler = false)
        {
            this.handler                = handler;
            this.disposeHandler         = disposeHandler;
            this.disableConnectionReuse = disableConnectionReuse;

            if (disableConnectionReuse)
            {
                client = new HttpClient();
            }
            else
            {
                if (handler == null)
                {
                    client = new HttpClient();
                }
                else
                {
                    client = new HttpClient(handler, disposeHandler: false);
                }
            }
        }

        /// <inheritdoc/>
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
                if (client != null)
                {
                    client.Dispose();
                    client = null;
                }

                if (handler != null && disposeHandler)
                {
                    handler.Dispose();
                    handler = null;
                }
            }
        }

        /// <summary>
        /// Returns the client, ensuring that it hasn't been disposed.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
        private HttpClient GetClient()
        {
            var client = this.client;

            if (client == null)
            {
                throw new ObjectDisposedException(nameof(TestHttpClient));
            }

            return client;
        }

        /// <summary>
        /// Returns a single-use client.
        /// </summary>
        /// <returns>The client.</returns>
        /// <exception cref="ObjectDisposedException">Thrown when the instance is disposed.</exception>
        private HttpClient GetSingleUseClient()
        {
            Covenant.Assert(disableConnectionReuse);

            var client = GetClient();

            HttpClient singleUseClient;

            if (handler == null)
            {
                singleUseClient = new HttpClient();
            }
            else
            {
                singleUseClient = new HttpClient(handler, disposeHandler: false);
            }

            // Copy the settings from client to the new single use client.

            singleUseClient.BaseAddress = client.BaseAddress;
            singleUseClient.Timeout     = client.Timeout;

            foreach (var item in client.DefaultRequestHeaders)
            {
                singleUseClient.DefaultRequestHeaders.Add(item.Key, item.Value);
            }

            return singleUseClient;
        }

        /// <summary>
        /// The headers that should be sent with each request.
        /// </summary>
        public HttpRequestHeaders DefaultRequestHeaders
        {
            get { return GetClient().DefaultRequestHeaders; }
        }

        /// <summary>
        /// The base address that to be used when sending requests.
        /// </summary>
        public Uri BaseAddress
        {
            get { return GetClient().BaseAddress; }
            set { GetClient().BaseAddress = value; }
        }

        /// <summary>
        /// the maximum number of bytes to buffer when reading the response content.
        /// This defaults to 2GiB.
        /// </summary>
        public long MaxResponseContentBufferSize
        {
            get { return GetClient().MaxResponseContentBufferSize; }
            set { GetClient().MaxResponseContentBufferSize = value; }
        }

        /// <summary>
        /// The maximum time allowed before a request times out.
        /// </summary>
        public TimeSpan Timeout
        {
            get { return GetClient().Timeout; }
            set { GetClient().Timeout = value; }
        }

        /// <summary>
        /// Sends a <b>DELETE</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> DeleteAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.DeleteAsync(requestUri, cancellationToken);
                }
            }
            else
            {
                return await client.DeleteAsync(requestUri, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a <b>DELETE</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> DeleteAsync(string requestUri, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.DeleteAsync(requestUri, cancellationToken);
                }
            }
            else
            {
                return await client.DeleteAsync(requestUri, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a <b>DELETE</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> DeleteAsync(Uri requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.DeleteAsync(requestUri);
                }
            }
            else
            {
                return await client.DeleteAsync(requestUri);
            }
        }

        /// <summary>
        /// Sends a <b>DELETE</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> DeleteAsync(string requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.DeleteAsync(requestUri);
                }
            }
            else
            {
                return await client.DeleteAsync(requestUri);
            }
        }

        /// <summary>
        /// Sends a <b>GET</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> GetAsync(string requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetAsync(requestUri);
                }
            }
            else
            {
                return await client.GetAsync(requestUri);
            }
        }

        /// <summary>
        /// Sends a <b>GET</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="completionOption">The copmpletion options.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> GetAsync(string requestUri, HttpCompletionOption completionOption)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetAsync(requestUri, completionOption);
                }
            }
            else
            {
                return await client.GetAsync(requestUri, completionOption);
            }
        }

        /// <summary>
        /// Sends a <b>GET</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="completionOption">The copmpletion options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> GetAsync(string requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetAsync(requestUri, completionOption, cancellationToken);
                }
            }
            else
            {
                return await client.GetAsync(requestUri, completionOption, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a <b>GET</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> GetAsync(string requestUri, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetAsync(requestUri, cancellationToken);
                }
            }
            else
            {
                return await client.GetAsync(requestUri, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a <b>GET</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> GetAsync(Uri requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetAsync(requestUri);
                }
            }
            else
            {
                return await client.GetAsync(requestUri);
            }
        }

        /// <summary>
        /// Sends a <b>GET</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="completionOption">The completion option.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> GetAsync(Uri requestUri, HttpCompletionOption completionOption)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetAsync(requestUri, completionOption);
                }
            }
            else
            {
                return await client.GetAsync(requestUri, completionOption);
            }
        }

        /// <summary>
        /// Sends a <b>GET</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="completionOption">The completion option.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> GetAsync(Uri requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetAsync(requestUri, completionOption, cancellationToken);
                }
            }
            else
            {
                return await client.GetAsync(requestUri, completionOption, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a <b>GET</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> GetAsync(Uri requestUri, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetAsync(requestUri, cancellationToken);
                }
            }
            else
            {
                return await client.GetAsync(requestUri, cancellationToken);
            }
        }

        /// <summary>
        /// Performs a <b>GET</b> request and returns the response contents as a byte array.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The content bytes.</returns>
        public async Task<byte[]> GetByteArrayAsync(string requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetByteArrayAsync(requestUri);
                }
            }
            else
            {
                return await client.GetByteArrayAsync(requestUri);
            }
        }

        /// <summary>
        /// Performs a <b>GET</b> request and returns the response contents as a byte array.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The content bytes.</returns>
        public async Task<byte[]> GetByteArrayAsync(Uri requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetByteArrayAsync(requestUri);
                }
            }
            else
            {
                return await client.GetByteArrayAsync(requestUri);
            }
        }

        /// <summary>
        /// Performs a <b>GET</b> request and returns the response as a stream.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The response stream..</returns>
        public async Task<Stream> GetStreamAsync(string requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetStreamAsync(requestUri);
                }
            }
            else
            {
                return await client.GetStreamAsync(requestUri);
            }
        }

        /// <summary>
        /// Performs a <b>GET</b> request and returns the response as a stream.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The response stream..</returns>
        public async Task<Stream> GetStreamAsync(Uri requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetStreamAsync(requestUri);
                }
            }
            else
            {
                return await client.GetStreamAsync(requestUri);
            }
        }

        /// <summary>
        /// Performs a <b>GET</b> request and returns the response as a string.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The response string..</returns>
        public async Task<string> GetStringAsync(string requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetStringAsync(requestUri);
                }
            }
            else
            {
                return await client.GetStringAsync(requestUri);
            }
        }

        /// <summary>
        /// Performs a <b>GET</b> request and returns the response as a string.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <returns>The response string..</returns>
        public async Task<string> GetStringAsync(Uri requestUri)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.GetStringAsync(requestUri);
                }
            }
            else
            {
                return await client.GetStringAsync(requestUri);
            }
        }

        /// <summary>
        /// Performs a <b>POST</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The request contents.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.PostAsync(requestUri, content);
                }
            }
            else
            {
                return await client.PostAsync(requestUri, content);
            }
        }

        /// <summary>
        /// Performs a <b>POST</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The request contents.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> PostAsync(string requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.PostAsync(requestUri, content, cancellationToken);
                }
            }
            else
            {
                return await client.PostAsync(requestUri, content, cancellationToken);
            }
        }

        /// <summary>
        /// Performs a <b>POST</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The request contents.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.PostAsync(requestUri, content);
                }
            }
            else
            {
                return await client.PostAsync(requestUri, content);
            }
        }

        /// <summary>
        /// Performs a <b>POST</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The request contents.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> PostAsync(Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.PostAsync(requestUri, content, cancellationToken);
                }
            }
            else
            {
                return await client.PostAsync(requestUri, content, cancellationToken);
            }
        }

        /// <summary>
        /// Performs a <b>PUT</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The request contents.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.PutAsync(requestUri, content);
                }
            }
            else
            {
                return await client.PutAsync(requestUri, content);
            }
        }

        /// <summary>
        /// Performs a <b>PUT</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The request contents.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> PutAsync(string requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.PutAsync(requestUri, content, cancellationToken);
                }
            }
            else
            {
                return await client.PutAsync(requestUri, content, cancellationToken);
            }
        }

        /// <summary>
        /// Performs a <b>PUT</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The request contents.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> PutAsync(Uri requestUri, HttpContent content)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.PutAsync(requestUri, content);
                }
            }
            else
            {
                return await client.PutAsync(requestUri, content);
            }
        }

        /// <summary>
        /// Performs a <b>PUT</b> request.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The request contents.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> PutAsync(Uri requestUri, HttpContent content, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.PutAsync(requestUri, content, cancellationToken);
                }
            }
            else
            {
                return await client.PutAsync(requestUri, content, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.SendAsync(request);
                }
            }
            else
            {
                return await client.SendAsync(request);
            }
        }

        /// <summary>
        /// Sends a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="completionOption">The completion option.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.SendAsync(request, completionOption);
                }
            }
            else
            {
                return await client.SendAsync(request, completionOption);
            }
        }

        /// <summary>
        /// Sends a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="completionOption">The completion option.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.SendAsync(request, completionOption, cancellationToken);
                }
            }
            else
            {
                return await client.SendAsync(request, completionOption, cancellationToken);
            }
        }

        /// <summary>
        /// Sends a request.
        /// </summary>
        /// <param name="request">The request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The response.</returns>
        public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await SyncContext.ClearAsync;

            if (disableConnectionReuse)
            {
                using (var client = GetSingleUseClient())
                {
                    return await client.SendAsync(request, cancellationToken);
                }
            }
            else
            {
                return await client.SendAsync(request, cancellationToken);
            }
        }
    }
}
