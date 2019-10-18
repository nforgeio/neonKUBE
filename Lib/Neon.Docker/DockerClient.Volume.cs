//-----------------------------------------------------------------------------
// FILE:	    DockerClient.Volume.cs
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
        // Implements Docker Volume related operations.

        /// <summary>
        /// Lists the volumes managed by the Docker engine.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="VolumeListResponse"/></returns>
        public async Task<VolumeListResponse> VolumeListAsync(CancellationToken cancellationToken = default)
        {
            await SyncContext.ResetAsync;

            return new VolumeListResponse(await JsonClient.GetAsync(GetUri("volumes"), cancellationToken: cancellationToken));
        }

        /// <summary>
        /// Creates a Docker volume.
        /// </summary>
        /// <param name="name">The optional volume name (Docker will generate a name if this is not specified).</param>
        /// <param name="driver">The optional volume driver name (defaults to <c>local).</c></param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="driverOpts">The custom driver options.</param>
        /// <returns></returns>
        public async Task<DockerVolume> VolumeCreate(string name = null, string driver = null, CancellationToken cancellationToken = default, params KeyValuePair<string, string>[] driverOpts)
        {
            await SyncContext.ResetAsync;

            dynamic args = new ExpandoObject();

            if (!string.IsNullOrEmpty(name))
            {
                args.Name = name;
            }

            if (!string.IsNullOrEmpty(driver))
            {
                args.Driver = driver;
            }

            if (driverOpts != null && driverOpts.Length > 0)
            {
                args.DriverOpts = driverOpts;
            }

            var response = await JsonClient.PostAsync(GetUri("volumes", "create"), args, cancellationToken: cancellationToken);

            return new DockerVolume(response.AsDynamic());
        }

        /// <summary>
        /// Returns information about a Docker volume.
        /// </summary>
        /// <param name="nameOrId">The volume name or ID.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The <see cref="DockerVolume"/>.</returns>
        public async Task<DockerVolume> VolumeInspect(string nameOrId, CancellationToken cancellationToken = default)
        {
            await SyncContext.ResetAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrId), nameof(nameOrId));

            var response = await JsonClient.GetAsync(GetUri("volumes", nameOrId), cancellationToken: cancellationToken);

            return new DockerVolume(response.AsDynamic());
        }

        /// <summary>
        /// Removes a Docker volume.
        /// </summary>
        /// <param name="nameOrId">The volume name or ID.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The tracking <see cref="Task"/>.</returns>
        public async Task VolumeRemove(string nameOrId, CancellationToken cancellationToken = default)
        {
            await SyncContext.ResetAsync;
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrId), nameof(nameOrId));

            await JsonClient.DeleteAsync(GetUri("volumes", nameOrId), cancellationToken: cancellationToken);
        }
    }
}
