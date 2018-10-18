//-----------------------------------------------------------------------------
// FILE:	    DockerClient.Service.cs
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
        // Implements Docker Service related operations.

        /// <summary>
        /// Lists the services deployed to a Docker Swarm.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>A <see cref="ServiceListResponse"/></returns>
        public async Task<ServiceListResponse> ServiceListAsync(CancellationToken cancellationToken = default)
        {
            return new ServiceListResponse(await JsonClient.GetAsync(GetUri("services"), cancellationToken: cancellationToken));
        }
    }
}
