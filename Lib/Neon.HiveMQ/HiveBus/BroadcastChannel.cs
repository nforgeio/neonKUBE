//-----------------------------------------------------------------------------
// FILE:	    BroadcastChannel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;
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
    /// Implements broadcast messaging operations for a <see cref="HiveBus"/>.  
    /// Message producers and consumers each need to declare a channel with the 
    /// same name by calling one of the <see cref="HiveBus"/> to be able to
    /// broadcast and consume messages.
    /// </para>
    /// <note>
    /// <see cref="BroadcastChannel"/> has nothing to do with an underlying
    /// RabbitMQ channel.  These are two entirely different concepts.
    /// </note>
    /// </summary>
    /// <remarks>
    /// <para>
    /// This channel provides a way to broadcast messages to all listening
    /// consumers.  To use this class:
    /// </para>
    /// <list type="number">
    /// <item>
    /// </item>
    ///     Construct an instance call <see cref="HiveBus.GetBroadcastChannel(string, bool, bool, TimeSpan?, int?, int?, bool)"/>,
    ///     passing the channel name any required optional parameters to control
    ///     the channel durability, exclusivity, message TTL, and length constraints.
    /// <item>
    ///     <para>
    ///     Call <see cref="Consume{TMessage}(Action{TMessage})"/>,
    ///     <see cref="Consume{TMessage}(Action{TMessage, MessageProperties, ConsumerContext})"/>,
    ///     <see cref="ConsumeAsync{TMessage}(Func{TMessage, Task})"/>, or
    ///     <see cref="ConsumeAsync{TMessage}(Func{TMessage, MessageProperties, ConsumerContext, Task})"/>
    ///     to register synchronous or asunchronous message consumption callbacks for each of the message
    ///     types you may receive.  Your callback will be passed the received message and optionally
    ///     the message envelope with the raw message bytes and a <see cref="ConsumerContext"/>.
    ///     </para>
    ///     <note>
    ///     The consume methods all return the channel instance to support
    ///     the fluent coding style.
    ///     </note>
    /// </item>
    /// <item>
    ///     <para>
    ///     Call <see cref="Open()"/> to connect the channel so it will begin receiving inbound
    ///     messages and also enables the publication of outbound messages.
    ///     </para>
    ///     <note>
    ///     <b>WARNING:</b> Channel instances that will consume messages should 
    ///     generally configure all consumers before calling <see cref="Open()"/>
    ///     to ensure that no messages are indavertently lost.  It is possible to
    ///     add consumers after the channel has been started but the channel will 
    ///     begin receiving and processing messages when <see cref="Open()"/> is 
    ///     called and messages without a registered consumer will be silently 
    ///     dropped.  This means that messages received between the time the 
    ///     channel was opened and the consumer was registered will be lost.
    ///     </note>
    /// </item>
    /// <item>
    ///     Call <see cref="Publish{TMessage}(TMessage)"/> or <see cref="PublishAsync{TMessage}(TMessage)"/>
    ///     to send a message.  This will result in one of the consumer callbacks registered
    ///     for the type to be called.
    /// </item>
    /// </list>
    /// <note>
    /// We recommend that most applications, particularily services, use the
    /// asynchronous versions of the publication and consumption APIs for better
    /// performance under load.
    /// </note>
    /// <para><b>Implementation:</b></para>
    /// <para>
    /// This is currently implemented by creating a fanout exchange using
    /// the channel name.  Then each channel instance is assigned an internal UUID
    /// and then each channel creates an auto-delete queue named like:
    /// </para>
    /// <code>
    /// CHANNEL-UUID
    /// </code>
    /// <para>
    /// Each channel also creates an binding that routes messages from the 
    /// exchange to the specific channel created by the instance.  This
    /// implements the broadcast semantics.
    /// </para>
    /// </remarks>
    public class BroadcastChannel : Channel
    {
        private string      sourceID;           // Unique channel ID
        private IQueue      queue;
        private IExchange   exchange;
        private bool        publishOnly;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="hiveBus">The <see cref="HiveBus"/>.</param>
        /// <param name="name">The channel name.</param>
        /// <param name="durable">
        /// Optionally specifies that the channel should survive message cluster restarts.  
        /// This defaults to <c>false</c>.
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
        /// <param name="publishOnly">
        /// Optionally specifies that the channel instance returned will only be able
        /// to publish messages and not consume them.  Enabling this avoid the creation
        /// of a queue that will unnecessary for this situation.
        /// </param>
        internal BroadcastChannel(
            HiveBus     hiveBus, 
            string      name,
            bool        durable = false,
            bool        autoDelete = false,
            TimeSpan?   messageTTL = null,
            int?        maxLength = null,
            int?        maxLengthBytes = null,
            bool        publishOnly = false)

            : base(hiveBus, name)
        {
            Covenant.Requires<ArgumentNullException>(hiveBus != null);
            Covenant.Requires<ArgumentException>(maxLength == null || maxLength.Value > 0);
            Covenant.Requires<ArgumentException>(maxLengthBytes == null || maxLengthBytes.Value > 0);

            this.sourceID    = Guid.NewGuid().ToString("D").ToLowerInvariant();
            this.publishOnly = publishOnly;

            exchange = EasyBus.ExchangeDeclare(
                name: name, 
                type: EasyNetQ.Topology.ExchangeType.Fanout, 
                passive: false,
                durable: durable, 
                autoDelete: autoDelete,
                alternateExchange: null,
                delayed: false);

            queue = EasyBus.QueueDeclare(
                name: $"{name}-{sourceID}",
                passive: false,
                durable: durable,
                exclusive: true,
                autoDelete: true,
                perQueueMessageTtl: HiveBus.TTLToMilliseconds(messageTTL),
                maxLength: maxLength,
                maxLengthBytes: maxLengthBytes);

            EasyBus.Bind(exchange, queue, routingKey: "#");
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
        /// Opens the channel so that messages can be published and consumed.  This must be
        /// called before a channel is usable, generally after you've added any message
        /// consumers.
        /// </summary>
        /// <returns>The channel instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the channel has already been opened.</exception>
        public new BroadcastChannel Open()
        {
            EnsureNotOpened();

            StartListening(GetQueue());
            base.Open();

            return this;
        }

        /// <summary>
        /// Synchronously broadcasts a message to the channel consumers.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        public void Publish<TMessage>(TMessage message)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            EnsureOpened();
            base.Publish(GetExchange(), message, routingKey: Name);
        }

        /// <summary>
        /// Asynchronously broadcasts a message to the channel consumers.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        public async Task PublishAsync<TMessage>(TMessage message)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            EnsureOpened();
            await base.PublishAsync(GetExchange(), message, routingKey: Name);
        }

        /// <summary>
        /// Ensures that <see cref="publishOnly"/> was not specified when creating the channel.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the channel was created with <c>publishOnly=true</c>.</exception>
        private void EnsureNotPublishOnly()
        {
            if (publishOnly)
            {
                throw new InvalidOperationException($"Channel was created with [{nameof(publishOnly)}=true] so message consumption is disabled.");
            }
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are received by the channel.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <returns>The channel instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the channel was created with <c>publishOnly=true</c>.</exception>
        /// <remarks>
        /// <para>
        /// The synchronous <paramref name="onMessage"/> callback is passed the 
        /// received message as a single argument.
        /// </para>
        /// <note>
        /// This method is suitable for many graphical client applications but 
        /// should generally be avoided for high performance service applications
        /// which should register an asynchronous callback.
        /// </note>
        /// <note>
        /// The consume methods all return the channel instance to support
        /// the fluent coding style.
        /// </note>
        /// </remarks>
        public BroadcastChannel Consume<TMessage>(Action<TMessage> onMessage)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            EnsureNotPublishOnly();
            AddConsumer(new Consumer<TMessage>(onMessage));
            return this;
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are received by the channel.  This override
        /// also passes the additional <see cref="MessageProperties"/> and
        /// <see cref="ConsumerContext"/> parameters to the callback.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <returns>The channel instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the channel was created with <c>publishOnly=true</c>.</exception>
        /// <remarks>
        /// <para>
        /// The synchronous <paramref name="onMessage"/> callback is passed the
        /// received message, the message envelope including the raw message bytes,
        /// and additional information about the message delivery as three
        /// arguments.
        /// </para>
        /// <note>
        /// This method is suitable for many graphical client applications but 
        /// should generally be avoided for high performance service applications
        /// which should register an asynchronous callback.
        /// </note>
        /// <note>
        /// The consume methods all return the channel instance to support
        /// the fluent coding style.
        /// </note>
        /// </remarks>
        public BroadcastChannel Consume<TMessage>(Action<TMessage, MessageProperties, ConsumerContext> onMessage)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            EnsureNotPublishOnly();
            AddConsumer(new Consumer<TMessage>(onMessage));
            return this;
        }

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are received by the channel.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <returns>The channel instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the channel was created with <c>publishOnly=true</c>.</exception>
        /// <remarks>
        /// <para>
        /// The asynchronous <paramref name="onMessage"/> callback is passed the 
        /// received message as a single argument.
        /// </para>
        /// <note>
        /// Most applications (especially services) should register asynchronous
        /// callbacks using this method for better performance under load.
        /// </note>
        /// <note>
        /// The consume methods all return the channel instance to support
        /// the fluent coding style.
        /// </note>
        /// </remarks>
        public BroadcastChannel ConsumeAsync<TMessage>(Func<TMessage, Task> onMessage)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            EnsureNotPublishOnly();
            AddConsumer(new Consumer<TMessage>(onMessage));
            return this;
        }

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are received by the channel.  This override
        /// also passes the additional <see cref="MessageProperties"/> and
        /// <see cref="ConsumerContext"/> parameters to the callback.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <returns>The channel instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the channel was created with <c>publishOnly=true</c>.</exception>
        /// <remarks>
        /// <para>
        /// The asynchronous <paramref name="onMessage"/> callback is passed the
        /// received message, the message envelope including the raw message bytes,
        /// and additional information about the message delivery as three
        /// arguments.
        /// </para>
        /// <note>
        /// Most applications (especially services) should register asynchronous
        /// callbacks using this method for better performance under load.
        /// </note>
        /// <note>
        /// The consume methods all return the channel instance to support
        /// the fluent coding style.
        /// </note>
        /// </remarks>
        public BroadcastChannel ConsumeAsync<TMessage>(Func<TMessage, MessageProperties, ConsumerContext, Task> onMessage)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            EnsureNotPublishOnly();
            AddConsumer(new Consumer<TMessage>(onMessage));
            return this;
        }
    }
}
