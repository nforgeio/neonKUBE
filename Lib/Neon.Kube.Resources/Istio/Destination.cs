//-----------------------------------------------------------------------------
// FILE:	    Destination.cs
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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// <para>
    /// Destination indicates the network addressable service to which the request/connection will be sent after processing a routing rule. 
    /// The destination.host should unambiguously refer to a service in the service registry. Istio’s service registry is composed of all the 
    /// services found in the platform’s service registry (e.g., Kubernetes services, Consul services), as well as services declared through
    /// the ServiceEntry resource.
    /// </para>
    /// <para>
    /// Note for Kubernetes users: When short names are used(e.g. “reviews” instead of “reviews.default.svc.cluster.local”), Istio will interpret 
    /// the short name based on the namespace of the rule, not the service.A rule in the “default” namespace containing a host “reviews will be 
    /// interpreted as “reviews.default.svc.cluster.local”, irrespective of the actual namespace associated with the reviews service.To avoid 
    /// potential misconfigurations, it is recommended to always use fully qualified domain names over short names.
    /// </para>
    /// <para>
    /// The following Kubernetes example routes all traffic by default to pods of the reviews service with label “version: v1” (i.e., subset v1),
    /// and some to subset v2, in a Kubernetes environment.
    /// </para>
    /// </summary>
    public class Destination : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the Destination class.
        /// </summary>
        public Destination()
        {
        }

        /// <summary>
        /// <para>
        /// The name of a service from the service registry. Service names are looked up from the platform’s service registry (e.g., Kubernetes services, 
        /// Consul services, etc.) and from the hosts declared by ServiceEntry. Traffic forwarded to destinations that are not found in either of the 
        /// two, will be dropped.
        /// </para>
        /// <para>
        /// Note for Kubernetes users: When short names are used (e.g. “reviews” instead of “reviews.default.svc.cluster.local”), Istio will interpret 
        /// the short name based on the namespace of the rule, not the service. A rule in the “default” namespace containing a host “reviews will be
        /// interpreted as “reviews.default.svc.cluster.local”, irrespective of the actual namespace associated with the reviews service. To avoid 
        /// potential misconfiguration, it is recommended to always use fully qualified domain names over short names.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "host", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Host { get; set; }

        /// <summary>
        /// <para>
        /// The name of a subset within the service. Applicable only to services within the mesh. The subset must be defined in a corresponding DestinationRule.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "subset", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Subset { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the port on the host that is being addressed. If a service exposes only a single port it is not required to explicitly select the port.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "port", Required = Required.Always)]
        public PortSelector Port { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}
