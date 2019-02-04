//-----------------------------------------------------------------------------
// FILE:	    HttpException.cs
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

            return $", reason=[{reasonPhrase}]";
        }

        private static string GetUriString(string uri)
        {
            if (string.IsNullOrEmpty(uri))
            {
                return string.Empty;
            }

            return $", uri=[{uri}]";
        }

        /// <summary>
        /// Constructs an exception message using an inner exception.
        /// </summary>
        /// <param name="message">The base message.</param>
        /// <param name="innerException">The inner exception or <c>null</c>.</param>
        private static string GetMessage(string message, Exception innerException)
        {
            var httpException = innerException as HttpException;

            if (httpException != null)
            {
                return $"{message} [status={(int)httpException.StatusCode}{GetReasonString(httpException.ReasonPhrase)}{GetUriString(httpException.RequestUri)}]";
            }
            else
            {
                return message;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="innerException">Optional inner exception.</param>
        /// <param name="requestUri">The optional request URL.</param>
        public HttpException(string message, Exception innerException = null, string requestUri = null)
            : base(GetMessage(message, innerException))
        {
            var httpException = innerException as HttpException;

            if (httpException != null)
            {
                this.StatusCode   = httpException.StatusCode;
                this.RequestUri   = httpException.RequestUri;
                this.ReasonPhrase = httpException.ReasonPhrase ?? string.Empty;
            }
            else
            {
                this.StatusCode   = (HttpStatusCode)0;
                this.RequestUri   = requestUri;
                this.ReasonPhrase = message;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="innerException">The inner exception.</param>
        /// <param name="requestUri">The optional request URL.</param>
        public HttpException(Exception innerException, string requestUri = null)
            : base($"[exception={innerException.GetType().Name}({innerException.Message}){GetUriString(requestUri)}]")
        {
            this.StatusCode   = (HttpStatusCode)0;
            this.RequestUri   = requestUri;
            this.ReasonPhrase = innerException.Message;
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="statusCode">The HTTP response status code.</param>
        /// <param name="reasonPhrase">The HTTP response peason phrase (or <c>null</c>).</param>
        /// <param name="requestUri">The optional request URL.</param>
        public HttpException(HttpStatusCode statusCode, string reasonPhrase = null, string requestUri = null)
            : base($"[status={(int)statusCode}{GetReasonString(reasonPhrase)}{GetUriString(requestUri)}]: {statusCode}")
        {
            this.StatusCode   = statusCode;
            this.RequestUri   = requestUri;
            this.ReasonPhrase = reasonPhrase ?? string.Empty;
        }

        /// <summary>
        /// Returns the HTTP response status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Returns the request URI.
        /// </summary>
        public string RequestUri { get; private set; }

        /// <summary>
        /// Returns the HTTP response status message.
        /// </summary>
        public string ReasonPhrase { get; private set; }
    }
}
