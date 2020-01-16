//-----------------------------------------------------------------------------
// FILE:	    DockerNetworkContainer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Dynamic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Docker
{
    /// <summary>
    /// Describes a container attached to a Docker network.
    /// </summary>
    public class DockerNetworkContainer
    {
        /// <summary>
        /// Constructs an instance from the dynamic attached container information
        /// returned by docker.
        /// </summary>
        /// <param name="source">The dynamic source value.</param>
        public DockerNetworkContainer(dynamic source)
        {
            this.Inner = source;
            this.Id    = source.Name;

            var properties   = source.Value;

            this.EndpointId  = properties.EndpointID;
            this.MacAddress  = properties.MacAddress;
            this.IPv4Address = properties.IPv4Address;
            this.IPv6Address = properties.IPv6Address;
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Returns the container's ID.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Returns the container's endpoint ID.
        /// </summary>
        public string EndpointId { get; private set; }

        /// <summary>
        /// Returns the container's MAC address.
        /// </summary>
        public string MacAddress { get; private set; }

        /// <summary>
        /// Returns the container's IPv4 address.
        /// </summary>
        public string IPv4Address { get; private set; }

        /// <summary>
        /// Returns the container's IPv6 address.
        /// </summary>
        public string IPv6Address { get; private set; }
    }
}
