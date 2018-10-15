//-----------------------------------------------------------------------------
// FILE:	    QueryChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using EasyNetQ;
using EasyNetQ.DI;
using EasyNetQ.Logging;
using EasyNetQ.Management.Client;
using EasyNetQ.Topology;

using RabbitMQ;
using RabbitMQ.Client;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Net;

namespace Neon.HiveMQ
{
    /// <summary>
    /// <para>
    /// Implements query/response messaging operations for a <see cref="HiveBus"/>.  
    /// Message producers and consumers each need to declare a channel with the 
    /// same name by calling one of the <see cref="HiveBus"/> to be able to
    /// broadcast and consume messages.
    /// </para>
    /// <note>
    /// <see cref="QueryChannel"/> has nothing to do with an underlying
    /// RabbitMQ channel.  These are two entirely different concepts.
    /// </note>
    /// </summary>
    public class QueryChannel : Channel
    {
        private static readonly TimeSpan defaultTimeout = TimeSpan.FromSeconds(15);

        private IQueue      queue;
        private IExchange   exchange;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hiveBus">The <see cref="HiveBus"/>.</param>
        /// <param name="name">The channel name.</param>
        /// <param name="durable">
        /// Optionally specifies that the channel should survive message cluster restarts.  
        /// This defaults to <c>false</c>.
        /// </param>
        /// <param name="exclusive">
        /// Optionally specifies that this channel instance will exclusively receive
        /// messages from the queue.  This defaults to <c>false</c>.
        /// </param>
        /// <param name="autoDelete">
        /// Optionally specifies that channel should be automatically deleted when the
        /// last consumer is removed.
        /// </param>
        /// <param name="messageTTL">
        /// <para>
        /// Optionally specifies the maximum time a message can remain in the channel before 
        /// being deleted.  This defaults to <c>null</c> which disables this feature.
        /// </para>
        /// <note>
        /// The maximum possible TTL is about <b>24.855 days</b>.
        /// </note>
        /// </param>
        /// <param name="maxLength">
        /// Optionally specifies the maximum number of messages that can be waiting in the channel
        /// before messages at the front of the channel will be deleted.  This defaults 
        /// to unconstrained.
        /// </param>
        /// <param name="maxLengthBytes">
        /// Optionally specifies the maximum total bytes of messages that can be waiting in 
        /// the channel before messages at the front of the channel will be deleted.  This 
        /// defaults to unconstrained.
        /// </param>
        internal QueryChannel(
            HiveBus     hiveBus, 
            string      name,
            bool        durable = false,
            bool        exclusive = false,
            bool        autoDelete = false,
            TimeSpan?   messageTTL = null,
            int?        maxLength = null,
            int?        maxLengthBytes = null)

            : base(hiveBus, name)
        {
            Covenant.Requires<ArgumentNullException>(hiveBus != null);
            Covenant.Requires<ArgumentException>(maxLength == null || maxLength.Value > 0);
            Covenant.Requires<ArgumentException>(maxLengthBytes == null || maxLengthBytes.Value > 0);

            throw new NotImplementedException("$todo(jeff.lill): Implement this.");
        }

        /// <summary>
        /// Opens the channel so that messages can be published and consumed.  This must be
        /// called before a channel is usable, generally after you've added any message
        /// consumers.
        /// </summary>
        /// <returns>The channel instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the channel has already been opened.</exception>
        public new QueryChannel Open()
        {
            throw new NotImplementedException();

            //EnsureNotOpened();

            //StartListening(GetQueue());
            //base.Open();

            //return this;
        }

        /// <summary>
        /// Synchronously sends a query message on the channel and waits for a response.  
        /// </summary>
        /// <typeparam name="TRequest">The request message type.</typeparam>
        /// <typeparam name="TResponse">The response message type.</typeparam>
        /// <param name="request">The request message.</param>
        /// <param name="timeout">The maximum time to wait (defaults to <b>15 seconds</b>).</param>
        /// <returns>The response message.</returns>
        /// <exception cref="TimeoutException">Thrown if the timeout expired before a response was received.</exception>
        /// <remarks>
        /// <note>
        /// Synchronous queries are not particularily efficient and their use
        /// should be restricted to situations where query traffic will be low.
        /// We recommend that most applications, especially services, use
        /// <see cref="QueryAsync{TQuery, TResponse}(TQuery, TimeSpan, CancellationToken)"/>
        /// instead.
        /// </note>
        /// </remarks>
        public TResponse Query<TRequest, TResponse>(TRequest request, TimeSpan timeout = default)
            where TRequest : class, new()
            where TResponse : class, new()
        {
            Covenant.Requires<ArgumentNullException>(request != null);
            Covenant.Requires<ArgumentException>(timeout >= TimeSpan.Zero);

            if (timeout == TimeSpan.Zero)
            {
                timeout = defaultTimeout;
            }

            EnsureOpened();
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously sends a query message on the channel and waits for a response.
        /// </summary>
        /// <typeparam name="TRequest">The request message type.</typeparam>
        /// <typeparam name="TResponse">The response message type.</typeparam>
        /// <param name="request">The request message.</param>
        /// <param name="timeout">The maximum time to wait (defaults to <b>15 seconds</b>).</param>
        /// <param name="cancellationToken">The optional cancellation token.</param>
        /// <returns>The response message.</returns>
        /// <exception cref="TimeoutException">Thrown if the timeout expired before a response was received.</exception>
        public async Task<TResponse> QueryAsync<TRequest, TResponse>(TRequest request, TimeSpan timeout = default, CancellationToken cancellationToken = default)
            where TRequest : class, new()
            where TResponse : class, new()
        {
            Covenant.Requires<ArgumentNullException>(request != null);
            Covenant.Requires<ArgumentException>(timeout >= TimeSpan.Zero);

            if (timeout == TimeSpan.Zero)
            {
                timeout = defaultTimeout;
            }

            EnsureOpened();
            await Task.CompletedTask;
            throw new NotImplementedException();
        }

        /// <summary>
        /// Synchronously handles a query request.
        /// </summary>
        /// <typeparam name="TRequest">The request message type.</typeparam>
        /// <typeparam name="TResponse">The response message type.</typeparam>
        /// <param name="request">The request message.</param>
        /// <param name="onRequest">The synchronous request handler.</param>
        /// <exception cref="QueryException">Thrown when the remote handler throws an exception.</exception>
        public void Receive<TRequest, TResponse>(TRequest request, Func<TRequest, TResponse> onRequest)
            where TRequest : class, new()
            where TResponse : class, new()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Asynchronously handles a query request.  This override also
        /// passes an additional <see cref="ConsumerContext"/> parameter
        /// to the callback.
        /// </summary>
        /// <typeparam name="TRequest">The request message type.</typeparam>
        /// <typeparam name="TResponse">The response message type.</typeparam>
        /// <param name="request">The request message.</param>
        /// <param name="onRequest">The asynchronous request handler.</param>
        /// <exception cref="QueryException">Thrown when the remote handler throws an exception.</exception>
        public void Receive<TRequest, TResponse>(TRequest request, Func<TRequest, ConsumerContext, Task<TResponse>> onRequest)
            where TRequest : class, new()
            where TResponse : class, new()
        {
            throw new NotImplementedException();
        }
    }
}
