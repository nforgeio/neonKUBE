//-----------------------------------------------------------------------------
// FILE:	    GatewayManager.User.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
        /// Returns a list of the Sync Gateway user names for a database.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <returns>The list of user names.</returns>
        public async Task<List<string>> UserListAsync(string database)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));

            var response = await jsonClient.GetAsync(GetUri(database, "_user/"));

            return response.As<List<string>>();
        }

        /// <summary>
        /// Creates a database user.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="properties">The user properties.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task UserCreateAsync(string database, UserProperties properties)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(properties != null);

            await jsonClient.PostAsync(GetUri(database, "_user/"), properties);
        }

        /// <summary>
        /// Gets a database user's properties.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="user">The user name.</param>
        /// <returns>The <see cref="UserProperties"/> properties.</returns>
        public async Task<UserProperties> UserGetAsync(string database, string user)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user));

            return await jsonClient.GetAsync<UserProperties>(GetUri(database, "_user", user));
        }

        /// <summary>
        /// Updates a database user.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="user">The user name.</param>
        /// <param name="properties">The new user properties.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task UserUpdateAsync(string database, string user, UserProperties properties)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user));
            Covenant.Requires<ArgumentNullException>(properties != null);

            await jsonClient.PutAsync(GetUri(database, "_user", user), properties);
        }

        /// <summary>
        /// Removes a database user.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="user">The user name.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task UserRemoveAsync(string database, string user)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user));

            await jsonClient.DeleteAsync(GetUri(database, "_user", user));
        }
    }
}
