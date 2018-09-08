//-----------------------------------------------------------------------------
// FILE:	    RabbitMQExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

using RabbitMQ;
using RabbitMQ.Client;

using Neon.Common;

namespace Neon.HiveMQ
{
    /// <summary>
    /// RabbitMQ related extensions.
    /// </summary>
    public static class RabbitMQExtensions
    {
        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings, optionally overriding
        /// the username and password.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="username">Optional username.</param>
        /// <param name="password">Optional password.</param>
        /// <param name="dispatchConsumersAsync">Optionally enables <c>async</c> message consumers.  This defaults to <c>false</c>.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        public static IConnection Connect(this RabbitMQSettings settings, string username = null, string password = null, bool dispatchConsumersAsync = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.VirtualHost));
            Covenant.Requires<ArgumentNullException>(settings.Hosts != null && settings.Hosts.Count > 0);

            var connectionFactory = new ConnectionFactory();

            connectionFactory.VirtualHost            = settings.VirtualHost;
            connectionFactory.UserName               = username ?? settings.Username;
            connectionFactory.Password               = password ?? settings.Password;
            connectionFactory.Port                   = settings.Port;
            connectionFactory.DispatchConsumersAsync = dispatchConsumersAsync;

            if (settings.TlsEnabled)
            {
                connectionFactory.Ssl = new SslOption() { Enabled = true };
            }

            return new RabbitMQConnection(connectionFactory.CreateConnection(settings.Hosts));
        }

        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings and credentials.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="credentials">The credentials.</param>
        /// <param name="dispatchConsumersAsync">Optionally enables <c>async</c> message consumers.  This defaults to <c>false</c>.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        public static IConnection Connect(this RabbitMQSettings settings, Credentials credentials, bool dispatchConsumersAsync = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.VirtualHost));
            Covenant.Requires<ArgumentNullException>(settings.Hosts != null && settings.Hosts.Count > 0);
            Covenant.Requires<ArgumentNullException>(credentials != null);

            return Connect(settings, credentials.Username, credentials.Password, dispatchConsumersAsync);
        }
    }
}
