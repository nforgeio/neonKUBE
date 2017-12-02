//-----------------------------------------------------------------------------
// FILE:	    DockerService.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Docker
{
    /// <summary>
    /// Describes a Docker volume.
    /// </summary>
    public class DockerService
    {
        /// <summary>
        /// Constructs an instance from the dynamic volume information returned by
        /// the Docker engine.
        /// </summary>
        /// <param name="source">The dynamic source value.</param>
        internal DockerService(dynamic source)
        {
            this.Inner = source;
            this.Name  = source.Spec.Name;
            this.Image = source.Spec.TaskTemplate.ContainerSpec.Image.Image;

            foreach (var virtualIP in source.Spec.Endpoint.VirtualIPs)
            {
                VirtualAddresses.Add(new VirtualServiceAddress(virtualIP));
            }
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Returns the service name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the service container image.
        /// </summary>
        public string Image { get; private set; }

        /// <summary>
        /// Returns information about the networks and virtual IP addresses assigned
        /// to instances of the service.
        /// </summary>
        public List<VirtualServiceAddress> VirtualAddresses { get; private set; } = new List<VirtualServiceAddress>();
    }
}
