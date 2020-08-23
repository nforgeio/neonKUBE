//-----------------------------------------------------------------------------
// FILE:	    ElasticsearchOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
    /// Specifies the options for configuring the cluster integrated Elasticsearch 
    /// metrics stack: <a href="https://Elastic.co/">https://Elastic.co/</a>
    /// </summary>
    public class ElasticsearchOptions
    {
        /// <summary>
        /// Indicates whether Elasticsearch metrics are to be enabled for the cluster.  
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default)]
        [YamlMember(Alias = "enabled", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Indicates whether Elasticsearch metrics are to be enabled for the cluster.  
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "DiskSize", Required = Required.Default)]
        [YamlMember(Alias = "diskSize", ApplyNamingConventions = false)]
        [DefaultValue("1Gi")]
        public string DiskSize { get; set; } = "1Gi";

        /// <summary>
        /// Compute Resources required by Elasticsearch.
        /// </summary>
        [JsonProperty(PropertyName = "Resources", Required = Required.Default)]
        [YamlMember(Alias = "resources", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public V1ResourceRequirements Resources { get; set; } = null;

        /// <summary>
        /// Log retention period. Logs beyond this number of days will be purged by the ClusterManager
        /// </summary>
        [JsonProperty(PropertyName = "LogRetentionDays", Required = Required.Default)]
        [YamlMember(Alias = "logRetentionDays", ApplyNamingConventions = false)]
        [DefaultValue(14)]
        public int LogRetentionDays { get; set; } = 14;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (!Enabled)
            {
                return;
            }
        }
    }
}