//-----------------------------------------------------------------------------
// FILE:	    Consumer.cs
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
    /// Describes a message consumer.
    /// </summary>
    /// <typeparam name="TMessage">The subscribed message type.</typeparam>
    internal class Consumer<TMessage> : IConsumer
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

            try
            {
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
            catch (Exception e)
            {
                HiveBus.Log.LogError("Consumer exception.", e);
                throw;
            }
        }
    }
}
