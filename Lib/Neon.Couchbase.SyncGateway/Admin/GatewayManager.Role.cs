//-----------------------------------------------------------------------------
// FILE:	    GatewayManager.Role.cs
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
        /// Returns a list of the Sync Gateway roles for a database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The list of role names.</returns>
        public async Task<List<string>> RoleListAsync(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            var response = await jsonClient.GetAsync(GetUri(database, "_role/"));

            return response.As<List<string>>();
        }

        /// <summary>
        /// Creates a database user.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="properties">The role properties.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task RoleCreateAsync(string database, RoleProperties properties)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(properties != null);

            await jsonClient.PostAsync(GetUri(database, "_role/"), properties);
        }

        /// <summary>
        /// Gets a database role's properties.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="role">The role name.</param>
        /// <returns>The <see cref="RoleProperties"/> properties.</returns>
        public async Task<RoleProperties> RoleGetAsync(string database, string role)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(role));

            return await jsonClient.GetAsync<RoleProperties>(GetUri(database, "_role", role));
        }

        /// <summary>
        /// Updates a database role.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="role">The user name.</param>
        /// <param name="properties">The new role properties.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task RoleUpdateAsync(string database, string role, RoleProperties properties)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(role));
            Covenant.Requires<ArgumentNullException>(properties != null);

            await jsonClient.PutAsync(GetUri(database, "_role", role), properties);
        }

        /// <summary>
        /// Removes a database role.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="role">The user name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task RoleRemoveAsync(string database, string role)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(role));

            await jsonClient.DeleteAsync(GetUri(database, "_role", role));
        }
    }
}
