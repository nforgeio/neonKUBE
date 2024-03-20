//-----------------------------------------------------------------------------
// FILE:        MinioOptions.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies cluster Minio options.
    /// </summary>
    public class MinioOptions
    {
        private const string minVolumeSize = "2 GiB";

        /// <summary>
        /// <para>
        /// Specifies the number of volumes per server. This defaults to 4.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "VolumesPerNode", Required = Required.Default)]
        [YamlMember(Alias = "volumesPerNode", ApplyNamingConventions = false)]
        [DefaultValue(4)]
        public int VolumesPerNode { get; set; } = 4;

        /// <summary>
        /// The size of each volume to be mounted to each server.  This defaults to
        /// <b>2 GiB</b>.
        /// </summary>
        [JsonProperty(PropertyName = "VolumeSize", Required = Required.Default)]
        [YamlMember(Alias = "volumeSize", ApplyNamingConventions = false)]
        [DefaultValue(minVolumeSize)]
        public string VolumeSize { get; set; } = minVolumeSize;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var minioOptionsPrefix = $"{nameof(ClusterDefinition.Storage)}.{nameof(ClusterDefinition.Storage.Minio)}";

            if (!clusterDefinition.Nodes.Any(n => n.Labels.SystemMinioServices))
            {
                if (clusterDefinition.Kubelet.AllowPodsOnControlPlane.GetValueOrDefault())
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.SystemMinioServices = true;
                    }
                }
                else
                {
                    foreach (var node in clusterDefinition.Workers)
                    {
                        node.Labels.SystemMinioServices = true;
                    }
                }
            }

            var serverCount = clusterDefinition.Nodes.Where(n => n.Labels.SystemMinioServices).Count();

            if (serverCount * VolumesPerNode < 4)
            {
                throw new ClusterDefinitionException($"Minio requires at least [4] volumes within the cluster.  Increase [{minioOptionsPrefix}.{nameof(MinioOptions.VolumesPerNode)}] so the number of nodes hosting Minio times [{VolumesPerNode}] is at least [4].");
            }

            var minOsDiskAfterMinio = ByteUnits.Parse(KubeConst.MinimumOsDiskAfterMinio);

            foreach (var node in clusterDefinition.Nodes.Where(node => node.Labels.SystemMinioServices))
            {
                var osDisk       = ByteUnits.Parse(node.GetOsDiskSize(clusterDefinition));
                var minioVolumes = ByteUnits.Parse(VolumeSize) * VolumesPerNode;

                if (osDisk - minioVolumes < minOsDiskAfterMinio)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] Operating System (boot) disk is too small.  Increase this to at least [{ByteUnits.Humanize(minOsDiskAfterMinio + minioVolumes, powerOfTwo: true, spaceBeforeUnit: false)}].");
                }
            }
        }
    }
}
