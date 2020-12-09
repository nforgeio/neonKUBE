//-----------------------------------------------------------------------------
// FILE:	    PersistedGrantStore.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Service;

using IdentityServer4;
using IdentityServer4.Stores;
using IdentityServer4.Models;
using IdentityServer4.Services;

using Npgsql;

namespace NeonIdentityService
{
    /// <summary>
    /// Implements the <see cref="IPersistedGrantStore"/> extension for our custom Postgres/Yugabyte database.
    /// </summary>
    public class PersistedGrantStore : IPersistedGrantStore
    {
        /// <summary>
        /// Gets all grants that satisfy a filter.
        /// </summary>
        /// <param name="filter">The grant filter.</param>
        /// <returns>The persisted filters.</returns>
        public Task<IEnumerable<PersistedGrant>> GetAllAsync(PersistedGrantFilter filter)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Gets a grant based on its key.
        /// </summary>
        /// <param name="key">The grant key.</param>
        /// <returns>The persisted grant.</returns>
        public Task<PersistedGrant> GetAsync(string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Remove all grants that satisfy a filter.
        /// </summary>
        /// <param name="filter">The grant filter.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task RemoveAllAsync(PersistedGrantFilter filter)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Removes a specific grant, based on its key.
        /// </summary>
        /// <param name="key">The grant key.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task RemoveAsync(string key)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Persists a grant.
        /// </summary>
        /// <param name="grant">The grant.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public Task StoreAsync(PersistedGrant grant)
        {
            throw new NotImplementedException();
        }
    }
}
