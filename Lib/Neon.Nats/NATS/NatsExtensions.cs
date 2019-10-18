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
using Neon.Tasks;

namespace NATS.Client
{
    /// <summary>
    /// Implements handy NATS extension methods.
    /// </summary>
    public static class NatsExtensions
    {
        //---------------------------------------------------------------------
        // IConnection extensions

        /// <summary>
        /// Publishes an <see cref="IRoundtripData"/> instance to the given <paramref name="subject"/>.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
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
        public static void Publish(this IConnection connection, string subject, IRoundtripData data)
        {
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));

            connection.Publish(subject, data.ToBytes());
        }

        /// <summary>
        /// Publishes an <see cref="IRoundtripData"/> instance to the given <paramref name="subject"/>.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="reply">An optional reply subject.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        /// <seealso cref="IConnection.Publish(string, byte[])"/>
        public static void Publish(this IConnection connection, string subject, string reply, IRoundtripData data)
        {
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));

            connection.Publish(subject, reply, data.ToBytes());
        }

        /// <summary>
        /// Sends a request payload and returns the response <see cref="Msg"/>, or throws
        /// <see cref="NATSTimeoutException"/> if the <paramref name="timeout"/> expires.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        /// <param name="timeout">The number of milliseconds to wait.</param>
        /// <returns>A <see cref="Msg"/> with the response from the NATS server.</returns>
        /// <remarks>
        /// <typeparamref name="TRequest">The request message type.</typeparamref>
        /// <typeparamref name="TResponse">The response message type.</typeparamref>
        /// This method will create an unique inbox for this request, sharing a single
        /// subscription for all replies to this <see cref="IConnection"/> instance. However, if 
        /// <see cref="Options.UseOldRequestStyle"/> is set, each request will have its own underlying subscription. 
        /// The old behavior is not recommended as it may cause unnecessary overhead on connected NATS servers.
        /// </remarks>
        /// <seealso cref="IConnection.Request(string, byte[])"/>
        public static Msg<TResponse> Request<TRequest, TResponse>(this IConnection connection, string subject, TRequest data, int timeout)
            where TRequest : class, IRoundtripData, new()
            where TResponse : class, IRoundtripData, new()
        {
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));

            var response = connection.Request(subject, data.ToBytes(), timeout);
            var payload  = RoundtripDataFactory.CreateFrom<TResponse>(response.Data);

            return new Msg<TResponse>(response.Subject, response.Reply, payload)
            {
                ArrivalSubscription = response.ArrivalSubcription
            };
        }

        /// <summary>
        /// Sends a request payload and returns the response <see cref="Msg"/>.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        /// <returns>A <see cref="Msg"/> with the response from the NATS server.</returns>
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
        public static Msg<TResponse> Request<TRequest, TResponse>(this IConnection connection, string subject, TRequest data)
            where TRequest : class, IRoundtripData, new()
            where TResponse : class, IRoundtripData, new()
        {
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));

            var response = connection.Request(subject, data.ToBytes());
            var payload  = RoundtripDataFactory.CreateFrom<TResponse>(response.Data);

            return new Msg<TResponse>(response.Subject, response.Reply, payload)
            {
                ArrivalSubscription = response.ArrivalSubcription
            };
        }

        /// <summary>
        /// Asynchronously sends a request payload and returns the response <see cref="Msg"/>.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject to publish <paramref name="data"/> to over
        /// the current connection.</param>
        /// <param name="data">The data to to publish to the connected NATS server.</param>
        /// <param name="timeout">Optional timeout in milliseconds.</param>
        /// <param name="token">Optional cancellation token.</param>
        /// <returns>A <see cref="Msg"/> with the response from the NATS server.</returns>
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
        public static async Task<Msg<TResponse>> RequestAsync<TRequest, TResponse>(
            this IConnection    connection, 
            string              subject, 
            TRequest            data, 
            int                 timeout = 0,
            CancellationToken   token = default)

            where TRequest : class, IRoundtripData, new()
            where TResponse : class, IRoundtripData, new()
        {
            await TaskContext.ResetAsync;
            Covenant.Requires<ArgumentNullException>(data != null, nameof(data));

            Msg response;

            if (timeout == 0)
            {
                response = await connection.RequestAsync(subject, data.ToBytes(), token);
            }
            else
            {
                response = await connection.RequestAsync(subject, data.ToBytes(), timeout, token);
            }

            var payload  = RoundtripDataFactory.CreateFrom<TResponse>(response.Data);

            return new Msg<TResponse>(response.Subject, response.Reply, payload)
            {
                ArrivalSubscription = response.ArrivalSubcription
            };
        }

        /// <summary>
        /// Expresses interest in the given <paramref name="subject"/> to the NATS Server.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">
        /// The subject on which to listen for messages.  The subject can have
        /// wildcards (partial: <c>*</c>, full: <c>&gt;</c>).
        /// </param>
        /// <returns>
        /// An <see cref="ISyncSubscription"/> to use to read any messages received
        /// from the NATS Server on the given <paramref name="subject"/>.
        /// </returns>
        /// <seealso cref="ISubscription.Subject"/>
        public static ISyncSubscription<TMessage> SubscribeSync<TMessage>(this IConnection connection, string subject)
            where TMessage : class, IRoundtripData, new()
        {
            return new SyncSubscription<TMessage>(connection.SubscribeSync(subject));
        }

        /// <summary>
        /// Expresses interest in the given <paramref name="subject"/> to the NATS Server.
        /// </summary>
        /// <remarks>
        /// The <see cref="IAsyncSubscription"/> returned will not start receiving messages until
        /// <see cref="IAsyncSubscription.Start"/> is called.
        /// </remarks>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">
        /// The subject on which to listen for messages. 
        /// The subject can have wildcards (partial: <c>*</c>, full: <c>&gt;</c>).</param>
        /// <returns>An <see cref="IAsyncSubscription"/> to use to read any messages received
        /// from the NATS Server on the given <paramref name="subject"/>.
        /// </returns>
        /// <seealso cref="ISubscription.Subject"/>
        public static IAsyncSubscription<TMessage> SubscribeAsync<TMessage>(this IConnection connection, string subject)
            where TMessage : class, IRoundtripData, new()
        {
            return new AsyncSubscription<TMessage>(connection.SubscribeAsync(subject));
        }

        /// <summary>
        /// Expresses interest in the given <paramref name="subject"/> to the NATS Server, and begins delivering
        /// messages to the given event handler.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">
        /// The subject on which to listen for messages.
        /// The subject can have wildcards (partial: <c>*</c>, full: <c>&gt;</c>).
        /// </param>
        /// <param name="handler">
        /// The <see cref="EventHandler{TEventArgs}"/> invoked when messages are received 
        /// on the returned <see cref="IAsyncSubscription"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncSubscription"/> to use to read any messages received
        /// from the NATS Server on the given <paramref name="subject"/>.
        /// </returns>
        /// <remarks>
        /// The <see cref="IAsyncSubscription"/> returned will start delivering messages
        /// to the event handler as soon as they are received. The caller does not have to invoke
        /// <see cref="IAsyncSubscription.Start"/>.
        /// </remarks>
        /// <seealso cref="ISubscription.Subject"/>
        public static IAsyncSubscription<TMessage> SubscribeAsync<TMessage>(this IConnection connection, string subject, EventHandler<MsgHandlerEventArgs<TMessage>> handler)
            where TMessage : class, IRoundtripData, new()
        {
            var subscription = new AsyncSubscription<TMessage>(connection.SubscribeAsync(subject));

            if (handler != null)
            {
                subscription.RoundtripMessageHandler +=
                    (sender, args) =>
                    {
                        handler.Invoke(sender, args);
                    };
            }

            return subscription;
        }

        /// <summary>
        /// Creates a synchronous queue subscriber on the given <paramref name="subject"/>.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">The subject on which to listen for messages.</param>
        /// <param name="queue">The name of the queue group in which to participate.</param>
        /// <returns>
        /// An <see cref="ISyncSubscription"/> to use to read any messages received
        /// from the NATS Server on the given <paramref name="subject"/>, as part of 
        /// the given queue group.
        /// </returns>
        /// <remarks>
        /// All subscribers with the same queue name will form the queue group and
        /// only one member of the group will be selected to receive any given message
        /// synchronously.
        /// </remarks>
        /// <seealso cref="ISubscription.Subject"/>
        /// <seealso cref="ISubscription.Queue"/>
        public static ISyncSubscription<TMessage> SubscribeSync<TMessage>(this IConnection connection, string subject, string queue)
            where TMessage : class, IRoundtripData, new()
        {
            return new SyncSubscription<TMessage>(connection.SubscribeSync(subject));
        }

        /// <summary>
        /// Creates an asynchronous queue subscriber on the given <paramref name="subject"/>.
        /// </summary>
        /// <param name="queue">The name of the queue group in which to participate.</param>
        /// <returns>
        /// An <see cref="IAsyncSubscription"/> to use to read any messages received
        /// from the NATS Server on the given <paramref name="subject"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// All subscribers with the same queue name will form the queue group and
        /// only one member of the group will be selected to receive any given message.
        /// </para>
        /// <para>
        /// The <see cref="IAsyncSubscription"/> returned will not start receiving messages until
        /// <see cref="IAsyncSubscription.Start"/> is called.
        /// </para>
        /// </remarks>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">
        /// The subject on which to listen for messages.
        /// The subject can have wildcards (partial: <c>*</c>, full: <c>&gt;</c>).
        /// </param>
        /// <seealso cref="ISubscription.Subject"/>
        /// <seealso cref="ISubscription.Queue"/>
        public static IAsyncSubscription<TMessage> SubscribeAsync<TMessage>(this IConnection connection, string subject, string queue)
            where TMessage : class, IRoundtripData, new()
        {
            return new AsyncSubscription<TMessage>(connection.SubscribeAsync(subject, queue));
        }

        /// <summary>
        /// Creates an asynchronous queue subscriber on the given <paramref name="subject"/>, and begins delivering
        /// messages to the given event handler.
        /// </summary>
        /// <param name="connection">The connection.</param>
        /// <param name="subject">
        /// The subject on which to listen for messages.
        /// The subject can have wildcards (partial: <c>*</c>, full: <c>&gt;</c>).
        /// </param>
        /// <param name="queue">The name of the queue group in which to participate.</param>
        /// <param name="handler">
        /// The <see cref="EventHandler{MsgHandlerEventArgs}"/> invoked when messages are received 
        /// on the returned <see cref="IAsyncSubscription"/>.
        /// </param>
        /// <returns>
        /// An <see cref="IAsyncSubscription"/> to use to read any messages received
        /// from the NATS Server on the given <paramref name="subject"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// All subscribers with the same queue name will form the queue group and
        /// only one member of the group will be selected to receive any given message.
        /// </para>
        /// <para>
        /// The <see cref="IAsyncSubscription"/> returned will start delivering messages
        /// to the event handler as soon as they are received. The caller does not have to invoke
        /// <see cref="IAsyncSubscription.Start"/>.
        /// </para>
        /// </remarks>
        /// <seealso cref="ISubscription.Subject"/>
        /// <seealso cref="ISubscription.Queue"/>
        public static IAsyncSubscription<TMessage> SubscribeAsync<TMessage>(this IConnection connection, string subject, string queue, EventHandler<MsgHandlerEventArgs<TMessage>> handler)
            where TMessage : class, IRoundtripData, new()
        {
            var subscription = new AsyncSubscription<TMessage>(connection.SubscribeAsync(subject, queue));

            if (handler != null)
            {
                subscription.RoundtripMessageHandler +=
                    (sender, args) =>
                    {
                        handler.Invoke(sender, args);
                    };
            }

            return subscription;
        }
    }
}
