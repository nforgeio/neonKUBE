//-----------------------------------------------------------------------------
// FILE:	    TransientDetector.cs
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
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Net;

namespace Neon.Retry
{
    /// <summary>
    /// Provides some common transient error detection functions for use by
    /// <see cref="IRetryPolicy"/> implementations.
    /// </summary>
    public static class TransientDetector
    {
        /// <summary>
        /// Always determines that the exception is always transient.
        /// </summary>
        /// <param name="e">The potential transient exception.</param>
        /// <returns><c>true</c></returns>
        public static bool Always(Exception e)
        {
            Covenant.Requires<ArgumentException>(e != null);

            return true;
        }

        /// <summary>
        /// Never determines that the exception is never transient.
        /// </summary>
        /// <param name="e">The potential transient exception.</param>
        /// <returns><c>false</c></returns>
        public static bool Never(Exception e)
        {
            Covenant.Requires<ArgumentException>(e != null);

            return false;
        }

        /// <summary>
        /// Considers <see cref="SocketException"/> and <see cref="TransientException"/> as possible
        /// transient errors as well as these exceptions nested within an <see cref="AggregateException"/>.
        /// </summary>
        /// <param name="e">The potential transient exception.</param>
        /// <returns><c>true</c> if the exception is to be considered as transient.</returns>
        /// <remarks>
        /// <note>
        /// <see cref="TransientException"/> is always considered to be a transient exception.
        /// </note>
        /// </remarks>
        public static bool Network(Exception e)
        {
            Covenant.Requires<ArgumentException>(e != null);

            if (e is TransientException)
            {
                return true;
            }

            var httpException = e as HttpException;

            if (httpException != null)
            {
                switch (httpException.StatusCode)
                {
                    case HttpStatusCode.GatewayTimeout:
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.RequestTimeout:
                    case HttpStatusCode.ServiceUnavailable:
                    case (HttpStatusCode)423:   // Locked
                    case (HttpStatusCode)429:   // Too many requests

                        return true;
                }
            }

            var aggregateException = e as AggregateException;

            if (aggregateException != null)
            {
                e = aggregateException.InnerException;
            }

            var socketException = e as SocketException;

            if (socketException != null)
            {
                switch (socketException.SocketErrorCode)
                {
                    case SocketError.ConnectionAborted:
                    case SocketError.ConnectionRefused:
                    case SocketError.ConnectionReset:
                    case SocketError.HostDown:
                    case SocketError.HostNotFound:
                    case SocketError.Interrupted:
                    case SocketError.NotConnected:
                    case SocketError.NetworkReset:
                    case SocketError.TimedOut:

                        return true;

                    // These really aren't transient.

                    case SocketError.HostUnreachable:
                    case SocketError.NetworkDown:
                    case SocketError.NetworkUnreachable:

                        return false;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Considers <see cref="HttpException"/>, <see cref="HttpRequestException"/>, and
        /// <see cref="TransientException"/> as possible transient errors as well as these 
        /// exceptions nested within an <see cref="AggregateException"/>.
        /// </summary>
        /// <param name="e">The potential transient exception.</param>
        /// <returns><c>true</c> if the exception is to be considered as transient.</returns>
        /// <remarks>
        /// <note>
        /// <see cref="TransientException"/> is always considered to be a transient exception.
        /// </note>
        /// </remarks>
        public static bool Http(Exception e)
        {
            Covenant.Requires<ArgumentException>(e != null);

            var aggregateException = e as AggregateException;

            if (aggregateException != null)
            {
                e = aggregateException.InnerException;
            }

            if (e is TransientException)
            {
                return true;
            }

            var httpException = e as HttpException;

            if (httpException != null)
            {
                if ((int)httpException.StatusCode < 400)
                {
                    return true;
                }

                switch (httpException.StatusCode)
                {
                    case HttpStatusCode.GatewayTimeout:
                    case HttpStatusCode.InternalServerError:
                    case HttpStatusCode.ServiceUnavailable:
                    case (HttpStatusCode)429: // To many requests

                        return true;
                }

                return false;
            }

            var httpRequestException = e as HttpRequestException;

            if (httpRequestException != null)
            {
                // $hack(jeff.lill): 
                //
                // Extract the formatted status code from the message which
                // will look like this:
                //
                //      [status=404, reason=[Not Found]]: NotFound

                var message = httpRequestException.Message;

                if (message.StartsWith("[status="))
                {
                    var pos    = "[status=".Length;
                    var posEnd = message.IndexOf(',', 0);

                    if (int.TryParse(message.Substring(pos, posEnd - pos), out var statusCode))
                    {
                        switch ((HttpStatusCode)statusCode)
                        {
                            case HttpStatusCode.GatewayTimeout:
                            case HttpStatusCode.InternalServerError:
                            case HttpStatusCode.ServiceUnavailable:
                            case (HttpStatusCode)429: // To many requests

                                return true;
                        }
                    }
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Considers <see cref="SocketException"/>, <see cref="HttpRequestException"/>, and
        /// <see cref="TransientException"/> as possible transient errors as well as these 
        /// exceptions nested within an <see cref="AggregateException"/>.
        /// </summary>
        /// <param name="e">The potential transient exception.</param>
        /// <returns><c>true</c> if the exception is to be considered as transient.</returns>
        /// <remarks>
        /// <note>
        /// <see cref="TransientException"/> is always considered to be a transient exception.
        /// </note>
        /// </remarks>
        public static bool NetworkOrHttp(Exception e)
        {
            Covenant.Requires<ArgumentException>(e != null);

            return Network(e) || Http(e);
        }

        /// <summary>
        /// Used internally to determine whether a thrown exception matches a specific exception type.
        /// </summary>
        /// <param name="e">The thrown exception or <c>null</c>.</param>
        /// <param name="exceptionType">The exception type to be matched.</param>
        /// <returns>
        /// <c>true</c> if <paramref name="e"/> is not <c>null</c> and
        /// it's type is <paramref name="exceptionType"/> or if <paramref name="e"/>
        /// is a <see cref="AggregateException"/> and one of the subexceptions
        /// is a <paramref name="exceptionType"/>.
        /// </returns>
        internal static bool MatchException(Exception e, Type exceptionType)
        {
            Covenant.Requires<ArgumentException>(exceptionType != null);

            if (e == null)
            {
                return false;
            }

            if (e.GetType() == exceptionType)
            {
                return true;
            }

            var aggregateException = e as AggregateException;

            if (aggregateException != null && aggregateException.Find(exceptionType) != null)
            {
                return true;
            }

            return false;
        }
    }
}
