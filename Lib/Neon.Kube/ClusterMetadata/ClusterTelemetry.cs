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
        [JsonProperty(PropertyName = "ClusterInfo", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Include)]
        public ClusterInfo ClusterInfo { get; set; }

        /// <summary>
        /// Node status information.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<Node> Nodes { get; set; } = new List<Node>();
    }

    /// <summary>
    /// Node Telemetry
    /// </summary>
    public class Node
    {
        /// <summary>
        /// Node role
        /// </summary>
        public string Role { get; set; }
        
        /// <summary>
        /// The CUP Arch.
        /// </summary>
        public string CpuArchitecture { get; set; }
        
        /// <summary>
        /// The number of cores.
        /// </summary>
        public int Cores { get; set; }

        /// <summary>
        /// The memory available.
        /// </summary>
        public string Memory { get; set; }

        /// <summary>
        /// The current kernel version.
        /// </summary>
        public string KernelVersion { get; set; }

        /// <summary>
        /// the current OS Image.
        /// </summary>
        public string OsImage { get; set; }

        /// <summary>
        /// The current Container runtime version.
        /// </summary>
        public string ContainerRuntimeVersion { get; set; }

        /// <summary>
        /// The current Kubelet version.
        /// </summary>
        public string KubeletVersion { get; set; }
        
        /// <summary>
        /// the current kube proxy version.
        /// </summary>
        public string KubeProxyVersion { get; set; }

        /// <summary>
        /// The current OS.
        /// </summary>
        public string OperatingSystem { get; set; }
    }
}
