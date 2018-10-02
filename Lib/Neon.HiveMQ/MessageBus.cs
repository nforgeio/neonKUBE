//-----------------------------------------------------------------------------
// FILE:	    MessageBus.cs
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
    /// <inheritdoc/>
    public class MessageBus : IMessageBus
    {
        private bool isDisposed = false;

        /// <summary>
        /// The string used to prefix RabbitMQ exchange and queue names to distinguish
        /// these from items created via the RabbitMQ or EasyNetQ APIs.
        /// </summary>
        public const string NamePrefix = "nbus-";

        /// <summary>
        /// Constructor.
        /// </summary>
        public MessageBus()
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
                throw new ObjectDisposedException(nameof(MessageBus));
            }
        }

        /// <inheritdoc/>
        public IMessageQueue DeclareBasicQueue(
            string      name, 
            bool        durable = false, 
            bool        autoDelete = false, 
            TimeSpan?   messageTTL = null, 
            int?        maxLength = null, 
            int?        maxLengthBytes = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentException>(name.Length <= 255 - NamePrefix.Length);
            Covenant.Requires<ArgumentException>(!messageTTL.HasValue || messageTTL.Value >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(!maxLength.HasValue || maxLength.Value > 0);
            Covenant.Requires<ArgumentException>(!maxLengthBytes.HasValue || maxLengthBytes.Value > 0);
            CheckDisposed();

            if (messageTTL.HasValue && messageTTL.Value.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentException($"[{nameof(messageTTL)}={messageTTL}] cannot exceed about 24.85 days.");
            }

            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public IMessageQueue DeclareBroadcastQueue(
            string      name,
            bool        durable = false,
            bool        autoDelete = false,
            TimeSpan?   messageTTL = null,
            int?        maxLength = null,
            int?        maxLengthBytes = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentException>(name.Length <= 255 - NamePrefix.Length);
            Covenant.Requires<ArgumentException>(!messageTTL.HasValue || messageTTL.Value >= TimeSpan.Zero);
            Covenant.Requires<ArgumentException>(!maxLength.HasValue || maxLength.Value > 0);
            Covenant.Requires<ArgumentException>(!maxLengthBytes.HasValue || maxLengthBytes.Value > 0);
            CheckDisposed();

            if (messageTTL.HasValue && messageTTL.Value.TotalMilliseconds > int.MaxValue)
            {
                throw new ArgumentException($"[{nameof(messageTTL)}={messageTTL}] cannot exceed about 24.85 days.");
            }

            throw new NotImplementedException();
        }
    }
}
