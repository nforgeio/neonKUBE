//-----------------------------------------------------------------------------
// FILE:	    BasicChannel.cs
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
    /// Implements basic messaging operations for a <see cref="HiveBus"/>.  
    /// Message producers and consumers each need to declare a channel with the 
    /// same name by calling one of the <see cref="HiveBus"/> to be able to
    /// publish and consume messages.
    /// </para>
    /// <note>
    /// <see cref="BasicChannel"/> has nothing to do with an underlying
    /// RabbitMQ channel.  These are two entirely different concepts.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This channel provides a way to distribute work across one or more
    /// message consumers.  Each message published to the channel will be
    /// delivered to one of the consumers.  To use this class:
    /// </para>
    /// <list type="number">
    /// <item>
    /// Construct an instance call <see cref="HiveBus.CreateBasicChannel(string, bool, bool, bool, TimeSpan?, int?, int?)"/>,
    /// passing the channel name any required optional parameters to control
    /// the channel durability, exclusivity, message TTL, and length constraints.
    /// </item>
    /// <item>
    /// Call <see cref="Consume{TMessage}(Action{IMessage{TMessage}})"/>,
    /// <see cref="Consume{TMessage}(Action{IMessage{TMessage}, ConsumerContext})"/>,
    /// <see cref="Consume{TMessage}(Func{IMessage{TMessage}, Task}, bool)"/>, or
    /// <see cref="Consume{TMessage}(Func{IMessage{TMessage}, ConsumerContext, Task}, bool)"/>
    /// to register message consumption callbacks for each of the message types you
    /// need to handle.  Your callback will be passed an <see cref="IMessage{TMessage}"/>
    /// parameter as the message envelope.  Your message can be accessed via  <see cref="IMessage.GetBody()"/>
    /// There are method overrides that register both synchronous and asynchronous callbacks as well
    /// as callbacks that accept the a <see cref="ConsumerContext"/> that provides additional 
    /// information about the message as well as extended message related operations.
    /// </item>
    /// <item>
    /// Call <see cref="Publish{TMessage}(TMessage)"/> or <see cref="PublishAsync{TMessage}(TMessage)"/>
    /// to send a message.  This will result in one of the consumer callbacks registered
    /// for the type to be called.
    /// </item>
    /// </list>
    /// <note>
    /// We recommend that most applications, particularily services, use the
    /// asynchronous versions of the publication and consumption APIs for better
    /// performance under load.
    /// </note>
    /// <para><b>Implementation:</b></para>
    /// <para>
    /// This is currently implemented using the built-in direct exchange routing
    /// to a single underyling RabbitMQ queue created using the channel name.
    /// </para>
    /// </remarks>
    public class BasicChannel : Channel
    {
        private IQueue      queue;
        private IExchange   exchange;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="messageBus">The <see cref="HiveBus"/>.</param>
        /// <param name="name">The channel name (maximum of 250 characters).</param>
        /// <param name="durable">
        /// Optionally specifies that the channel should survive message cluster restarts.  
        /// This defaults to <c>false</c>.
        /// </param>
        /// <param name="exclusive">
        /// Optionally specifies that this channel instance will exclusively receive
        /// messages from the queue.  This defaults to <c>false</c>.
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
        internal BasicChannel(
            HiveBus     messageBus, 
            string      name,
            bool        durable = false,
            bool        exclusive = false,
            bool        autoDelete = false,
            TimeSpan?   messageTTL = null,
            int?        maxLength = null,
            int?        maxLengthBytes = null)

            : base(messageBus, name)
        {
            Covenant.Requires<ArgumentNullException>(messageBus != null);

            queue = EasyBus.QueueDeclare(
                name: name,
                passive: false,
                durable: durable,
                exclusive: exclusive,
                autoDelete: autoDelete,
                perQueueMessageTtl: messageTTL.HasValue ? (int?)messageTTL.Value.TotalMilliseconds : null,
                maxLength: maxLength,
                maxLengthBytes: maxLengthBytes);

            // We're going to use the default exchange which automatically
            // routes messages to the queue by name.

            exchange = Exchange.GetDefault();
        }

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            queue    = null;
            exchange = null;

            base.Dispose(disposing);
        }

        /// <summary>
        /// Ensures that the channel isn't disposed and returns the queue instance.
        /// </summary>
        /// <returns>The queue instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the channel is disposed.</exception>
        private IQueue GetQueue()
        {
            var queue = this.queue;

            if (queue == null)
            {
                throw new ObjectDisposedException(nameof(BasicChannel));
            }

            return queue;
        }

        /// <summary>
        /// Ensures that the channel isn't disposed and returns the exchange instance.
        /// </summary>
        /// <returns>The exchange instance.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the channel is disposed.</exception>
        private IExchange GetExchange()
        {
            var exchange = this.exchange;

            if (exchange == null)
            {
                throw new ObjectDisposedException(nameof(BasicChannel));
            }

            return exchange;
        }

        /// <summary>
        /// Synchronously publishes a message to the channel.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        public void Publish<TMessage>(TMessage message)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            var exchange = GetExchange();
            var envelope = new Message<TMessage>(message);

            EasyBus.PublishAsync(exchange, Name, mandatory: false, message: envelope).Wait();
        }

        /// <summary>
        /// Asynchronously publishes a message to the channel.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        public async Task PublishAsync<TMessage>(TMessage message)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            var exchange = GetExchange();
            var envelope = new Message<TMessage>(message);

            await EasyBus.PublishAsync(exchange, Name, mandatory: false, message: envelope);
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
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
        public Subscription Consume<TMessage>(Action<IMessage<TMessage>> onMessage) 
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            var queue        = GetQueue();
            var subscription = EasyBus.Consume<TMessage>(queue,
                (envelope, info) =>
                {
                    onMessage(envelope);
                });

            return base.AddSubscription(new Subscription(this, typeof(TMessage), subscription));
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  This override
        /// also passes an additional <see cref="ConsumerContext"/> parameter to
        /// the callback.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
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
        public Subscription Consume<TMessage>(Action<IMessage<TMessage>, ConsumerContext> onMessage) 
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            var queue = GetQueue();
            var subscription = EasyBus.Consume<TMessage>(queue,
                (envelope, info) =>
                {
                    onMessage(envelope, ConsumerContext.Create(info));
                });

            return base.AddSubscription(new Subscription(this, typeof(TMessage), subscription));
        }

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <param name="exclusive">
        /// Optionally indicates that this is is to be the exclusive consumer 
        /// of messages on the channel.  This defaults to <c>false</c>.
        /// </param>
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
        public Subscription Consume<TMessage>(Func<IMessage<TMessage>, Task> onMessage, bool exclusive = false)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            var queue = GetQueue();
            var subscription = EasyBus.Consume<TMessage>(queue,
                async (envelope, info) =>
                {
                    await onMessage(envelope);
                });

            return base.AddSubscription(new Subscription(this, typeof(TMessage), subscription));
        }

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  This override
        /// also passes an additional <see cref="ConsumerContext"/> parameter to the
        /// callback.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <param name="exclusive">
        /// Optionally indicates that this is is to be the exclusive consumer 
        /// of messages on the channel.  This defaults to <c>false</c>.
        /// </param>
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
        public Subscription Consume<TMessage>(Func<IMessage<TMessage>, ConsumerContext, Task> onMessage, bool exclusive = false) 
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            var queue = GetQueue();
            var subscription = EasyBus.Consume<TMessage>(queue,
                async (envelope, info) =>
                {
                    await onMessage(envelope, ConsumerContext.Create(info));
                });

            return base.AddSubscription(new Subscription(this, typeof(TMessage), subscription));
        }
    }
}
