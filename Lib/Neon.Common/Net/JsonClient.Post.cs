//-----------------------------------------------------------------------------
// FILE:	    JsonClient.Post.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Diagnostics;
using Neon.Retry;

namespace Neon.Net
{
    public partial class JsonClient : IDisposable
    {
        /// <summary>
        /// Performs an HTTP <b>POST</b> ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="headers">The Optional HTTP headers.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> PostAsync(
            string              uri, 
            object              document          = null, 
            ArgDictionary       args              = null, 
            ArgDictionary       headers           = null,
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity       = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken: cancellationToken, headers: headers, activity: logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> returning a specific type and ensuring that a success code was returned.
        /// </summary>
        /// <typeparam name="TResult">The desired result type.</typeparam>
        /// <param name="uri">The URI</param>
        /// <param name="document">Optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="headers">The Optional HTTP headers.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<TResult> PostAsync<TResult>(
            string              uri, 
            object              document          = null, 
            ArgDictionary       args              = null, 
            ArgDictionary       headers           = null,
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity       = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            var result = await safeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken: cancellationToken, headers: headers, activity: logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });

            return result.As<TResult>();
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> using a specific <see cref="IRetryPolicy"/> and ensuring that
        /// a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="headers">The Optional HTTP headers.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        /// <exception cref="HttpException">Thrown when the server responds with an HTTP error status code.</exception>
        public async Task<JsonResponse> PostAsync(
            IRetryPolicy        retryPolicy, 
            string              uri,
            object              document          = null, 
            ArgDictionary       args              = null, 
            ArgDictionary       headers           = null,
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity       = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken: cancellationToken, headers: headers, activity: logActivity);
                        var jsonResponse = new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());

                        jsonResponse.EnsureSuccess();

                        return jsonResponse;
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> without ensuring that a success code was returned.
        /// </summary>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="headers">The Optional HTTP headers.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> PostUnsafeAsync(
            string              uri,
            object              document          = null, 
            ArgDictionary       args              = null, 
            ArgDictionary       headers           = null,
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity       = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            return await unsafeRetryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken: cancellationToken, headers: headers, activity: logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }

        /// <summary>
        /// Performs an HTTP <b>POST</b> using a specific <see cref="IRetryPolicy"/> and without ensuring
        /// that a success code was returned.
        /// </summary>
        /// <param name="retryPolicy">The retry policy or <c>null</c> to disable retries.</param>
        /// <param name="uri">The URI</param>
        /// <param name="document">The optional object to be uploaded as the request payload.</param>
        /// <param name="args">The optional query arguments.</param>
        /// <param name="headers">The Optional HTTP headers.</param>
        /// <param name="cancellationToken">The optional <see cref="CancellationToken"/>.</param>
        /// <param name="logActivity">The optional <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The <see cref="JsonResponse"/>.</returns>
        /// <exception cref="SocketException">Thrown for network connectivity issues.</exception>
        public async Task<JsonResponse> PostUnsafeAsync(
            IRetryPolicy        retryPolicy, 
            string              uri, 
            object              document          = null,
            ArgDictionary       args              = null, 
            ArgDictionary       headers           = null,
            CancellationToken   cancellationToken = default, 
            LogActivity         logActivity       = default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(uri));

            retryPolicy = retryPolicy ?? NoRetryPolicy.Instance;

            return await retryPolicy.InvokeAsync(
                async () =>
                {
                    var requestUri = FormatUri(uri, args);

                    try
                    {
                        var client = this.HttpClient;

                        if (client == null)
                        {
                            throw new ObjectDisposedException(nameof(JsonClient));
                        }

                        var httpResponse = await client.PostAsync(requestUri, CreateContent(document), cancellationToken: cancellationToken, headers: headers, activity: logActivity);

                        return new JsonResponse(requestUri, httpResponse, await httpResponse.Content.ReadAsStringAsync());
                    }
                    catch (HttpRequestException e)
                    {
                        throw new HttpException(e, requestUri);
                    }
                });
        }
    }
}
