//-----------------------------------------------------------------------------
// FILE:	    Consumer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Reflection;

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
    /// Describes the state of a message consumer's subscription to a queue.
    /// </summary>
    /// <typeparam name="TMessage">The message type to be consumed.</typeparam>
    internal class Consumer<TMessage> : ConsumerBase
        where TMessage : class, new()
    {
        private Channel                                         channel;
        private Action<IMessage<TMessage>>                      simpleConsumer;
        private Action<IMessage<TMessage>, ConsumerContext>     advancedConsumer;

        /// <summary>
        /// Constructs a subscription with a simple message consumer.
        /// </summary>
        /// <param name="channel">The associated channel.</param>
        /// <param name="consumer">The message consumption method.</param>
        public Consumer(Channel channel, Action<IMessage<TMessage>> consumer)
            : base(typeof(TMessage))
        {
            Covenant.Requires<ArgumentNullException>(channel != null);
            Covenant.Requires<ArgumentNullException>(consumer != null);

            this.channel        = channel;
            this.simpleConsumer = consumer;
        }

        /// <summary>
        /// Constructs a subscription with an advanced message consumer.
        /// </summary>
        /// <param name="channel">The associated channel.</param>
        /// <param name="consumer">The message consumption method.</param>
        public Consumer(Channel channel, Action<IMessage<TMessage>, ConsumerContext> consumer)
            : base(typeof(TMessage))
        {
            Covenant.Requires<ArgumentNullException>(channel != null);
            Covenant.Requires<ArgumentNullException>(consumer != null);

            this.channel          = channel;
            this.advancedConsumer = consumer;
        }

        /// <summary>
        /// Calls the message consumer, passing the <see cref="ConsumerContext"/> for
        /// advanced consumers.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="context">The consumer context.</param>
        internal void CallConsumer(IMessage<TMessage> message, ConsumerContext context)
        {
            Covenant.Requires<ArgumentNullException>(message != null);

            if (advancedConsumer != null)
            {
                advancedConsumer(message, context);
            }
            else
            {
                simpleConsumer(message);
            }
        }
    }
}
