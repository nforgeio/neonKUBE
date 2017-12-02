//-----------------------------------------------------------------------------
// FILE:	    DockerClient.Network.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Common;

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
        /// <returns>A <see cref="NetworkCreateResponse"/>.</returns>
        public async Task<NetworkCreateResponse> NetworkCreateAsync(DockerNetwork network)
        {
            Covenant.Requires<ArgumentNullException>(network != null);

            // $todo(jeff.lill):
            //
            // Ipam.Config settings aren't working.  Looks like I need to wrap them into
            // a JSON array before sending.  It's a bit weird.

            return new NetworkCreateResponse(await JsonClient.PostAsync(GetUri("networks", "create"), network));
        }

        /// <summary>
        /// Lists the networks managed by the Docker engine.
        /// </summary>
        /// <returns>A list of <see cref="DockerNetwork"/> instances.</returns>
        public async Task<List<DockerNetwork>> NetworkListAsync()
        {
            var response = await JsonClient.GetAsync(GetUri("networks"));
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
        /// <returns>A <see cref="DockerNetwork"/> instance.</returns>
        public async Task<DockerNetwork> NetworkInspect(string nameOrId)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrId));

            var response = await JsonClient.GetAsync(GetUri("networks", nameOrId));

            return new DockerNetwork(response.AsDynamic());
        }

        /// <summary>
        /// Removes a Docker network.
        /// </summary>
        /// <param name="nameOrId">The network name or ID.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task NetworkRemove(string nameOrId)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrId));

            await JsonClient.DeleteAsync(GetUri("networks", nameOrId));
        }
    }
}
