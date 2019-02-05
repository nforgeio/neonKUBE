//-----------------------------------------------------------------------------
// FILE:	    ReverseProxy.cs
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
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Net.Http.Server;

using Neon.Common;

namespace NShell
{
    /// <summary>
    /// <para>
    /// Implements a reverse HTTP or proxy between an endpoint on the local machine
    /// and an endpoint on a remote machine.
    /// </para>
    /// <note>
    /// This is supported <b>only on Windows</b>.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// Use the <see cref="ReverseProxy"/> constructor to create a proxy.  You'll
    /// pass the local and remote endpoints and optional request and response 
    /// handlers.
    /// </para>
    /// <para>
    /// The request handler will be called when a request is received on the local
    /// endpoint give the handler a chance to modify the request before it is
    /// forwarded on to the remote endpoint.  The response handler is called when
    /// a response is received from the remote endpoint, giving the handler a
    /// chance to examine and possibly modify the response before it is returned
    /// to the caller.
    /// </para>
    /// </remarks>
    public sealed class ReverseProxy : IDisposable
    {
        private object                  syncLock = new object();
        private EndPoint                localEndpoint;
        private EndPoint                remoteEndpoint;
        private Action<RequestContext>  requestHandler;
        private Action<RequestContext>  responseHandler;
        private WebListener             listener;
        private HttpClient              client;

        /// <summary>
        /// Constructs a reverse proxy.
        /// </summary>
        /// <param name="localEndpoint">The local endpoint.</param>
        /// <param name="remoteEndpoint">The remote endpoint.</param>
        /// <param name="requestHandler">Optional request hook.</param>
        /// <param name="responseHandler">Optional response hook.</param>
        public ReverseProxy(
            EndPoint                localEndpoint,
            EndPoint                remoteEndpoint,
            Action<RequestContext>  requestHandler = null, 
            Action<RequestContext>  responseHandler = null)
        {
            Covenant.Requires<ArgumentNullException>(localEndpoint != null);
            Covenant.Requires<ArgumentNullException>(remoteEndpoint != null);

            if (!NeonHelper.IsWindows)
            {
                throw new NotSupportedException($"[{nameof(ReverseProxy)}] is only supported on Windows.");
            }

            this.localEndpoint   = localEndpoint;
            this.remoteEndpoint  = remoteEndpoint;
            this.requestHandler  = requestHandler;
            this.responseHandler = responseHandler;

            // Create the client.

            client = new HttpClient()
            {
                 BaseAddress = new Uri($"http://{remoteEndpoint}/")
            };

            // Crank up the HTTP listener.

            var settings = new WebListenerSettings();

            settings.UrlPrefixes.Add($"http://{localEndpoint}/");

            this.listener = new WebListener(settings);
            this.listener.Start();

            // Handle received requests in a background task.

            Task.Run(() => RequestProcessor());
        }

        /// <ingeritdoc/>
        public void Dispose()
        {
            lock (syncLock)
            {
                if (client != null)
                {
                    client.Dispose();
                    client = null;
                }

                if (listener != null)
                {
                    listener.Dispose();
                    listener = null;
                }
            }
        }

        /// <summary>
        /// Handles received requests.
        /// </summary>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        private async Task RequestProcessor()
        {
            while (true)
            {
                try
                {
                    var context = await listener.AcceptAsync();

                    var task = Task.Run(
                        async () =>
                        {
                            using (context)
                            {
                                requestHandler?.Invoke(context);

                                // Copy the headers, body, and other state from the received request to the client request. 

                                var remoteRequest = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path);

                                foreach (var header in context.Request.Headers)
                                {
                                    remoteRequest.Headers.Add(header.Key, header.Value.ToArray());
                                }

                                var bodyStream = context.Request.Body;

                                if (bodyStream != null)
                                {
                                    remoteRequest.Content = new StreamContent(bodyStream);
                                }

                                // Forward the request.

                                var remoteResponse = await client.SendAsync(remoteRequest, HttpCompletionOption.ResponseHeadersRead);

                                // Copy the remote response headers, body, and other state to the client response.

                                context.Response.StatusCode   = (int)remoteResponse.StatusCode;
                                context.Response.ReasonPhrase = remoteResponse.ReasonPhrase;

                                foreach (var header in remoteResponse.Headers)
                                {
                                    context.Response.Headers.Add(header.Key, header.Value.ToArray());
                                }

                                var responseContent = remoteResponse.Content;

                                if (responseContent != null)
                                {
                                    await responseContent.CopyToAsync(context.Response.Body);
                                }

                                // Let the response handler have a look.

                                responseHandler?.Invoke(context);
                            }
                        });
                }
                catch (ObjectDisposedException)
                {
                    return; // We're going to use this as the signal to stop.
                }
            }
        }
    }
}
