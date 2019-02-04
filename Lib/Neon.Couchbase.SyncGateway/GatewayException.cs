//-----------------------------------------------------------------------------
// FILE:	    GatewayException.cs
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

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

namespace Neon.Couchbase.SyncGateway
{
    /// <summary>
    /// Describes an error returned by a Couchbase Sync Gateway.
    /// </summary>
    public class GatewayException : Exception
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Throws a <see cref="GatewayException"/> if the <see cref="JsonResponse"/> indicates
        /// that the server reported an error.
        /// </summary>
        /// <param name="jsonResponse">The JSON response returned by the Sync Gateway.</param>
        /// <returns>The corresponding <see cref="GatewayException"/>.</returns>
        internal static void ThrowOnError(JsonResponse jsonResponse)
        {
            if (jsonResponse.IsSuccess)
            {
                return;
            }

            var doc = jsonResponse.AsDynamic();

            if (doc == null)
            {
                throw new GatewayException($"[status={jsonResponse.StatusCode}]: {jsonResponse.HttpResponse.ReasonPhrase}");
            }

            throw new GatewayException($"[status={jsonResponse.StatusCode}]: {doc.reason}")
            {
                StatusCode = jsonResponse.StatusCode,
                Error      = doc.error,
                Reason     = doc.reason
            };
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        /// <param name="message">The exception message.</param>
        private GatewayException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Returns the HTTP status code.
        /// </summary>
        public HttpStatusCode StatusCode { get; private set; }

        /// <summary>
        /// Returns the server error.
        /// </summary>
        public string Error { get; private set; }

        /// <summary>
        /// Returns a more detailed service reason.
        /// </summary>
        public string Reason { get; private set; }
    }
}
