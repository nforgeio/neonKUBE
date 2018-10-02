//-----------------------------------------------------------------------------
// FILE:	    IMessageBus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;

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
    /// Manages messaging operations for a RabbitMQ cluster.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This interfaces is implemented by <see cref="MessageBus"/> and <see cref="MessageQueue"/> as
    /// a relatively thin layer over the <a href="https://github.com/EasyNetQ/EasyNetQ">EasyNetQ</a>
    /// <a href="https://github.com/EasyNetQ/EasyNetQ/wiki/The-Advanced-API">Advanced API</a>.  This
    /// interface is designed to be a little more flexible than the simple EasyNetQ API while still
    /// being very easy to use.
    /// </para>
    /// <para>
    /// The main issue I have with the simple EasyNetQ API is that hides the concept of queues from
    /// the application by implicitly creating a queue for every message type (based on its name).
    /// This doesn't cleanly support the ability to havegeneric messages that can targeted at specific 
    /// consumers.
    /// </para>
    /// <para>
    /// For example, say I have two separate services, <b>S1</b> and <b>S2</b> and I want to be
    /// able to send a <b>RESET</b> message to <b>S1</b> and another <b>RESET</b> message to <b>S2</b>.
    /// This could be accomplished with EasyNetQ by defining a separate message for each target
    /// like <b>RESET1</b> and <b>RESET2</b> and having the service subscribe to the corresponding
    /// message.  While this works, it's not super scalable; you'd have to define 10 <b>RESET</b>
    /// classes to handle 10 services.
    /// </para>
    /// <para>
    /// Another approach would be define a single <b>RESET</b> message that includes a field
    /// identifing the target service and then broadcast these to both <b>S1</b> and <b>S2</b>
    /// and then have each service ignore messages that weren't targeted at it.  This is messy
    /// because it depends on custom service code to reject messages which will consume some 
    /// netork and CPU for no real reason.
    /// </para>
    /// <para>
    /// It may be possible to implement another solution using topic routing, but that's
    /// somewhat more complex than is really necessary for a typical user.
    /// </para>
    /// <para>
    /// Message buses are typically constructed by instantiating a <see cref="HiveMQSettings"/> 
    /// instance and then calling its <see cref="HiveMQSettings.ConnectBus(string, string, string, BusSettings)"/>
    /// method.  The instance returned is thread-safe and generally most applications will instantiate
    /// a single <see cref="IMessageBus"/> instance, use it throughout the lifespan of the application
    /// and then dispose the bus when it's done.
    /// </para>
    /// <para>
    /// <see cref="IMessageBus"/> implementations combine the RabbitMQ concepts of exchanges and
    /// queues into a single simple queue.  The bus transparently creates and manages the underlying 
    /// queues and exchanges.  The bus prefixes the names of the entities it creates with <see cref="MessageBus.NamePrefix"/>
    /// (<b>"nbus-"</b> short for "neon bus") to help avoid conflicts with entities created by other frameworks.
    /// </para>
    /// <para>
    /// Two types of queues are currently implemented:
    /// </para>
    /// <list type="table">
    /// <item>
    ///     <term><b>basic</b></term>
    ///     <description>
    ///     Basic queues deliver each message to a <b>single consumer</b>.  A typical use is
    ///     to load balance work across multiple consumers.  Call <see cref="DeclareBasicQueue(string, bool, bool, TimeSpan?, int?, int?)"/>
    ///     to create a basic queue.
    ///     </description>
    /// </item>
    /// <item>
    ///     <term><b>broadcast</b></term>
    ///     <description>
    ///     Broadcast queues deliver each message to <b>all consumers</b>.  Call <see cref="DeclareBroadcastQueue(string, bool, bool, TimeSpan?, int?, int?)"/> 
    ///     to create a broadcast queue.
    ///     </description>
    /// </item>
    /// </list>
    /// <para>
    /// 
    /// </para>
    /// </remarks>
    public interface IMessageBus : IDisposable
    {
        /// <summary>
        /// Creates a basic message queue if it doesn't already exist.  Basic message queues
        /// are used to forward messages to one or more consumers such that each message
        /// is delivered to a <b>single consumer</b>.  This is typically used for load balancing
        /// work across multiple consumers.
        /// </summary>
        /// <param name="name">The queue name.  This can be a maximum of 250 characters.</param>
        /// <param name="durable">Optionally specifies that the queue should survive message cluster restarts.  This defaults to <c>false</c>.</param>
        /// <param name="autoDelete">Optionally specifies that the queue should be deleted once all consumers have disconnected.  This defaults to <c>false</c>.</param>
        /// <param name="messageTTL">Optionally specifies the maximum time a message can remain in the queue before being deleted.  This defaults to <c>null</c> which disables this feature.</param>
        /// <param name="maxLength">Optional specifies the maximum number of messages that can be waiting in the queue before messages at the front of the queue will be deleted.  This defaults to unconstrained.</param>
        /// <param name="maxLengthBytes">Optional specifies the maximum total bytes of messages that can be waiting in the queue before messages at the front of the queue will be deleted.  This defaults to unconstrained.</param>
        /// <returns>The <see cref="IMessageQueue"/> created.</returns>
        /// <remarks>
        /// <note>
        /// The instance returned should be disposed when you're done with it.
        /// </note>
        /// <note>
        /// The maximum possible <paramref name="messageTTL"/> is <see cref="int.MaxValue"/> or just
        /// under 24 days.  An <see cref="ArgumentException"/> will be thrown if this is exceeded.
        /// </note>
        /// </remarks>
        IMessageQueue DeclareBasicQueue(
            string      name, 
            bool        durable = false, 
            bool        autoDelete = false,
            TimeSpan?   messageTTL = null,
            int?        maxLength = null,
            int?        maxLengthBytes = null);

        /// <summary>
        /// Creates a broadcast message queue if it doesn't already exist.  Basic message queues
        /// are used to forward messages to one or more consumers such that each message
        /// is delivered to <b>all consumers</b>.
        /// </summary>
        /// <param name="name">The queue name.  This can be a maximum of 250 characters.</param>
        /// <param name="durable">Optionally specifies that the queue should survive message cluster restarts.  This defaults to <c>false</c>.</param>
        /// <param name="autoDelete">Optionally specifies that the queue should be deleted once all consumers have disconnected.  This defaults to <c>false</c>.</param>
        /// <param name="messageTTL">Optionally specifies the maximum time a message can remain in the queue before being deleted.  This defaults to <c>null</c> which disables this feature.</param>
        /// <param name="maxLength">Optional specifies the maximum number of messages that can be waiting in the queue before messages at the front of the queue will be deleted.  This defaults to unconstrained.</param>
        /// <param name="maxLengthBytes">Optional specifies the maximum total bytes of messages that can be waiting in the queue before messages at the front of the queue will be deleted.  This defaults to unconstrained.</param>
        /// <returns>The <see cref="IMessageQueue"/> created.</returns>
        /// <remarks>
        /// <note>
        /// The instance returned should be disposed when you're done with it.
        /// </note>
        /// <note>
        /// The maximum possible <paramref name="messageTTL"/> is <see cref="int.MaxValue"/> or just
        /// under 24 days.  An <see cref="ArgumentException"/> will be thrown if this is exceeded.
        /// </note>
        /// </remarks>
        IMessageQueue DeclareBroadcastQueue(
            string      name, 
            bool        durable = false, 
            bool        autoDelete = false,
            TimeSpan?   messageTTL = null,
            int?        maxLength = null,
            int?        maxLengthBytes = null);
    }
}
