//-----------------------------------------------------------------------------
// FILE:	    DockerNetworkIPAM.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
    /// Describes a Docker network's IPAM configuration.
    /// </summary>
    public class DockerNetworkIpam
    {
        /// <summary>
        /// Constructs an instance from the dynamic network IPAM information
        /// returned by docker.
        /// </summary>
        /// <param name="source">The dynamic source value.</param>
        public DockerNetworkIpam(dynamic source)
        {
            this.Inner  = source;
            this.Driver = source.Driver;
            this.Config = new Dictionary<string, string>();

            foreach (var subConfig in source.Config)
            {
                foreach (var item in subConfig)
                {
                    Config.Add(item.Name, item.Value.ToString());
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
        /// Returns the IPAM driver.
        /// </summary>
        public string Driver { get; private set; }

        /// <summary>
        /// Returns the IPAM configuration settings.
        /// </summary>
        public Dictionary<string, string> Config { get; private set; }
    }
}
