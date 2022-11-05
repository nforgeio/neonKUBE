//-----------------------------------------------------------------------------
// FILE:	    MonitorOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
    /// Specifies the options for configuring the cluster integrated logging stack.
    /// </summary>
    public class MonitorOptions
    {
        /// <summary>
        /// Logging options.
        /// </summary>
        [JsonProperty(PropertyName = "Logs", Required = Required.Default)]
        [YamlMember(Alias = "logs", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public LogOptions Logs { get; set; } = new LogOptions();

        /// <summary>
        /// Metrics options
        /// </summary>
        [JsonProperty(PropertyName = "Metrics", Required = Required.Default)]
        [YamlMember(Alias = "metrics", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public MetricsOptions Metrics { get; set; } = new MetricsOptions();

        /// <summary>
        /// Tracing options
        /// </summary>
        [JsonProperty(PropertyName = "Traces", Required = Required.Default)]
        [YamlMember(Alias = "traces", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public TraceOptions Traces { get; set; } = new TraceOptions();

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Logs    = Logs ?? new LogOptions();
            Metrics = Metrics ?? new MetricsOptions();
            Traces  = Traces ?? new TraceOptions();

            Logs.Validate(clusterDefinition);
            Metrics.Validate(clusterDefinition);
            Traces.Validate(clusterDefinition);
        }
    }
}
