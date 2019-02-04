//-----------------------------------------------------------------------------
// FILE:	    KubeSetupInfo.cs
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
    /// Holds information required to setup a Kubernetes cluster.
    /// </summary>
    public class KubeSetupInfo
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupInfo()
        {
        }

        /// <summary>
        /// Lists the installed component versions.
        /// </summary>
        [JsonProperty(PropertyName = "Versions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "Versions", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public KubeSetupVersions Versions { get; set; } = new KubeSetupVersions();

        //---------------------------------------------------------------------
        // kubectl:

        /// <summary>
        /// The <b>kubectl</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "KubeCtlLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeCtlLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeCtlLinuxUri { get; set; }

        /// <summary>
        /// The <b>kubectl</b> binary download URI for OS/X.
        /// </summary>
        [JsonProperty(PropertyName = "KubeCtlOsxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeCtlOsxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeCtlOsxUri { get; set; }

        /// <summary>
        /// The <b>kubectl</b> binary download URI for Windows.
        /// </summary>
        [JsonProperty(PropertyName = "KubeCtlWindowsUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeCtlWindowsUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeCtlWindowsUri { get; set; }

        //---------------------------------------------------------------------
        // kubeadm:

        /// <summary>
        /// The <b>kubeadm</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "KubeAdmLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeAdmLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeAdmLinuxUri { get; set; }

        /// <summary>
        /// The <b>kubelet</b> binary download URI for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "KubeletLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeletLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeletLinuxUri { get; set; }

        //---------------------------------------------------------------------
        // Ubuntu Kubernetes component package versions:

        /// <summary>
        /// The Ubuntu package version for <b>kubeadm</b>.
        /// </summary>
        [JsonProperty(PropertyName = "KubeAdmPackageUbuntuVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeAdmPackageUbuntuVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeAdmPackageUbuntuVersion { get; set; }

        /// <summary>
        /// The Ubuntu package version for <b>kubectl</b>.
        /// </summary>
        [JsonProperty(PropertyName = "KubeCtlPackageUbuntuVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeCtlPackageUbuntuVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeCtlPackageUbuntuVersion { get; set; }

        /// <summary>
        /// The Ubuntu package version for <b>kubelet</b>.
        /// </summary>
        [JsonProperty(PropertyName = "KubeletPackageUbuntuVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeletPackageUbuntuVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeletPackageUbuntuVersion { get; set; }

        //---------------------------------------------------------------------
        // Docker:

        /// <summary>
        /// The Docker package for Ubuntu.
        /// </summary>
        [JsonProperty(PropertyName = "DockerPackageUbuntuUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "DockerPackageUbuntuUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string DockerPackageUbuntuUri { get; set; }

        //---------------------------------------------------------------------
        // Helm:

        /// <summary>
        /// The Helm binary URL for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "HelmLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "HelmLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HelmLinuxUri { get; set; }

        /// <summary>
        /// The Helm binary URL for OS/X.
        /// </summary>
        [JsonProperty(PropertyName = "HelmOsxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "HelmOsxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HelmOsxUri { get; set; }

        /// <summary>
        /// The Helm binary URL for Windows.
        /// </summary>
        [JsonProperty(PropertyName = "HelmWindowsUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "HelmWindowsUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string HelmWindowsUri { get; set; }

        //---------------------------------------------------------------------
        // Calico:

        /// <summary>
        /// The Calico RBAC rules download (YAML for kubectl).
        /// </summary>
        [JsonProperty(PropertyName = "CalicoRbacYamlUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "CalicoRbacYamlUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CalicoRbacYamlUri { get; set; }

        /// <summary>
        /// The Calico setup download (YAML for kubectl).
        /// </summary>
        [JsonProperty(PropertyName = "CalicoSetupYamUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "CalicoSetupYamUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string CalicoSetupYamUri { get; set; }

        //---------------------------------------------------------------------
        // Istio:

        /// <summary>
        /// The Istio binary URL for Linux.
        /// </summary>
        [JsonProperty(PropertyName = "IstioLinuxUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "IstioLinuxUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string IstioLinuxUri { get; set; }

        //---------------------------------------------------------------------
        // Kubernetes Dashboard:

        /// <summary>
        /// The Kubernetes Dashboard resource configuration URI.
        /// </summary>
        [JsonProperty(PropertyName = "KubeDashboardUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "KubeDashboardUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubeDashboardUri { get; set; }
    }
}
