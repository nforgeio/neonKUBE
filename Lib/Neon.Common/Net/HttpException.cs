//-----------------------------------------------------------------------------
// FILE:	    HttpException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

        //---------------------------------------------------------------------
        // Instance members

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
