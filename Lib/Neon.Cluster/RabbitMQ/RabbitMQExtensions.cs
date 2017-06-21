//-----------------------------------------------------------------------------
// FILE:	    RabbitMQExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

using RabbitMQ;
using RabbitMQ.Client;

using Neon.Cluster;
using Neon.Common;

namespace RabbitMQ.Client
{
    /// <summary>
    /// RabbitMQ related extensions.
    /// </summary>
    public static class RabbitMQExtensions
    {
        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings and the username and password.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        public static IConnection OpenBroker(this RabbitMQSettings settings, string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.VirtualHost));
            Covenant.Requires<ArgumentNullException>(settings.Hostnames != null && settings.Hostnames.Count > 0);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(password));

            var connectionFactory = new ConnectionFactory();

            connectionFactory.VirtualHost = settings.VirtualHost;
            connectionFactory.UserName = username;
            connectionFactory.Password = password;

            return connectionFactory.CreateConnection(settings.Hostnames);
        }

        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings and credentials.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        public static IConnection OpenBroker(this RabbitMQSettings settings, Credentials credentials)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.VirtualHost));
            Covenant.Requires<ArgumentNullException>(settings.Hostnames != null && settings.Hostnames.Count > 0);
            Covenant.Requires<ArgumentNullException>(credentials != null);

            return OpenBroker(settings, credentials.Username, credentials.Password);
        }

        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings and a Docker secret.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="secretName">The name of the Docker secret holding the credentials.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        public static IConnection OpenBroker(this RabbitMQSettings settings, string secretName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.VirtualHost));
            Covenant.Requires<ArgumentNullException>(settings.Hostnames != null && settings.Hostnames.Count > 0);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretName));

            var credentials = NeonHelper.JsonDeserialize<Credentials>(NeonClusterHelper.GetSecret(secretName));

            return OpenBroker(settings, credentials);
        }
    }
}
