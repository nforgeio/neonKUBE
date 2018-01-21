//-----------------------------------------------------------------------------
// FILE:	    GatewayManager.Database.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;

namespace Neon.Couchbase.SyncGateway
{
    public partial class GatewayManager
    {
        /// <summary>
        /// Returns a list of the Sync Gateway databases.
        /// </summary>
        /// <returns>The list of database names.</returns>
        public async Task<List<string>> DatabaseListAsync()
        {
            var response = await jsonClient.GetAsync(GetUri("_all_dbs"));

            return response.As<List<string>>();
        }

        /// <summary>
        /// Creates a database.
        /// </summary>
        /// <param name="config">The database configuration information.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DatabaseCreateAsync(DatabaseConfiguration config)
        {
            Covenant.Requires<ArgumentNullException>(config != null);

            GatewayException.ThrowOnError(await jsonClient.PutUnsafeAsync(GetUri(config.Name + "/"), config));
        }

        /// <summary>
        /// Reconfigures an existing database.
        /// </summary>
        /// <param name="name">The database name.</param>
        /// <param name="config">The database configuration information.</param>
        /// <returns></returns>
        public async Task DatabaseConfigAsync(string name, DatabaseConfiguration config)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name));
            Covenant.Requires<ArgumentNullException>(config != null);

            GatewayException.ThrowOnError(await jsonClient.PutUnsafeAsync(GetUri(config.Name + "/_config"), config));
        }

        /// <summary>
        /// Returns status information for a database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>A <see cref="DatabaseStatus"/> instance.</returns>
        public async Task<DatabaseStatus> DatabaseStatusAsync(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            var jsonResponse = await jsonClient.GetAsync(GetUri(database + "/"));
            var doc          = jsonResponse.AsDynamic();
            var status       = new DatabaseStatus();

            status.Name                 = doc.db_name;
            status.UpdateSequence       = doc.update_seq;
            status.CommitUpdateSequence = doc.committed_update_seq;
            status.IsCompacting         = doc.compact_running;
            status.DiskFormatVersion    = doc.disk_format_version;
            status.StartTimeUtc         = new DateTime(1970, 1, 1) + TimeSpan.FromSeconds((long)doc.instance_start_time / 1000000.0);
            status.State                = doc.state;

            return status;
        }

        /// <summary>
        /// Removes a database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DatabaseRemoveAsync(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            GatewayException.ThrowOnError(await jsonClient.DeleteUnsafeAsync(GetUri(database + "/")));
        }

        /// <summary>
        /// Compacts a database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DatabaseCompact(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            GatewayException.ThrowOnError(await jsonClient.PostUnsafeAsync(GetUri(database + "/_compact"), string.Empty));
        }

        /// <summary>
        /// Takes a database offline.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DatabaseOffline(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            GatewayException.ThrowOnError(await jsonClient.PostUnsafeAsync(GetUri(database + "/_offline"), string.Empty));
        }

        /// <summary>
        /// Takes a database back online after an optional delay.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="delay">The optional delay.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DatabaseOnline(string database, TimeSpan? delay = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentException>(!delay.HasValue || delay.Value >= TimeSpan.Zero);

            delay = delay ?? TimeSpan.Zero;

            dynamic message = new ExpandoObject();

            message.delay = (int)delay.Value.TotalSeconds;

            GatewayException.ThrowOnError(await jsonClient.PostUnsafeAsync(GetUri(database + "/_online"), message));
        }

        /// <summary>
        /// Purges all document tombstones from a database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task DatabasePurgeAsync(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            dynamic message = new ExpandoObject();

            message.docID = new string[] { "*" };

            GatewayException.ThrowOnError(await jsonClient.PostUnsafeAsync(GetUri(database + "/_purge"), message));
        }

        /// <summary>
        /// Causes all database documents to be reprocessed by the database sync function.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// <note>
        /// Then database must be offline before resynchronizing.
        /// </note>
        /// </remarks>
        public async Task DatabaseResyncAsync(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            GatewayException.ThrowOnError(await jsonClient.PostUnsafeAsync(GetUri(database + "/_resync"), string.Empty));
        }
    }
}
