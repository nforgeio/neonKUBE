//-----------------------------------------------------------------------------
// FILE:        MetricsOptions.cs
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

using k8s.Models;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies the options for configuring the cluster integrated Prometheus 
    /// metrics stack.
    /// </summary>
    public class MetricsOptions
    {
        /// <summary>
        /// Specifies where Prometheus metrics should be stored.
        /// This defaults to <see cref="MetricsStorageOptions.Ephemeral"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Storage", Required = Required.Default)]
        [YamlMember(Alias = "storage", ApplyNamingConventions = false)]
        [DefaultValue(MetricsStorageOptions.Ephemeral)]
        public MetricsStorageOptions Storage { get; set; } = MetricsStorageOptions.Ephemeral;

        /// <summary>
        /// Optionally specifies the interval in <b>seconds</b> that Prometheus will scrape metrics from
        /// NeonKUBE cluster services.  This defaults to <b>zero</b> which has NeonKUBE choose a reasonable
        /// value based in the size of your cluster.
        /// </summary>
        [JsonProperty(PropertyName = "ScrapeSeconds", Required = Required.Default)]
        [YamlMember(Alias = "scrapeSeconds", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ScrapeSeconds { get; set; } = 0;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values, as required.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (!clusterDefinition.Nodes.Any(node => node.Labels.SystemMetricServices))
            {
                if (clusterDefinition.Kubernetes.AllowPodsOnControlPlane.GetValueOrDefault())
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.SystemMetricServices = true;
                    }
                }
                else
                {
                    foreach (var node in clusterDefinition.Workers)
                    {
                        node.Labels.SystemMetricServices = true;
                    }
                }
            }

            if (clusterDefinition.Monitor.Metrics.ScrapeSeconds < 0)
            {
                throw new ClusterDefinitionException($"[{nameof(clusterDefinition.Monitor)}.{nameof(clusterDefinition.Monitor.Metrics)}.{nameof(clusterDefinition.Monitor.Metrics.ScrapeSeconds)}={clusterDefinition.Monitor.Metrics.ScrapeSeconds}] is invalid.");
            }
        }
    }
}
