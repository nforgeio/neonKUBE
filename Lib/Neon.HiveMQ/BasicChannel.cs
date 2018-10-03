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
    /// Implements basic messaging operations for a <see cref="MessageBus"/>.  
    /// Message producers and consumers each need to declare a channel with the 
    /// same name by calling one of the <see cref="MessageBus"/> to be able to
    /// publish and consume messages.
    /// </para>
    /// <note>
    /// <see cref="BasicChannel"/> has nothing to do with an underlying
    /// RabbitMQ channel.  These are two entirely different concepts.
    /// </note>
    /// </summary>
    public class BasicChannel : Channel
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="messageBus">The <see cref="MessageBus"/>.</param>
        /// <param name="name">The channel name.</param>
        internal BasicChannel(MessageBus messageBus, string name)
            : base(messageBus, name)
        {
        }

        /// <summary>
        /// Asynchronously publishes a message to the channel.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        public Task PublishAsync<TMessage>(TMessage message)
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

            lock (SyncLock)
            {
                CheckDisposed();
            }

            throw new NotImplementedException();
        }

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  This override
        /// also passed additional context information via the <see cref="MessageReceivedInfo"/>
        /// callback parameter.
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
        public Subscription Consume<TMessage>(Action<IMessage<TMessage>, MessageReceivedInfo> onMessage) 
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
        public Subscription Consume<TMessage>(Func<IMessage<TMessage>, Task> onMessage)
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
        /// also passed additional context information via the <see cref="MessageReceivedInfo"/>
        /// callback parameter.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
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
        public Subscription Consume<TMessage>(Func<IMessage<TMessage>, MessageReceivedInfo, Task> onMessage) 
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
