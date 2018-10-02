//-----------------------------------------------------------------------------
// FILE:	    RabbitMQExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

using RabbitMQ;
using RabbitMQ.Client;

using Neon.Common;
using Neon.Hive;
using Neon.HiveMQ;

namespace RabbitMQ.Client
{
    /// <summary>
    /// RabbitMQ related extensions.
    /// </summary>
    public static class HiveRabbitMQExtensions
    {
        /// <summary>
        /// Returns a RabbitMQ cluster connection using specified settings and credentials 
        /// loaded from a Docker secret.  This works only for Docker services where the
        /// Docker secret was mounted into the service containers.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="secretName">The local name of the Docker secret holding the credentials.</param>
        /// <param name="dispatchConsumersAsync">Optionally enables <c>async</c> message consumers.  This defaults to <c>false</c>.</param>
        /// <returns>The RabbitMQ <see cref="IConnection"/>.</returns>
        /// <remarks>
        /// The credentials must be formatted as JSON as serialized by the <see cref="Credentials"/>
        /// class.
        /// </remarks>
        public static IConnection ConnectUsingSecret(this HiveMQSettings settings, string secretName, bool dispatchConsumersAsync = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(settings.VirtualHost));
            Covenant.Requires<ArgumentNullException>(settings.AmqpHosts != null && settings.AmqpHosts.Count > 0);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretName));

            var credentials = NeonHelper.JsonDeserialize<Credentials>(HiveHelper.GetSecret(secretName), dispatchConsumersAsync);

            return new RabbitMQConnection(settings.ConnectRabbitMQ(credentials));
        }
    }
}
