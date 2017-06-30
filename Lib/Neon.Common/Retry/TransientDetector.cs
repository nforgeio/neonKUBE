//-----------------------------------------------------------------------------
// FILE:	    TransientDetector.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
        /// Considers <see cref="SocketException"/> as possible transient errors as well as these
        /// exceptions nested within an <see cref="AggregateException"/>.
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

            var transientException = e as TransientException;

            if (transientException != null)
            {
                return true;
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
                    case SocketError.HostUnreachable:
                    case SocketError.Interrupted:
                    case SocketError.NotConnected:
                    case SocketError.NetworkDown:
                    case SocketError.NetworkReset:
                    case SocketError.NetworkUnreachable:
                    case SocketError.TimedOut:

                        return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Considers <see cref="HttpException"/> and <see cref="HttpRequestException"/> as possible 
        /// transient errors as well as this exception nested within an <see cref="AggregateException"/>.
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

            var transientException = e as TransientException;

            if (transientException != null)
            {
                return true;
            }

            var aggregateException = e as AggregateException;

            if (aggregateException != null)
            {
                e = aggregateException.InnerException;
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

            return false;
        }

        /// <summary>
        /// Considers <see cref="SocketException"/> or <see cref="HttpRequestException"/> as possible
        /// transient errors as well as these exceptions nested within an <see cref="AggregateException"/>.
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
    }
}
