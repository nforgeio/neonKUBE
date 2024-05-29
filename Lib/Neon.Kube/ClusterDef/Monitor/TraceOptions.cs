//-----------------------------------------------------------------------------
// FILE:        TraceOptions.cs
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

using k8s.Models;

using Neon.Common;
using Neon.IO;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using YamlDotNet.Serialization;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies the options for configuring the cluster integrated traceging and
    /// metrics.
    /// </summary>
    public class TraceOptions
    {
        /// <summary>
        /// Specifies the trace retention period in days. Traces older than this will be purged.
        /// This defaults to: <b>14 days</b>
        /// </summary>
        [JsonProperty(PropertyName = "TraceRetentionDays", Required = Required.Default)]
        [YamlMember(Alias = "traceRetentionDays", ApplyNamingConventions = false)]
        [DefaultValue(14)]
        public int RetentionDays { get; set; } = 14;

        /// <summary>
        /// Specifies the percentage of distributed traces to collect from the cluster's <b>default</b> namespace.
        /// This defaults <b>1.0</b> percent.
        /// </summary>
        [JsonProperty(PropertyName = "DefaultNamespaceSamplePercentage", Required = Required.Default)]
        [YamlMember(Alias = "defaultNamespaceSamplePercentage", ApplyNamingConventions = false)]
        [DefaultValue(100.0)]
        public double DefaultNamespaceSamplingPercentage { get; set; } = 1.0;

        /// <summary>
        /// Specifies the percentage of distributed traces to collect from the cluster's <b>istio-system</b> namespace.
        /// This defaults <b>0.0</b> percent.
        /// </summary>
        [JsonProperty(PropertyName = "KubeIstioSystemNamespaceSamplePercentage", Required = Required.Default)]
        [YamlMember(Alias = "kubeIstioSystemNamespaceSamplePercentage", ApplyNamingConventions = false)]
        [DefaultValue(1.0)]
        public double KubeIstioSystemNamespaceSamplingPercentage { get; set; } = 1.0;

        /// <summary>
        /// Specifies the percentage of distributed traces to collect from the cluster's <b>kube-public</b> namespace.
        /// This defaults <b>100.0</b> percent.
        /// </summary>
        /// This defaults <b>1.0</b> percent.
        [JsonProperty(PropertyName = "KubePublicNamespaceSamplePercentage", Required = Required.Default)]
        [YamlMember(Alias = "kubePublicNamespaceSamplePercentage", ApplyNamingConventions = false)]
        [DefaultValue(0.0)]
        public double KubePublicNamespaceSamplingPercentage { get; set; } = 1.0;

        /// <summary>
        /// Specifies the percentage of distributed traces to collect from the cluster's <b>kube-system</b> namespace.
        /// This defaults <b>1.0</b> percent.
        /// </summary>
        [JsonProperty(PropertyName = "KubeSystemNamespaceSamplePercentage", Required = Required.Default)]
        [YamlMember(Alias = "kubeSystemNamespaceSamplePercentage", ApplyNamingConventions = false)]
        [DefaultValue(1.0)]
        public double KubeSystemNamespaceSamplingPercentage { get; set; } = 1.0;

        /// <summary>
        /// Specifies the percentage of distributed traces to collect from the cluster's <b>neon-monitor</b> namespace.
        /// This defaults <b>0.0</b> percent.
        /// </summary>
        [JsonProperty(PropertyName = "NeonMonitorNamespaceSamplePercentage", Required = Required.Default)]
        [YamlMember(Alias = "neonMonitorNamespaceSamplePercentage", ApplyNamingConventions = false)]
        [DefaultValue(0.0)]
        public double NeonMonitorNamespaceSamplingPercentage { get; set; } = 0.0;

        /// <summary>
        /// Specifies the percentage of distributed traces to collect from the cluster's <b>neon-status</b> namespace.
        /// This defaults <b>0.0</b> percent.
        /// </summary>
        [JsonProperty(PropertyName = "NeonStatusNamespaceSamplePercentage", Required = Required.Default)]
        [YamlMember(Alias = "neonStatusNamespaceSamplePercentage", ApplyNamingConventions = false)]
        [DefaultValue(0.0)]
        public double NeonStatusNamespaceSamplingPercentage { get; set; } = 0.0;

        /// <summary>
        /// Specifies the percentage of distributed traces to collect from the cluster's <b>neon-storage</b> namespace.
        /// This defaults <b>0.0</b> percent.
        /// </summary>
        [JsonProperty(PropertyName = "NeonStorageNamespaceSamplePercentage", Required = Required.Default)]
        [YamlMember(Alias = "neonStorageNamespaceSamplePercentage", ApplyNamingConventions = false)]
        [DefaultValue(0.0)]
        public double NeonStorageNamespaceSamplingPercentage { get; set; } = 0.0;

        /// <summary>
        /// Specifies the percentage of distributed traces to collect from the cluster's <b>neon-system</b> namespace.
        /// This defaults <b>100.0</b> percent.
        /// </summary>
        [JsonProperty(PropertyName = "NeonSystemNamespaceSamplePercentage", Required = Required.Default)]
        [YamlMember(Alias = "neonSystemNamespaceSamplePercentage", ApplyNamingConventions = false)]
        [DefaultValue(1.0)]
        public double NeonSystemNamespaceSamplingPercentage { get; set; } = 1.0;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values, as required.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            var optionsPrefix = $"{nameof(ClusterDefinition.Monitor)}.{nameof(ClusterDefinition.Monitor.Trace)}";

            if (RetentionDays < 1)
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(RetentionDays)}={RetentionDays}] is invalid.  This must be at least one day.");
            }

            Action<string, double> CheckSampleRate =
                (propertyName, sampleRate) =>
                {
                    if (sampleRate < 0 || sampleRate > 100)
                    {
                        throw new ClusterDefinitionException($"[{optionsPrefix}.{propertyName}={sampleRate}] is invalid.  This must be between [0.0 ... 100.0] inclusive.");
                    }
                };

            CheckSampleRate(nameof(DefaultNamespaceSamplingPercentage), DefaultNamespaceSamplingPercentage);
            CheckSampleRate(nameof(KubeIstioSystemNamespaceSamplingPercentage), KubeIstioSystemNamespaceSamplingPercentage);
            CheckSampleRate(nameof(KubePublicNamespaceSamplingPercentage), KubePublicNamespaceSamplingPercentage);
            CheckSampleRate(nameof(KubeSystemNamespaceSamplingPercentage), KubeSystemNamespaceSamplingPercentage);
            CheckSampleRate(nameof(NeonMonitorNamespaceSamplingPercentage), NeonMonitorNamespaceSamplingPercentage);
            CheckSampleRate(nameof(NeonStatusNamespaceSamplingPercentage), NeonStatusNamespaceSamplingPercentage);
            CheckSampleRate(nameof(NeonStorageNamespaceSamplingPercentage), NeonStorageNamespaceSamplingPercentage);
            CheckSampleRate(nameof(NeonSystemNamespaceSamplingPercentage), NeonSystemNamespaceSamplingPercentage);

            if (!clusterDefinition.Nodes.Any(node => node.Labels.SystemTraceServices))
            {
                if (clusterDefinition.Kubernetes.AllowPodsOnControlPlane.GetValueOrDefault())
                {
                    foreach (var node in clusterDefinition.Nodes)
                    {
                        node.Labels.SystemTraceServices = true;
                    }
                }
                else
                {
                    foreach (var node in clusterDefinition.Workers)
                    {
                        node.Labels.SystemTraceServices = true;
                    }
                }
            }
            else
            {
                foreach (var node in clusterDefinition.Nodes.Where(node => node.Labels.SystemTraceServices))
                {
                    node.Labels.SystemTraceServices = true;
                }
            }
        }
    }
}
