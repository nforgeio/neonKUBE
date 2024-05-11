//-----------------------------------------------------------------------------
// FILE:        KubeConfigCluster.cs
// CONTRIBUTOR: Jeff Lill
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
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;
using Neon.Kube.K8s;
using Namotion.Reflection;
using Neon.Kube.ClusterDef;
using Neon.Kube.Deployment;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Describes a Kubernetes cluster configuration.
    /// </summary>
    public class KubeConfigCluster
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeConfigCluster()
        {
        }

        /// <summary>
        /// The local nickname for the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// The cluster properties.
        /// </summary>
        [JsonProperty(PropertyName = "cluster", Required = Required.Always)]
        [YamlMember(Alias = "cluster", ApplyNamingConventions = false)]
        public KubeConfigClusterConfig Cluster { get; set; }

        /// <summary>
        /// Returns an extension value.
        /// </summary>
        /// <typeparam name="T">Specifies the value type.</typeparam>
        /// <param name="name">Specifies the extension name.</param>
        /// <param name="default">Specifies the value to be returned when the extension is not found.</param>
        /// <returns>The extension value.</returns>
        public T GetExtensionValue<T>(string name, T @default)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (Cluster == null || Cluster.Extensions == null)
            {
                return @default;
            }

            try
            {
                return Cluster.Extensions.Get<T>(name, @default);
            }
            catch
            {
                return @default;
            }
        }

        /// <summary>
        /// Sets an extension value.
        /// </summary>
        /// <typeparam name="T">Specifies the value type.</typeparam>
        /// <param name="name">Specifies the extension name.</param>
        /// <param name="value">Specifies the value being set.</param>
        public void SetExtensionValue<T>(string name, T value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(name), nameof(name));

            if (Cluster == null)
            {
                Cluster = new KubeConfigClusterConfig();
            }

            if (Cluster.Extensions == null)
            {
                Cluster.Extensions = new List<NamedExtension>();
            }

            Cluster.Extensions.Set<T>(name, value);
        }

        /// <summary>
        /// Holds additional information about NeonKUBE clusters.  This will be
        /// <c>null</c> for non-NeonKUBE clusters.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public KubeClusterInfo ClusterInfo
        {
            get => GetExtensionValue<KubeClusterInfo>(NeonKubeExtensions.ClusterInfo, null);
            set => SetExtensionValue<KubeClusterInfo>(NeonKubeExtensions.ClusterInfo, value);
        }

        /// <summary>
        /// Identifies the <see cref="Neon.Kube.ClusterDef.HostingEnvironment"/> for NeonKUBE clusters.
        /// This will be <see cref="HostingEnvironment.Unknown"/> for non-NeonKUBE clusters.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public HostingEnvironment HostingEnvironment
        {
            get => GetExtensionValue<HostingEnvironment>(NeonKubeExtensions.HostingEnvironment, HostingEnvironment.Unknown);
            set => SetExtensionValue<HostingEnvironment>(NeonKubeExtensions.HostingEnvironment, value);
        }

        /// <summary>
        /// Holds the <see cref="Neon.Kube.ClusterDef.HostingOptions"/> for NeonKUBE clusters.
        /// This will be <c>null</c> for non-NeonKUBE clusters.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public HostingOptions Hosting
        {
            get => GetExtensionValue<HostingOptions>(NeonKubeExtensions.Hosting, null);
            set => SetExtensionValue<HostingOptions>(NeonKubeExtensions.Hosting, value);
        }

        /// <summary>
        /// Specifies the prefix added by the hosting environment to virtual machine names.  This may
        /// be the empty string for NeonKUBE clusters and always will be <c>null</c> for non-NeonKUBE
        /// clusters.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string HostingNamePrefix
        {
            get => GetExtensionValue<string>(NeonKubeExtensions.HostingNamePrefix, null);
            set => SetExtensionValue<string>(NeonKubeExtensions.HostingNamePrefix , value);
        }

        /// <summary>
        /// Indicates that this is a neon-desktop cluster. This will be <c>false</c> for
        /// non-NeonKUBE clusters.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsNeonDesktop
        {
            get => GetExtensionValue<bool>(NeonKubeExtensions.IsNeonDesktop, false);
            set => SetExtensionValue<bool>(NeonKubeExtensions.IsNeonDesktop, value);
        }

        /// <summary>
        /// Indicates that this is a NeonKUBE cluster.  This will be <c>false</c> for
        /// non-NeonKUBE clusters.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsNeonKube
        {
            get => GetExtensionValue<bool>(NeonKubeExtensions.IsNeonKube, false);
            set => SetExtensionValue<bool>(NeonKubeExtensions.IsNeonKube, value);
        }

        /// <summary>
        /// Specifies the cluster's SSO admin password.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string SsoUsername
        {
            get => GetExtensionValue<string>(NeonKubeExtensions.SsoUsername, null);
            set => SetExtensionValue<string>(NeonKubeExtensions.SsoUsername, value);
        }

        /// <summary>
        /// Specifies the cluster's SSO admin password.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string SsoPassword
        {
            get => GetExtensionValue<string>(NeonKubeExtensions.SsoPassword, null);
            set => SetExtensionValue<string>(NeonKubeExtensions.SsoPassword, value);
        }

        /// <summary>
        /// Specifies the SSH admin username for cluster nodes.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string SshUsername
        {
            get => GetExtensionValue<string>(NeonKubeExtensions.SshUsername, null);
            set => SetExtensionValue<string>(NeonKubeExtensions.SshUsername, value);
        }

        /// <summary>
        /// Specifies the SSH admin password for cluster nodes.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string SshPassword
        {
            get => GetExtensionValue<string>(NeonKubeExtensions.SshPassword, null);
            set => SetExtensionValue<string>(NeonKubeExtensions.SshPassword, value);
        }

        /// <summary>
        /// Holds the cluster definition for testing clusters.  <b>ClusterFixture</b>
        /// uses this decide whether to deploy a new cluster when the definition has changed.
        /// This will be <c>null</c> for non-NeonKUBE clusters.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public ClusterDefinition TestClusterDefinition
        {
            get => GetExtensionValue<ClusterDefinition>(NeonKubeExtensions.TestClusterDefinition, null);
            set => SetExtensionValue<ClusterDefinition>(NeonKubeExtensions.TestClusterDefinition, value);
        }
    }
}
