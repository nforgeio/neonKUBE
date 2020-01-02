//-----------------------------------------------------------------------------
// FILE:	    KubeSetupVersions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

namespace Neon.Kube
{
    /// <summary>
    /// Indicates the versions for the installed components.
    /// </summary>
    public class KubeSetupVersions
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupVersions()
        {
        }

        /// <summary>
        /// The Kubernetes version.
        /// </summary>
        [JsonProperty(PropertyName = "Kubernetes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubernetes", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Kubernetes { get; set; }

        /// <summary>
        /// The Kubernetes dashboard version;
        /// </summary>
        [JsonProperty(PropertyName = "KubernetesDashboard", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubernetesDashboard", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubernetesDashboard { get; set; }

        /// <summary>
        /// The Docker version.
        /// </summary>
        [JsonProperty(PropertyName = "DockerVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "dockerVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Docker { get; set; }

        /// <summary>
        /// The Helm version.
        /// </summary>
        [JsonProperty(PropertyName = "Helm", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "helm", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Helm { get; set; }

        /// <summary>
        /// The Calico version.
        /// </summary>
        [JsonProperty(PropertyName = "Calico", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "calico", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Calico { get; set; }

        /// <summary>
        /// The Istio version.
        /// </summary>
        [JsonProperty(PropertyName = "Istio", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "istio", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Istio { get; set; }
    }
}
