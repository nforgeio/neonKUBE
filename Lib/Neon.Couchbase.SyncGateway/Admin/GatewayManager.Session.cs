//-----------------------------------------------------------------------------
// FILE:	    GatewayManager.Session.cs
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
        /// Creates a database session for a user.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="user">The user name.</param>
        /// <param name="ttl">The optional session time-to-live (defaults to 24 hours).</param>
        /// <returns>The <see cref="Session"/> information.</returns>
        public async Task<Session> SessionCreateAsync(string database, string user, TimeSpan? ttl = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user));
            Covenant.Requires<ArgumentException>(!ttl.HasValue || ttl.Value > TimeSpan.Zero);

            ttl = ttl ?? TimeSpan.FromHours(24);

            dynamic properties = new ExpandoObject();

            properties.name = user;
            properties.ttl  = (long)ttl.Value.TotalSeconds;

            return await jsonClient.PostAsync<Session>(GetUri(database, "_session"), properties);
        }

        /// <summary>
        /// Returns the details for a specific session.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="sessionId">The session ID.</param>
        /// <returns>The <see cref="SessionDetails"/>.</returns>
        public async Task<SessionDetails> SessionGetAsync(string database, string sessionId)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sessionId));

            dynamic response = (await jsonClient.GetAsync(GetUri(database, "_session", sessionId))).AsDynamic();

            var details = new SessionDetails()
            {
                 IsSuccess = response.ok,
                 User      = response.userCtx.name
            };

            foreach (var authenticator in response.authentication_handlers)
            {
                details.Authenticators.Add((string)authenticator);
            }

            foreach (var channel in response.userCtx.channels)
            {
                details.Channels.Add(channel.Name);
            }

            return details;
        }

        /// <summary>
        /// Removes a specific session based on it's ID.
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="sessionId">The session ID.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task SessionRemoveAsync(string database, string sessionId)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(sessionId));

            await jsonClient.DeleteAsync(GetUri(database, "_session", sessionId));
        }

        /// <summary>
        /// Removes a specific user's session based on it's ID or all of a user's sessions.
        /// <note>
        /// The session will not be removed if it doesn't belong to the user.
        /// </note>
        /// </summary>
        /// <param name="database">The database name.</param>
        /// <param name="user">The user name.</param>
        /// <param name="sessionId">The optional session ID.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        /// <remarks>
        /// If <paramref name="sessionId"/> is passed as <c>null</c> then all of the
        /// user's sessions will be removed.  Otherwise, the specific session will be
        /// removed only if it belongs to the user.
        /// </remarks>
        public async Task SessionUserRemoveAsync(string database, string user, string sessionId = null)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(database));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(user));

            if (sessionId != null)
            {
                await jsonClient.DeleteAsync(GetUri(database, "_user", user, "_session", sessionId));
            }
            else
            {
                await jsonClient.DeleteAsync(GetUri(database, "_user", user, "_session"));
            }
        }
    }
}
