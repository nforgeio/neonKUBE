//-----------------------------------------------------------------------------
// FILE:        OpenEbsOptions.cs
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
    /// Specifies cluster OpenEBS options.
    /// </summary>
    public class OpenEbsOptions
    {
        /// <summary>
        /// Specifies the minimum number of <b>2 MiB</b> (or <b>2 GiB RAM)</b> hugepages required by
        /// Mayastor on the nodes where it is deployed.
        /// </summary>
        public const int MinHugepages = 1024;

        /// <summary>
        /// <para>
        /// Specifies which OpenEBS engine will be deployed within the cluster.  This defaults
        /// to <see cref="OpenEbsEngine.Default"/> which selects the <see cref="OpenEbsEngine.HostPath"/>
        /// engine for single node clusters, <see cref="OpenEbsEngine.Mayastor"/> otherwise.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "Engine", Required = Required.Default)]
        [YamlMember(Alias = "engine", ApplyNamingConventions = false)]
        [DefaultValue(OpenEbsEngine.Default)]
        public OpenEbsEngine Engine { get; set; } = OpenEbsEngine.Default;

        /// <summary>
        /// Specifies the number of <b>2 MiB</b> required to be dedicated to the OpenEBS
        /// Mayastor engine deployed on storage nodes.  This defaults to <b>1024 pages</b>
        /// which is equivalant to <b>2 GiB RAM</b> and is the minimum required by Mayastor.
        /// </summary>
        [JsonProperty(PropertyName = "Hugepages", Required = Required.Default)]
        [YamlMember(Alias = "hugepages", ApplyNamingConventions = false)]
        [DefaultValue(MinHugepages)]
        public int Hugepages { get; set; } = MinHugepages;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var optionsPrefix = $"{nameof(ClusterDefinition.Storage)}.{nameof(ClusterDefinition.Storage.OpenEbs)}";

            // Choose an actual engine when [OpenEbsEngine.Default] is specified, based
            // on the number cluster nodes.

            if (clusterDefinition.Storage.OpenEbs.Engine == OpenEbsEngine.Default)
            {
                if (clusterDefinition.ControlNodes.Count() < 3 && clusterDefinition.Workers.Count() < 3)
                {
                    clusterDefinition.Storage.OpenEbs.Engine = OpenEbsEngine.HostPath;
                }
                else
                {
                    clusterDefinition.Storage.OpenEbs.Engine = OpenEbsEngine.Mayastor;
                }
            }

            // Validate the Mayastor hugepage count.

            if (clusterDefinition.Storage.OpenEbs.Engine == OpenEbsEngine.Mayastor && Hugepages < MinHugepages)
            {
                throw new ClusterDefinitionException($"{optionsPrefix}.{nameof(Hugepages)}={Hugepages} must be at least [{MinHugepages}].");
            }
        }
    }
}
