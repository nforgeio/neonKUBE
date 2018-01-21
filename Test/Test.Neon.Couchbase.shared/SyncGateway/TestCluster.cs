//-----------------------------------------------------------------------------
// FILE:	    TestCluster.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;

using Neon.Common;
using Neon.Couchbase.SyncGateway;

using Xunit;

namespace TestSyncGateway
{
    /// <summary>
    /// Manages the local Sync Gateway server state for testing purposes.
    /// </summary>
    public static class TestCluster
    {
        /// <summary>
        /// The URI to the in-memory Walrus test server.
        /// </summary>
        public const string WalrusUri = "walrus:data";

        /// <summary>
        /// Returns the settings to be used for accessing the test Sync Gateway.
        /// </summary>
        public static GatewaySettings Settings
        {
            get
            {
                return new GatewaySettings()
                {
                    Host = "localhost"
                };
            }
        }

        /// <summary>
        /// Creates a <see cref="Gateway"/> connection to the local test Sync Gateway.
        /// </summary>
        /// <returns>The <see cref="Gateway"/>.</returns>
        public static Gateway CreateGateway()
        {
            return new Gateway(Settings);
        }

        /// <summary>
        /// Removes all databases from the gateway.
        /// </summary>
        public static async Task ClearAsync()
        {
            using (var gateway = new Gateway(Settings))
            {
                var manager = gateway.CreateManager();

                foreach (var database in await manager.DatabaseListAsync())
                {
                    await manager.DatabaseRemoveAsync(database);
                }
            }

            // $hack(jeff.lill): 
            //
            // Looks like the test server needs some time to stablize after
            // deleting databases.

            await Task.Delay(1000);
        }

        /// <summary>
        /// Creates the specified databases with each referencing a bucket
        /// named <b>[database]_bucket</b>.
        /// </summary>
        /// <param name="databases">The list of databases to create.</param>
        public static async Task CreateDatabaseAsync(params string[] databases)
        {
            using (var gateway = new Gateway(Settings))
            {
                var manager = gateway.CreateManager();

                foreach (var name in databases)
                {
                    var databaseConfig = new DatabaseConfiguration()
                    {
                        Name   = name,
                        Bucket = $"{name}_bucket",
                        Server = WalrusUri,
                        Sync   = "function (doc, oldDoc) { channel(doc.channels); }"
                    };

                    await manager.DatabaseCreateAsync(databaseConfig);
                }
            }
        }
    }
}
