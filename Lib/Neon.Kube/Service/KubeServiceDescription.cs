//-----------------------------------------------------------------------------
// FILE:	    KubeServiceDescription.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.OpenApi.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Net;

namespace Neon.Kube.Service
{
    /// <summary>
    /// Describes a <see cref="KubeService"/> or <see cref="AspNetKubeService"/>.
    /// </summary>
    public class KubeServiceDescription
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public KubeServiceDescription()
        {
        }

        /// <summary>
        /// The service name as deployed to Kubernetes.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// The Kubernetes namespace where the service is deployed.  This defaults to <b>"default"</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Namespace", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "namespace", ApplyNamingConventions = false)]
        [DefaultValue("default")]
        public string Namespace { get; set; } = "default";

        /// <summary>
        /// The cluster's configured domain (aka zone).  This defaults to <b>"cluster.local"</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Domain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "domain", ApplyNamingConventions = false)]
        [DefaultValue("cluster.local")]
        public string Domain { get; set; } = "cluster.local";

        /// <summary>
        /// When set, this overrides <see cref="Name"/>, <see cref="Namespace"/>, and
        /// <see cref="Domain"/> when generating the <see cref="Hostname"/> result.
        /// This is typically set when testing on a local machine.  This defaults
        /// to <c>null</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Address", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "address", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public IPAddress Address { get; set; }

        /// <summary>
        /// Returns the hostname to be used to communcate with this service.  When deployed
        /// to a Kubernetes cluster, this will be formed from <see cref="Name"/>, <see cref="Namespace"/>,
        /// and <see cref="Domain"/>.  When testing and <see cref="Address"/> is not <c>null</c>,
        /// then this will simply be the address converted to a string.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string Hostname
        {
            get
            {
                if (Address != null)
                {
                    return Address.ToString();
                }
                else
                {
                    Covenant.Assert(!string.IsNullOrEmpty(Name));
                    Covenant.Assert(!string.IsNullOrEmpty(Namespace));
                    Covenant.Assert(!string.IsNullOrEmpty(Domain));

                    return $"{Name}.{Namespace}.{Domain}";
                }
            }
        }

        /// <summary>
        /// The service's network endpoints.
        /// </summary>
        [JsonProperty(PropertyName = "Endpoints", Required = Required.Always)]
        [YamlMember(Alias = "endpoints", ApplyNamingConventions = false)]
        public Dictionary<string, KubeServiceEndpoint> Endpoints { get; set; } = new Dictionary<string, KubeServiceEndpoint>();
    }
}
