//-----------------------------------------------------------------------------
// FILE:	    MinioOptions.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// Specifies cluster Minio options.
    /// </summary>
    public class MinioOptions
    {
        private const string minVolumeSize = "10 GiB";

        /// <summary>
        /// <para>
        /// Specifies the number of volumes per server. This defaults to 4.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "VolumesPerServer", Required = Required.Default)]
        [YamlMember(Alias = "volumesPerServer", ApplyNamingConventions = false)]
        [DefaultValue(4)]
        public int VolumesPerServer { get; set; } = 4;

        /// <summary>
        /// The size of each volume to be mounted to each server.
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
        [Pure]
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (!clusterDefinition.Nodes.Any(n => n.Labels.Minio))
            {
                if (clusterDefinition.Kubernetes.AllowPodsOnMasters.GetValueOrDefault() == true)
                {
                    foreach (var n in clusterDefinition.Nodes)
                    {
                        n.Labels.MinioInternal = true;
                    }
                }
                else
                {
                    foreach (var n in clusterDefinition.Workers)
                    {
                        n.Labels.MinioInternal = true;
                    }
                }
            }
            else
            {
                foreach (var n in clusterDefinition.Nodes.Where(n => n.Labels.Minio))
                {
                    n.Labels.MinioInternal = true;
                }
            }

            var serverCount = clusterDefinition.Nodes.Where(n => n.Labels.MinioInternal).Count();

            Covenant.Assert(serverCount * VolumesPerServer >= 4);

            foreach (var n in clusterDefinition.Nodes.Where(n => n.Labels.MinioInternal))
            {
                Covenant.Assert(ByteUnits.Parse(n.Vm.OsDisk) - (ByteUnits.Parse(VolumeSize) * VolumesPerServer) > ByteUnits.Parse("40Gi"));
            }
        }
    }
}
