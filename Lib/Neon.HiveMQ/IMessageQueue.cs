//-----------------------------------------------------------------------------
// FILE:	    IMessageQueue.cs
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
    /// Manages operations on a message queue managed by an <see cref="IMessageBus"/>.
    /// </summary>
    public interface IMessageQueue : IDisposable
    {
        /// <summary>
        /// Synchronously publishes a message to the queue.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        void Publish<TMessage>(TMessage message);

        /// <summary>
        /// Asynchronously publishes a message to the queue.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="message">The message.</param>
        Task PublishAsync<TMessage>(TMessage message);

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <returns>An <see cref="IConsumerSubscription"/> instance.</returns>
        /// <remarks>
        /// <note>
        /// This method is suitable for many graphical client applications but 
        /// should generally be avoided for high performance service applications
        /// which should register an asynchronous callback.
        /// </note>
        /// <para>
        /// To cancel the subscription, dispose the <see cref="IConsumerSubscription"/>
        /// returned by this method.
        /// </para>
        /// </remarks>
        IConsumerSubscription Consume<TMessage>(Action<IMessage<TMessage>> onMessage)
            where TMessage : class, new();

        /// <summary>
        /// Registers a synchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  This override
        /// also passed additional context information via the <see cref="MessageReceivedInfo"/>
        /// callback parameter.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <returns>An <see cref="IConsumerSubscription"/> instance.</returns>
        /// <remarks>
        /// <note>
        /// This method is suitable for many graphical client applications but 
        /// should generally be avoided for high performance service applications
        /// which should register an asynchronous callback.
        /// </note>
        /// <para>
        /// To cancel the subscription, dispose the <see cref="IConsumerSubscription"/>
        /// returned by this method.
        /// </para>
        /// </remarks>
        IConsumerSubscription Consume<TMessage>(Action<IMessage<TMessage>, MessageReceivedInfo> onMessage)
            where TMessage : class, new();

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <returns>An <see cref="IConsumerSubscription"/> instance.</returns>
        /// <remarks>
        /// <note>
        /// Most applications (especially services) should register asynchronous
        /// callbacks using this method for better performance under load.
        /// </note>
        /// <para>
        /// To cancel the subscription, dispose the <see cref="IConsumerSubscription"/>
        /// returned by this method.
        /// </para>
        /// </remarks>
        IConsumerSubscription Consume<TMessage>(Func<IMessage<TMessage>, Task> onMessage)
            where TMessage : class, new();

        /// <summary>
        /// Registers an asynchronous callback that will be called as messages of type
        /// <typeparamref name="TMessage"/> are delivered to the consumer.  This override
        /// also passed additional context information via the <see cref="MessageReceivedInfo"/>
        /// callback parameter.
        /// </summary>
        /// <typeparam name="TMessage">The message type.</typeparam>
        /// <param name="onMessage">Called when a message is delivered.</param>
        /// <returns>An <see cref="IConsumerSubscription"/> instance.</returns>
        /// <remarks>
        /// <note>
        /// Most applications (especially services) should register asynchronous
        /// callbacks using this method for better performance under load.
        /// </note>
        /// <para>
        /// To cancel the subscription, dispose the <see cref="IConsumerSubscription"/>
        /// returned by this method.
        /// </para>
        /// </remarks>
        IConsumerSubscription Consume<TMessage>(Func<IMessage<TMessage>, MessageReceivedInfo, Task> onMessage)
            where TMessage : class, new();
    }
}
