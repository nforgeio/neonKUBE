//-----------------------------------------------------------------------------
// FILE:	    IClientStoreExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
    /// Defines additional client store operations.
    /// </summary>
    public interface IClientStoreExtensions
    {
        /// <summary>
        /// Upserts a client to the database.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task UpsertClientAsync(Client client);

        /// <summary>
        /// Updates the set of clients in the database by upserting and clients
        /// passed and removing any clients that are in the database but are
        /// not in the list.
        /// </summary>
        /// <param name="clients">The clients expected in the database.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task SyncClients(IEnumerable<Client> clients);

        /// <summary>
        /// Removes a client from the database if present.
        /// </summary>
        /// <param name="client">The client to be removed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task RemoveClient(Client client);

        /// <summary>
        /// Removes a client from the database by ID if present.
        /// </summary>
        /// <param name="cliendId">The ID of the client to be removed.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        Task RemoveClient(string cliendId);
    }
}
