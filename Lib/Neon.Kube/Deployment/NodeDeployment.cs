// -----------------------------------------------------------------------------
// FILE:	    NodeDeployment.cs
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
        /// Constructs an instances from a cluster <see cref="NodeDefinition"/>.
        /// </summary>
        /// <param name="nodeDefinition">Specifies the node definition.</param>
        public NodeDeployment(NodeDefinition nodeDefinition)
        {
            Covenant.Requires<ArgumentNullException>(nodeDefinition != null, nameof(nodeDefinition));
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
        public string Name { get; set; }

        /// <summary>
        /// Specifies the IP address for the node.
        /// </summary>
        public string Address { get; set; }

        /// <summary>
        /// <para>
        /// Identifies the hypervisor instance where this node.  This name must map to
        /// the name of one of the <see cref="ClusterDeployment.Hosts"/> when set.
        /// </para>
        /// <note>
        /// This property applies only for on-premise hypervisor hosting environments like
        /// Hyper-V and XenServer and will be <c>null</c> for cloud hosts.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Host", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "host", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Host { get; set; } = null;
    }
}
