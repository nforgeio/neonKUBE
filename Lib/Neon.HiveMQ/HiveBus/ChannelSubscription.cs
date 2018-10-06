//-----------------------------------------------------------------------------
// FILE:	    ChannelSubscription.cs
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
    /// <para>
    /// Describes the state of a message consumer's subscription to a queue.
    /// Dispose the instance to cancel the subscription.
    /// </para>
    /// <note>
    /// <b>WARNING:</b> Never dispose a message consumer subscription within
    /// a message consumption callback.
    /// </note>
    /// </summary>
    public class ChannelSubscription : IDisposable
    {
        private bool            isDisposed = false;
        private Channel         channel;
        private IDisposable     easyNetQSubscription;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="channel">The associated channel.</param>
        /// <param name="messageType">The type of the message being subscribed.</param>
        /// <param name="easyNetQSubscription">The lower-level consumption subscription.</param>
        public ChannelSubscription(Channel channel, Type messageType, IDisposable easyNetQSubscription)
        {
            Covenant.Requires<ArgumentNullException>(channel != null);
            Covenant.Requires<ArgumentNullException>(messageType != null);
            Covenant.Requires<ArgumentNullException>(easyNetQSubscription != null);

            this.channel              = channel;
            this.MessageType          = messageType;
            this.easyNetQSubscription = easyNetQSubscription;
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
                    easyNetQSubscription.Dispose();
                    channel.RemoveSubscription(this);

                    channel              = null;
                    easyNetQSubscription = null;
                }

                isDisposed = true;
            }
        }

        /// <summary>
        /// Returns the message type being subscribed.
        /// </summary>
        public Type MessageType { get; private set; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }
    }
}
