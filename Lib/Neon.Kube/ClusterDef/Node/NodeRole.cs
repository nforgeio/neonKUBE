//-----------------------------------------------------------------------------
// FILE:        NodeRole.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;

using Neon.Kube.Setup;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Enumerates the roles a cluster node can assume.
    /// </summary>
    public static class NodeRole
    {
        /// <summary>
        /// The node is a cluster control-plane node.
        /// </summary>
        public const string ControlPlane = "control-plane";

        /// <summary>
        /// The node is a cluster worker.
        /// </summary>
        public const string Worker = "worker";

        /// <summary>
        /// <b>HACK:</b> The node is actually a XenServer host machine and not an actual Kubernetes node.
        /// This seemed like the least bad place to define this for <see cref="SetupNodeStatus.Role"/>
        /// values when preparing a cluster on XenServer.
        /// </summary>
        public const string XenServer = "xenserver";

        /// <summary>
        /// <b>HACK:</b> The node is actually a Hyper-V host machine and not an actual Kubernetes node.
        /// This seemed like the least bad place to define this for <see cref="SetupNodeStatus.Role"/>
        /// values when preparing a cluster on Hyper-V servers.
        /// </summary>
        public const string HyperV = "hyperv";
    }
}
