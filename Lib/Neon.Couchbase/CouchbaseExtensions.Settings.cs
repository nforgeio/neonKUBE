//-----------------------------------------------------------------------------
// FILE:	    CouchbaseExtensions.Settings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.Diagnostics.Contracts;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Serialization;
using Couchbase.N1QL;
using Neon.Common;
using Neon.Data;
using Neon.Time;

namespace Couchbase
{
    /// <summary>
    /// Couchbase related extensions.
    /// </summary>
    public static partial class CouchbaseExtensions
    {
        /// <summary>
        /// Returns a Couchbase cluster connection using specified settings and the username and password.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="username">The cluster username.</param>
        /// <param name="password">The cluster password.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster OpenCluster(this CouchbaseSettings settings, string username, string password)
        {
            Covenant.Requires<ArgumentNullException>(settings.Servers != null && settings.Servers.Count > 0, nameof(settings));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(username), nameof(username));
            Covenant.Requires<ArgumentNullException>(password != null, nameof(password));

            var config  = settings.ToClientConfig();
            var cluster = new Cluster(config);

            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(password))
            {
                cluster.Authenticate(new Authentication.PasswordAuthenticator(username, password));
            }

            return cluster;
        }

        /// <summary>
        /// Returns a Couchbase cluster connection using specified settings and <see cref="Credentials"/>.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="credentials">Cluster credentials.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster OpenCluster(this CouchbaseSettings settings, Credentials credentials)
        {
            Covenant.Requires<ArgumentNullException>(settings.Servers != null && settings.Servers.Count > 0, nameof(settings));
            Covenant.Requires<ArgumentNullException>(credentials != null, nameof(credentials));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(credentials.Username), nameof(credentials));
            Covenant.Requires<ArgumentNullException>(credentials.Password != null, nameof(credentials));

            var config  = settings.ToClientConfig();
            var cluster = new Cluster(config);

            cluster.Authenticate(credentials.Username, credentials.Password);

            return cluster;
        }

        /// <summary>
        /// Returns a Couchbase bucket connection using specified settings and the cluster credentials.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="username">The username.</param>
        /// <param name="password">The password.</param>
        /// <param name="timeout">The optional timeout (defaults to 60 seconds).</param>
        /// <param name="ignoreDurability">Optionally configure the bucket to ignore durability parameters.</param>
        /// <returns>The connected <see cref="NeonBucket"/>.</returns>
        /// <exception cref="TimeoutException">Thrown if the bucket is not ready after waiting <paramref name="timeout"/>.</exception>
        /// <remarks>
        /// <para>
        /// You may explicitly pass <paramref name="ignoreDurability"/><c>=true</c> for 
        /// development and test environments where there may not be enough cluster nodes to
        /// satisfy durability constraints.  If this is <c>null</c> (the default) then the bucket 
        /// will look for the presence of the <b>DEV_WORKSTATION</b> environment variable
        /// and ignore durability constraints if this variable exists, otherwise durability
        /// constraints will be honored.
        /// </para>
        /// </remarks>
        public static NeonBucket OpenBucket(this CouchbaseSettings settings, 
            string      username, 
            string      password, 
            TimeSpan    timeout = default(TimeSpan), 
            bool?       ignoreDurability = null)
        {
            var bucketConfig =
                new BucketConfiguration()
                {
                    BucketName = settings.Bucket,
                    UseEnhancedDurability = settings.UseEnhancedDurability,

                    PoolConfiguration = new PoolConfiguration()
                    {
                        ConnectTimeout = settings.ConnectTimeout,
                        SendTimeout    = settings.SendTimeout,
                        MaxSize        = settings.MaxPoolConnections,
                        MinSize        = settings.MinPoolConnections
                    }
                };

            bucketConfig.PoolConfiguration.ClientConfiguration =
                new ClientConfiguration()
                {
                    QueryRequestTimeout = (uint)settings.QueryRequestTimeout,
                    ViewRequestTimeout  = settings.ViewRequestTimeout,
                    Serializer          = () => new EntitySerializer()
                };

            var config = settings.ToClientConfig();

            config.BucketConfigs.Clear();
            config.BucketConfigs.Add(settings.Bucket, bucketConfig);

            var cluster = new Cluster(config);

            // We have to wait for three Couchbase operations to complete
            // successfully:
            //
            //      1. Authenticate
            //      2. Open Bucket
            //      3. List Indexes
            //
            // Each of these can fail if Couchbase isn't ready.  The Open Bucket
            // can fail after the Authenticate succeeded because the bucket is 
            // still warming up in the cluster.

            if (timeout <= TimeSpan.Zero)
            {
                timeout = NeonBucket.ReadyTimeout;
            }

            //-----------------------------------------------------------------
            // Authenticate against the Couchbase cluster.

            var stopwatch = new Stopwatch();

            stopwatch.Start();

            NeonHelper.WaitFor(
                () =>
                {
                    try
                    {
                        cluster.Authenticate(username, password);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout:      timeout,
                pollInterval: TimeSpan.FromSeconds(0.5));

            //-----------------------------------------------------------------
            // Open the bucket.  Note that we're going to recreate the bucket after
            // each failed attempt because it doesn't seem to recover from connection
            // failures.
            //
            // I believe this may be due to the index and or query services not being
            // ready yet after Couchbase has just been spun up and the bucket isn't 
            // notified when these become ready, even after some time passes.

            timeout = timeout - stopwatch.Elapsed;  // Adjust the timeout downward by the time taken to authenticate.
            stopwatch.Restart();

            var bucket = (NeonBucket)null;

            NeonHelper.WaitFor(
                () =>
                {
                    try
                    {
                        bucket = new NeonBucket(cluster.OpenBucket(settings.Bucket), settings, ignoreDurability);
                        return true;
                    }
                    catch
                    {
                        return false;
                    }
                },
                timeout:      timeout,
                pollInterval: TimeSpan.FromSeconds(0.5));

            //-----------------------------------------------------------------
            // Wait until the bucket can perform a simple read operation without
            // failing.  It appears that we can see early failures for Couchbase
            // clusters that have just been created (e.g. during unit tests).

            timeout = timeout - stopwatch.Elapsed;  // Adjust the timeout downward by the time taken to connect.
            stopwatch.Restart();

            bucket.WaitUntilReadyAsync(timeout).Wait();

            return bucket;
        }

        /// <summary>
        /// Returns a Couchbase bucket connection using specified settings and credentials.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="credentials">The credentials.</param>
        /// <param name="timeout">The optional timeout (defaults to 30 seconds).</param>
        /// <param name="ignoreDurability">Optionally configure the bucket to ignore durability parameters.</param>
        /// <returns>The connected <see cref="NeonBucket"/>.</returns>
        /// <remarks>
        /// <para>
        /// You may explicitly pass <paramref name="ignoreDurability"/><c>=true</c> for 
        /// development and test environments where there may not be enough cluster nodes to
        /// satisfy durability constraints.  If this is <c>null</c> (the default) then the bucket 
        /// will look for the presence of the <b>DEV_WORKSTATION</b> environment variable
        /// and ignore durability constraints if this variable exists, otherwise durability
        /// constraints will be honored.
        /// </para>
        /// </remarks>
        public static NeonBucket OpenBucket(this CouchbaseSettings settings, 
            Credentials     credentials,
            TimeSpan        timeout = default,
            bool?           ignoreDurability = null)
        {
            Covenant.Requires<ArgumentNullException>(credentials != null, nameof(credentials));

            return settings.OpenBucket(credentials.Username, credentials.Password, timeout: timeout, ignoreDurability: ignoreDurability);
        }

        /// <summary>
        /// Converts a <see cref="CouchbaseSettings"/> into a <see cref="ClientConfiguration"/>.
        /// </summary>
        /// <param name="settings">The simplified Couchbase settings instance.</param>
        /// <returns>A Couchbase <see cref="ClientConfiguration"/>.</returns>
        public static ClientConfiguration ToClientConfig(this CouchbaseSettings settings)
        {
            var config = new ClientConfiguration();

            config.Servers.Clear();

            foreach (var uri in settings.Servers)
            {
                config.Servers.Add(uri);
            }

            config.UseSsl                   = false;
            config.Serializer               = () => new DefaultSerializer(NeonHelper.JsonRelaxedSerializerSettings.Value, NeonHelper.JsonRelaxedSerializerSettings.Value);
            config.DefaultOperationLifespan = (uint)settings.OperationTimeout;
            config.DefaultConnectionLimit   = settings.MaxPoolConnections;
            config.Serializer               = () => new EntitySerializer();

            return config;
        }
    }
}
