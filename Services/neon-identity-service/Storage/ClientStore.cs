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
    /// Implements the <see cref="IClientStore"/> extension for our custom Postgres/Yugabyte database.
    /// </summary>
    public class ClientStore : IClientStore
    {
        //---------------------------------------------------------------------
        // Static members

        private static Func<Task<NpgsqlConnection>> connectionOpenerAsync;
        private static int                          absoluteRefreshTokenLifetimeIndex;
        private static int                          accessTokenLifetimeIndex;
        private static int                          accessTokenTypeIndex;
        private static int                          allowAccessTokensViaBrowserIndex;
        private static int                          allowOfflineAccessIndex;
        private static int                          allowPlainTextPkceIndex;
        private static int                          allowRememberConsentIndex;
        private static int                          alwaysIncludeUserClaimsInIdTokenIndex;
        private static int                          alwaysSendClientClaimsIndex;
        private static int                          authorizationCodeLifetimeIndex;
        private static int                          clientIdIndex;
        private static int                          clientNameIndex;
        private static int                          clientUriIndex;
        private static int                          enableLocalLoginIndex;
        private static int                          enabledIndex;
        private static int                          identityTokenLifetimeIndex;
        private static int                          includeJwtIdIndex;
        private static int                          logoUriIndex;
        private static int                          logoutSessionRequiredIndex;
        private static int                          logoutUriIndex;
        private static int                          prefixClientClaimsIndex;
        private static int                          protocolTypeIndex;
        private static int                          refreshTokenExpirationIndex;
        private static int                          refreshTokenUsageIndex;
        private static int                          requireClientSecretIndex;
        private static int                          requireConsentIndex;
        private static int                          requirePkceIndex;
        private static int                          slidingRefreshTokenLifetimeIndex;
        private static int                          updateAccessTokenClaimsOnRefreshIndex;

        /// <summary>
        /// <para>
        /// Creates the <see cref="ClientStore"/> instance for the process.
        /// </para>
        /// <note>
        /// This may be called more than once per process but the same <paramref name="connectionOpenerAsync"/>
        /// value must be passed each time.
        /// </note>
        /// </summary>
        /// <param name="connectionOpenerAsync">Asynchronous function that returns a new database connection for operations.</param>
        /// <returns></returns>
        public async static Task<ClientStore> CreateAsync(Func<Task<NpgsqlConnection>> connectionOpenerAsync)
        {
            Covenant.Requires<ArgumentNullException>(connectionOpenerAsync != null, nameof(connectionOpenerAsync));

            if (ClientStore.connectionOpenerAsync != null && !object.ReferenceEquals(ClientStore.connectionOpenerAsync, connectionOpenerAsync))
            {
                throw new InvalidOperationException($"[{nameof(ClientStore)}.{nameof(CreateAsync)}] Cannot be called with different [{nameof(connectionOpenerAsync)}] values in the same process.");
            }

            if (ClientStore.connectionOpenerAsync == null)
            {
                ClientStore.connectionOpenerAsync = connectionOpenerAsync;

                // Prepare the find command and compute the result column indexes.

                await using (var connection = await connectionOpenerAsync())
                {
                    var findCommand = await CreateFindCommand(connection, null, forPrepareOnly: true);

                    await findCommand.PrepareAsync();
                }
            }

            return new ClientStore();
        }

        /// <summary>
        /// Creates a prepared command that queries the <b>ClientStore</b> table for a
        /// specific <paramref name="clientId"/>.
        /// </summary>
        /// <param name="connection">The database connection.</param>
        /// <param name="clientId">The requested client ID.</param>
        /// <param name="forPrepareOnly">
        /// Optionally specifies that the command is intended to be prepared only and not
        /// be executed.  This is used by the static constructor to do the initial
        /// command preparation.
        /// </param>
        /// <returns>The find command.</returns>
        private async static Task<NpgsqlCommand> CreateFindCommand(NpgsqlConnection connection, string clientId, bool forPrepareOnly = false)
        {
            Covenant.Requires<ArgumentNullException>(connection != null, nameof(connection));

            var findCommand = new NpgsqlCommand(
@"SELECT AbsoluteRefreshTokenLifetime,
         AccessTokenLifetime,
         AccessTokenType,
         AllowAccessTokensViaBrowser,
         AllowOfflineAccess,
         AllowPlainTextPkce,
         AllowRememberConsent,
         AlwaysIncludeUserClaimsInIdToken,
         AlwaysSendClientClaims,
         AuthorizationCodeLifetime,
         ClientId,
         ClientName,
         ClientUri,
         EnableLocalLogin,
         Enabled,
         IdentityTokenLifetime,
         IncludeJwtId,
         LogoUri,
         LogoutSessionRequired,
         LogoutUri,
         PrefixClientClaims,
         ProtocolType,
         RefreshTokenExpiration,
         RefreshTokenUsage,
         RequireClientSecret,
         RequireConsent,
         RequirePkce,
         SlidingRefreshTokenLifetime,
         UpdateAccessTokenClaimsOnRefresh,
    FROM Clients
    WHERE ClientId = @clientId;", connection);

            if (forPrepareOnly)
            {
                findCommand.Parameters.Add("clientId", NpgsqlTypes.NpgsqlDbType.Integer);
                await findCommand.PrepareAsync();

                var columnIndex = 1;

                absoluteRefreshTokenLifetimeIndex     = columnIndex++;
                accessTokenLifetimeIndex              = columnIndex++;
                accessTokenTypeIndex                  = columnIndex++;
                allowAccessTokensViaBrowserIndex      = columnIndex++;
                allowOfflineAccessIndex               = columnIndex++;
                allowPlainTextPkceIndex               = columnIndex++;
                allowRememberConsentIndex             = columnIndex++;
                alwaysIncludeUserClaimsInIdTokenIndex = columnIndex++;
                alwaysSendClientClaimsIndex           = columnIndex++;
                authorizationCodeLifetimeIndex        = columnIndex++;
                clientIdIndex                         = columnIndex++;
                clientNameIndex                       = columnIndex++;
                clientUriIndex                        = columnIndex++;
                enableLocalLoginIndex                 = columnIndex++;
                enabledIndex                          = columnIndex++;
                identityTokenLifetimeIndex            = columnIndex++;
                includeJwtIdIndex                     = columnIndex++;
                logoUriIndex                          = columnIndex++;
                logoutSessionRequiredIndex            = columnIndex++;
                logoutUriIndex                        = columnIndex++;
                prefixClientClaimsIndex               = columnIndex++;
                protocolTypeIndex                     = columnIndex++;
                refreshTokenExpirationIndex           = columnIndex++;
                refreshTokenUsageIndex                = columnIndex++;
                requireClientSecretIndex              = columnIndex++;
                requireConsentIndex                   = columnIndex++;
                requirePkceIndex                      = columnIndex++;
                slidingRefreshTokenLifetimeIndex      = columnIndex++;
                updateAccessTokenClaimsOnRefreshIndex = columnIndex++;
            }
            else
            {
                findCommand.Parameters.Add(new NpgsqlParameter<string>("clientId", clientId));
            }

            return findCommand;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        private ClientStore()
        {
        }

        /// <summary>
        /// Returns the information for a client by client ID.
        /// </summary>
        /// <param name="clientId">The client ID.</param>
        /// <returns>The <see cref="Client"/> information or <c>null</c> when the client doesn't exist.</returns>
        public async Task<Client> FindClientByIdAsync(string clientId)
        {
            await Task.CompletedTask;
            throw new NotImplementedException();

            //await using (var connection = await connectionOpenerAsync())
            //{
            //    findCommand.ExecuteReaderAsync();
            //}
        }
    }
}
