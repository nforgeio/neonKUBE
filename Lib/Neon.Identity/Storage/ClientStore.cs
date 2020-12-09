//-----------------------------------------------------------------------------
// FILE:	    ClientStore.cs
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
    /// <summary>
    /// Implements the identity <b>Client Store</b> persistence to a Postgres database.
    /// </summary>
    public partial class ClientStore : IClientStore, IClientStoreExtensions
    {
        //---------------------------------------------------------------------
        // Private types

        /// <summary>
        /// Queries for a client by <b>ClientId</b>.
        /// </summary>
        private class QueryClientCommand : PreparedCommand
        {
            //-----------------------------------------------------------------
            // Static members

            private const string sqlText = @"SELECT ClientJson FROM Clients WHERE ClientId = @clientId;";

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
            public QueryClientCommand(NpgsqlConnection connection)
                : base(connection, sqlText, paramDefinitions, prepareNow: true)
            {
            }

            /// <summary>
            /// Returns direct properties for the identified client.
            /// </summary>
            /// <param name="clientId">The target client ID.</param>
            /// <returns>The <see cref="Client"/> or <c>null</c>.</returns>
            public async Task<Client> FindClientByIdAsync(string clientId)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clientId), nameof(clientId));

                var command = this.Clone();

                command.Parameters["clientId"].Value = clientId;

                await foreach (var row in (await command.ExecuteReaderAsync()).ToAsyncEnumerable())
                {
                    // There's only ever going to be one client with a given ID.

                    return NeonHelper.JsonDeserialize<Client>(row.GetString(0));
                }

                return null;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        private NpgsqlConnection        connection;
        private QueryClientCommand      queryClientCommand;
        private UpsertClientCommand     upsertClientCommand;
        private ListClientsCommand      listClientsCommand;
        private RemoveClientCommand     removeClientCommand;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connection">The open database connection.</param>
        public ClientStore(NpgsqlConnection connection)
        {
            Covenant.Requires<ArgumentNullException>(connection != null, nameof(connection));
            Covenant.Requires<ArgumentException>(connection.State == ConnectionState.Open, nameof(connection));

            this.connection          = connection;
            this.queryClientCommand  = new QueryClientCommand(connection);
            this.upsertClientCommand = new UpsertClientCommand(connection);
            this.listClientsCommand  = new ListClientsCommand(connection);
            this.removeClientCommand = new RemoveClientCommand(connection);
        }

        //---------------------------------------------------------------------
        // IClientStore implementation

        /// <inheritdoc/>
        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            // Fetch the client if it exixts.

            var client = await queryClientCommand.FindClientByIdAsync(clientId);

            if (client == null)
            {
                return null;
            }

            // The client exists so load all of the related rows from the subtables.

            return client;
        }
    }
}
