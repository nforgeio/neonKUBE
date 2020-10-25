//-----------------------------------------------------------------------------
// FILE:	    CortexSchemaConfig.cs
// CONTRIBUTOR: Marcus Bowyer
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

using k8s.Models;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies the options for configuring the cluster integrated Prometheus 
    /// metrics stack: <a href="https://prometheus.io/">https://prometheus.io/</a>
    /// </summary>
    public class CortexSchemaConfig
    {
        /// <summary>
        /// The date from which to keep metrics, in YYYY-MM-DD format, for example: 2020-03-01.
        /// </summary>
        [JsonProperty(PropertyName = "From", Required = Required.Default)]
        [YamlMember(Alias = "from", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DateTime? From { get; set; } = null;

        /// <summary>
        /// The index client to use.
        /// </summary>
        [JsonProperty(PropertyName = "Store", Required = Required.Default)]
        [YamlMember(Alias = "store", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public CortexStorageOptions? Store { get; set; } = null;

        /// <summary>
        /// The object client to use. If none is specified, `store` is used for storing chunks as well.
        /// </summary>
        [JsonProperty(PropertyName = "ObjectStore", Required = Required.Default)]
        [YamlMember(Alias = "object_store", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public CortexStorageOptions? ObjectStore { get; set; } = null;

        /// <summary>
        /// The schema version to use. Valid ones are v1, v2, v3,... v6, v9, v10, v11.
        /// Recommended for production: v9 for most use cases or v10 if you expect to have very high cardinality metrics.
        /// </summary>
        [JsonProperty(PropertyName = "Schema", Required = Required.Default)]
        [YamlMember(Alias = "schema", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Schema { get; set; } = null;

        /// <summary>
        /// Specifies the index retention options.
        /// </summary>
        [JsonProperty(PropertyName = "Index", Required = Required.Default)]
        [YamlMember(Alias = "index", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public CortexRetentionConfig Index { get; set; } = null;

        /// <summary>
        /// Specifies the chunks retention options.
        /// </summary>
        [JsonProperty(PropertyName = "Chunks", Required = Required.Default)]
        [YamlMember(Alias = "chunks", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public CortexRetentionConfig Chunks { get; set; } = null;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (true)
            {
                return;
            }
        }
    }
}
