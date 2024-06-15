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
        public const int MinMayastorHugepages2Gi = 1024;

        /// <summary>
        /// Optionally enables the Mayastor replicated storage engine for the cluster.  This
        /// defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Mayastor", Required = Required.Default)]
        [YamlMember(Alias = "mayastor", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool Mayastor { get; set; } = false;

        /// <summary>
        /// Specifies the number of <b>2 MiB</b> required to be dedicated to the OpenEBS
        /// Mayastor engine deployed on storage nodes.  This defaults to <b>1024 pages</b>
        /// which is equivalant to <b>2 GiB RAM</b> and is the minimum required by Mayastor.
        /// </summary>
        [JsonProperty(PropertyName = "MayastorHugepages2Gi", Required = Required.Default)]
        [YamlMember(Alias = "mayastorHugepages2Gi", ApplyNamingConventions = false)]
        [DefaultValue(MinMayastorHugepages2Gi)]
        public int MayastorHugepages2Gi { get; set; } = MinMayastorHugepages2Gi;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var openEbsOptions = clusterDefinition.Storage.OpenEbs;
            var optionsPrefix  = $"{nameof(ClusterDefinition.Storage)}.{nameof(ClusterDefinition.Storage.OpenEbs)}";

            // The cluster needs at least three workers or three control-plane nodes
            // to support Mayastor.

            if (openEbsOptions.Mayastor && clusterDefinition.Workers.Count() < 3 && clusterDefinition.ControlNodes.Count() < 3)
            {
                throw new ClusterDefinitionException("OpenEBS Mayastor engine requires at least 3 cluster worker or control-plane nodes.");
            }

            // Validate the Mayastor properties.

            if (openEbsOptions.Mayastor)
            {
                if (MayastorHugepages2Gi < MinMayastorHugepages2Gi)
                {
                    throw new ClusterDefinitionException($"{optionsPrefix}.{nameof(MayastorHugepages2Gi)}={MayastorHugepages2Gi} must be at least [{MinMayastorHugepages2Gi}].");
                }
            }
        }
    }
}
