//-----------------------------------------------------------------------------
// FILE:	    CouchbaseClusterExtensions.Settings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics.Contracts;

using Couchbase;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core;
using Couchbase.Core.Serialization;

using Neon.Common;
using Neon.Data;
using Neon.Hive;

namespace Couchbase
{
    /// <summary>
    /// Couchbase related extensions.
    /// </summary>
    public static class CouchbaseClusterExtensions
    {
        /// <summary>
        /// Returns a Couchbase cluster connection using specified settings and a Docker secret.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="secretName">The name of the Docker secret holding the credentials.</param>
        /// <returns>The connected <see cref="Cluster"/>.</returns>
        public static Cluster OpenCluster(this CouchbaseSettings settings, string secretName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretName));

            var credentials = NeonHelper.JsonDeserialize<Credentials>(HiveHelper.GetSecret(secretName));

            return global::Couchbase.CouchbaseExtensions.OpenCluster(settings, credentials);
        }

        /// <summary>
        /// Returns a Couchbase bucket connection using specified settings and a Docker secret.
        /// </summary>
        /// <param name="settings">The Couchbase settings.</param>
        /// <param name="secretName">The Docker secret name.</param>
        /// <returns>The connected <see cref="IBucket"/>.</returns>
        public static IBucket ConnectBucket(this CouchbaseSettings settings, string secretName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretName));

            var credentials = NeonHelper.JsonDeserialize<Credentials>(HiveHelper.GetSecret(secretName));

            return global::Couchbase.CouchbaseExtensions.OpenBucket(settings, credentials);
        }
    }
}
