//-----------------------------------------------------------------------------
// FILE:	    IngressRuleTarget.cs
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

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the possible targets for inbound network traffic.
    /// </summary>
    internal enum IngressRuleTarget
    {
        /// <summary>
        /// Send traffic to the cluster nodes with the <b>ingress</b> label.
        /// </summary>
        IngressNodes,

        /// <summary>
        /// Send traffic to the cluster master nodes.
        /// </summary>
        MasterNodes
    }
}
