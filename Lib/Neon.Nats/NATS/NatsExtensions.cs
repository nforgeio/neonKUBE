//-----------------------------------------------------------------------------
// FILE:	    NatsExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
// COPYRIGHT:   Copyright (c) 2015-2018 The NATS Authors (method comments)
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using NATS.Client;
using STAN.Client;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.Net;

namespace NATS.Client
{
    /// <summary>
    /// Implements handy extension methods.
    /// </summary>
    public static class NatsExtensions
    {
        //---------------------------------------------------------------------
        // IConnection extensions

        /// <summary>
        /// Publishes an <see cref="IGeneratedType"/> instance to the given <paramref name="subject"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NATS implements a publish-subscribe message distribution model. NATS publish subscribe is a
        /// one-to-many communication. A publisher sends a message on a subject. Any active subscriber listening
        /// on that subject receives the message. Subscribers can register interest in wildcard subjects.
        /// </para>
        /// <para>
        /// In the basic NATS platfrom, if a subscriber is not listening on the subject (no subject match),
        /// or is not acive when the message is sent, the message is not recieved. NATS is a fire-and-forget
        /// messaging system. If you need higher levels of service, you can either use NATS Streaming, or build the
        /// additional reliability into your client(s) yourself.
        /// </para>
        /// </remarks>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        public static void Publish(this IConnection connection, string subject, IGeneratedType data)
        {
            Covenant.Requires<ArgumentNullException>(data != null);

            connection.Publish(subject, data.ToBytes());
        }

        /// <summary>
        /// Publishes an <see cref="IGeneratedType"/> instance to the given <paramref name="subject"/>.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="reply">An optional reply subject.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        /// <seealso cref="IConnection.Publish(string, byte[])"/>
        public static void Publish(this IConnection connection, string subject, string reply, IGeneratedType data)
        {
            Covenant.Requires<ArgumentNullException>(data != null);

            connection.Publish(subject, reply, data.ToBytes());
        }

        /// <summary>
        /// Sends a request payload and returns the response <see cref="Msg"/>, or throws
        /// <see cref="NATSTimeoutException"/> if the <paramref name="timeout"/> expires.
        /// </summary>
        /// <remarks>
        /// <typeparamref name="TRequest">The request message type.</typeparamref>
        /// <typeparamref name="TResponse">The response message type.</typeparamref>
        /// This method will create an unique inbox for this request, sharing a single
        /// subscription for all replies to this <see cref="IConnection"/> instance. However, if 
        /// <see cref="Options.UseOldRequestStyle"/> is set, each request will have its own underlying subscription. 
        /// The old behavior is not recommended as it may cause unnecessary overhead on connected NATS servers.
        /// </remarks>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        /// <param name="timeout">The number of milliseconds to wait.</param>
        /// <returns>A <see cref="Msg"/> with the response from the NATS server.</returns>
        /// <seealso cref="IConnection.Request(string, byte[])"/>
        public static Msg<TResponse> Request<TRequest, TResponse>(this IConnection connection, string subject, TRequest data, int timeout)
            where TRequest : class, IGeneratedType, new()
            where TResponse : class, IGeneratedType, new()
        {
            Covenant.Requires<ArgumentNullException>(data != null);

            var response = connection.Request(subject, data.ToBytes(), timeout);
            var payload  = GeneratedTypeFactory.CreateFrom<TResponse>(response.Data);

            return new Msg<TResponse>(response.Subject, response.Reply, payload)
            {
                ArrivalSubscription = response.ArrivalSubcription
            };
        }

        /// <summary>
        /// Sends a request payload and returns the response <see cref="Msg"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NATS supports two flavors of request-reply messaging: point-to-point or one-to-many. Point-to-point
        /// involves the fastest or first to respond. In a one-to-many exchange, you set a limit on the number of 
        /// responses the requestor may receive and instead must use a subscription (<see cref="ISubscription.AutoUnsubscribe(int)"/>).
        /// In a request-response exchange, publish request operation publishes a message with a reply subject expecting
        /// a response on that reply subject.
        /// </para>
        /// <para>
        /// This method will create an unique inbox for this request, sharing a single
        /// subscription for all replies to this <see cref="IConnection"/> instance. However, if 
        /// <see cref="Options.UseOldRequestStyle"/> is set, each request will have its own underlying subscription. 
        /// The old behavior is not recommended as it may cause unnecessary overhead on connected NATS servers.
        /// </para>
        /// </remarks>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        /// <returns>A <see cref="Msg"/> with the response from the NATS server.</returns>
        public static Msg<TResponse> Request<TRequest, TResponse>(this IConnection connection, string subject, TRequest data)
            where TRequest : class, IGeneratedType, new()
            where TResponse : class, IGeneratedType, new()
        {
            Covenant.Requires<ArgumentNullException>(data != null);

            var response = connection.Request(subject, data.ToBytes());
            var payload  = GeneratedTypeFactory.CreateFrom<TResponse>(response.Data);

            return new Msg<TResponse>(response.Subject, response.Reply, payload)
            {
                ArrivalSubscription = response.ArrivalSubcription
            };
        }

        /// <summary>
        /// Asynchronously sends a request payload and returns the response <see cref="Msg"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// NATS supports two flavors of request-reply messaging: point-to-point or one-to-many. Point-to-point
        /// involves the fastest or first to respond. In a one-to-many exchange, you set a limit on the number of 
        /// responses the requestor may receive and instead must use a subscription (<see cref="ISubscription.AutoUnsubscribe(int)"/>).
        /// In a request-response exchange, publish request operation publishes a message with a reply subject expecting
        /// a response on that reply subject.
        /// </para>
        /// <para>
        /// This method will create an unique inbox for this request, sharing a single
        /// subscription for all replies to this <see cref="IConnection"/> instance. However, if 
        /// <see cref="Options.UseOldRequestStyle"/> is set, each request will have its own underlying subscription. 
        /// The old behavior is not recommended as it may cause unnecessary overhead on connected NATS servers.
        /// </para>
        /// </remarks>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        /// <param name="timeout">Optional timeout in milliseconds.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>A <see cref="Msg"/> with the response from the NATS server.</returns>
        public static async Task<Msg<TResponse>> RequestAsync<TRequest, TResponse>(
            this IConnection    connection, 
            string              subject, 
            TRequest            data, 
            int                 timeout = 0,
            CancellationToken   token = default)

            where TRequest : class, IGeneratedType, new()
            where TResponse : class, IGeneratedType, new()
        {
            Covenant.Requires<ArgumentNullException>(data != null);

            Msg response;

            if (timeout == 0)
            {
                response = await connection.RequestAsync(subject, data.ToBytes(), token);
            }
            else
            {
                response = await connection.RequestAsync(subject, data.ToBytes(), timeout, token);
            }

            var payload  = GeneratedTypeFactory.CreateFrom<TResponse>(response.Data);

            return new Msg<TResponse>(response.Subject, response.Reply, payload)
            {
                ArrivalSubscription = response.ArrivalSubcription
            };
        }
    }
}
