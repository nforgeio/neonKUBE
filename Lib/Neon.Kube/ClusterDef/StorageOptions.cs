//-----------------------------------------------------------------------------
// FILE:        StorageOptions.cs
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
    /// Specifies cluster Storage options.
    /// </summary>
    public class StorageOptions
    {
        /// <summary>
        /// Specifies the cluster Minio related options.
        /// </summary>
        [JsonProperty(PropertyName = "Minio", Required = Required.Always)]
        [YamlMember(Alias = "minio", ApplyNamingConventions = false)]
        public MinioOptions Minio { get; set; } = new MinioOptions();

        /// <summary>
        /// Specifies the cluster OpenEbs related options.
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbs", Required = Required.Always)]
        [YamlMember(Alias = "openEbs", ApplyNamingConventions = false)]
        public OpenEbsOptions OpenEbs { get; set; } = new OpenEbsOptions();

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            OpenEbs = OpenEbs ?? new OpenEbsOptions();
            Minio   = Minio   ?? new MinioOptions();

            OpenEbs.Validate(clusterDefinition);
            Minio.Validate(clusterDefinition);
        }
    }
}
