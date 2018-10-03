//-----------------------------------------------------------------------------
// FILE:	    Channel.cs
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
    /// Channel base class.
    /// </summary>
    public class Channel : IDisposable
    {
        private object              syncLock   = new object();
        private bool                isDisposed = false;
        private List<Subscription>  subscriptions;

        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="messageBus">The <see cref="MessageBus"/>.</param>
        /// <param name="name">The channel name.</param>
        protected Channel(MessageBus messageBus, string name)
        {
            Covenant.Requires<ArgumentNullException>(messageBus != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.MessageBus    = messageBus;
            this.Name          = name;
            this.subscriptions = new List<Subscription>();
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            lock (syncLock)
            {
                if (!isDisposed)
                {
                    if (disposing)
                    {
                        foreach (var subscription in subscriptions)
                        {
                            subscription.Dispose();
                        }
                    }

                    isDisposed = true;
                }
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Ensures that the instance has not been disposed.
        /// </summary>
        protected void CheckDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(MessageBus));
            }
        }

        /// <summary>
        /// Returns the channel name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the object used for thread synchronization.
        /// </summary>
        protected object SyncLock => syncLock;

        /// <summary>
        /// Returns the message bus.
        /// </summary>
        protected MessageBus MessageBus { get; private set; }

        /// <summary>
        /// Removes a message consumption subscription from the channel.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        internal void RemoveSubscription(Subscription subscription)
        {
            Covenant.Requires<ArgumentNullException>(subscription != null);

            lock (syncLock)
            {
                subscriptions.Remove(subscription);
            }
        }
    }
}
