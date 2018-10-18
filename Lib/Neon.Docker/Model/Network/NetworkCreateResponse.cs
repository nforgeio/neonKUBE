//-----------------------------------------------------------------------------
// FILE:	    NetworkCreateResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

namespace Neon.Docker
{
    /// <summary>
    /// The response from a <see cref="DockerClient.NetworkCreateAsync(DockerNetwork, CancellationToken)"/> command.
    /// </summary>
    public class NetworkCreateResponse : DockerResponse
    {
        /// <summary>
        /// Constructs the response from a lower-level <see cref="JsonResponse"/>.
        /// </summary>
        /// <param name="response"></param>
        internal NetworkCreateResponse(JsonResponse response)
            : base(response)
        {
            this.Inner = response.AsDynamic();
            this.Id    = Inner().Id;
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Returns the ID for the created network.
        /// </summary>
        public string Id { get; private set; }
    }
}
