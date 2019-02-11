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
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Net.Http.Server;

using Neon.Common;

namespace Neon.Kube
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
        private const int BufferSize = 16 * 1024;

        private object                  syncLock = new object();
        private IPEndPoint              localEndpoint;
        private IPEndPoint              remoteEndpoint;
        private Action<RequestContext>  requestHandler;
        private Action<RequestContext>  responseHandler;
        private WebListener             listener;
        private HttpClient              client;
        private Queue<byte[]>           bufferPool;

        /// <summary>
        /// Constructs a reverse proxy.
        /// </summary>
        /// <param name="localEndpoint">The local endpoint.</param>
        /// <param name="remoteEndpoint">The remote endpoint.</param>
        /// <param name="requestHandler">Optional request hook.</param>
        /// <param name="responseHandler">Optional response hook.</param>
        public ReverseProxy(
            IPEndPoint              localEndpoint,
            IPEndPoint              remoteEndpoint,
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

            // Allow a reasonable number of remote HTTP socket connections.

            ServicePointManager.DefaultConnectionLimit = 100;

            // Initialize the buffer pool.  We're going to use this to reduce
            // pressure on the garbarge collector.

            bufferPool = new Queue<byte[]>();

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
        /// Returns a buffer from the pool or allocates a new buffer if
        /// the pool is empty.
        /// </summary>
        private byte[] GetBuffer()
        {
            byte[] buffer = null;

            lock (syncLock)
            {
                if (bufferPool.Count > 0)
                {
                    buffer = bufferPool.Dequeue();
                }
            }

            return buffer ?? new byte[BufferSize];
        }

        /// <summary>
        /// Releases a buffer by adding it back to the pool.
        /// </summary>
        /// <param name="buffer">The buffer.</param>
        private void ReleaseBuffer(byte[] buffer)
        {
            Covenant.Requires<ArgumentNullException>(buffer != null);

            lock (syncLock)
            {
                bufferPool.Enqueue(buffer);
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
                    var newContext = await listener.AcceptAsync();

                    // Process the request in its own task.

                    var task = Task.Factory.StartNew(
                        async (object arg) =>
                        {
                            var context  = (RequestContext)arg;
                            var request  = context.Request;
                            var response = context.Response;

                            using (context)
                            {
                                try
                                {
                                    // Let the request handler have a look.

                                    requestHandler?.Invoke(context);

                                    // Copy the headers, body, and other state from the received request to the client request. 

                                    var remoteRequest = new HttpRequestMessage(new HttpMethod(request.Method), request.Path);

                                    foreach (var header in request.Headers)
                                    {
                                        remoteRequest.Headers.Add(header.Key, header.Value.ToArray());
                                    }

                                    var bodyStream = request.Body;

                                    if (request.ContentLength > 0)
                                    {
                                        remoteRequest.Content = new StreamContent(bodyStream);
                                    }

                                    // Forward the request.

                                    var remoteResponse = await client.SendAsync(remoteRequest, HttpCompletionOption.ResponseHeadersRead);

                                    // Copy the remote response headers, body, and other state to the client response.

                                    response.StatusCode   = (int)remoteResponse.StatusCode;
                                    response.ReasonPhrase = remoteResponse.ReasonPhrase;

                                    foreach (var header in remoteResponse.Headers)
                                    {
                                        switch (header.Key.ToLowerInvariant())
                                        {
                                            case "transfer-encoding":

                                                // Don't copy this header from the remote response because it
                                                // will prevent any content from being returned to the client.

                                                break;

                                            case "server":

                                                // Don't copy this one header because it will append the value
                                                // to the default server name resulting in multiple values.

                                                break;

                                            default:

                                                response.Headers.Add(header.Key, header.Value.ToArray());
                                                break;
                                        }
                                    }

                                    // Use a buffer from the pool write the data returned from the
                                    // remote endpoint to the client response.

                                    var buffer = GetBuffer();

                                    using (var remoteStream = await remoteResponse.Content.ReadAsStreamAsync())
                                    {
                                        try
                                        {
                                            while (true)
                                            {
                                                var cb = await remoteStream.ReadAsync(buffer, 0, buffer.Length);

                                                if (cb == 0)
                                                {
                                                    break;
                                                }

                                                await response.Body.WriteAsync(buffer, 0, cb);
                                            }
                                        }
                                        finally
                                        {
                                            ReleaseBuffer(buffer);
                                        }
                                    }

                                    await remoteResponse.Content.CopyToAsync(response.Body);

                                    // Let the response handler have a look.

                                    responseHandler?.Invoke(context);
                                }
                                catch (Exception e)
                                {
                                    response.StatusCode   = 503;
                                    response.ReasonPhrase = "service unavailable";

                                    response.Body.Write(Encoding.UTF32.GetBytes(NeonHelper.ExceptionError(e)));
                                }
                            }
                        },
                        newContext);
                }
                catch (ObjectDisposedException)
                {
                    return; // We're going to use this as the signal to stop.
                }
            }
        }
    }
}
