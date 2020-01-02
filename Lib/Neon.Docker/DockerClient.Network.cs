//-----------------------------------------------------------------------------
// FILE:	    DockerClient.Network.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Tasks;

namespace Neon.Docker
{
    public partial class DockerClient
    {
        //---------------------------------------------------------------------
        // Implements Docker Network related operations.

        /// <summary>
        /// Creates a Docker network.
        /// </summary>
        /// <param name="network">The network details.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="NetworkCreateResponse"/>.</returns>
        public async Task<NetworkCreateResponse> NetworkCreateAsync(DockerNetwork network, CancellationToken cancellationToken = default)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(network != null, nameof(network));

            // $todo(jefflill):
            //
            // Ipam.Config settings aren't working.  Looks like I need to wrap them into
            // a JSON array before sending.  It's a bit weird.

            return new NetworkCreateResponse(await JsonClient.PostAsync(GetUri("networks", "create"), network, cancellationToken: cancellationToken));
        }

        /// <summary>
        /// Lists the networks managed by the Docker engine.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A list of <see cref="DockerNetwork"/> instances.</returns>
        public async Task<List<DockerNetwork>> NetworkListAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.ClearAsync;

            var response = await JsonClient.GetAsync(GetUri("networks"), cancellationToken: cancellationToken);
            var networks = new List<DockerNetwork>();

            foreach (var item in response.AsDynamic())
            {
                networks.Add(new DockerNetwork(item));
            }

            return networks;
        }

        /// <summary>
        /// Returns details about a specific Docker network.
        /// </summary>
        /// <param name="nameOrId">The network name or ID.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="DockerNetwork"/> instance.</returns>
        public async Task<DockerNetwork> NetworkInspect(string nameOrId, CancellationToken cancellationToken = default)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrId), nameof(nameOrId));

            var response = await JsonClient.GetAsync(GetUri("networks", nameOrId), cancellationToken: cancellationToken);

            return new DockerNetwork(response.AsDynamic());
        }

        /// <summary>
        /// Removes a Docker network.
        /// </summary>
        /// <param name="nameOrId">The network name or ID.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task NetworkRemove(string nameOrId, CancellationToken cancellationToken = default)
        {
            await SyncContext.ClearAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrId), nameof(nameOrId));

            await JsonClient.DeleteAsync(GetUri("networks", nameOrId), cancellationToken: cancellationToken);
        }
    }
}
