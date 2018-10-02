//-----------------------------------------------------------------------------
// FILE:	    MessageQueue.cs
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
using System.Threading.Tasks;

namespace Neon.HiveMQ
{
    /// <inheritdoc/>
    public class MessageQueue : IMessageQueue
    {
        private bool isDisposed = false;

        /// <summary>
        /// Constructor.
        /// </summary>
        public MessageQueue()
        {
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
                }

                isDisposed = true;
            }
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Ensures that the instance has bot been disposed.
        /// </summary>
        private void CheckDisposed()
        {
            if (isDisposed)
            {
                throw new ObjectDisposedException(nameof(MessageQueue));
            }
        }

        /// <inheritdoc/>
        public void Publish<TMessage>(TMessage message)
        {
            Covenant.Requires<ArgumentNullException>(message != null);
            CheckDisposed();

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task PublishAsync<TMessage>(TMessage message)
        {
            Covenant.Requires<ArgumentNullException>(message != null);
            CheckDisposed();

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IConsumerSubscription Consume<TMessage>(Action<IMessage<TMessage>> onMessage) where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);
            CheckDisposed();

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IConsumerSubscription Consume<TMessage>(Action<IMessage<TMessage>, MessageReceivedInfo> onMessage) where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);
            CheckDisposed();

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IConsumerSubscription Consume<TMessage>(Func<IMessage<TMessage>, Task> onMessage) where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);
            CheckDisposed();

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IConsumerSubscription Consume<TMessage>(Func<IMessage<TMessage>, MessageReceivedInfo, Task> onMessage) where TMessage : class, new()
        {
            Covenant.Requires<ArgumentNullException>(onMessage != null);
            CheckDisposed();

            throw new NotImplementedException();
        }
    }
}
