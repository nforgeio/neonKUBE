//-----------------------------------------------------------------------------
// FILE:	    CorsPolicyService.cs
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

namespace Neon.Identity
{
    /// <summary>
    /// Implements the identity <b>CORS Policy Service</b> persistence to a Postgres database.
    /// </summary>
    public class CorsPolicyService : ICorsPolicyService
    {
        private NpgsqlConnection connection;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="connection">The open database connection.</param>
        public CorsPolicyService(NpgsqlConnection connection)
        {
            Covenant.Requires<ArgumentNullException>(connection != null, nameof(connection));
            Covenant.Requires<ArgumentException>(connection.State == ConnectionState.Open, nameof(connection));

            this.connection = connection;
        }

        //---------------------------------------------------------------------
        // ICorsPolicyService implementation

        /// <inheritdoc/>
        public Task<bool> IsOriginAllowedAsync(string origin)
        {
            throw new NotImplementedException();
        }
    }
}
