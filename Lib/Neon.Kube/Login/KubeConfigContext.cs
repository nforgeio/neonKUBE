//-----------------------------------------------------------------------------
// FILE:	    KubeConfigContext.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using k8s.KubeConfigModels;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;

namespace Neon.Kube.Login
{
    /// <summary>
    /// Describes a Kubernetes cluster configuration.
    /// </summary>
    public class KubeConfigContext
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigContext()
        {
            this.Properties = new KubeConfigClusterProperties();
            this.Extensions = new List<NamedExtension>();
        }

        /// <summary>
        /// Specifies the cluster name.
        /// </summary>
        /// <param name="clusterName">Specifies the cluster name.</param>
        public KubeConfigContext(string clusterName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(clusterName), nameof(clusterName));

            this.Name = clusterName;
        }

        /// <summary>
        /// Specifies cluster name.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// The cluster properties.
        /// </summary>
        [JsonProperty(PropertyName = "cluster", Required = Required.Always)]
        [YamlMember(Alias = "cluster", ApplyNamingConventions = false)]
        public KubeConfigContextProperties Properties { get; set; }

        /// <summary>
        /// Lists any custom extension properties.  Extensions are name/value pairs added
        /// by vendors to hold arbitrary information.  Take care to choose property names
        /// that are unlikely to conflict with properties created by other vendors by adding
        /// a custom sffix like <b>my-property.neonkube.io</b>, where <b>my-property</b> 
        /// identifies the property and <b>neonkibe.io</b> helps avoid conflicts.
        /// </summary>
        [JsonProperty(PropertyName = "Extensions", Required = Required.Default)]
        [YamlMember(Alias = "extensions", ApplyNamingConventions = false)]
        public List<NamedExtension> Extensions { get; set; } = new List<NamedExtension>();

        /// <summary>
        /// Indicates that this is a neonKUBE cluster.
        /// </summary>
        [JsonIgnore]
        public bool IsNeonKube
        {
            get => Extensions.Get<bool>(NeonKubeExtensionName.IsNeonKube, false);
            set => Extensions.Set<bool>(NeonKubeExtensionName.IsNeonKube, value);
        }

        /// <summary>
        /// Indicates that this is a neon-desktop cluster.
        /// </summary>
        [JsonIgnore]
        public bool IsNeonDesktop
        {
            get => Extensions.Get<bool>(NeonKubeExtensionName.IsNeonDesktop, false);
            set => Extensions.Set<bool>(NeonKubeExtensionName.IsNeonDesktop, value);
        }

        /// <summary>
        /// Holds additional information about neonKUBE clusters.  This will be
        /// <c>null</c> for non-neonKUBE clusters.
        /// </summary>
        public KubeClusterInfo ClusterInfo
        {
            get => Extensions.Get<KubeClusterInfo>(NeonKubeExtensionName.ClusterInfo, null);
            set => Extensions.Set<KubeClusterInfo>(NeonKubeExtensionName.ClusterInfo, value);
        }
    }
}
