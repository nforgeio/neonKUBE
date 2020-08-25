//-----------------------------------------------------------------------------
// FILE:	    M3DBOptions.cs
// CONTRIBUTOR: Marcus Bowyer
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
    /// Specifies the options for configuring the cluster integrated M3DB 
    /// metrics stack: <a href="https://M3DB.io/">https://M3DB.io/</a>
    /// </summary>
    public class M3DBOptions
    {
        /// <summary>
        /// Compute Resources required by Elasticsearch.
        /// </summary>
        [JsonProperty(PropertyName = "Resources", Required = Required.Default)]
        [YamlMember(Alias = "resources", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public V1ResourceRequirements Resources { get; set; } = null;

        /// <summary>
        /// Etcd options.
        /// </summary>
        [JsonProperty(PropertyName = "Etcd", Required = Required.Default)]
        [YamlMember(Alias = "etcd", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public EtcdOptions Etcd { get; set; } = null;

        /// <summary>
        /// Indicates disk size for M3DB nodes.  
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "DiskSize", Required = Required.Default)]
        [YamlMember(Alias = "diskSize", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ResourceQuantity DiskSize { get; set; } = null;
    }
}
