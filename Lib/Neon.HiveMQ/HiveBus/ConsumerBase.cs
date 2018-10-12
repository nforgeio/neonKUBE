//-----------------------------------------------------------------------------
// FILE:	    ConsumerBase.cs
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
    /// Base class for all <see cref="Consumer{TMessage}"/> instances.
    /// </summary>
    public abstract class ConsumerBase
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        /// <param name="messageType">Identifies the subscribed message type.</param>
        internal ConsumerBase(Type messageType)
        {
            Covenant.Requires<ArgumentNullException>(messageType != null);

            this.MessageType = messageType;
        }

        /// <summary>
        /// Returns the subscribed message type.
        /// </summary>
        public Type MessageType { get; private set; }
    }
}
