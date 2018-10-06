//-----------------------------------------------------------------------------
// FILE:	    Channel.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
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
    public class Channel : IDisposable
    {
        private object              syncLock   = new object();
        private bool                isDisposed = false;
        private List<ChannelSubscription>  subscriptions;

        /// <summary>
        /// Protected constructor.
        /// </summary>
        /// <param name="hiveBus">The <see cref="HiveMQ.HiveBus"/>.</param>
        /// <param name="name">The channel name.</param>
        protected Channel(HiveBus hiveBus, string name)
        {
            Covenant.Requires<ArgumentNullException>(hiveBus != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));

            this.HiveBus       = hiveBus;
            this.EasyBus       = hiveBus.EasyBus.Advanced;
            this.Name          = name;
            this.subscriptions = new List<ChannelSubscription>();
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

                        subscriptions.Clear();
                        HiveBus.RemoveChannel(this);
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
                throw new ObjectDisposedException(nameof(HiveBus));
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
        /// Addes a message consumption subscription the the channel.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        /// <returns>The scibscription.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a subscription for the related message type is already present for the channel.</exception>
        protected ChannelSubscription AddSubscription(ChannelSubscription subscription)
        {
            Covenant.Requires<ArgumentNullException>(subscription != null);

            lock (syncLock)
            {
                if (!subscriptions.Where(s => s.MessageType == subscription.MessageType).IsEmpty())
                {
                    throw new InvalidOperationException($"Channel [{Name}] already has a subscription for message type [{subscription.MessageType.FullName}].");
                }

                subscriptions.Add(subscription);
            }

            return subscription;
        }

        /// <summary>
        /// Removes a message consumption subscription from the channel.
        /// </summary>
        /// <param name="subscription">The subscription.</param>
        internal void RemoveSubscription(ChannelSubscription subscription)
        {
            Covenant.Requires<ArgumentNullException>(subscription != null);

            lock (syncLock)
            {
                subscriptions.Remove(subscription);
            }
        }
    }
}
