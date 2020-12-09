//-----------------------------------------------------------------------------
// FILE:	    ClientStore.Extensions.cs
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
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Diagnostics;
using Neon.Service;
using Neon.Postgres;

using IdentityServer4;
using IdentityServer4.Models;
using IdentityServer4.Services;
using IdentityServer4.Stores;

using Npgsql;
using NpgsqlTypes;

namespace Neon.Identity
{
    public partial class ClientStore : IClientStore, IClientStoreExtensions
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Inserts or updates a client.
        /// </summary>
        private class UpsertClientCommand : PreparedCommand
        {
            //-----------------------------------------------------------------
            // Static members

            private const string sqlText = 
@"
INSERT INTO Clients
SET ClientId   = @clientId,
    ClientJson = @clientJson
ON CONFLICT DO UPDATE;
";

            // Parameter definitions.

            private static readonly Dictionary<string, NpgsqlDbType> paramDefinitions =
                new Dictionary<string, NpgsqlDbType>()
                {
                    { "clientId", NpgsqlDbType.Text },
                    { "clientJson", NpgsqlDbType.Jsonb }
                };

            //-----------------------------------------------------------------
            // Instance members

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="connection">The database connection.</param>
            public UpsertClientCommand(NpgsqlConnection connection)
                : base(connection, sqlText, paramDefinitions, prepareNow: true)
            {
            }

            /// <summary>
            /// Upserts a client.
            /// </summary>
            /// <param name="client">The client being upserted.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            public async Task UpsertClientAsync(Client client)
            {
                var command = this.Clone();

                command.Parameters["clientId"].Value   = client.ClientId;
                command.Parameters["clientJson"].Value = NeonHelper.JsonSerialize(client);

                await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Removes a client from the database if it exists.
        /// </summary>
        private class RemoveClientCommand : PreparedCommand
        {
            //-----------------------------------------------------------------
            // Static members

            private const string sqlText = "DELETE FROM Clients WHERE ClientId = @clientId;";

            // Parameter definitions.

            private static readonly Dictionary<string, NpgsqlDbType> paramDefinitions =
                new Dictionary<string, NpgsqlDbType>()
                {
                    { "clientId", NpgsqlDbType.Text }
                };

            //-----------------------------------------------------------------
            // Instance members

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="connection">The database connection.</param>
            public RemoveClientCommand(NpgsqlConnection connection)
                : base(connection, sqlText, paramDefinitions, prepareNow: true)
            {
            }

            /// <summary>
            /// Removes a client by ID.
            /// </summary>
            /// <param name="clientId">The ID of the client being deleted.</param>
            /// <returns>The tracking <see cref="Task"/>.</returns>
            public async Task RemoveClientAsync(string clientId)
            {
                var command = this.Clone();

                command.Parameters["clientId"].Value = clientId;

                await command.ExecuteNonQueryAsync();
            }
        }

        /// <summary>
        /// Lists all clients from the database.
        /// </summary>
        private class ListClientsCommand : PreparedCommand
        {
            //-----------------------------------------------------------------
            // Static members

            private const string sqlText = "SELECT ClientJson FROM Clients;";

            //-----------------------------------------------------------------
            // Instance members

            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="connection">The database connection.</param>
            public ListClientsCommand(NpgsqlConnection connection)
                : base(connection, sqlText, prepareNow: true)
            {
            }

            /// <summary>
            /// Lists all clients.
            /// </summary>
            /// <returns>The list of clients.</returns>
            public async Task<List<Client>> ListClientsAsync()
            {
                var command = this.Clone();
                var list    = new List<Client>();

                await foreach (var row in (await command.ExecuteReaderAsync()).ToAsyncEnumerable())
                {
                    var client = NeonHelper.JsonDeserialize<Client>(row.GetString(0));

                    list.Add(client);
                }

                return list;
            }
        }

        //---------------------------------------------------------------------
        // IClientStoreExtensions implementation

        /// <inheritdoc/>/>
        public async Task UpsertClientAsync(Client client)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            await upsertClientCommand.UpsertClientAsync(client);
        }

        /// <inheritdoc/>/>
        public async Task SyncClients(IEnumerable<Client> clients)
        {
            // We're going to use a serialzable transaction to list the existing
            // clients, remove any clients not present in the list passed, and
            // then upsert the clients.

            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                var clientIds = new HashSet<string>();

                foreach (var client in clients)
                {
                    clientIds.Add(client.ClientId);
                }

                foreach (var existingClient in await listClientsCommand.ListClientsAsync())
                {
                    if (!clientIds.Contains(existingClient.ClientId))
                    {
                        await removeClientCommand.RemoveClientAsync(existingClient.ClientId);
                    }
                }

                foreach (var client in clients)
                {
                    await upsertClientCommand.UpsertClientAsync(client);
                }

                await transaction.CommitAsync();
            }
        }

        /// <inheritdoc/>/>
        public async Task RemoveClient(Client client)
        {
            Covenant.Requires<ArgumentNullException>(client != null, nameof(client));

            await removeClientCommand.RemoveClientAsync(client.ClientId);
        }

        /// <inheritdoc/>/>
        public async Task RemoveClient(string clientId)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clientId), nameof(clientId));

            await removeClientCommand.RemoveClientAsync(clientId);
        }
    }
}
