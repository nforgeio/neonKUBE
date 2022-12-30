//-----------------------------------------------------------------------------
// FILE:	    WorkloadSelector.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// WorkloadSelector specifies the criteria used to determine if the Gateway, Sidecar, EnvoyFilter, or ServiceEntry 
    /// configuration can be applied to a proxy. The matching criteria includes the metadata associated with a proxy, workload
    /// instance info such as labels attached to the pod/VM, or any other info that the proxy provides to Istio during the initial 
    /// handshake. If multiple conditions are specified, all conditions need to match in order for the workload instance to be selected. 
    /// Currently, only label based selection mechanism is supported.
    /// </summary>
    public class WorkloadSelector
    {
        /// <summary>
        /// Initializes a new instance of the WorkloadSelector class.
        /// </summary>
        public WorkloadSelector()
        {
        }

        /// <summary>
        /// Address associated with the network endpoint without the port. Domain names can be used if and only if the resolution is set 
        /// to DNS, and must be fully-qualified without wildcards. Use the form unix:///absolute/path/to/socket for Unix domain socket 
        /// endpoints.
        /// </summary>
        [JsonProperty(PropertyName = "labels", Required = Required.Always)]
        public Dictionary<string, string> Labels { get; set; }
    }
}
