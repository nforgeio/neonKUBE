//-----------------------------------------------------------------------------
// FILE:	    VolumeListResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;

namespace Neon.Docker
{
    /// <summary>
    /// The response from a <see cref="DockerClient.VolumeListAsync"/> command.
    /// </summary>
    public class VolumeListResponse : DockerResponse
    {
        /// <summary>
        /// Constructs the response from a lower-level <see cref="JsonResponse"/>.
        /// </summary>
        /// <param name="response"></param>
        internal VolumeListResponse(JsonResponse response)
            : base(response)
        {
            this.Inner = response.AsDynamic();

            var volumes = this.Inner.Volumes;

            if (volumes != null)
            {
                foreach (var volume in volumes)
                {
                    this.Volumes.Add(new DockerVolume(volume));
                }
            }
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Returns the list of volumes returned by the Docker engine.
        /// </summary>
        public List<DockerVolume> Volumes { get; private set; } = new List<DockerVolume>();
    }
}
