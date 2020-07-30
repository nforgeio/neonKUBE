//-----------------------------------------------------------------------------
// FILE:	    IngressRoute.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// Specifies the ingress rules for the cluster.
    /// </summary>
    public class IngressRoute
    {
        /// <summary>
        /// The name of the ingress rule.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// The ingress port.
        /// </summary>
        [JsonProperty(PropertyName = "Port", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "port", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int? Port { get; set; }

        /// <summary>
        /// The target port for the ingress rule.
        /// </summary>
        [JsonProperty(PropertyName = "TargetPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "targetPort", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int? TargetPort { get; set; }

        /// <summary>
        /// The Kubernetes NodePort. This is where the ingress gateway will listen.
        /// </summary>
        [JsonProperty(PropertyName = "NodePort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodePort", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public int? NodePort { get; set; }

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRoute)}.{nameof(Name)}] is required when specifying an ingress rule.");
            }

            if (Port == null)
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRoute)}.{nameof(Port)}] is required when specifying an ingress rule.");
            }

            if (TargetPort == null)
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRoute)}.{nameof(TargetPort)}] is required when specifying an ingress rule.");
            }

            if (NodePort == null)
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRoute)}.{nameof(NodePort)}] is required when specifying an ingress rule.");
            }
        }
    }
}
