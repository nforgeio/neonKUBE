//-----------------------------------------------------------------------------
// FILE:        V1CStorPoolSpec.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.Resources.OpenEBS
{
    /// <summary>
    /// OpenEBS cStor pool specification.
    /// </summary>
    public class V1CStorPoolSpec
    {
        /// <summary>
        /// Initializes a new instance of the V1CStorPoolSpec class.
        /// </summary>
        public V1CStorPoolSpec()
        {
        }

        /// <summary>
        /// Selector identifying the nodes holding the targeted cStor block devices.
        /// </summary>
        public Dictionary<string, string> NodeSelector { get; set; }

        /// <summary>
        /// Identifies the associated block devices.
        /// </summary>
        public List<V1CStorDataRaidGroup> DataRaidGroups { get; set; }

        /// <summary>
        /// Specifies the pool configuration.
        /// </summary>
        public V1CStorPoolConfig PoolConfig { get; set; }
    }
}
