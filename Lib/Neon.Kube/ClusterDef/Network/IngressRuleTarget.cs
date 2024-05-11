//-----------------------------------------------------------------------------
// FILE:        IngressRuleTarget.cs
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Enumerates the possible targets for inbound network traffic.
    /// </summary>
    internal enum IngressRuleTarget
    {
        // WARNING: 
        //
        // The [EnumMember] values may only include alphanumeric characters and
        // dashes.

        /// <summary>
        /// Provisioned for required NeonKUBE cluster ingress ports, like the Kubernetes
        /// API port 6443.  This traffic will be routed to the control-plane nodes.
        /// </summary>
        [EnumMember(Value = "control-plane")]
        ControlPlane,

        /// <summary>
        /// Provisioned for user-defined ingress rules.  These groups include all
        /// cluster nodes with the <b>ingress</b> label.  This traffic will be
        /// routed to the ingress nodes.
        /// </summary>
        [EnumMember(Value = "ingress")]
        Ingress,

        /// <summary>
        /// Provisioned for SSH management.  A group will be created for each node
        /// in the cluster and traffic will be routed directly to the associated
        /// cluster node.
        /// </summary>
        [EnumMember(Value = "ssh")]
        Ssh
    }
}
