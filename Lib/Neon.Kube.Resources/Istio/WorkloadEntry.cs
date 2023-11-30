//-----------------------------------------------------------------------------
// FILE:        WorkloadEntry.cs
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Enables specifying the properties of a single non-Kubernetes workload such a VM or a bare metal services that can be 
    /// referred to by service entries.
    /// </summary>
    public class WorkloadEntry
    {
        /// <summary>
        /// Initializes a new instance of the WorkloadEntry class.
        /// </summary>
        public WorkloadEntry()
        {
        }

        /// <summary>
        /// Address associated with the network endpoint without the port. Domain names can be used if and only if the resolution is set 
        /// to DNS, and must be fully-qualified without wildcards. Use the form unix:///absolute/path/to/socket for Unix domain socket 
        /// endpoints.
        /// </summary>
        [JsonProperty(PropertyName = "address", Required = Required.Always)]
        public string Address { get; set; }

        /// <summary>
        /// <para>
        /// Set of ports associated with the endpoint. If the port map is specified, it must be a map of servicePortName to this endpoint’s port, such that traffic to the service port will be forwarded to the endpoint port that maps to the service’s portName. If omitted, and the targetPort is specified as part of the service’s port specification, traffic to the service port will be forwarded to one of the endpoints on the specified targetPort. If both the targetPort and endpoint’s port map are not specified, traffic to a service port will be forwarded to one of the endpoints on the same port.
        /// </para>
        /// <note>
        /// Do not use for unix:// addresses.
        /// </note>
        /// <note>
        /// endpoint port map takes precedence over targetPort.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "ports", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, int> Ports { get; set; }

        /// <summary>
        /// One or more labels associated with the endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "labels", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public Dictionary<string, string> Labels { get; set; }

        /// <summary>
        /// <para>
        /// Network enables Istio to group endpoints resident in the same L3 domain/network. All endpoints in the same network are assumed 
        /// to be directly reachable from one another. When endpoints in different networks cannot reach each other directly, an Istio Gateway
        /// can be used to establish connectivity (usually using the AUTO_PASSTHROUGH mode in a Gateway Server). This is an advanced configuration
        /// used typically for spanning an Istio mesh over multiple clusters.</para>
        /// </summary>
        [JsonProperty(PropertyName = "network", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Network { get; set; }

        /// <summary>
        /// The locality associated with the endpoint. A locality corresponds to a failure domain (e.g., country/region/zone). Arbitrary failure 
        /// domain hierarchies can be represented by separating each encapsulating failure domain by /. For example, the locality of an an 
        /// endpoint in US, in US-East-1 region, within availability zone az-1, in data center rack r11 can be represented as us/us-east-1/az-1/r11.
        /// Istio will configure the sidecar to route to endpoints within the same locality as the sidecar. If none of the endpoints in the locality 
        /// are available, endpoints parent locality (but within the same network ID) will be chosen. For example, if there are two endpoints in 
        /// same network (networkID “n1”), say e1 with locality us/us-east-1/az-1/r11 and e2 with locality us/us-east-1/az-2/r12, a sidecar from
        /// us/us-east-1/az-1/r11 locality will prefer e1 from the same locality over e2 from a different locality. Endpoint e2 could be the IP
        /// associated with a gateway (that bridges networks n1 and n2), or the IP associated with a standard service endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "locality", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Locality { get; set; }

        /// <summary>
        /// The load balancing weight associated with the endpoint. Endpoints with higher weights will receive proportionally higher traffic.
        /// </summary>
        [JsonProperty(PropertyName = "weight", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? weight { get; set; }

        /// <summary>
        /// The service account associated with the workload if a sidecar is present in the workload. The service account must be present in the same 
        /// namespace as the configuration ( WorkloadEntry or a ServiceEntry)
        /// </summary>
        [JsonProperty(PropertyName = "serviceAccount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ServiceAccount { get; set; }
    }
}
