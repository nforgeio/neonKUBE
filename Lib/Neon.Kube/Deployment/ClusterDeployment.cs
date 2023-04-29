// -----------------------------------------------------------------------------
// FILE:	    ClusterDeployment.cs
// CONTRIBUTOR: Jeff Lill
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

using Octokit;

using YamlDotNet.Serialization;

namespace Neon.Kube.Deployment
{
    /// <summary>
    /// Holds information about the cluster deployment that is required for managing clusters
    /// after they are deployed.  This is persisted to the <see cref="KubeNamespace.NeonSystem"/>
    /// namespace as the <see cref="KubeConfigMapName.ClusterDeployment"/> configmap.
    /// </summary>
    public class ClusterDeployment
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterDeployment()
        {
            Hosting = new HostingDeployment();
            Nodes   = new List<NodeDeployment>();
        }

        /// <summary>
        /// Constructs an instance by extracting values from a <see cref="ClusterDefinition"/>.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        public ClusterDeployment(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            Hosting = new HostingDeployment(clusterDefinition);
            Nodes   = new List<NodeDeployment>();

            foreach (var nodeDefinition in clusterDefinition.Nodes)
            {
                Nodes.Add(new NodeDeployment(nodeDefinition));
            }
        }

        /// <summary>
        /// Holds information about the environment hosting the cluster.
        /// </summary>
        public HostingDeployment Hosting { get; set; }

        /// <summary>
        /// Holds information about the cluster nodes.
        /// </summary>
        public List<NodeDeployment> Nodes { get; set; }
    }
}
