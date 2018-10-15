//-----------------------------------------------------------------------------
// FILE:	    Channel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

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
    /// Channel base class.
    /// </summary>
    public abstract class Channel : IDisposable
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Describes the behavior of a message consumer.
        /// </summary>
        private interface IConsumer
        {
            /// <summary>
            /// Returns the message type being consumed.
            /// </summary>
            Type MessageType { get; }

            /// <summary>
            /// Asynchronously dispatches a received message to the registered callback.
            /// </summary>
            /// <param name="message">The received message.</param>
            /// <param name="properties">The message properties.</param>
            /// <param name="info">Additional delivery information.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            Task DispatchAsync(object message, MessageProperties properties, MessageReceivedInfo info);
        }

        /// <summary>
        /// Describes a message consumer.
        /// </summary>
        /// <typeparam name="TMessage">The subscribed message type.</typeparam>
        private class Consumer<TMessage> : IConsumer
            where TMessage : class, new()
        {
            private Action<TMessage>                                            syncSimpleCallback;
            private Action<TMessage, MessageProperties, ConsumerContext>        syncAdvancedCallback;
            private Func<TMessage, Task>                                        asyncSimpleCallback;
            private Func<TMessage, MessageProperties, ConsumerContext, Task>    asyncAdvancedCallback;

            /// <summary>
            /// Constructs an instance with a simple synchronous message callback.
            /// </summary>
            /// <param name="onMessage">The message callback.</param>
            public Consumer(Action<TMessage> onMessage)
            {
                Covenant.Requires<ArgumentNullException>(onMessage != null);

                syncSimpleCallback = onMessage;
            }

            /// <summary>
            /// Constructs an instance with an advanced synchronous message callback.
            /// </summary>
            /// <param name="onMessage">The message callback.</param>
            public Consumer(Action<TMessage, MessageProperties, ConsumerContext> onMessage)
            {
                Covenant.Requires<ArgumentNullException>(onMessage != null);

                syncAdvancedCallback = onMessage;
            }

            /// <summary>
            /// Constructs an instance with a simple asynchronous message callback.
            /// </summary>
            /// <param name="onMessage">The message callback.</param>
            public Consumer(Func<TMessage, Task> onMessage)
            {
                Covenant.Requires<ArgumentNullException>(onMessage != null);

                asyncSimpleCallback = onMessage;
            }

            /// <summary>
            /// Constructs an instance with an advanced asynchronous message callback.
            /// </summary>
            /// <param name="onMessage">The message callback.</param>
            public Consumer(Func<TMessage, MessageProperties, ConsumerContext, Task> onMessage)
            {
                Covenant.Requires<ArgumentNullException>(onMessage != null);

                asyncAdvancedCallback = onMessage;
            }

            /// <inheritdoc/>
            public Type MessageType => typeof(TMessage);

            /// <inheritdoc/>
            public async Task DispatchAsync(object message, MessageProperties properties, MessageReceivedInfo info)
            {
                Covenant.Requires<ArgumentNullException>(message != null);
                Covenant.Requires<ArgumentNullException>(properties != null);
                Covenant.Requires<ArgumentNullException>(info != null);

                // One of the callback fields will be non-null.  We'll call
                // that one.

                if (syncSimpleCallback != null)
                {
                    syncSimpleCallback((TMessage)message);
                }
                else if (syncAdvancedCallback != null)
                {
                    syncAdvancedCallback((TMessage)message, properties, ConsumerContext.Create(info));
                }
                else if (asyncSimpleCallback != null)
                {
                    await asyncSimpleCallback((TMessage)message);
                }
                else if (asyncAdvancedCallback != null)
                {
                    await asyncAdvancedCallback((TMessage)message, properties, ConsumerContext.Create(info));
                }
                else
                {
                    Covenant.Assert(false);
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        private ConcurrentDictionary<string, IConsumer> typeNameToConsumer;
        private IDisposable                             subscription;

        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="hiveBus">The <see cref="HiveMQ.HiveBus"/>.</param>
        /// <param name="name">The channel name.</param>
        protected Channel(HiveBus hiveBus, string name)
        {
            Covenant.Requires<ArgumentNullException>(hiveBus != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.HiveBus            = hiveBus;
            this.EasyBus            = hiveBus.EasyBus.Advanced;
            this.Name               = name;
            this.typeNameToConsumer = new ConcurrentDictionary<string, IConsumer>();
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (subscription != null)
            {
                subscription.Dispose();
                subscription = null;
            }
        }

        /// <summary>
        /// Returns the channel name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the channel is currently connected to a RabbitMQ broker.
        /// </summary>
        public bool IsConnected => EasyBus.IsConnected;

        /// <summary>
        /// Returns the hive message bus.
        /// </summary>
        protected HiveBus HiveBus { get; private set; }

        /// <summary>
        /// Returns the lower level EasyNetQ <see cref="IAdvancedBus"/> implementation.
        /// </summary>
        protected IAdvancedBus EasyBus { get; private set; }

        /// <summary>
        /// Indicates whether <see cref="Open()"/> has been called.
        /// </summary>
        protected bool IsOpen { get; private set; }

        /// <summary>
        /// Ensures that the channel hasn't been opened.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the channel is already opened.</exception>
        protected void EnsureNotOpened()
        {
            if (IsOpen)
            {
                throw new InvalidOperationException($"The [{Name} channel has already been opened.");
            }
        }

        /// <summary>
        /// Ensures that the channel has been opened.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the channel has not been opened.</exception>
        protected void EnsureOpened()
        {
            if (!IsOpen)
            {
                throw new InvalidOperationException($"The [{Name}] channel is not open.");
            }
        }

        /// <summary>
        /// Opens the channel so that messages can be published and consumed.  This must be
        /// called before a channel is usable, generally after you've added any message
        /// consumers.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if the channel has already been opened.</exception>
        public virtual void Open()
        {
            EnsureNotOpened();
            IsOpen = true;
        }

        /// <summary>
        /// Adds a message consumer to the channel.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if consumer for the message type is already present for the channel.</exception>
        private void AddConsumer(IConsumer consumer)
        {
            Covenant.Requires<ArgumentNullException>(consumer != null);

            var messageTypeName = consumer.MessageType.FullName;

            if (typeNameToConsumer.ContainsKey(messageTypeName))
            {
                throw new InvalidOperationException($"Channel [{Name}] already has a message consumer for message type [{messageTypeName}].");
            }

            typeNameToConsumer.Add(messageTypeName, consumer);
        }

        /// <summary>
        /// Serializes a message to bytes.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">Thghe message.</param>
        /// <param name="encoding">The encoding to use when serialization the JSON text.</param>
        /// <returns>The serialized message bytes.</returns>
        private byte[] Serialize<TMessage>(TMessage message, Encoding encoding)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(message != null);
            Covenant.Requires<ArgumentNullException>(encoding != null);

            return encoding.GetBytes(NeonHelper.JsonSerialize(message, Formatting.None));
        }

        /// <summary>
        /// Asynchronously deserializes a message from raw bytes and dispatches the message 
        /// to the registered consumer callback (if there is one).  Note that the message
        /// type will be encoded in the envelope's <see cref="IBasicProperties.Type"/>
        /// </summary>
        /// <param name="bytes">The received message serialized as bytes.</param>
        /// <param name="properties">The message properties.</param>
        /// <param name="info">Additional delivery information.</param>
        /// <exception cref="FormatException">Thrown if the message could not be deserialized.</exception>
        private async Task DispatchAsync(byte[] bytes, MessageProperties properties, MessageReceivedInfo info)
        {
            Covenant.Requires<ArgumentNullException>(properties != null);

            // We're going to assume [application/json] when content type is not specified.

            if (properties.ContentTypePresent)
            {
                if (!properties.ContentType.Equals("application/json", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw new FormatException($"[{nameof(IBasicProperties.ContentType)}={properties.ContentType}] is not supported.");
                }
            }

            if (!properties.TypePresent)
            {
                throw new FormatException($"Received message is missing [{nameof(IBasicProperties.Type)}].");
            }

            var encoding = Encoding.UTF8;   // Default to UTF-8 encoding

            if (properties.ContentEncodingPresent)
            {
                switch (properties.ContentEncoding.ToLowerInvariant())
                {
                    case "utf-7":

                        encoding = Encoding.UTF7;
                        break;

                    case "utf-8":

                        encoding = Encoding.UTF8;
                        break;

                    case "utf-32":

                        encoding = Encoding.UTF32;
                        break;

                    case "ascii":

                        encoding = Encoding.ASCII;
                        break;
                }
            }

            if (!typeNameToConsumer.TryGetValue(properties.Type, out var consumer))
            {
                // There is no consumer registered for a message type so
                // we'll ignore it.

                return;
            }

            var json    = encoding.GetString(bytes);
            var message = NeonHelper.JsonDeserialize(consumer.MessageType, json, strict: false);

            await consumer.DispatchAsync(message, properties, info);
        }

        /// <summary>
        /// Begins consuming messages from a queue.
        /// </summary>
        /// <param name="queue">The source queue.</param>
        protected void StartListening(IQueue queue)
        {
            Covenant.Requires<ArgumentNullException>(queue != null);

            subscription = EasyBus.Consume(queue,
                async (byte[] bytes, MessageProperties properties, MessageReceivedInfo info) =>
                {
                    await DispatchAsync(bytes, properties, info);
                });
        }

        /// <summary>
        /// Synchronously publishes a message to an exchange.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="exchange">The target exchane.</param>
        /// <param name="message">The message.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="headers">Optional message headers.</param>
        protected void Publish<TMessage>(IExchange exchange, TMessage message, string routingKey, params KeyValuePair<string, string>[] headers)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(exchange != null);
            Covenant.Requires<ArgumentNullException>(message != null);

            var body       = Serialize(message, Encoding.UTF8);
            var properties = new MessageProperties()
            {
                Type            = typeof(TMessage).FullName,
                ContentType     = "application/json",
                ContentEncoding = "utf-8"
            };

            if (headers != null && headers.Length > 0)
            {
                foreach (var header in headers)
                {
                    properties.Headers.Add(header.Key, header.Value);
                }
            }

            EasyBus.Publish(exchange, routingKey, mandatory: false, properties, body);
        }

        /// <summary>
        /// aSynchronously publishes a message to an exchange.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="exchange">The target exchane.</param>
        /// <param name="message">The message.</param>
        /// <param name="routingKey">The routing key.</param>
        /// <param name="headers">Optional message headers.</param>
        protected async Task PublishAsync<TMessage>(IExchange exchange, TMessage message, string routingKey, params KeyValuePair<string, string>[] headers)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(exchange != null);
            Covenant.Requires<ArgumentNullException>(message != null);

            var body       = Serialize(message, Encoding.UTF8);
            var properties = new MessageProperties()
            {
                Type            = typeof(TMessage).FullName,
                ContentType     = "application/json",
                ContentEncoding = "utf-8"
            };

            if (headers != null && headers.Length > 0)
            {
                foreach (var header in headers)
                {
                    properties.Headers.Add(header.Key, header.Value);
                }
            }

            await EasyBus.PublishAsync(exchange, routingKey, mandatory: false, properties, body);
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are received by the channel.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
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
        /// </remarks>
        public void Consume<TMessage>(Action<TMessage> onMessage)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            AddConsumer(new Consumer<TMessage>(onMessage));
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are received by the channel.  This override
        /// also passes the additional <see cref="MessageProperties"/> and
        /// <see cref="ConsumerContext"/> parameters to the callback.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
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
        /// </remarks>
        public void Consume<TMessage>(Action<TMessage, MessageProperties, ConsumerContext> onMessage)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            AddConsumer(new Consumer<TMessage>(onMessage));
        }

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are received by the channel.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <param name="exclusive">
        /// Optionally indicates that this is is to be the exclusive consumer 
        /// of messages on the channel.  This defaults to <c>false</c>.
        /// </param>
        /// <remarks>
        /// <para>
        /// The asynchronous <paramref name="onMessage"/> callback is passed the 
        /// received message as a single argument.
        /// </para>
        /// <note>
        /// Most applications (especially services) should register asynchronous
        /// callbacks using this method for better performance under load.
        /// </note>
        /// </remarks>
        public void ConsumeAsync<TMessage>(Func<TMessage, Task> onMessage, bool exclusive = false)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            AddConsumer(new Consumer<TMessage>(onMessage));
        }

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are received by the channel.  This override
        /// also passes the additional <see cref="MessageProperties"/> and
        /// <see cref="ConsumerContext"/> parameters to the callback.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <param name="exclusive">
        /// Optionally indicates that this is is to be the exclusive consumer 
        /// of messages on the channel.  This defaults to <c>false</c>.
        /// </param>
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
        /// </remarks>
        public void ConsumeAsync<TMessage>(Func<TMessage, MessageProperties, ConsumerContext, Task> onMessage, bool exclusive = false)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);

            AddConsumer(new Consumer<TMessage>(onMessage));
        }
    }
}
