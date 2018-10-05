//-----------------------------------------------------------------------------
// FILE:	    ConsumerContext.cs
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
    /// Holds additional state for messages delivered to a consumer.
    /// </summary>
    public struct ConsumerContext
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Constructs an instance from an EasyNetQ <see cref="MessageReceivedInfo"/>.
        /// </summary>
        /// <param name="info">The receive information.</param>
        internal static ConsumerContext Create(MessageReceivedInfo info)
        {
            return new ConsumerContext()
            {
                ConsumerTag = info.ConsumerTag,
                DeliverTag  = info.DeliverTag,
                Redelivered = info.Redelivered,
                Exchange    = info.Exchange,
                RoutingKey  = info.RoutingKey,
                Queue       = info.Queue
            };
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Returns the consumer tag.
        /// </summary>
        public string ConsumerTag { get; private set; }

        /// <summary>
        /// Returns the delivery tag.
        /// </summary>
        public ulong DeliverTag { get; private set; }

        /// <summary>
        /// Indicates whether this message has been relivered.
        /// </summary>
        public bool Redelivered { get; private set; }

        /// <summary>
        /// Returns the exchange name.
        /// </summary>
        public string Exchange { get; private set; }

        /// <summary>
        /// Returns the message routing key.
        /// </summary>
        public string RoutingKey { get; private set; }

        /// <summary>
        /// Returns the queue name.
        /// </summary>
        public string Queue { get; private set; }
    }
}
