//-----------------------------------------------------------------------------
// FILE:	    IConsumer.cs
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
    /// Describes the behavior of a message consumer.
    /// </summary>
    internal interface IConsumer
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
}
