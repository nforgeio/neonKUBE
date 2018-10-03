//-----------------------------------------------------------------------------
// FILE:	    BroadcastChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
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
    /// Implements broadcast messaging operations for a <see cref="MessageBus"/>.  
    /// Message producers and consumers each need to declare a channel with the 
    /// same name by calling one of the <see cref="MessageBus"/> to be able to
    /// broadcast and consume messages.
    /// </para>
    /// <note>
    /// <see cref="BroadcastChannel"/> has nothing to do with an underlying
    /// RabbitMQ channel.  These are two entirely different concepts.
    /// </note>
    /// </summary>
    public class BroadcastChannel : Channel
    {
        private string instanceId = Guid.NewGuid().ToString("D").ToLowerInvariant();

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="messageBus">The <see cref="MessageBus"/>.</param>
        /// <param name="name">The channel name.</param>
        /// <param name="durable">
        /// Optionally specifies that the channel should survive message cluster restarts.  
        /// This defaults to <c>false</c>.
        /// </param>
        /// <param name="autoDelete">
        /// Optionally specifies that the channel should be deleted once all consumers have 
        /// disconnected.  This defaults to <c>false</c>.
        /// </param>
        /// <param name="messageTTL">
        /// Optionally specifies the maximum time a message can remain in the channel before 
        /// being deleted.  This defaults to <c>null</c> which disables this feature.
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
        internal BroadcastChannel(MessageBus messageBus, string name,
            bool durable = false,
            bool autoDelete = false,
            TimeSpan? messageTTL = null,
            int? maxLength = null,
            int? maxLengthBytes = null)

            : base(messageBus, name)
        {
            Covenant.Requires<ArgumentNullException>(messageBus != null);
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
        }

        /// <summary>
        /// Synchronously broadcasts a message to the channel consumers.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        public void Broadcast<TMessage>(TMessage message)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            lock (SyncLock)
            {
                CheckDisposed();
            }
        }

        /// <summary>
        /// Asynchronously broadcasts a message to the channel consumers.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        public Task BroadcastAsync<TMessage>(TMessage message)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            lock (SyncLock)
            {
                CheckDisposed();
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <param name="filterSelf">Optionally filter messages broadcast by this channel instance.</param>
        /// <returns>A <see cref="Subscription"/> instance.</returns>
        /// <remarks>
        /// <note>
        /// This method is suitable for many graphical client applications but 
        /// should generally be avoided for high performance service applications
        /// which should register an asynchronous callback.
        /// </note>
        /// <para>
        /// To cancel the subscription, dispose the <see cref="Subscription"/>
        /// returned by this method.
        /// </para>
        /// </remarks>
        public Subscription Consume<TMessage>(Action<IMessage<TMessage>> onMessage, bool filterSelf = false) 
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            lock (SyncLock)
            {
                CheckDisposed();
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  This override
        /// also passes an additional <see cref="ConsumerContext"/> parameter to the
        /// callback.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <param name="filterSelf">Optionally filter messages broadcast by this channel instance.</param>
        /// <returns>n <see cref="Subscription"/> instance.</returns>
        /// <remarks>
        /// <note>
        /// This method is suitable for many graphical client applications but 
        /// should generally be avoided for high performance service applications
        /// which should register an asynchronous callback.
        /// </note>
        /// <para>
        /// To cancel the subscription, dispose the <see cref="Subscription"/>
        /// returned by this method.
        /// </para>
        /// </remarks>
        public Subscription Consume<TMessage>(Action<IMessage<TMessage>, ConsumerContext> onMessage, bool filterSelf = false) 
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            lock (SyncLock)
            {
                CheckDisposed();
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <param name="filterSelf">Optionally filter messages broadcast by this channel instance.</param>
        /// <returns>A <see cref="Subscription"/> instance.</returns>
        /// <remarks>
        /// <note>
        /// Most applications (especially services) should register asynchronous
        /// callbacks using this method for better performance under load.
        /// </note>
        /// <para>
        /// To cancel the subscription, dispose the <see cref="Subscription"/>
        /// returned by this method.
        /// </para>
        /// </remarks>
        public Subscription Consume<TMessage>(Func<IMessage<TMessage>, Task> onMessage, bool filterSelf = false) 
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            lock (SyncLock)
            {
                CheckDisposed();
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  This override
        /// also passes an additional <see cref="ConsumerContext"/> parameter to the
        /// callback.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <param name="filterSelf">Optionally filter messages broadcast by this channel instance.</param>
        /// <returns>A <see cref="Subscription"/> instance.</returns>
        /// <remarks>
        /// <note>
        /// Most applications (especially services) should register asynchronous
        /// callbacks using this method for better performance under load.
        /// </note>
        /// <para>
        /// To cancel the subscription, dispose the <see cref="Subscription"/>
        /// returned by this method.
        /// </para>
        /// </remarks>
        public Subscription Consume<TMessage>(Func<IMessage<TMessage>, ConsumerContext, Task> onMessage, bool filterSelf = false)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            lock (SyncLock)
            {
                CheckDisposed();
            }

            throw new NotImplementedException();
        }
    }
}
