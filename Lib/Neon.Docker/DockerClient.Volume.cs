//-----------------------------------------------------------------------------
// FILE:	    DockerClient.Volume.cs
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
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrId));

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
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(nameOrId));

            await JsonClient.DeleteAsync(GetUri("volumes", nameOrId), cancellationToken: cancellationToken);
        }
    }
}
