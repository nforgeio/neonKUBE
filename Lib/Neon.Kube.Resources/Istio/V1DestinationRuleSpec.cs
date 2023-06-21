//-----------------------------------------------------------------------------
// FILE:        V1DestinationRuleSpec.cs
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

using System.Collections.Generic;
using System.ComponentModel;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Describes the <see cref="V1AuthorizationPolicy"/> spec.
    /// </summary>
    public class V1DestinationRuleSpec
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public V1DestinationRuleSpec()
        {
        }

        /// <summary>
        /// <para>
        /// The name of a service from the service registry. Service names are looked up from the platform’s service 
        /// registry (e.g., Kubernetes services, Consul services, etc.) and from the hosts declared by ServiceEntries. 
        /// Rules defined for services that do not exist in the service registry will be ignored.
        /// </para>
        /// <para>
        /// When short names are used (e.g. “reviews” instead of “reviews.default.svc.cluster.local”), Istio will interpret
        /// the short name based on the namespace of the rule, not the service. A rule in the “default” namespace containing 
        /// a host “reviews” will be interpreted as “reviews.default.svc.cluster.local”, irrespective of the actual 
        /// namespace associated with the reviews service.
        /// </para>
        /// </summary>
        [DefaultValue(null)]
        public string Host { get; set; } = null;

        /// <summary>
        /// <para>
        /// Traffic policies to apply for a specific destination, across all destination ports. See DestinationRule for examples.
        /// </para>
        /// </summary>
        [DefaultValue(null)]
        public TrafficPolicy TrafficPolicy { get; set; } = null;
    }
}
