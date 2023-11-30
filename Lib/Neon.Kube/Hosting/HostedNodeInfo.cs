// -----------------------------------------------------------------------------
// FILE:	    HostedNodeInfo.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Kube.ClusterDef;

namespace Neon.Kube.Hosting
{
    /// <summary>
    /// used for validating that cluster node definitions are valid for a hosting environment.
    /// </summary>
    public struct HostedNodeInfo
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">Specifies the node name.</param>
        /// <param name="role">Specifies the node role, one of the <see cref="NodeRole"/> values.</param>
        /// <param name="vCpus">Specifies the number of virtual CPUs assigned to the node.</param>
        /// <param name="memory">Specifies the bytes of memory assigned to the node.</param>
        public HostedNodeInfo(string name, string role, int vCpus, long memory)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(role), nameof(role));
            Covenant.Requires<ArgumentException>(vCpus > 0, nameof(vCpus));
            Covenant.Requires<ArgumentException>(memory > 0, nameof(memory));

            this.Name   = name;
            this.Role   = role;
            this.VCpus  = vCpus;
            this.Memory = memory;
        }

        /// <summary>
        /// Returns the node name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the node role, one of the <see cref="NodeRole"/> values.
        /// </summary>
        public string Role { get; private set; }

        /// <summary>
        /// Returns the number of virtual CPUs assigned to the node.
        /// </summary>
        public int VCpus { get; private set; }

        /// <summary>
        /// Returns the bytes of memory assigned to the node.
        /// </summary>
        public long Memory { get; set; }
    }
}
