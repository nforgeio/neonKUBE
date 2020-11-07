//-----------------------------------------------------------------------------
// FILE:	    OpenEbsOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
    /// Specifies cluster OpenEBS options.
    /// </summary>
    public class OpenEbsOptions
    {
        private const string minNfsSize = "10 GiB";

        /// <summary>
        /// The size of the NFS file system to be created for the cluster.  This defaults
        /// to <b>10 GiB</b> and cannot be any smaller than this.
        /// </summary>
        [JsonProperty(PropertyName = "NfsSize", Required = Required.Default)]
        [YamlMember(Alias = "nfsSize", ApplyNamingConventions = false)]
        [DefaultValue(minNfsSize)]
        public string NfsSize { get; set; } = minNfsSize;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            NfsSize = NfsSize ?? minNfsSize;

            ClusterDefinition.ValidateSize(NfsSize, typeof(OpenEbsOptions), nameof(NfsSize), minimum: minNfsSize);
        }
    }
}
