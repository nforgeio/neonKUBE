//-----------------------------------------------------------------------------
// FILE:	    DockerNodeManagerStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
    /// Describes a Docker manager node's status.
    /// </summary>
    public class DockerNodeManagerStatus
    {
        /// <summary>
        /// Constructs an instance from the dynamic node manager status returned by
        /// the Docker engine.
        /// </summary>
        /// <param name="source">The dynamic source value.</param>
        internal DockerNodeManagerStatus(dynamic source)
        {
            this.Inner        = source;
            this.Leader       = (source.Leader is bool) ? source.Leader : false; // $todo(jefflill): Need a general way of handling this?
            this.Reachability = source.Reachability;
            this.Addr         = source.Addr;
        }

        /// <summary>
        /// Returns the raw <v>dynamic</v> object actually returned by Docker.
        /// You may use this to access newer Docker properties that have not
        /// yet been wrapped by this class.
        /// </summary>
        public dynamic Inner { get; private set; }

        /// <summary>
        /// Indicates whether the parent node is currently the swarm leader.
        /// </summary>
        public bool Leader { get; private set; }

        /// <summary>
        /// Provides an indication of this manager node is able to communicate 
        /// with a quorum of other managers.
        /// </summary>
        public string Reachability { get; private set; }

        /// <summary>
        /// Returns the address and port of the current lead manager node.
        /// </summary>
        public string Addr { get; private set; }
    }
}
