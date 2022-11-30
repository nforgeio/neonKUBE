//-----------------------------------------------------------------------------
// FILE:	    V1CStorPoolClusterSpec.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// The kubernetes spec for the pool cluster.
    /// </summary>
    public class V1CStorPoolClusterSpec
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public V1CStorPoolClusterSpec()
        {
        }

        /// <summary>
        /// The list of pools in the cluster.
        /// </summary>
        public List<V1CStorPoolSpec> Pools { get; set; }

        /// <summary>
        /// Compute resources for the cstor pool containers.
        /// </summary>
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonV1ResourceConverter))]
        public V1ResourceRequirements Resources { get; set; }

        /// <summary>
        /// Compute resources for the cstor sidecar containers.
        /// </summary>
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonV1ResourceConverter))]
        public V1ResourceRequirements AuxResources { get; set; }
    }
}
