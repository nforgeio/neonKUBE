//-----------------------------------------------------------------------------
// FILE:	    HttpException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Net
{
    /// <summary>
    /// Describes an HTTP error.
    /// </summary>
    public class HttpException : Exception
    {
        //---------------------------------------------------------------------
        // Static members

        private static string GetReasonString(string reasonPhrase)
        {
            if (string.IsNullOrEmpty(reasonPhrase))
            {
                return string.Empty;
            }

            return $" reason=[{reasonPhrase}]";
        }

        private static string GetStatusString(HttpStatusCode statusCode)
        {
            if ((int)statusCode <= 0)
            {
                return string.Empty;
            }

            return $" status=[{(int)statusCode}]";
        }

        private static string GetMethodString(string method)
        {
            if (string.IsNullOrEmpty(method))
            {
                return string.Empty;
            }

            return $" method={method}";
        }

        private static string GetUriString(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return string.Empty;
            }

            return $" uri={uri}";
        }

        /// <summary>
        /// Constructs an exception message using an inner <see cref="HttpException"/> if passed or 
        /// </summary>
        /// <param name="reasonPhrase">The HTTP response peason phrase (or <c>null</c>).</param>
        /// <param name="requestUri">Optionally specifies the request URL.</param>
        /// <param name="requestMethod">Optionally specifies the request method.</param>
        /// <param name="statusCode">Optionally specifies the response status code.</param>
        private static string GetMessage(string reasonPhrase, string requestUri = null, string requestMethod = null, HttpStatusCode statusCode = (HttpStatusCode)0)
        {
            return $"HTTP ERROR: {GetStatusString(statusCode)}{GetMethodString(requestMethod)}{GetUriString(requestUri)}{GetReasonString(reasonPhrase)}";
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructs an exception from the request and response details passed.
        /// </summary>
        /// <param name="reasonPhrase">The HTTP response reason phrase (or <c>null</c>).</param>
        /// <param name="requestUri">Optionally specifies the request URL.</param>
        /// <param name="requestMethod">Optionally specifies the request method.</param>
        /// <param name="statusCode">Optionally specifies the response status code.</param>
        public HttpException(string reasonPhrase = null, string requestUri = null, string requestMethod = null, HttpStatusCode statusCode = (HttpStatusCode)0)
            : base(GetMessage(reasonPhrase, requestUri, requestMethod, statusCode))
        {
            this.RequestUri    = requestUri;
            this.RequestMethod = requestMethod;
            this.ReasonPhrase  = reasonPhrase ?? string.Empty;
            this.StatusCode    = statusCode;
        }

        /// <summary>
        /// Constructs an exception from a <see cref="HttpRequestException"/> and optional
        /// request details.
        /// </summary>
        /// <param name="requestException">The <see cref="HttpRequestException"/>.</param>
        /// <param name="requestUri">Optionally specifies the request URL.</param>
        /// <param name="requestMethod">Optionally specifies the request method.</param>
        public HttpException(HttpRequestException requestException, string requestUri = null, string requestMethod = null)
            : base(GetMessage(requestException.Message ?? string.Empty, requestUri, requestMethod), requestException)
        {
            Covenant.Requires<ArgumentNullException>(requestException != null, nameof(requestException));

            this.RequestUri    = requestUri;
            this.RequestMethod = requestMethod;
            this.ReasonPhrase  = requestException.Message;
            this.StatusCode    = (HttpStatusCode)0;
        }

        /// <summary>
        /// Returns the HTTP response status message.
        /// </summary>
        public string ReasonPhrase { get; private set; }

        /// <summary>
        /// Returns the request URI when known.
        /// </summary>
        public string RequestUri { get; private set; }

        /// <summary>
        /// Returns the request requestMethod when known.
        /// </summary>
        public string RequestMethod { get; private set; }

        /// <summary>
        /// Returns the HTTP response status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }
    }
}
