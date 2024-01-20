//-----------------------------------------------------------------------------
// FILE:        TrafficPolicy.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Describes the properties of the proxy on a given load balancer port.
    /// </summary>
    public class TrafficPolicy
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public TrafficPolicy()
        {
        }

        /// <summary>
        /// Settings controlling the load balancer algorithms.
        /// </summary>
        public dynamic LoadBalancer { get; set; }

        /// <summary>
        /// Settings controlling the volume of connections to an upstream service.
        /// </summary>
        public dynamic ConnectionPool { get; set; }

        /// <summary>
        /// Settings controlling eviction of unhealthy hosts from the load balancing pool.
        /// </summary>
        public dynamic OutlierDetection { get; set; }

        /// <summary>
        /// TLS related settings for connections to the upstream service.
        /// </summary>
        public ClientTLSSettings Tls { get; set; }

        /// <summary>
        /// Traffic policies specific to individual ports. Note that port level settings 
        /// will override the destination-level settings. Traffic settings specified at the 
        /// destination-level will not be inherited when overridden by port-level settings, i.e. 
        /// default values will be applied to fields omitted in port-level traffic policies.
        /// </summary>
        public dynamic PortLevelSettings { get; set; }

        /// <summary>
        /// Configuration of tunneling TCP over other transport or application layers for the
        /// host configured in the DestinationRule. Tunnel settings can be applied to TCP or 
        /// TLS routes and can’t be applied to HTTP routes.
        /// </summary>
        public dynamic Tunnel { get; set; } 
    }
}
