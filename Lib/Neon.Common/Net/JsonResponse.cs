//-----------------------------------------------------------------------------
// FILE:	    JsonResponse.cs
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
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Net;
using Neon.Retry;

namespace Neon.Net
{
    /// <summary>
    /// Encapsulates the response returned from a <see cref="JsonClient"/> 
    /// server call.
    /// </summary>
    public class JsonResponse
    {
        /// <summary>
        /// Constructs a <see cref="JsonResponse"/> from a lower level <see cref="HttpResponseMessage"/>.
        /// </summary>
        /// <param name="requestUri">The request URI.</param>
        /// <param name="httpRespose">The low-level response.</param>
        /// <param name="responseText">The response text.</param>
        public JsonResponse(string requestUri, HttpResponseMessage httpRespose, string responseText)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(requestUri));
            Covenant.Requires<ArgumentNullException>(httpRespose != null);

            // $note(jeff.lill):
            //
            // I've seen services where JSON REST APIs return [Content-Type] as [text/plain] and [text/json]
            // so we'll accept those too.

            var jsonContent = httpRespose.Content.Headers.ContentType != null &&
                (
                    httpRespose.Content.Headers.ContentType.MediaType.Equals("application/json", StringComparison.OrdinalIgnoreCase) ||
                    httpRespose.Content.Headers.ContentType.MediaType.Equals("text/plain", StringComparison.OrdinalIgnoreCase) ||
                    httpRespose.Content.Headers.ContentType.MediaType.Equals("text/json", StringComparison.OrdinalIgnoreCase)
                );

            this.RequestUri   = requestUri;
            this.HttpResponse = httpRespose;

            if (httpRespose.Content.Headers.ContentType != null
                && jsonContent
                && responseText != null
                && responseText.Length > 0)
            {
                this.JsonText = responseText;
            }
        }

        /// <summary>
        /// Returns the request URI.
        /// </summary>
        public string RequestUri { get; private set; }

        /// <summary>
        /// Returns the low-level HTTP response.
        /// </summary>
        public HttpResponseMessage HttpResponse { get; private set; }

        /// <summary>
        /// Returns the response as JSON text or <c>null</c> if the server didn't
        /// respond with JSON.
        /// </summary>
        public string JsonText { get; private set; }

        /// <summary>
        /// Returns the dynamic JSON response document, array, value or <c>null</c> if the server didn't return
        /// JSON content.
        /// </summary>
        /// <returns>The dynamic document or <c>null</c>.</returns>
        public dynamic AsDynamic()
        {
            if (JsonText == null)
            {
                return null;
            }

            return JToken.Parse(JsonText);
        }

        /// <summary>
        /// Converts the response document to a specified type or <c>null</c> if the server didn't 
        /// return JSON content.
        /// </summary>
        /// <typeparam name="TResult">The specified type.</typeparam>
        /// <returns>The converted document or its default value.</returns>
        public TResult As<TResult>()
        {
            if (JsonText == null)
            {
                return default;
            }

            return NeonHelper.JsonDeserialize<TResult>(JsonText);
        }

        /// <summary>
        /// Returns the HTTP response status code.
        /// </summary>
        public HttpStatusCode StatusCode
        {
            get { return HttpResponse.StatusCode; }
        }

        /// <summary>
        /// Returns <c>true</c> if the response status code indicates success.
        /// </summary>
        public bool IsSuccess
        {
            get { return HttpResponse.IsSuccessStatusCode; }
        }

        /// <summary>
        /// Ensures that the status code indicates success by throwing an 
        /// exception for any error related status codes.
        /// </summary>
        /// <exception cref="HttpException">Thrown if the response doesn't indicate success.</exception>
        public void EnsureSuccess()
        {
            if (!IsSuccess)
            {
                throw new HttpException(HttpResponse.StatusCode, HttpResponse.ReasonPhrase, RequestUri);
            }
        }
    }
}
