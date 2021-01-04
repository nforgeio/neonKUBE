//-----------------------------------------------------------------------------
// FILE:	    IdentityHelper.cs
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
    /// Implements utility methods for managing the identity database.  These are
    /// called internally by the <b>neon-identity-operator</b> in production but
    /// may also be called by unit tests to initialize the database before starting
    /// composed <b>neon-identity-service</b> instances.
    /// </summary>
    public static class IdentityHelper
    {
        private static INeonLogger logger = LogManager.Default.GetLogger(nameof(IdentityHelper));

        /// <summary>
        /// Creates the identity database if it doesn't already exist and then
        /// ensures that its schema is up to date.
        /// </summary>
        /// <param name="connectionString">
        /// The connection string for the master database.  The user must have privileges 
        /// required to create the database and the identity service user.
        /// </param>
        /// <param name="databaseName">
        /// Specifies the identity database name.  This must satisfy standard SQL restrictions
        /// without quoting. 
        /// </param>
        /// <param name="userName">Specifies the name of the user for the identity service.</param>
        /// <param name="password">The user password.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <exception cref="SchemaManagerException">Thrown if the database initialization failed.</exception>
        public async static Task InitializeDatabaseAsync(string connectionString, string databaseName, string userName, string password)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(connectionString), nameof(connectionString));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(databaseName), nameof(databaseName));
            Covenant.Requires<ArgumentNullException>(password != null, nameof(password));

            logger.LogInfo("Initializing identity database.");

            var schemaDirectory = Assembly.GetExecutingAssembly().GetResourceFileSystem("Neon.Identity.Schema");

            var variables = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase)
            {
                { "sts_user", userName },
                { "sts_password", password }
            };

            using (var identityDb = new NpgsqlConnection(connectionString))
            {
                await identityDb.OpenAsync();

                using (var schemaManager = new SchemaManager(identityDb, databaseName, schemaDirectory, variables))
                {
                    var status  = await schemaManager.GetStatusAsync();
                    var message = (string)null;

                    switch (status.SchemaStatus)
                    {
                        case SchemaStatus.ExistsNoSchema:

                            logger.LogInfo($"[{databaseName}] database exists but is not initialized.");
                            break;

                        case SchemaStatus.ExistsWithSchema:

                            logger.LogInfo($"[{databaseName}] database exists with [version={status.Version}].");
                            break;

                        case SchemaStatus.NotFound:

                            logger.LogInfo($"[{databaseName}] database does not exist.");
                            break;

                        case SchemaStatus.Updating:

                            message = $"[{databaseName}] database is currently being updated by [updater={status.Updater}].";

                            logger.LogWarn(message);
                            throw new SchemaManagerException(message);

                        case SchemaStatus.UpgradeError:

                            message = $"[{databaseName}] database is in an inconsistent state due to a previous update failure [updater={status.Updater}] [error={status.Error}].  This will require manual intervention.";

                            logger.LogError(message);
                            throw new SchemaManagerException(message);

                        default:

                            throw new NotImplementedException();
                    }

                    await schemaManager.CreateDatabaseAsync();

                    var version = await schemaManager.UpgradeDatabaseAsync();

                    if (version == status.Version)
                    {
                        logger.LogInfo($"[{databaseName}] database is up to date at [version={version}]");
                    }
                    else
                    {
                        logger.LogInfo($"[{databaseName}] database is upgraded from [version={status.Version}] to [version={version}].");
                    }
                }
            }
        }
    }
}
