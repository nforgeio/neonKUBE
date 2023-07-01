//-----------------------------------------------------------------------------
// FILE:        ClusterTelemetry.cs
// CONTRIBUTOR: Marcus Bowyer
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

namespace Neon.Kube
{
    /// <summary>
    /// Models cluster telemetry.
    /// </summary>
    public class ClusterTelemetry
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ClusterTelemetry() 
        {

        }

        /// <summary>
        /// Cluster information
        /// </summary>
        [JsonProperty(PropertyName = "ClusterInfo", Required = Required.Always)]
        public ClusterInfo ClusterInfo { get; set; }

        /// <summary>
        /// Node status information.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Always)]
        public List<ClusterNodeTelemetry> Nodes { get; set; } = new List<ClusterNodeTelemetry>();
    }

    /// <summary>
    /// Node Telemetry
    /// </summary>
    public class ClusterNodeTelemetry
    {
        /// <summary>
        /// Identifies the node role
        /// </summary>
        [JsonProperty(PropertyName = "Role", Required = Required.Always)]
        public string Role { get; set; }

        /// <summary>
        /// Identifies the CPU architecture.
        /// </summary>
        [JsonProperty(PropertyName = "CpuArchitecture", Required = Required.Always)]
        public string CpuArchitecture { get; set; }
        
        /// <summary>
        /// Reports number of node vCPUs.
        /// </summary>
        [JsonProperty(PropertyName = "VCpus", Required = Required.Always)]
        public int VCpus { get; set; }

        /// <summary>
        /// The memory available.
        /// </summary>
        [JsonProperty(PropertyName = "Memory", Required = Required.Always | Required.AllowNull)]
        public string Memory { get; set; }

        /// <summary>
        /// Identifies the node kernel version.
        /// </summary>
        [JsonProperty(PropertyName = "KernelVersion", Required = Required.Always | Required.AllowNull)]
        public string KernelVersion { get; set; }

        /// <summary>
        /// Identifies the node operation system.
        /// </summary>
        [JsonProperty(PropertyName = "OperatingSystem", Required = Required.Always)]
        public string OperatingSystem { get; set; }

        /// <summary>
        /// Identifies the node operating system for Linux systems from: <b>/etc/os-release</b>
        /// </summary>
        [JsonProperty(PropertyName = "OsImage", Required = Required.Always | Required.AllowNull)]
        public string OsImage { get; set; }

        /// <summary>
        /// Identifies the node's container runtime version.
        /// </summary>
        [JsonProperty(PropertyName = "ContainerRuntimeVersion", Required = Required.Always)]
        public string ContainerRuntimeVersion { get; set; }

        /// <summary>
        /// Identifies the node's Kubelet version.
        /// </summary>
        [JsonProperty(PropertyName = "KubeletVersion", Required = Required.Always)]
        public string KubeletVersion { get; set; }

        /// <summary>
        /// Identifies the node's kube-proxy version.
        /// </summary>
        [JsonProperty(PropertyName = "KubeProxyVersion", Required = Required.Always)]
        public string KubeProxyVersion { get; set; }

        /// <summary>
        /// Identifies the node's private address.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateAddress", Required = Required.Always | Required.AllowNull)]
        public string PrivateAddress { get; set; }

        /// <summary>
        /// Indicates whether the node is configured and is ready to accept external network traffic
        /// for the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Ingress", Required = Required.Always)]
        public bool Ingress { get; set; }
    }
}
