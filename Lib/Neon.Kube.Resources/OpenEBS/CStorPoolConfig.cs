//-----------------------------------------------------------------------------
// FILE:	    V1CStorPoolConfig.cs
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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using k8s;
using k8s.Models;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// OpenEBS cStor pool configuration.
    /// </summary>
    public class V1CStorPoolConfig
    {
        /// <summary>
        /// Initializes a new instance of the V1CStorPoolConfig class.
        /// </summary>
        public V1CStorPoolConfig()
        {
        }

        /// <summary>
        /// The raid type.
        /// </summary>
        [DefaultValue(DataRaidGroupTypes.Stripe)]
        [JsonPropertyName("dataRaidGroupType")]
        public string DataRaidGroupType { get; set; }

        /// <summary>
        /// Tolerations to be applied to the CStor Pool pods.
        /// </summary>
        [DefaultValue(null)]
        public List<V1Toleration> Tolerations { get; set; }
    }
}
