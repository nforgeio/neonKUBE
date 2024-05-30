//-----------------------------------------------------------------------------
// FILE:        ServiceAdvice.cs
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
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Collections;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube.SSH;
using Neon.Retry;
using Neon.SSH;
using Neon.Tasks;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Used by <see cref="ClusterAdvisor"/> to record configuration advice for a specific
    /// cluster service being deployed.
    /// </summary>
    public class ServiceAdvice
    {
        private ClusterAdvisor   clusterAdvisor;

        /// <summary>
        /// Default constructor for deserialization only.
        /// </summary>
        public ServiceAdvice()
        {
        }

        /// <summary>
        /// Parameterized constructor.
        /// </summary>
        /// <param name="clusterAdvisor">Specifies the parent <see cref="ClusterAdvisor"/>.</param>
        /// <param name="serviceName">Identifies the service.</param>
        public ServiceAdvice(ClusterAdvisor clusterAdvisor, string serviceName)
        {
            Covenant.Requires<ArgumentNullException>(clusterAdvisor != null, nameof(clusterAdvisor));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(serviceName), nameof(serviceName));

            this.clusterAdvisor = clusterAdvisor;
            this.ServiceName    = serviceName;
        }

        /// <summary>
        /// Called after deserialization to rehydrate the cluster advisor so we don't have to
        /// serialize the cluster advisor multiple times since it's already serialized in the
        /// cluster setup state.
        /// </summary>
        /// <param name="clusterAdvisor">Specifies the parent <see cref="ClusterAdvisor"/>.</param>
        public void Rehydrate(ClusterAdvisor clusterAdvisor)
        {
            this.clusterAdvisor = clusterAdvisor;
        }

        /// <summary>
        /// Specifies the service name.
        /// </summary>
        [JsonProperty(PropertyName = "ServiceName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "serviceName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ServiceName { get; set; }

        /// <summary>
        /// Specifies the CPU limit for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        [JsonProperty(PropertyName = "PodCpuLimit", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "podCpuLimit", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public double? PodCpuLimit { get; set; }

        /// <summary>
        /// Specifies the CPU request for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        [JsonProperty(PropertyName = "PodCpuRequest", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "podCpuRequest", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public double? PodCpuRequest { get; set; }

        /// <summary>
        /// Specifies the memory limit for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        [JsonProperty(PropertyName = "PodMemoryLimit", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "podMemoryLimit", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public decimal? PodMemoryLimit { get; set; }

        /// <summary>
        /// Specifies the memory request for each service pod or <c>null</c> when this property is not set.
        /// </summary>
        [JsonProperty(PropertyName = "PodMemoryRequest", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "podMemoryRequest", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public decimal? PodMemoryRequest { get; set; }

        /// <summary>
        /// Specifies the number of <b>2 MiB</b> hugepage requested by the service.  This will be provisioned
        /// as both the pod request and limit when specified.
        /// </summary>
        [JsonProperty(PropertyName = "Hugepages", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hugepages", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int? Hugepages { get; set; }

        /// <summary>
        /// Returns the pod resource requests and limits as a map.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public StructuredHelmValue Resources
        {
            get
            {
                var hasLimits    = PodCpuLimit != null || PodMemoryLimit != null;
                var hasRequests  = PodCpuRequest != null || PodMemoryRequest != null;
                var hasHugepages = Hugepages != null;

                if (hasHugepages)
                {
                    hasLimits   = true;
                    hasRequests = true;
                }

                if (!hasRequests && !hasLimits)
                {
                    return "{}";
                }

                var sb = new StringBuilder();

                sb.AppendLine("resources:");

                if (hasLimits)
                {
                    sb.AppendLine($"  limits:");

                    if (PodCpuLimit != null)
                    {
                        sb.AppendLine($"    cpu: {KubeHelper.ToSiString(PodCpuLimit)}");
                    }

                    if (PodMemoryLimit != null)
                    {
                        sb.AppendLine($"    memory: {KubeHelper.ToSiString(PodMemoryLimit)}");
                    }

                    if (hasHugepages)
                    {
                        sb.AppendLine($"    hugepages-2Mi: {KubeHelper.ToSiString(2 * ByteUnits.MebiBytes * Hugepages)}");
                    }
                }

                if (hasRequests)
                {
                    sb.AppendLine($"  requests:");

                    if (PodCpuRequest != null)
                    {
                        sb.AppendLine($"    cpu: {KubeHelper.ToSiString(PodCpuLimit)}");
                    }

                    if (PodMemoryRequest != null)
                    {
                        sb.AppendLine($"    memory: {KubeHelper.ToSiString(PodMemoryLimit)}");
                    }

                    if (hasHugepages)
                    {
                        sb.AppendLine($"    hugepages-2Mi: {KubeHelper.ToSiString(2 * ByteUnits.MebiBytes * Hugepages)}");
                    }
                }

                var yaml  = sb.ToString();
                var value = NeonHelper.YamlDeserialize<dynamic>(yaml);
                var json  = NeonHelper.JsonSerialize(value, Formatting.None);

                return json;
            }
        }

        /// <summary>
        /// Specifies the number of pods to be seployed for the service or <b>1</b> when this property is not set.
        /// </summary>
        [JsonProperty(PropertyName = "Replicas", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "replicas", ApplyNamingConventions = false)]
        [DefaultValue(1)]
        public int Replicas { get; set; } = 1;

        /// <summary>
        /// <para>
        /// Specifies whether metrics should be collected for the service.
        /// </para>
        /// <note>
        /// <see cref="ClusterAdvisor.MetricsEnabled"/> will be returned when this
        /// property isn't set explicitly.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "MetricsEnabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "metricsEnabled", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool MetricsEnabled { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the metrics scrape interval or <c>null</c> when this property is not set.
        /// </para>
        /// <note>
        /// <see cref="ClusterAdvisor.MetricsInterval"/> will be returned when this
        /// property isn't set explicitly.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "MetricsInterval", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "metricsInterval", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string MetricsInterval { get; set; }

        /// <summary>
        /// Returns the priority class name for the service.  This defaults to
        /// <see cref="PriorityClass.NeonMin"/>.
        /// </summary>
        [JsonProperty(PropertyName = "PriorityClassName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "priorityClassName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PriorityClassName { get; set; }

        /// <summary>
        /// Specifies the <b>nodeSelector</b> object for the service.
        /// </summary>
        [JsonProperty(PropertyName = "NodeSelector", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodeSelector", ApplyNamingConventions = false)]
        [DefaultValue("{}")]
        public StructuredHelmValue NodeSelector { get; set; } = "{}";

        /// <summary>
        /// Specifies the the <b>tolerations</b> array for the service.
        /// </summary>
        [JsonProperty(PropertyName = "Tolerations", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tolerations", ApplyNamingConventions = false)]
        [DefaultValue("[]")]
        public StructuredHelmValue Tolerations { get; set; } = "[]";
    }
}
