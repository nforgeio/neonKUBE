//-----------------------------------------------------------------------------
// FILE:        NodeAdvice.cs
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube.ClusterDef;
using Neon.Kube.SSH;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Used by <see cref="ClusterAdvisor"/> to record configuration advice for a specific
    /// cluster node being deployed.
    /// </summary>
    public class NodeAdvice
    {
        private ClusterAdvisor   clusterAdvisor;

        /// <summary>
        /// Default constructor for deserialization only.
        /// </summary>
        public NodeAdvice()
        {
        }

        /// <summary>
        /// Parameterized constructor.
        /// </summary>
        /// <param name="clusterAdvisor">Specifies the parent <see cref="ClusterAdvisor"/>.</param>
        /// <param name="nodeDefinition">target node for this advice instance.</param>
        public NodeAdvice(ClusterAdvisor clusterAdvisor, NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterAdvisor != null, nameof(clusterAdvisor));
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));

            this.clusterAdvisor = clusterAdvisor;
            this.NodeDefinition = nodeDefinition;
        }

        /// <summary>
        /// Called after deserialization to rehydrate the cluster advisor and related node definition
        /// so we don't have to serialize those multiple times because we already serialize the
        /// cluster definition in the cluster setup state.
        /// </summary>
        /// <param name="clusterAdvisor">Specifies the parent <see cref="ClusterAdvisor"/>.</param>
        /// <param name="nodeDefinition">target node for this advice instance.</param>
        public void Rehydrate(ClusterAdvisor clusterAdvisor, NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterAdvisor != null, nameof(clusterAdvisor));
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));

            this.clusterAdvisor = clusterAdvisor;
            this.NodeDefinition = nodeDefinition;
        }

        /// <summary>
        /// Returns the target node definition for this advice instance.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public NodeDefinition NodeDefinition { get; private set; }

        /// <summary>
        /// Specifies the number of <b>2 MiB</b> sized huge pages required by
        /// OpenEBS for this node.  This is computed by the cluster advisor
        /// for nodes hosting the Mayastor engine.
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbsHugePages2MiB", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "openEbsHugePages2MiB", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public long OpenEbsHugePages2MiB { get; set; } = 0;

        /// <summary>
        /// Returns the total number of <b>2 MiB</b> hugepages required for the node.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public long TotalHugePages2MiB => NodeDefinition.HugePages2MiB + OpenEbsHugePages2MiB;
    }
}
