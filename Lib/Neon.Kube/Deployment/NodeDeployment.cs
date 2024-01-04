// -----------------------------------------------------------------------------
// FILE:        NodeDeployment.cs
// CONTRIBUTOR: NEONFORGE Team
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Kube.ClusterDef;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Deployment
{
    /// <summary>
    /// Holds information about a deployed cluster node.
    /// </summary>
    public class NodeDeployment
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public NodeDeployment()
        {
        }

        /// <summary>
        /// Constructs an instances from a cluster definition <see cref="NodeDefinition"/>.
        /// </summary>
        /// <param name="nodeDefinition">Specifies the node definition.</param>
        public NodeDeployment(NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));

            this.Name       = nodeDefinition.Name;
            this.Address    = nodeDefinition.Address;
            this.Hypervisor = nodeDefinition.Hypervisor;
        }

        /// <summary>
        /// <para>
        /// Identifies the node by name.
        /// </para>
        /// <note>
        /// This is same name the node had in the cluster definition and it excludes any
        /// prefix added for the hosting environment.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// Specifies the IP address for the node.
        /// </summary>
        [JsonProperty(PropertyName = "Address", Required = Required.Always)]
        [YamlMember(Alias = "address", ApplyNamingConventions = false)]
        public string Address { get; set; }

        /// <summary>
        /// Optionally specifies hypervisor hosting related options for environments like Hyper-V and XenServer.
        /// </summary>
        [JsonProperty(PropertyName = "Hypervisor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hypervisor", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HypervisorNodeOptions Hypervisor { get; set; } = null;
    }
}
