//-----------------------------------------------------------------------------
// FILE:	    HttpException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    /// An extension of <see cref="HttpRequestException"/> that includes the response
    /// <see cref="StatusCode"/> and <see cref="ReasonPhrase"/>.
    /// </summary>
    public class HttpException : HttpRequestException
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
        public HttpException(string message, Exception innerException = null)
            : base(GetMessage(message, innerException))
        {
            var httpException = innerException as HttpException;

            if (httpException != null)
            {
                this.StatusCode   = httpException.StatusCode;
                this.RequestUri   = httpException.RequestUri;
                this.ReasonPhrase = httpException.ReasonPhrase ?? string.Empty;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="statusCode">The HTTP response status code.</param>
        /// <param name="reasonPhrase">The HTTP response peason phrase (or <c>null</c>).</param>
        /// <param name="requestUri">The optional URL.</param>
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
