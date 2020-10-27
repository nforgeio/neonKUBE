//-----------------------------------------------------------------------------
// FILE:	    CortexRetentionConfig.cs
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
using YamlDotNet.Serialization.Converters;

using Neon.Common;
using Neon.IO;

using k8s.Models;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies the retention options for Cortex.
    /// </summary>
    public class CortexRetentionConfig
    {
        /// <summary>
        /// The prefix to use for the tables.
        /// </summary>
        [JsonProperty(PropertyName = "Prefix", Required = Required.Default)]
        [YamlMember(Alias = "Prefix", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Prefix { get; set; } = null;

        /// <summary>
        /// We typically run Cortex with new tables every week to keep the index size low and to make retention easier. 
        /// This sets the period at which new tables are created and used. Typically 1w (1week).
        /// </summary>
        [JsonProperty(PropertyName = "Period", Required = Required.Default)]
        [YamlMember(Alias = "period", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Period { get; set; } = null;

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
