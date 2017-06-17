//-----------------------------------------------------------------------------
// FILE:	    CouchbaseExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

using Couchbase;
using Couchbase.Configuration.Client;
using Couchbase.Core;

using Neon.Cluster;
using Neon.Common;

namespace Couchbase
{
    /// <summary>
    /// Couchbase related extensions.
    /// </summary>
    public static class CouchbaseExtensions
    {
        /// <summary>
        /// Returns a Couchbase cluster connection using specified settings and the username and password.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster ConnectCluster(this CouchbaseSettings settings, string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(settings.Servers != null && settings.Servers.Count > 0);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(password));

            var config = new ClientConfiguration()
            {
                Servers = settings.Servers
            };

            var cluster = new Cluster(config);

            cluster.Authenticate(username, password);

            return cluster;
        }

        /// <summary>
        /// Returns a Couchbase cluster connection using specified settings and <see cref="Credentials"/>.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster ConnectCluster(this CouchbaseSettings settings, Credentials credentials)
        {
            Covenant.Requires<ArgumentNullException>(settings.Servers != null && settings.Servers.Count > 0);
            Covenant.Requires<ArgumentNullException>(credentials != null);
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(credentials.Username));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(credentials.Password));

            var config = new ClientConfiguration()
            {
                Servers = settings.Servers
            };

            var cluster = new Cluster(config);

            cluster.Authenticate(credentials.Username, credentials.Password);

            return cluster;
        }

        /// <summary>
        /// Returns a Couchbase cluster connection using specified settings and a Docker secret.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="secretName">The name of the Docker secret holding the credentials.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster ConnectCluster(this CouchbaseSettings settings, string secretName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretName));

            var credentials = NeonHelper.JsonDeserialize<Credentials>(NeonClusterHelper.GetSecret(secretName));

            return ConnectCluster(settings, credentials);
        }

        /// <summary>
        /// Returns a Couchbase bucket connection using specified settings and the username and password.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <returns>The connected <see cref="CouchbaseBucket"/>.</returns>
        public static IBucket ConnectBucket(this CouchbaseSettings settings, string username, string password)
        {
            var cluster = settings.ConnectCluster(username, password);

            return cluster.OpenBucket(settings.Bucket ?? "default");
        }

        /// <summary>
        /// Returns a Couchbase bucket connection using specified settings and credentials.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>The connected <see cref="CouchbaseBucket"/>.</returns>
        public static IBucket ConnectBucket(this CouchbaseSettings settings, Credentials credentials)
        {
            var cluster = settings.ConnectCluster(credentials);

            return cluster.OpenBucket(settings.Bucket ?? "default");
        }

        /// <summary>
        /// Returns a Couchbase bucket connection using specified settings and a Docker secret.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="secretName">The Docker secret name.</param>
        /// <returns>The connected <see cref="CouchbaseBucket"/>.</returns>
        public static IBucket ConnectBucket(this CouchbaseSettings settings, string secretName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretName));

            var credentials = NeonHelper.JsonDeserialize<Credentials>(NeonClusterHelper.GetSecret(secretName));

            return ConnectBucket(settings, credentials);
        }
    }
}
