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
    /// Channel base class.
    /// </summary>
    public class Channel
    {
        private ConcurrentDictionary<string, ConsumerBase>  typeToConsumer;

        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="hiveBus">The <see cref="HiveMQ.HiveBus"/>.</param>
        /// <param name="name">The channel name.</param>
        protected Channel(HiveBus hiveBus, string name)
        {
            Covenant.Requires<ArgumentNullException>(hiveBus != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.HiveBus        = hiveBus;
            this.EasyBus        = hiveBus.EasyBus.Advanced;
            this.Name           = name;
            this.typeToConsumer = new ConcurrentDictionary<string, ConsumerBase>();
        }

        /// <summary>
        /// Returns the channel name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the hive message bus.
        /// </summary>
        protected HiveBus HiveBus { get; private set; }

        /// <summary>
        /// Returns the lower level EasyNetQ <see cref="IAdvancedBus"/> implementation.
        /// </summary>
        protected IAdvancedBus EasyBus { get; private set; }

        /// <summary>
        /// Returns <c>true</c> if the channel is currently connected to a RabbitMQ broker.
        /// </summary>
        public bool IsConnected => EasyBus.IsConnected;

        /// <summary>
        /// Adds a message consumer the channel.
        /// </summary>
        /// <typeparam name="TMessage">The subscribed message type.</typeparam>
        /// <param name="consumer">The subscription.</param>
        /// <exception cref="InvalidOperationException">Thrown if consumer for <typeparamref name="TMessage"/> is already present for the channel.</exception>
        internal void AddConsumer<TMessage>(Consumer<TMessage> consumer)
            where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(consumer != null);

            var messageTypeName = typeof(TMessage).FullName;

            if (typeToConsumer.ContainsKey(messageTypeName))
            {
                throw new InvalidOperationException($"Channel [{Name}] already a message consumer for message type [{messageTypeName}].");
            }

            typeToConsumer.Add(messageTypeName, consumer);
        }
    }
}
