//-----------------------------------------------------------------------------
// FILE:        TraceOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies the options for configuring the cluster integrated traceging and
    /// metrics.
    /// </summary>
    public class TraceOptions
    {
        /// <summary>
        /// Trace retention period. Traces beyond this number of days will be purged by the ClusterManager
        /// </summary>
        [JsonProperty(PropertyName = "TraceRetentionDays", Required = Required.Default)]
        [YamlMember(Alias = "traceRetentionDays", ApplyNamingConventions = false)]
        [DefaultValue(14)]
        public int TraceRetentionDays { get; set; } = 14;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            var traceOptionsPrefix = $"{nameof(ClusterDefinition.Monitor)}.{nameof(ClusterDefinition.Monitor.Traces)}";

            if (TraceRetentionDays < 1)
            {
                throw new ClusterDefinitionException($"[{traceOptionsPrefix}.{nameof(TraceRetentionDays)}={TraceRetentionDays}] is valid.  This must be at least one day.");
            }

            if (!clusterDefinition.Nodes.Any(n => n.Labels.Traces))
            {
                if (clusterDefinition.Kubelet.AllowPodsOnControlPlane.GetValueOrDefault())
                {
                    foreach (var n in clusterDefinition.Nodes)
                    {
                        n.Labels.TracesInternal = true;
                    }
                }
                else
                {
                    foreach (var n in clusterDefinition.Workers)
                    {
                        n.Labels.TracesInternal = true;
                    }
                }
            }
            else
            {
                foreach (var n in clusterDefinition.Nodes.Where(n => n.Labels.Traces))
                {
                    n.Labels.TracesInternal = true;
                }
            }
        }
    }
}
