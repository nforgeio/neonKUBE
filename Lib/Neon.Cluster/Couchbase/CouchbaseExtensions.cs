//-----------------------------------------------------------------------------
// FILE:	    CouchbaseExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;

using Neon.Cluster;
using Neon.Common;
using Neon.Data;

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
        /// <param name="adminUsername">Optional cluster admin username.</param>
        /// <param name="adminPassword">Optional cluster admin password.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster OpenCluster(this CouchbaseSettings settings, string adminUsername, string adminPassword)
        {
            Covenant.Requires<ArgumentNullException>(settings.Servers != null && settings.Servers.Count > 0);

            var config  = settings.ToClientConfig();
            var cluster = new Cluster(config);

            if (!string.IsNullOrEmpty(adminUsername) && !string.IsNullOrEmpty(adminPassword))
            {
                cluster.Authenticate(new Authentication.PasswordAuthenticator(adminUsername, adminPassword));
            }

            return cluster;
        }

        /// <summary>
        /// Returns a Couchbase cluster connection using specified settings and <see cref="Credentials"/>.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="adminCredentials">The optional cluster admin credentials.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster OpenCluster(this CouchbaseSettings settings, Credentials adminCredentials = null)
        {
            Covenant.Requires<ArgumentNullException>(settings.Servers != null && settings.Servers.Count > 0);
            Covenant.Requires<ArgumentNullException>(adminCredentials == null || !string.IsNullOrEmpty(adminCredentials.Username));
            Covenant.Requires<ArgumentNullException>(adminCredentials == null || !string.IsNullOrEmpty(adminCredentials.Password));

            var config  = settings.ToClientConfig();
            var cluster = new Cluster(config);

            if (adminCredentials != null)
            {
                cluster.Authenticate(new Authentication.PasswordAuthenticator(adminCredentials.Username, adminCredentials.Password));
            }

            return cluster;
        }

        /// <summary>
        /// Returns a Couchbase cluster connection using specified settings and a Docker secret.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="secretName">The name of the Docker secret holding the credentials.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster OpenCluster(this CouchbaseSettings settings, string secretName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretName));

            var credentials = NeonHelper.JsonDeserialize<Credentials>(NeonClusterHelper.GetSecret(secretName));

            return OpenCluster(settings, credentials);
        }

        /// <summary>
        /// Returns a Couchbase bucket connection using specified settings and the username and password.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="username">Optional username.</param>
        /// <param name="password">Optional password.</param>
        /// <returns>The connected <see cref="CouchbaseBucket"/>.</returns>
        public static IBucket OpenBucket(this CouchbaseSettings settings, string username = null, string password = null)
        {
            var config = settings.ToClientConfig();

            config.BucketConfigs.Clear();

            config.BucketConfigs.Add(settings.Bucket,
                new BucketConfiguration()
                {
                    BucketName = settings.Bucket,
                    Username   = username,
                    Password   = password,

                    PoolConfiguration = new PoolConfiguration()
                    {
                        SendTimeout    = int.MaxValue,
                        ConnectTimeout = int.MaxValue,
                        MaxSize        = 10,
                        MinSize        = 5
                    }
                });

            var cluster = new Cluster(config);
            
            return cluster.OpenBucket(settings.Bucket);
        }

        /// <summary>
        /// Returns a Couchbase bucket connection using specified settings and credentials.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="credentials">The credentials.</param>
        /// <returns>The connected <see cref="CouchbaseBucket"/>.</returns>
        public static IBucket OpenBucket(this CouchbaseSettings settings, Credentials credentials)
        {
            Covenant.Requires<ArgumentNullException>(credentials != null);

            return settings.OpenBucket(credentials.Username, credentials.Password);
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

            return OpenBucket(settings, credentials);
        }

        /// <summary>
        /// Converts a <see cref="CouchbaseSettings"/> into a <see cref="ClientConfiguration"/>.
        /// </summary>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static ClientConfiguration ToClientConfig(this CouchbaseSettings settings)
        {
            var config = new ClientConfiguration();

            config.Servers.Clear();

            foreach (var uri in settings.Servers)
            {
                config.Servers.Add(uri);
            }

            config.UseSsl = false;

            return config;
        }

        /// <summary>
        /// Generates a globally unique document key.
        /// </summary>
        /// <param name="bucket">The bucket.</param>
        /// <returns>A <see cref="Guid"/> formatted as a string.</returns>
        public static string GenKey(this IBucket bucket)
        {
            return Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        }
    }
}
