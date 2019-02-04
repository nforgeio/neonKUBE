//-----------------------------------------------------------------------------
// FILE:	    KubeHostPorts.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube
{
    /// <summary>
    /// Defines reserved local node and cluster network ports.
    /// </summary>
    public static class KubeHostPorts
    {
        /// <summary>
        /// Kubernetes API server load balancer port exposed by HAProxy
        /// instances running on each of the master nodes.  This balances
        /// traffic across all of the masters.
        /// </summary>
        public const int ApiServerProxy = 5000;

        /// <summary>
        /// Port exposed by the Kubernetes API servers on the master nodes.
        /// </summary>
        public const int KubeApiServer = 6443;
    }
}
