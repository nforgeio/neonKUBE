//-----------------------------------------------------------------------------
// FILE:	    HttpClientExtensions.cs
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
using System.IO;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Diagnostics;

namespace System.Net.Http
{
    /// <summary>
    /// <see cref="HttpClient"/> extension methods, mostly related to supporting <see cref="LogActivity"/> 
    /// related headers.
    /// </summary>
    public static class HttpClientExtensions
    {
        /// <summary>
        /// Send a DELETE request including an activity ID to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request has already been sent by the <see cref="HttpClient"/> class.</exception>
        public static async Task<HttpResponseMessage> DeleteAsync(this HttpClient client, string requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send a DELETE request including an activity ID to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request has already been sent by the <see cref="HttpClient"/> class.</exception>
        public static async Task<HttpResponseMessage> DeleteAsync(this HttpClient client, Uri requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send a DELETE request including an activity ID to the specified Uri with a cancellation token 
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request has already been sent by the <see cref="HttpClient"/> class.</exception>
        public static async Task<HttpResponseMessage> DeleteAsync(this HttpClient client, string requestUri, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send a DELETE request including an activity ID to the specified Uri with a cancellation token 
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request has already been sent by the <see cref="HttpClient"/> class.</exception>
        public static async Task<HttpResponseMessage> DeleteAsync(this HttpClient client, Uri requestUri, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, string requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, Uri requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri with an HTTP completion option as an
        /// asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="completionOption">
        /// When the operation should complete (as soon as a response is available or after
        /// reading the whole response content).
        /// </param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, string requestUri, HttpCompletionOption completionOption, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, completionOption);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri with an HTTP completion option as an
        /// asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="completionOption">
        /// When the operation should complete (as soon as a response is available or after
        /// reading the whole response content).
        /// </param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, Uri requestUri, HttpCompletionOption completionOption, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, completionOption);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri with a cancellation token 
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, string requestUri, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri with a cancellation token
        /// as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, Uri requestUri, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri with an HTTP completion option
        /// and cancellation token as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="completionOption">
        /// When the operation should complete (as soon as a response is available or after
        /// reading the whole response content).
        /// </param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, Uri requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, completionOption, cancellationToken);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri with an HTTP completion option
        /// and cancellation token as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="completionOption">
        /// When the operation should complete (as soon as a response is available or after
        /// reading the whole response content).
        /// </param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> GetAsync(this HttpClient client, string requestUri, HttpCompletionOption completionOption, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, completionOption, cancellationToken);
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri and return the response body as a byte
        /// array in an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<byte[]> GetByteArrayAsync(this HttpClient client, string requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            var response = await client.SendAsync(request);

            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri and return the response body as a byte
        /// array in an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response byte array.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<byte[]> GetByteArrayAsync(this HttpClient client, Uri requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            var response = await client.SendAsync(request);

            return await response.Content.ReadAsByteArrayAsync();
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri and return the response body as a stream
        /// in an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response stream.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<Stream> GetStreamAsync(this HttpClient client, Uri requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            var response = await client.SendAsync(request);

            return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri and return the response body as a stream
        /// in an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response stream.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<Stream> GetStreamAsync(this HttpClient client, string requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            var response = await client.SendAsync(request);

            return await response.Content.ReadAsStreamAsync();
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri and return the response body as a string
        /// in an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<string> GetStringAsync(this HttpClient client, string requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            var response = await client.SendAsync(request);

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Send a GET request including an activity ID to the specified Uri and return the response body as a string
        /// in an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<string> GetStringAsync(this HttpClient client, Uri requestUri, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            var response = await client.SendAsync(request);

            return await response.Content.ReadAsStringAsync();
        }

        /// <summary>
        /// Send a POST request including an activity ID to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content to be sent to the server.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> PostAsync(this HttpClient client, string requestUri, HttpContent content, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

            request.Content = content;

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send a POST request including an activity ID to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content to be sent to the server.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> PostAsync(this HttpClient client, Uri requestUri, HttpContent content, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

            request.Content = content;

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send a POST request including an activity ID and with a cancellation token as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content to be sent to the server.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> PostAsync(this HttpClient client, string requestUri, HttpContent content, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

            request.Content = content;

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send a POST request including an activity ID and with a cancellation token as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content to be sent to the server.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> PostAsync(this HttpClient client, Uri requestUri, HttpContent content, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, requestUri);

            request.Content = content;

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send a PUT request including an activity ID to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content to be sent to the server.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> PutAsync(this HttpClient client, string requestUri, HttpContent content, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, requestUri);

            request.Content = content;

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send a PUT request including an activity ID to the specified Uri as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content to be sent to the server.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> PutAsync(this HttpClient client, Uri requestUri, HttpContent content, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, requestUri);

            request.Content = content;

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send a PUT request including an activity ID and with a cancellation token as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content to be sent to the server.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> PutAsync(this HttpClient client, string requestUri, HttpContent content, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, requestUri);

            request.Content = content;

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send a PUT request including an activity ID and with a cancellation token as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="content">The content to be sent to the server.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        public static async Task<HttpResponseMessage> PutAsync(this HttpClient client, Uri requestUri, HttpContent content, CancellationToken cancellationToken, LogActivity activity)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, requestUri);

            request.Content = content;

            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send an HTTP request including an activity ID as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request has already been sent by the <see cref="HttpClient"/> class.</exception>
        public static async Task<HttpResponseMessage> SendAsync(this HttpClient client, HttpRequestMessage request, LogActivity activity)
        {
            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request);
        }

        /// <summary>
        /// Send an HTTP request including an activity ID as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request has already been sent by the <see cref="HttpClient"/> class.</exception>
        public static async Task<HttpResponseMessage> SendAsync(this HttpClient client, HttpRequestMessage request, CancellationToken cancellationToken, LogActivity activity)
        {
            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, cancellationToken);
        }

        /// <summary>
        /// Send an HTTP request including an activity ID as an asynchronous operation.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="request">The HTTP request message to send.</param>
        /// <param name="completionOption">
        /// When the operation should complete (as soon as a response is available or after
        /// reading the whole response content).
        /// </param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request has already been sent by the <see cref="HttpClient"/> class.</exception>
        public static async Task<HttpResponseMessage> SendAsync(this HttpClient client, HttpRequestMessage request, HttpCompletionOption completionOption, LogActivity activity)
        {
            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, completionOption);
        }

        /// <summary>
        /// Send an HTTP request including an activity ID as an asynchronous operation with using a completion option and cancellation token
        /// and including a <see cref="LogActivity"/> ID.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <param name="request">The request.</param>
        /// <param name="completionOption">
        /// When the operation should complete (as soon as a response is available or after
        /// reading the whole response content).
        /// </param>
        /// <param name="cancellationToken">The cancellation token to cancel operation.</param>
        /// <param name="activity">The <see cref="LogActivity"/> whose ID is to be included in the request.</param>
        /// <returns>The response.</returns>
        /// <exception cref="ArgumentNullException">Thrown when a required argument is <c>null</c>.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the request has already been sent by the <see cref="HttpClient"/> class.</exception>
        public static async Task<HttpResponseMessage> SendAsync(this HttpClient client, HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken cancellationToken, LogActivity activity)
        {
            if (!string.IsNullOrEmpty(activity.Id))
            {
                request.Headers.Add(LogActivity.HttpHeader, activity.Id);
            }

            return await client.SendAsync(request, completionOption, cancellationToken);
        }
    }
}
